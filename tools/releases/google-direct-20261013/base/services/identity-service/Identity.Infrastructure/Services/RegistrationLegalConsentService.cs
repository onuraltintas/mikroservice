using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class RegistrationLegalConsentService(
    IdentityDbContext db,
    IPlatformLegalPages legalPages) : IRegistrationLegalConsentService
{
    public async Task<EduPlatform.Shared.Kernel.Results.Result<IReadOnlyList<PlatformLegalPageDto>>> ValidateAsync(
        PlatformProduct product,
        IEnumerable<LegalPageAcceptance>? acceptances,
        CancellationToken cancellationToken = default)
    {
        var currentPages = new List<PlatformLegalPageDto>();
        foreach (var slug in RegistrationLegalConsentPolicy.RequiredSlugs(product))
        {
            var page = await legalPages.GetPublishedAsync(slug, cancellationToken);
            if (page is not null)
                currentPages.Add(page);
        }

        return RegistrationLegalConsentPolicy.Validate(product, acceptances, currentPages);
    }

    public void TrackAcceptedDocuments(
        Guid userId,
        PlatformProduct product,
        IReadOnlyList<PlatformLegalPageDto> documents,
        string registrationMethod)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A user identity is required to track legal acceptance.", nameof(userId));
        if (string.IsNullOrWhiteSpace(registrationMethod) || registrationMethod.Length > 32)
            throw new ArgumentException("A valid registration method is required.", nameof(registrationMethod));

        var acceptedAt = DateTimeOffset.UtcNow;
        db.RegistrationLegalDocumentAcceptances.AddRange(documents.Select(document =>
            new RegistrationLegalDocumentAcceptance
            {
                UserId = userId,
                Product = product,
                DocumentSlug = document.Slug,
                DocumentVersion = document.Version,
                Action = document.Slug.EndsWith("-terms", StringComparison.Ordinal)
                    ? RegistrationLegalDocumentAction.Accepted
                    : RegistrationLegalDocumentAction.Acknowledged,
                RegistrationMethod = registrationMethod,
                AcceptedAt = acceptedAt
            }));
    }
}
