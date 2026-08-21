namespace Coaching.Application.Attachments;

public static class AssignmentAttachmentPolicy
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

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
        if (!AllowedTypes.TryGetValue(extension, out var expectedContentType)
            || !string.Equals(expectedContentType, contentType.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only JPEG, PNG and WebP photos are supported.", nameof(contentType));
        }
    }

    public static bool IsSupportedContentType(string contentType) =>
        AllowedTypes.Values.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public static void ValidateContentMetadata(string contentType, long sizeBytes)
    {
        if (sizeBytes is <= 0 or > MaxFileSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "File size is outside the supported range.");

        if (string.IsNullOrWhiteSpace(contentType)
            || !AllowedTypes.Values.Contains(contentType.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only JPEG, PNG and WebP photos are supported.", nameof(contentType));
        }
    }
}
