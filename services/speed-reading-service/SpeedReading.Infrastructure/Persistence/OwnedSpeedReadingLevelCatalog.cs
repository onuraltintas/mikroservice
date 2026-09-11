using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingLevelCatalog(OwnedSpeedReadingDbContext db) : ISpeedReadingLevelCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DefaultSpeedReadingLevelCatalog Fallback = new();

    public async Task<IReadOnlyList<SpeedReadingLevelCatalogSnapshot>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await db.AssessmentLevelCatalogs
            .AsNoTracking()
            .OrderByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Count == 0
            ? await Fallback.GetAllAsync(cancellationToken)
            : rows.Select(ToSnapshot).ToList();
    }

    public async Task<SpeedReadingLevelCatalogSnapshot> GetActiveAsync(CancellationToken cancellationToken)
    {
        var row = await db.AssessmentLevelCatalogs
            .AsNoTracking()
            .Where(item => item.Status == AssessmentLevelCatalogStatus.Published)
            .OrderByDescending(item => item.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? await Fallback.GetActiveAsync(cancellationToken) : ToSnapshot(row);
    }

    public async Task<SpeedReadingLevelCatalogSnapshot?> GetByVersionAsync(string version, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(version))
            return null;
        var row = await db.AssessmentLevelCatalogs
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CatalogVersion == version.Trim(), cancellationToken);
        return row is null
            ? await Fallback.GetByVersionAsync(version, cancellationToken)
            : ToSnapshot(row);
    }

    public async Task<SpeedReadingLevelCatalogSnapshot> CreateAsync(
        Guid actorId,
        CreateSpeedReadingLevelCatalogRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        SpeedReadingLevelRules.ValidateDefinitions(request.Definitions);
        var version = request.Version?.Trim() ?? string.Empty;
        if (await db.AssessmentLevelCatalogs.AnyAsync(item => item.CatalogVersion == version, cancellationToken))
            throw new ArgumentException("A level catalog with this version already exists.", nameof(request));
        var row = AssessmentLevelCatalog.CreateDraft(
            Guid.NewGuid(), version, request.Name, Serialize(request.Definitions), actorId, DateTime.UtcNow);
        db.AssessmentLevelCatalogs.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToSnapshot(row);
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        Guid actorId,
        UpdateSpeedReadingLevelCatalogRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        SpeedReadingLevelRules.ValidateDefinitions(request.Definitions);
        var row = await db.AssessmentLevelCatalogs.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
            return false;
        row.UpdateDraft(request.Name, Serialize(request.Definitions), actorId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> PublishAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var row = await db.AssessmentLevelCatalogs.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
            return false;
        var now = DateTime.UtcNow;
        var activeRows = await db.AssessmentLevelCatalogs
            .Where(item => item.Status == AssessmentLevelCatalogStatus.Published && item.Id != id)
            .ToListAsync(cancellationToken);
        foreach (var active in activeRows)
            active.Retire(actorId, now);
        row.Publish(actorId, now);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Serialize(IReadOnlyList<SpeedReadingLevelDefinition> definitions) =>
        JsonSerializer.Serialize(definitions, JsonOptions);

    private static SpeedReadingLevelCatalogSnapshot ToSnapshot(AssessmentLevelCatalog row)
    {
        var definitions = JsonSerializer.Deserialize<List<SpeedReadingLevelDefinition>>(row.DefinitionsJson, JsonOptions)
            ?? throw new InvalidOperationException($"Level catalog '{row.CatalogVersion}' has invalid definitions.");
        SpeedReadingLevelRules.ValidateDefinitions(definitions);
        return new SpeedReadingLevelCatalogSnapshot(
            row.Id, row.CatalogVersion, row.Name, row.Status, definitions,
            row.PublishedAt, row.CreatedAt, row.UpdatedAt);
    }
}
