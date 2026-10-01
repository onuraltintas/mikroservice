namespace Identity.Application.LegalPages;

public sealed record PlatformLegalPageUpdateRequest(string Title, string Content, bool IsPublished);

public sealed record PlatformLegalPageDto(
    string Slug,
    string Title,
    string Content,
    bool IsPublished,
    bool IsArchived,
    DateTime? ArchivedAt,
    int Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? UpdatedBy);

public sealed record PlatformLegalPageRevisionDto(
    Guid Id,
    string Slug,
    string Title,
    string Content,
    int Version,
    bool IsArchived,
    DateTime CreatedAt,
    Guid CreatedBy);

public interface IPlatformLegalPages
{
    Task<PlatformLegalPageDto?> GetPublishedAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformLegalPageDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PlatformLegalPageDto?> GetAsync(string slug, CancellationToken cancellationToken = default);
    Task<PlatformLegalPageDto> UpsertAsync(
        string slug,
        PlatformLegalPageUpdateRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);
    Task<int> CreateStarterDraftsAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<PlatformLegalPageDto?> ArchiveAsync(string slug, Guid actorId, CancellationToken cancellationToken = default);
    Task<PlatformLegalPageDto?> RestoreAsync(string slug, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformLegalPageRevisionDto>> GetRevisionsAsync(
        string slug,
        CancellationToken cancellationToken = default);
}

public interface IRegistrationLegalConsentService
{
    Task<EduPlatform.Shared.Kernel.Results.Result<IReadOnlyList<PlatformLegalPageDto>>> ValidateAsync(
        Identity.Domain.Enums.PlatformProduct product,
        IEnumerable<LegalPageAcceptance>? acceptances,
        CancellationToken cancellationToken = default);

    void TrackAcceptedDocuments(
        Guid userId,
        Identity.Domain.Enums.PlatformProduct product,
        IReadOnlyList<PlatformLegalPageDto> documents,
        string registrationMethod);
}
