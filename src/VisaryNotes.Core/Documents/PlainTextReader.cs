using System.Text;

namespace VisaryNotes.Core.Documents;

/// <summary>Reads .txt files with encoding and line ending detection (docs/SPEC.md 7.5).</summary>
public static class PlainTextReader
{
    static PlainTextReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static Document Read(Stream stream, string fileName)
    {
        var bytes = ReadLimits.ReadAll(stream);
        var (text, encoding) = Decode(bytes);
        var doc = new Document { Title = Path.GetFileName(fileName), Format = DocumentFormat.PlainText };
        doc.Warnings.Add($"Encoding: {encoding.WebName}. Line endings: {DetectLineEnding(text)}.");

        // Original view keeps the file exactly as written. Reading view reflows it (see HtmlRenderer).
        doc.Blocks.Add(new Preformatted(text.Replace("\r\n", "\n").Replace('\r', '\n')));
        return doc;
    }

    public static (string Text, Encoding Encoding) Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), Encoding.UTF8);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), Encoding.Unicode);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), Encoding.BigEndianUnicode);

        var strictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
        try
        {
            return (strictUtf8.GetString(bytes), Encoding.UTF8);
        }
        catch (DecoderFallbackException)
        {
            var legacy = Encoding.GetEncoding(1252);
            return (legacy.GetString(bytes), legacy);
        }
    }

    public static string DetectLineEnding(string text)
    {
        var crlf = 0;
        var lf = 0;
        var cr = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
            else if (text[i] == '\n')
            {
                lf++;
            }
        }
        if (crlf >= lf && crlf >= cr && crlf > 0) return "CRLF";
        if (cr > lf) return "CR";
        return "LF";
    }
}
