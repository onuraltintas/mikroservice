using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Enums;

namespace Identity.Application.LegalPages;

public sealed record LegalPageAcceptance(string Slug, int Version);

public static class RegistrationLegalConsentPolicy
{
    private static readonly string[] SharedRequiredSlugs = ["privacy", "kvkk"];

    public static IReadOnlyList<string> RequiredSlugs(PlatformProduct product) => product switch
    {
        PlatformProduct.Coaching => [.. SharedRequiredSlugs, "coaching-terms"],
        PlatformProduct.SpeedReading => [.. SharedRequiredSlugs, "speed-reading-terms"],
        _ => throw new ArgumentOutOfRangeException(nameof(product), product, "Unsupported platform product.")
    };

    public static Result<IReadOnlyList<PlatformLegalPageDto>> Validate(
        PlatformProduct product,
        IEnumerable<LegalPageAcceptance>? acceptances,
        IEnumerable<PlatformLegalPageDto> publishedPages)
    {
        var requiredSlugs = RequiredSlugs(product);
        var submitted = (acceptances ?? [])
            .Select(item => new LegalPageAcceptance(item.Slug?.Trim().ToLowerInvariant() ?? string.Empty, item.Version))
            .ToArray();

        if (submitted.Any(item => item.Slug.Length == 0 || item.Version < 1)
            || submitted.Select(item => item.Slug).Distinct(StringComparer.Ordinal).Count() != submitted.Length
            || submitted.Any(item => !requiredSlugs.Contains(item.Slug, StringComparer.Ordinal)))
        {
            return Result.Failure<IReadOnlyList<PlatformLegalPageDto>>(new Error(
                "Auth.InvalidLegalAcceptance",
                "Yasal belge onay bilgisi geçersiz. Sayfayı yenileyip tekrar deneyin."));
        }

        var currentPages = publishedPages.ToDictionary(page => page.Slug, StringComparer.Ordinal);
        if (requiredSlugs.Any(slug => !currentPages.TryGetValue(slug, out var page)
            || !page.IsPublished
            || page.IsArchived
            || string.IsNullOrWhiteSpace(page.Content)))
        {
            return Result.Failure<IReadOnlyList<PlatformLegalPageDto>>(new Error(
                "Auth.LegalDocumentUnavailable",
                "Kayıt için gerekli yasal metinlerden biri şu anda yayımlanmamış. Lütfen daha sonra tekrar deneyin."));
        }

        var submittedBySlug = submitted.ToDictionary(item => item.Slug, StringComparer.Ordinal);
        var requiredPages = requiredSlugs.Select(slug => currentPages[slug]).ToArray();
        if (submitted.Length != requiredSlugs.Count
            || requiredPages.Any(page => !submittedBySlug.TryGetValue(page.Slug, out var acceptance)
                || acceptance.Version != page.Version))
        {
            return Result.Failure<IReadOnlyList<PlatformLegalPageDto>>(new Error(
                "Auth.LegalAcceptanceRequired",
                "Kayıt için yayımlanmış yasal metinlerin güncel sürümlerini inceleyip onaylamanız gerekir."));
        }

        return Result.Success<IReadOnlyList<PlatformLegalPageDto>>(requiredPages);
    }
}
