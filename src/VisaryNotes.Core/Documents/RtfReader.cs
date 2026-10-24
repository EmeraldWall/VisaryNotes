using System.Globalization;
using System.Text;

namespace VisaryNotes.Core.Documents;

/// <summary>
/// Streaming RTF reader that produces the shared document model (docs/SPEC.md 7.5).
/// Based on the Microsoft RTF 1.9.1 specification. Unknown destinations are skipped safely.
/// </summary>
public sealed class RtfReader
{
    static RtfReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const double TwipsToMm = 25.4 / 1440.0;

    private enum Dest
    {
        Normal,
        Skip,
        FontTable,
        ColorTable,
        Info,
        Title,
        Author,
        Picture,
        FieldInstruction,
        FieldResult,
        Header,
        Footer
    }

    private sealed class State
    {
        public TextStyle Text = TextStyle.Plain;
        public ParagraphStyle Para = ParagraphStyle.Default;
        public Dest Dest = Dest.Normal;
        public int UnicodeSkip = 1;
        public bool InTable;
        public string? Link;
        public int FontIndex = -1;

        public State Clone() => (State)MemberwiseClone();
    }

    private readonly byte[] _data;
    private int _pos;
    private readonly Stack<State> _stack = new();
    private State _s = new();

    private readonly Document _doc = new() { Format = DocumentFormat.Rtf };
    private readonly Dictionary<int, string> _fonts = new();
    private readonly List<string?> _colors = new();
    private int _codePage = 1252;

    private readonly List<Run> _runs = new();
    private readonly StringBuilder _runText = new();
    private TextStyle _runStyle = TextStyle.Plain;
    private string? _runLink;

    private readonly List<byte> _pendingBytes = new();
    private int _skipChars;

    private TableBlock? _table;
    private List<TableCell>? _row;
    private TableCell? _cell;

    private readonly StringBuilder _fontName = new();
    private int _fontNumber;
    private int? _colorR, _colorG, _colorB;
    private readonly StringBuilder _special = new();
    private readonly StringBuilder _pictHex = new();
    private string? _pictMime;
    private string? _pendingFieldLink;

    private double _paperW = 210, _paperH = 297, _mTop = 25.4, _mBottom = 25.4, _mLeft = 31.75, _mRight = 31.75;

    private RtfReader(byte[] data) => _data = data;

