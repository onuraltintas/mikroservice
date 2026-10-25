namespace Coaching.Application.Attachments;

public static class AssignmentAttachmentPolicy
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string[]> AllowedTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"],
            [".webp"] = ["image/webp"],
            [".pdf"] = ["application/pdf"],
            [".doc"] = ["application/msword"],
            [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
            [".md"] = ["text/markdown", "text/plain", "text/x-markdown"]
        };

    private static readonly string[] SupportedContentTypes = AllowedTypes.Values
        .SelectMany(types => types)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static string ContentTypeForFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedTypes.TryGetValue(extension, out var contentTypes))
            throw new ArgumentException("Unsupported assignment attachment file type.", nameof(fileName));

        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            ? "text/markdown"
            : contentTypes[0];
    }

    public static void ValidateMetadata(string fileName, string contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));

        if (fileName.Length > 255
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.Any(char.IsControl))
            throw new ArgumentException("File name is invalid.", nameof(fileName));

        ValidateContentMetadata(contentType, sizeBytes);

        var extension = Path.GetExtension(fileName);
        if (!AllowedTypes.TryGetValue(extension, out var expectedContentTypes)
            || !expectedContentTypes.Contains(contentType.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The file extension and content type do not match a supported attachment type.", nameof(contentType));
        }
    }

    public static bool IsSupportedContentType(string contentType) =>
        SupportedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public static void ValidateContentMetadata(string contentType, long sizeBytes)
    {
        if (sizeBytes is <= 0 or > MaxFileSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "File size is outside the supported range.");

        if (string.IsNullOrWhiteSpace(contentType)
            || !SupportedContentTypes.Contains(contentType.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The attachment type is not supported.", nameof(contentType));
        }
    }
}
