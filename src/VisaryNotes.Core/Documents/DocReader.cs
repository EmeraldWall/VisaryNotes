using System.Buffers.Binary;
using System.Text;

namespace VisaryNotes.Core.Documents;

/// <summary>
/// Reads legacy Word 97 to 2003 documents (.doc). The file is a Compound File Binary container ([MS-CFB]);
/// text comes from the WordDocument stream through the piece table, bold and italic from the character
/// property pages, and headings and table rows from the paragraph property pages ([MS-DOC] 2.4 and 2.9).
/// Images, fonts, colours and page layout are not read, so Original view shows the text on a plain page.
/// </summary>
public static class DocReader
{
    private const int CellMark = 0x07;

    private sealed record Piece(int CpStart, int CpEnd, int FcStart, bool Compressed);

    private sealed record CharRange(int FcStart, int FcEnd, bool Bold, bool Italic);

    private sealed record ParaRange(int FcStart, int FcEnd, int Style, bool InTable, bool RowEnd);

    public static Document Read(Stream stream, string fileName)
    {
        var bytes = ReadLimits.ReadAll(stream);
        var cfb = new CompoundFile(bytes);
        var word = cfb.Stream("WordDocument") ?? throw new DocumentReadException("This file has no Word text stream, so it is not a Word .doc file.");
        if (word.Length < 0x200 || BinaryPrimitives.ReadUInt16LittleEndian(word) != 0xA5EC)
            throw new DocumentReadException("This .doc file has an unknown header.");

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(word.AsSpan(0x0A));
        if ((flags & 0x0100) != 0)
            throw new DocumentReadException("This .doc file is password protected.");
        var table = cfb.Stream((flags & 0x0200) != 0 ? "1Table" : "0Table") ?? throw new DocumentReadException("This .doc file has no table stream.");

        // FibBase is 32 bytes, then the counted arrays fibRgW, fibRgLw and fibRgFcLcb ([MS-DOC] 2.5.1).
        var pos = 32;
        var csw = U16(word, pos);
        pos += 2 + csw * 2;
        var cslw = U16(word, pos);
        var rgLw = pos + 2;
        pos += 2 + cslw * 4;
        var cbRgFcLcb = U16(word, pos);
        var rgFcLcb = pos + 2;
        if (cbRgFcLcb < 34)
            throw new DocumentReadException("This .doc file is from a Word version older than Word 97.");

        var ccpText = I32(word, rgLw + 3 * 4);
        (int Fc, int Lcb) Pair(int index) => (I32(word, rgFcLcb + index * 8), I32(word, rgFcLcb + index * 8 + 4));

        var pieces = ReadPieces(table, Pair(33));
        var chars = ReadCharRanges(word, table, Pair(12));
        var paras = ReadParaRanges(word, table, Pair(13));

        var doc = new Document { Format = DocumentFormat.Doc, Title = Path.GetFileNameWithoutExtension(fileName), Page = PageSetup.A4 };
        Build(doc, word, pieces, Math.Max(0, ccpText), chars, paras);
        doc.Warnings.Add("Older Word .doc files show their text, headings, bold and italic. Images and the exact page layout are not shown.");
        return doc;
    }

    private static List<Piece> ReadPieces(byte[] table, (int Fc, int Lcb) clx)
    {
        if (clx.Fc < 0 || clx.Lcb <= 0 || clx.Fc + clx.Lcb > table.Length)
            throw new DocumentReadException("The .doc piece table is missing or damaged.");
        var p = clx.Fc;
        var end = clx.Fc + clx.Lcb;
        // Skip RgPrc entries (0x01, size, data) until the Pcdt (0x02).
        while (p < end && table[p] == 0x01)
            p += 3 + U16(table, p + 1);
        if (p >= end || table[p] != 0x02)
            throw new DocumentReadException("The .doc piece table is damaged.");
        var lcb = I32(table, p + 1);
        var plc = p + 5;
        if (lcb < 4 || plc + lcb > table.Length)
            throw new DocumentReadException("The .doc piece table is damaged.");
        var n = (lcb - 4) / 12;
        var pieces = new List<Piece>(n);
        for (var i = 0; i < n; i++)
        {
            var cpStart = I32(table, plc + i * 4);
            var cpEnd = I32(table, plc + (i + 1) * 4);
            var pcd = plc + (n + 1) * 4 + i * 8;
            var fc = (uint)I32(table, pcd + 2);
            var compressed = (fc & 0x40000000) != 0;
            var start = compressed ? (int)((fc & ~0x40000000u) / 2) : (int)fc;
            pieces.Add(new Piece(cpStart, cpEnd, start, compressed));
        }
        return pieces;
    }

