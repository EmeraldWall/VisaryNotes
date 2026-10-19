namespace VisaryNotes.Core.Documents;

/// <summary>Guards that keep a malformed or hostile file from hanging or exhausting the app (docs/SPEC.md 7.9).</summary>
public static class ReadLimits
{
    public const long MaxFileBytes = 64L * 1024 * 1024;
    public const long MaxUncompressedBytes = 256L * 1024 * 1024;
    public const int MaxZipEntries = 5000;
    public const int MaxNestingDepth = 256;

    public static byte[] ReadAll(Stream stream)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int n;
        while ((n = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (ms.Length + n > MaxFileBytes)
                throw new DocumentReadException($"The file is larger than {MaxFileBytes / (1024 * 1024)} MB.");
            ms.Write(buffer, 0, n);
        }
        return ms.ToArray();
    }
}

public sealed class DocumentReadException : Exception
{
    public DocumentReadException(string message, Exception? inner = null) : base(message, inner) { }
}
