namespace ProphetOps.Api;

public static class ImageUpload
{
    public const long MaxBytes = 4 * 1024 * 1024;

    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        [".jpg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
    };

    public static string? ContentTypeFor(string storedName)
    {
        var extension = Path.GetExtension(storedName).ToLowerInvariant();
        return ContentTypes.TryGetValue(extension, out var type) ? type : null;
    }

    // The extension is taken from the file's own leading bytes rather than from the name or
    // content type the browser sent, because both of those are supplied by the caller and a
    // caller who can name the file can otherwise choose what the server later serves it as.
    public static string? SniffExtension(Stream stream)
    {
        if (!stream.CanSeek) return null;

        Span<byte> head = stackalloc byte[12];
        stream.Position = 0;
        var read = stream.Read(head);
        stream.Position = 0;
        if (read < head.Length) return null;

        if (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return ".jpg";

        if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47
            && head[4] == 0x0D && head[5] == 0x0A && head[6] == 0x1A && head[7] == 0x0A)
            return ".png";

        if (head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46
            && head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50)
            return ".webp";

        return null;
    }

    public static string NewStoredName(string extension) =>
        Guid.NewGuid().ToString("N") + extension;
}
