using SpeedReading.Domain.Assessment;

namespace SpeedReading.Application.Assessment;

public sealed record SpeedReadingLevelCatalogSnapshot(
    Guid? Id,
    string Version,
    string Name,
    AssessmentLevelCatalogStatus Status,
    IReadOnlyList<SpeedReadingLevelDefinition> Definitions,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateSpeedReadingLevelCatalogRequest(
    string Version,
    string Name,
    IReadOnlyList<SpeedReadingLevelDefinition> Definitions);

public sealed record UpdateSpeedReadingLevelCatalogRequest(
    string Name,
    IReadOnlyList<SpeedReadingLevelDefinition> Definitions);

public interface ISpeedReadingLevelCatalog
{
    Task<IReadOnlyList<SpeedReadingLevelCatalogSnapshot>> GetAllAsync(CancellationToken cancellationToken);
    Task<SpeedReadingLevelCatalogSnapshot> GetActiveAsync(CancellationToken cancellationToken);
    Task<SpeedReadingLevelCatalogSnapshot?> GetByVersionAsync(string version, CancellationToken cancellationToken);
    Task<SpeedReadingLevelCatalogSnapshot> CreateAsync(Guid actorId, CreateSpeedReadingLevelCatalogRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, Guid actorId, UpdateSpeedReadingLevelCatalogRequest request, CancellationToken cancellationToken);
    Task<bool> PublishAsync(Guid id, Guid actorId, CancellationToken cancellationToken);
}

public sealed class DefaultSpeedReadingLevelCatalog : ISpeedReadingLevelCatalog
{
    private static readonly SpeedReadingLevelCatalogSnapshot Default = new(
        null,
        SpeedReadingLevelRules.DefaultCatalogVersion,
        "Standart Seviye Kataloğu",
        AssessmentLevelCatalogStatus.Published,
        SpeedReadingLevelRules.Definitions,
        null,
        DateTime.UnixEpoch,
        null);

    public Task<IReadOnlyList<SpeedReadingLevelCatalogSnapshot>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpeedReadingLevelCatalogSnapshot>>([Default]);

    public Task<SpeedReadingLevelCatalogSnapshot> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult(Default);

    public Task<SpeedReadingLevelCatalogSnapshot?> GetByVersionAsync(string version, CancellationToken cancellationToken) =>
        Task.FromResult<SpeedReadingLevelCatalogSnapshot?>(
            string.Equals(version, Default.Version, StringComparison.OrdinalIgnoreCase) ? Default : null);

    public Task<SpeedReadingLevelCatalogSnapshot> CreateAsync(Guid actorId, CreateSpeedReadingLevelCatalogRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Editable level catalogs require owned Speed Reading data.");

    public Task<bool> UpdateAsync(Guid id, Guid actorId, UpdateSpeedReadingLevelCatalogRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Editable level catalogs require owned Speed Reading data.");

    public Task<bool> PublishAsync(Guid id, Guid actorId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Editable level catalogs require owned Speed Reading data.");
}