    private static List<CharRange> ReadCharRanges(byte[] word, byte[] table, (int Fc, int Lcb) plc)
    {
        var ranges = new List<CharRange>();
        foreach (var page in FkpPages(word, table, plc))
        {
            var crun = word[page + 511];
            for (var i = 0; i < crun; i++)
            {
                var fcStart = I32(word, page + i * 4);
                var fcEnd = I32(word, page + (i + 1) * 4);
                var offset = word[page + (crun + 1) * 4 + i] * 2;
                bool bold = false, italic = false;
                if (offset > 0 && page + offset < word.Length)
                {
                    var cb = word[page + offset];
                    foreach (var (sprm, operand) in Sprms(word, page + offset + 1, cb))
                    {
                        // Toggle values: 1 on, 0x81 the opposite of the style, which is on for body text.
                        if (sprm == 0x0835)
                            bold = operand is 1 or 0x81;
                        else if (sprm == 0x0836)
                            italic = operand is 1 or 0x81;
                    }
                }
                ranges.Add(new CharRange(fcStart, fcEnd, bold, italic));
            }
        }
        return ranges;
    }

    private static List<ParaRange> ReadParaRanges(byte[] word, byte[] table, (int Fc, int Lcb) plc)
    {
        var ranges = new List<ParaRange>();
        foreach (var page in FkpPages(word, table, plc))
        {
            var cpara = word[page + 511];
            for (var i = 0; i < cpara; i++)
            {
                var fcStart = I32(word, page + i * 4);
                var fcEnd = I32(word, page + (i + 1) * 4);
                var offset = word[page + (cpara + 1) * 4 + i * 13] * 2;
                int style = 0;
                bool inTable = false, rowEnd = false;
                if (offset > 0 && page + offset + 1 < word.Length)
                {
                    var at = page + offset;
                    int cb = word[at];
                    int len;
                    if (cb != 0)
                    {
                        len = cb * 2 - 1;
                        at += 1;
                    }
                    else
                    {
                        len = word[at + 1] * 2;
                        at += 2;
                    }
                    if (len >= 2 && at + len <= word.Length)
                    {
                        style = U16(word, at);
                        foreach (var (sprm, operand) in Sprms(word, at + 2, len - 2))
                        {
                            if (sprm == 0x2416)
                                inTable = operand == 1;
                            else if (sprm is 0x2417 or 0x244C)
                                rowEnd |= operand == 1;
                        }
                    }
                }
                ranges.Add(new ParaRange(fcStart, fcEnd, style, inTable, rowEnd));
            }
        }
        return ranges;
    }

    /// <summary>The 512 byte formatted disk pages listed by a PlcBteChpx or PlcBtePapx.</summary>
    private static IEnumerable<int> FkpPages(byte[] word, byte[] table, (int Fc, int Lcb) plc)
    {
        if (plc.Lcb < 8 || plc.Fc < 0 || plc.Fc + plc.Lcb > table.Length)
            yield break;
        var n = (plc.Lcb - 4) / 8;
        for (var i = 0; i < n; i++)
        {
            var pn = I32(table, plc.Fc + (n + 1) * 4 + i * 4) & 0x3FFFFF;
            var page = pn * 512;
            if (page >= 0 && page + 512 <= word.Length)
                yield return page;
        }
    }