    public static Document Read(Stream stream, string fileName)
    {
        var bytes = ReadLimits.ReadAll(stream);
        if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "{\\rtf")
            throw new DocumentReadException("This file is not a valid RTF document.");
        var reader = new RtfReader(bytes);
        reader.Parse();
        var doc = reader._doc;
        if (string.IsNullOrWhiteSpace(doc.Title))
            doc.Title = Path.GetFileNameWithoutExtension(fileName);
        return doc;
    }

    private void Parse()
    {
        while (_pos < _data.Length)
        {
            var c = (char)_data[_pos];
            switch (c)
            {
                case '{':
                    FlushBytes();
                    if (_stack.Count >= ReadLimits.MaxNestingDepth)
                        throw new DocumentReadException("The RTF file is nested too deeply.");
                    _stack.Push(_s);
                    _s = _s.Clone();
                    _pos++;
                    break;
                case '}':
                    FlushBytes();
                    EndGroup();
                    _pos++;
                    break;
                case '\\':
                    ReadControl();
                    break;
                case '\r':
                case '\n':
                    _pos++;
                    break;
                default:
                    FlushBytes();
                    AddChar(c);
                    _pos++;
                    break;
            }
        }
        FlushBytes();
        EndParagraph();
        CloseTable();
        _doc.Page = new PageSetup(_paperW, _paperH, _mTop, _mRight, _mBottom, _mLeft);
    }

    private void EndGroup()
    {
        var ending = _s;
        if (_stack.Count == 0)
            return;
        _s = _stack.Pop();

        switch (ending.Dest)
        {
            case Dest.FontTable when _s.Dest != Dest.FontTable || _fontName.Length > 0:
                CommitFont();
                break;
            case Dest.Picture when _s.Dest != Dest.Picture:
                CommitPicture();
                break;
            case Dest.FieldInstruction when _s.Dest != Dest.FieldInstruction:
                _pendingFieldLink = ParseHyperlink(_special.ToString());
                _special.Clear();
                break;
            case Dest.Title when _s.Dest != Dest.Title:
                _doc.Title = _special.ToString().Trim();
                _special.Clear();
                break;
            case Dest.Author when _s.Dest != Dest.Author:
                _doc.Author = _special.ToString().Trim();
                _special.Clear();
                break;
            case Dest.Header when _s.Dest != Dest.Header:
                _doc.HeaderText ??= Clean(_special.ToString());
                _special.Clear();
                break;
            case Dest.Footer when _s.Dest != Dest.Footer:
                _doc.FooterText ??= Clean(_special.ToString());
                _special.Clear();
                break;
            case Dest.FieldResult when _s.Dest != Dest.FieldResult:
                _pendingFieldLink = null;
                break;
        }
        if (ending.Dest == Dest.Normal || ending.Dest == Dest.FieldResult)
            _skipChars = 0;
    }

    private static string? Clean(string s)
    {
        var t = s.Trim();
        return t.Length == 0 ? null : t;
    }

    private void ReadControl()
    {
        _pos++;
        if (_pos >= _data.Length)
            return;
        var c = (char)_data[_pos];

        if (!char.IsAsciiLetter(c))
        {
            _pos++;
            switch (c)
            {
                case '\'':
                    if (_pos + 2 <= _data.Length &&
                        byte.TryParse(Encoding.ASCII.GetString(_data, _pos, 2), NumberStyles.HexNumber, null, out var b))
                    {
                        _pos += 2;
                        if (_skipChars > 0)
                        {
                            _skipChars--;
                            return;
                        }
                        _pendingBytes.Add(b);
                    }
                    return;
                case '\\':
                case '{':
                case '}':
                    FlushBytes();
                    AddChar(c);
                    return;
                case '~':
                    FlushBytes();
                    AddChar(' ');
                    return;
                case '_':
                    FlushBytes();
                    AddChar('-');
                    return;
                case '*':
                    // An optional destination: skip it unless the next control word is one we understand.
                    if (!PeekKnownDestination())
                        _s.Dest = Dest.Skip;
                    return;
                case '\r':
                case '\n':
                    FlushBytes();
                    EndParagraph();
                    return;
                default:
                    return;
            }
        }

        var start = _pos;
        while (_pos < _data.Length && char.IsAsciiLetter((char)_data[_pos]))
            _pos++;
        var word = Encoding.ASCII.GetString(_data, start, _pos - start);

        int? param = null;
        var negative = false;
        if (_pos < _data.Length && _data[_pos] == '-')
        {
            negative = true;
            _pos++;
        }
        var numStart = _pos;
        while (_pos < _data.Length && char.IsAsciiDigit((char)_data[_pos]) && _pos - numStart < 10)
            _pos++;
        if (_pos > numStart)
        {
            param = int.Parse(Encoding.ASCII.GetString(_data, numStart, _pos - numStart), CultureInfo.InvariantCulture);
            if (negative)
                param = -param;
        }
        if (_pos < _data.Length && _data[_pos] == ' ')
            _pos++;

        if (word != "u")
            FlushBytes();
        Apply(word, param);
    }

    private bool PeekKnownDestination()
    {
        var p = _pos;
        while (p < _data.Length && (_data[p] == ' ' || _data[p] == '\r' || _data[p] == '\n'))
            p++;
        if (p >= _data.Length || _data[p] != '\\')
            return false;
        p++;
        var s = p;
        while (p < _data.Length && char.IsAsciiLetter((char)_data[p]))
            p++;
        var w = Encoding.ASCII.GetString(_data, s, p - s);
        return w is "fldinst" or "shppict";
    }

    private void Apply(string word, int? p)
    {
        var on = p != 0;
        switch (word)
        {
            // Destinations
            case "fonttbl": _s.Dest = Dest.FontTable; break;
            case "colortbl": _s.Dest = Dest.ColorTable; _colors.Clear(); break;
            case "info": _s.Dest = Dest.Info; break;
            case "title": _s.Dest = Dest.Title; _special.Clear(); break;
            case "author": _s.Dest = Dest.Author; _special.Clear(); break;
            case "stylesheet" or "listtable" or "listoverridetable" or "revtbl" or "rsidtbl" or "generator"
                or "xmlnstbl" or "themedata" or "colorschememapping" or "latentstyles" or "datastore"
                or "mmathPr" or "pgdsctbl" or "operator" or "company" or "creatim" or "revtim" or "printim"
                or "buptim" or "keywords" or "subject" or "comment" or "doccomm" or "nonshppict" or "object"
                or "footnote" or "annotation" or "bkmkstart" or "bkmkend" or "pntxtb" or "pntxta":
                _s.Dest = Dest.Skip;
                break;
            case "header" or "headerl" or "headerr" or "headerf":
                _s.Dest = Dest.Header; _special.Clear();
                break;
            case "footer" or "footerl" or "footerr" or "footerf":
                _s.Dest = Dest.Footer; _special.Clear();
                break;
            case "pict":
                _s.Dest = Dest.Picture; _pictHex.Clear(); _pictMime = null;
                break;
            case "pngblip": _pictMime = "image/png"; break;
            case "jpegblip": _pictMime = "image/jpeg"; break;
            case "wmetafile" or "emfblip" or "macpict": _pictMime ??= "unsupported"; break;
            case "fldinst": _s.Dest = Dest.FieldInstruction; _special.Clear(); break;
            case "fldrslt": _s.Dest = Dest.FieldResult; _s.Link = _pendingFieldLink; break;

            // Document
            case "ansicpg" when p is > 0: _codePage = p.Value; break;
            case "paperw" when p is > 0: _paperW = p.Value * TwipsToMm; break;
            case "paperh" when p is > 0: _paperH = p.Value * TwipsToMm; break;
            case "margl" when p is >= 0: _mLeft = p.Value * TwipsToMm; break;
            case "margr" when p is >= 0: _mRight = p.Value * TwipsToMm; break;
            case "margt" when p is >= 0: _mTop = p.Value * TwipsToMm; break;
            case "margb" when p is >= 0: _mBottom = p.Value * TwipsToMm; break;
            case "uc" when p is >= 0: _s.UnicodeSkip = p.Value; break;

            // Font and colour tables
            case "f" when _s.Dest == Dest.FontTable:
                CommitFont();
                _fontNumber = p ?? 0;
                break;
            case "red" when _s.Dest == Dest.ColorTable: _colorR = p; break;
            case "green" when _s.Dest == Dest.ColorTable: _colorG = p; break;
            case "blue" when _s.Dest == Dest.ColorTable: _colorB = p; break;

            // Character formatting
            case "plain": _s.Text = TextStyle.Plain; break;
            case "b": _s.Text = _s.Text with { Bold = on }; break;
            case "i": _s.Text = _s.Text with { Italic = on }; break;
            case "ul": _s.Text = _s.Text with { Underline = on }; break;
            case "ulnone": _s.Text = _s.Text with { Underline = false }; break;
            case "strike": _s.Text = _s.Text with { Strike = on }; break;
            case "super": _s.Text = _s.Text with { Superscript = true, Subscript = false }; break;
            case "sub": _s.Text = _s.Text with { Subscript = true, Superscript = false }; break;
            case "nosupersub": _s.Text = _s.Text with { Subscript = false, Superscript = false }; break;
            case "fs" when p is > 0: _s.Text = _s.Text with { FontSizePt = p.Value / 2.0 }; break;
            case "f" when p is >= 0:
                _s.FontIndex = p.Value;
                _s.Text = _s.Text with { FontFamily = _fonts.GetValueOrDefault(p.Value) };
                break;
            case "cf": _s.Text = _s.Text with { Color = ColorAt(p) }; break;
            case "highlight" or "cb" or "chcbpat": _s.Text = _s.Text with { Highlight = ColorAt(p) }; break;

            // Paragraph formatting
            case "pard":
                _s.Para = ParagraphStyle.Default;
                _s.InTable = false;
                break;
            case "ql": _s.Para = _s.Para with { Align = TextAlign.Start }; break;
            case "qc": _s.Para = _s.Para with { Align = TextAlign.Center }; break;
            case "qr": _s.Para = _s.Para with { Align = TextAlign.End }; break;
            case "qj": _s.Para = _s.Para with { Align = TextAlign.Justify }; break;
            case "li" when p.HasValue: _s.Para = _s.Para with { IndentLeftMm = p.Value * TwipsToMm }; break;
            case "fi" when p.HasValue: _s.Para = _s.Para with { IndentFirstLineMm = p.Value * TwipsToMm }; break;
            case "sb" when p.HasValue: _s.Para = _s.Para with { SpaceBeforePt = p.Value / 20.0 }; break;
            case "sa" when p.HasValue: _s.Para = _s.Para with { SpaceAfterPt = p.Value / 20.0 }; break;

            // Text
            case "par": EndParagraph(); break;
            case "line": AddText("\n"); break;
            case "tab": AddText("\t"); break;
            case "page": EndParagraph(); _doc.Blocks.Add(new PageBreak()); break;
            case "emdash": AddChar('—'); break;
            case "endash": AddChar('–'); break;
            case "bullet": AddChar('•'); break;
            case "lquote": AddChar('‘'); break;
            case "rquote": AddChar('’'); break;
            case "ldblquote": AddChar('“'); break;
            case "rdblquote": AddChar('”'); break;
            case "u" when p.HasValue:
                FlushBytes();
                AddChar((char)(p.Value < 0 ? p.Value + 65536 : p.Value));
                _skipChars = _s.UnicodeSkip;
                break;

            // Tables
            case "trowd": _row ??= new List<TableCell>(); break;
            case "intbl": _s.InTable = true; break;
            case "cell": EndCell(); break;
            case "row": EndRow(); break;
        }
    }

    private string? ColorAt(int? index) =>
        index is > 0 && index.Value < _colors.Count ? _colors[index.Value] : null;

    private void AddChar(char c)
    {
        if (_skipChars > 0)
        {
            _skipChars--;
            return;
        }

        switch (_s.Dest)
        {
            case Dest.Skip or Dest.Info:
                return;
            case Dest.FontTable:
                if (c == ';')
                    CommitFont();
                else
                    _fontName.Append(c);
                return;
            case Dest.ColorTable:
                if (c == ';')
                {
                    _colors.Add(_colorR.HasValue ? $"#{_colorR:X2}{_colorG ?? 0:X2}{_colorB ?? 0:X2}" : null);
                    _colorR = _colorG = _colorB = null;
                }
                return;
            case Dest.Picture:
                if (Uri.IsHexDigit(c))
                    _pictHex.Append(c);
                return;
            case Dest.Title or Dest.Author or Dest.FieldInstruction or Dest.Header or Dest.Footer:
                _special.Append(c);
                return;
        }
        AddText(c.ToString());
    }

    private void FlushBytes()
    {
        if (_pendingBytes.Count == 0)
            return;
        Encoding enc;
        try
        {
            enc = Encoding.GetEncoding(_codePage);
        }
        catch (ArgumentException)
        {
            enc = Encoding.GetEncoding(1252);
        }
        var text = enc.GetString(_pendingBytes.ToArray());
        _pendingBytes.Clear();
        var saved = _skipChars;
        _skipChars = 0;
        foreach (var ch in text)
            AddChar(ch);
        _skipChars = saved;
    }

    private void AddText(string text)
    {
        if (_s.Dest is not (Dest.Normal or Dest.FieldResult))
            return;
        var style = _s.Text;
        var link = _s.Link;
        if (_runText.Length > 0 && (style != _runStyle || link != _runLink))
            FlushRun();
        _runStyle = style;
        _runLink = link;
        _runText.Append(text);
    }

    private void FlushRun()
    {
        if (_runText.Length == 0)
            return;
        _runs.Add(new Run(_runText.ToString(), _runStyle, _runLink));
        _runText.Clear();
    }

    private void EndParagraph()
    {
        if (_s.Dest is not (Dest.Normal or Dest.FieldResult))
            return;
        FlushRun();
        var para = new Paragraph { Style = _s.Para };
        para.Runs.AddRange(_runs);
        _runs.Clear();

        if (_s.InTable)
        {
            _cell ??= new TableCell();
            _cell.Blocks.Add(para);
            return;
        }

        CloseTable();
        _doc.Blocks.Add(para);
    }

    private void EndCell()
    {
        FlushRun();
        _cell ??= new TableCell();
        if (_runs.Count > 0)
        {
            var para = new Paragraph { Style = _s.Para };
            para.Runs.AddRange(_runs);
            _runs.Clear();
            _cell.Blocks.Add(para);
        }
        _row ??= new List<TableCell>();
        _row.Add(_cell);
        _cell = null;
    }

    private void EndRow()
    {
        if (_row is { Count: > 0 })
        {
            _table ??= new TableBlock();
            _table.Rows.Add(_row);
        }
        _row = null;
    }

    private void CloseTable()
    {
        if (_row is { Count: > 0 })
            EndRow();
        if (_table is null)
            return;
        _doc.Blocks.Add(_table);
        _table = null;
    }

    private void CommitFont()
    {
        var name = _fontName.ToString().Trim().TrimEnd(';');
        if (name.Length > 0)
            _fonts[_fontNumber] = name;
        _fontName.Clear();
    }

    private void CommitPicture()
    {
        if (_pictMime is "image/png" or "image/jpeg" && _pictHex.Length >= 2)
        {
            try
            {
                var hex = _pictHex.Length % 2 == 0 ? _pictHex.ToString() : _pictHex.ToString(0, _pictHex.Length - 1);
                var bytes = Convert.FromHexString(hex);
                EndParagraphIfNeeded();
                _doc.Blocks.Add(new ImageBlock(bytes, _pictMime));
            }
            catch (FormatException)
            {
                _doc.Warnings.Add("A picture could not be decoded and was skipped.");
            }
        }
        else if (_pictMime is not null)
        {
            _doc.Warnings.Add("A WMF, EMF or Mac picture was skipped. Only PNG and JPEG pictures are shown.");
        }
        _pictHex.Clear();
        _pictMime = null;
    }

    private void EndParagraphIfNeeded()
    {
        if (_runText.Length > 0 || _runs.Count > 0)
            EndParagraph();
    }

    private static string? ParseHyperlink(string instruction)
    {
        var text = instruction.Trim();
        if (!text.StartsWith("HYPERLINK", StringComparison.OrdinalIgnoreCase))
            return null;
        var first = text.IndexOf('"');
        var last = first >= 0 ? text.IndexOf('"', first + 1) : -1;
        var url = first >= 0 && last > first ? text[(first + 1)..last] : text[9..].Trim();
        return Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme is "http" or "https" or "mailto") ? u.ToString() : null;
    }
}
