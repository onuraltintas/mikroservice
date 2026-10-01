namespace SpeedReading.Application.Content;

public static class CmsPageSlugPolicy
{
    public const string SharedPrivacySlug = "privacy";
    public const string SharedKvkkSlug = "kvkk";

    public static bool IsSharedLegalPageSlug(string? slug)
    {
        var normalized = slug?.Trim();
        return string.Equals(normalized, SharedPrivacySlug, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, SharedKvkkSlug, StringComparison.OrdinalIgnoreCase);
    }
}