    /// <summary>Single property modifiers with their first operand byte; the operand size comes from the spra bits.</summary>
    private static IEnumerable<(int Sprm, int Operand)> Sprms(byte[] data, int start, int length)
    {
        var p = start;
        var end = Math.Min(data.Length, start + length);
        while (p + 2 <= end)
        {
            var sprm = U16(data, p);
            p += 2;
            var size = (sprm >> 13) switch
            {
                0 or 1 => 1,
                2 or 4 or 5 => 2,
                3 => 4,
                7 => 3,
                _ => p < end ? data[p] + 1 : 1
            };
            // sprmTDefTable and sprmPChgTabs carry a two byte length.
            if (sprm == 0xD608 && p + 2 <= end)
                size = U16(data, p) + 1;
            var operand = p < end ? data[p] : 0;
            yield return (sprm, operand);
            p += size;
        }
    }

    private static void Build(Document doc, byte[] word, List<Piece> pieces, int ccpText, List<CharRange> chars, List<ParaRange> paras)
    {
        var runs = new List<Run>();
        var text = new StringBuilder();
        var style = TextStyle.Plain;
        TableBlock? tableBlock = null;
        List<TableCell>? row = null;
        var fieldDepth = 0;
        var inResult = new Stack<bool>();
        var charIndex = 0;
        var paraIndex = 0;

        void FlushRun()
        {
            if (text.Length > 0)
                runs.Add(new Run(text.ToString(), style));
            text.Clear();
        }

        ParaRange? ParaAt(int fc)
        {
            while (paraIndex < paras.Count && paras[paraIndex].FcEnd <= fc)
                paraIndex++;
            if (paraIndex < paras.Count && paras[paraIndex].FcStart <= fc)
                return paras[paraIndex];
            return paras.FirstOrDefault(p => p.FcStart <= fc && fc < p.FcEnd);
        }

        TextStyle StyleAt(int fc)
        {
            if (charIndex < chars.Count && !(chars[charIndex].FcStart <= fc && fc < chars[charIndex].FcEnd))
            {
                charIndex = chars.FindIndex(c => c.FcStart <= fc && fc < c.FcEnd);
                if (charIndex < 0)
                {
                    charIndex = 0;
                    return TextStyle.Plain;
                }
            }
            if (charIndex >= chars.Count)
                return TextStyle.Plain;
            var c = chars[charIndex];
            return c.Bold || c.Italic ? new TextStyle { Bold = c.Bold, Italic = c.Italic } : TextStyle.Plain;
        }

        void EndParagraph(int fc, bool cellEnd)
        {
            FlushRun();
            var info = ParaAt(fc);
            var blocks = new List<Block>();
            if (info is { Style: >= 1 and <= 9 } && runs.Any(r => !string.IsNullOrWhiteSpace(r.Text)))
            {
                var h = new Heading(info.Style);
                h.Runs.AddRange(runs.Select(r => r with { Style = TextStyle.Plain }));
                blocks.Add(h);
            }
            else
            {
                var p = new Paragraph();
                p.Runs.AddRange(runs);
                blocks.Add(p);
            }
            runs.Clear();

            var tableRow = cellEnd || info is { InTable: true };
            if (!tableRow)
            {
                tableBlock = null;
                row = null;
                doc.Blocks.AddRange(blocks);
                return;
            }

            if (info is { RowEnd: true })
            {
                // The row end mark closes the row and carries no text of its own.
                if (row is { Count: > 0 })
                    tableBlock!.Rows.Add(row);
                row = null;
                return;
            }
            if (tableBlock is null)
            {
                tableBlock = new TableBlock();
                doc.Blocks.Add(tableBlock);
            }
            row ??= new List<TableCell>();
            if (cellEnd || row.Count == 0)
                row.Add(new TableCell());
            row[^1].Blocks.AddRange(blocks);
        }

        foreach (var piece in pieces)
        {
            if (piece.CpStart >= ccpText)
                break;
            var count = Math.Min(piece.CpEnd, ccpText) - piece.CpStart;
            for (var i = 0; i < count; i++)
            {
                var fc = piece.FcStart + i * (piece.Compressed ? 1 : 2);
                if (fc < 0 || fc + (piece.Compressed ? 1 : 2) > word.Length)
                    break;
                int ch = piece.Compressed ? Cp1252(word[fc]) : U16(word, fc);

                // Field codes: keep the result between the separator and the end, drop the instruction.
                if (ch == 0x13)
                {
                    fieldDepth++;
                    inResult.Push(false);
                    continue;
                }
                if (ch == 0x14 && fieldDepth > 0)
                {
                    inResult.Pop();
                    inResult.Push(true);
                    continue;
                }
                if (ch == 0x15 && fieldDepth > 0)
                {
                    fieldDepth--;
                    inResult.Pop();
                    continue;
                }
                if (fieldDepth > 0 && !inResult.Peek())
                    continue;

                if (ch is 0x0D or CellMark)
                {
                    EndParagraph(fc, ch == CellMark);
                    continue;
                }

                var s = StyleAt(fc);
                if (s != style)
                {
                    FlushRun();
                    style = s;
                }
                switch (ch)
                {
                    case 0x0B:
                        text.Append('\n');
                        break;
                    case 0x0C:
                        FlushRun();
                        if (runs.Count > 0)
                            EndParagraph(fc, false);
                        doc.Blocks.Add(new PageBreak());
                        break;
                    case 0x09:
                        text.Append('\t');
                        break;
                    case 0x1E:
                        text.Append('‑');
                        break;
                    case 0x1F or 0x01 or 0x02 or 0x03 or 0x04 or 0x05 or 0x08:
                        // Optional hyphen, pictures, footnote and annotation marks have no visible text here.
                        break;
                    case < 0x20:
                        break;
                    default:
                        text.Append((char)ch);
                        break;
                }
            }
        }
        if (text.Length > 0 || runs.Count > 0)
            EndParagraph(int.MaxValue, false);
    }

    /// <summary>Compressed pieces hold Windows-1252 text; 0x80 to 0x9F differ from Latin-1.</summary>
    private static int Cp1252(byte b) => b switch
    {
        0x80 => '\u20AC', 0x82 => '\u201A', 0x83 => '\u0192', 0x84 => '\u201E', 0x85 => '\u2026', 0x86 => '\u2020',
        0x87 => '\u2021', 0x88 => '\u02C6', 0x89 => '\u2030', 0x8A => '\u0160', 0x8B => '\u2039', 0x8C => '\u0152',
        0x8E => '\u017D', 0x91 => '\u2018', 0x92 => '\u2019', 0x93 => '\u201C', 0x94 => '\u201D', 0x95 => '\u2022',
        0x96 => '\u2013', 0x97 => '\u2014', 0x98 => '\u02DC', 0x99 => '\u2122', 0x9A => '\u0161', 0x9B => '\u203A',
        0x9C => '\u0153', 0x9E => '\u017E', 0x9F => '\u0178',
        _ => b
    };

    private static int U16(byte[] d, int at) => at >= 0 && at + 2 <= d.Length ? BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(at)) : 0;

    private static int I32(byte[] d, int at) => at >= 0 && at + 4 <= d.Length ? BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(at)) : 0;

    /// <summary>A minimal reader for Compound File Binary containers (version 3 and 4).</summary>
    private sealed class CompoundFile
    {
        private const uint EndOfChain = 0xFFFFFFFE;
        private readonly byte[] _data;
        private readonly int _sectorSize;
        private readonly int _miniSectorSize;
        private readonly uint _miniCutoff;
        private readonly List<uint> _fat = new();
        private readonly List<uint> _miniFat = new();
        private readonly Dictionary<string, (uint Start, long Size)> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly byte[] _miniStream = [];

        public CompoundFile(byte[] data)
        {
            _data = data;
            ReadOnlySpan<byte> magic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
            if (data.Length < 512 || !data.AsSpan(0, 8).SequenceEqual(magic))
                throw new DocumentReadException("This file is not a Word .doc file. It may be a .docx or another format renamed.");
            var sectorShift = U16(data, 0x1E);
            var miniShift = U16(data, 0x20);
            if (sectorShift is not (9 or 12) || miniShift != 6)
                throw new DocumentReadException("This .doc file has an unsupported container layout.");
            _sectorSize = 1 << sectorShift;
            _miniSectorSize = 1 << miniShift;
            _miniCutoff = (uint)I32(data, 0x38);

            // The FAT sectors are listed in the header DIFAT (109 entries) and then in chained DIFAT sectors.
            var fatSectors = new List<uint>();
            for (var i = 0; i < 109; i++)
            {
                var s = (uint)I32(data, 0x4C + i * 4);
                if (s < 0xFFFFFFFA)
                    fatSectors.Add(s);
            }
            var difat = (uint)I32(data, 0x44);
            var guard = 0;
            while (difat < 0xFFFFFFFA && guard++ < 10000)
            {
                var off = Offset(difat);
                var per = _sectorSize / 4 - 1;
                for (var i = 0; i < per; i++)
                {
                    var s = (uint)I32(data, off + i * 4);
                    if (s < 0xFFFFFFFA)
                        fatSectors.Add(s);
                }
                difat = (uint)I32(data, off + per * 4);
            }
            foreach (var s in fatSectors)
            {
                var off = Offset(s);
                for (var i = 0; i < _sectorSize / 4; i++)
                    _fat.Add((uint)I32(data, off + i * 4));
            }

            var dir = Chain((uint)I32(data, 0x30), long.MaxValue, mini: false);
            (uint Start, long Size) root = (EndOfChain, 0);
            for (var p = 0; p + 128 <= dir.Length; p += 128)
            {
                var nameLen = U16(dir, p + 64);
                var type = dir[p + 66];
                if (nameLen < 2 || nameLen > 64 || type == 0)
                    continue;
                var name = Encoding.Unicode.GetString(dir, p, nameLen - 2);
                var start = (uint)I32(dir, p + 116);
                var size = (uint)I32(dir, p + 120);
                if (type == 5)
                    root = (start, size);
                else if (type == 2)
                    _entries.TryAdd(name, (start, size));
            }

            var miniFatStart = (uint)I32(data, 0x3C);
            if (miniFatStart < 0xFFFFFFFA)
            {
                var mf = Chain(miniFatStart, long.MaxValue, mini: false);
                for (var i = 0; i + 4 <= mf.Length; i += 4)
                    _miniFat.Add((uint)I32(mf, i));
            }
            if (root.Start < 0xFFFFFFFA)
                _miniStream = Chain(root.Start, root.Size, mini: false);
        }

        public byte[]? Stream(string name)
        {
            if (!_entries.TryGetValue(name, out var e))
                return null;
            if (e.Size > ReadLimits.MaxFileBytes)
                throw new DocumentReadException("A stream inside the .doc file is too large.");
            return e.Size < _miniCutoff ? Chain(e.Start, e.Size, mini: true) : Chain(e.Start, e.Size, mini: false);
        }

        private int Offset(uint sector)
        {
            var off = (long)(sector + 1) * _sectorSize;
            if (off + _sectorSize > _data.Length)
                throw new DocumentReadException("The .doc file is truncated.");
            return (int)off;
        }

        private byte[] Chain(uint start, long size, bool mini)
        {
            var fat = mini ? _miniFat : _fat;
            var unit = mini ? _miniSectorSize : _sectorSize;
            using var ms = new MemoryStream();
            var sector = start;
            var guard = 0;
            while (sector < 0xFFFFFFFA && ms.Length < size)
            {
                if (guard++ > fat.Count + 1)
                    throw new DocumentReadException("The .doc file has a looping sector chain.");
                if (mini)
                {
                    var off = (long)sector * unit;
                    if (off + unit > _miniStream.Length)
                        break;
                    ms.Write(_miniStream, (int)off, unit);
                }
                else
                {
                    ms.Write(_data, Offset(sector), unit);
                }
                if (sector >= fat.Count)
                    break;
                sector = fat[(int)sector];
            }
            var bytes = ms.ToArray();
            return size < bytes.Length ? bytes[..(int)size] : bytes;
        }
    }
}
