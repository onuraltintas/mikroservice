using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Assessment;

public enum AssessmentLevelCatalogStatus
{
    Draft = 1,
    Published = 2,
    Retired = 3
}

public sealed class AssessmentLevelCatalog : AggregateRoot
{
    private AssessmentLevelCatalog()
    {
    }

    public string CatalogVersion { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string DefinitionsJson { get; private set; } = string.Empty;
    public AssessmentLevelCatalogStatus Status { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public static AssessmentLevelCatalog CreateDraft(
        Guid id,
        string version,
        string name,
        string definitionsJson,
        Guid actorId,
        DateTime createdAt)
    {
        Validate(id, version, name, definitionsJson, actorId);
        return new AssessmentLevelCatalog
        {
            Id = id,
            CatalogVersion = version.Trim(),
            Name = name.Trim(),
            DefinitionsJson = definitionsJson,
            Status = AssessmentLevelCatalogStatus.Draft,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = actorId.ToString()
        };
    }

    public void UpdateDraft(string name, string definitionsJson, Guid actorId, DateTime updatedAt)
    {
        if (Status != AssessmentLevelCatalogStatus.Draft)
            throw new InvalidOperationException("Only draft level catalogs can be edited.");
        Validate(Id, CatalogVersion, name, definitionsJson, actorId);
        Name = name.Trim();
        DefinitionsJson = definitionsJson;
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public void Publish(Guid actorId, DateTime publishedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("A valid actor is required.", nameof(actorId));
        if (Status != AssessmentLevelCatalogStatus.Draft)
            throw new InvalidOperationException("Only draft level catalogs can be published.");
        Status = AssessmentLevelCatalogStatus.Published;
        PublishedAt = EnsureUtc(publishedAt);
        UpdatedAt = PublishedAt;
        UpdatedBy = actorId.ToString();
    }

    public void Retire(Guid actorId, DateTime retiredAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("A valid actor is required.", nameof(actorId));
        if (Status != AssessmentLevelCatalogStatus.Published)
            return;
        Status = AssessmentLevelCatalogStatus.Retired;
        UpdatedAt = EnsureUtc(retiredAt);
        UpdatedBy = actorId.ToString();
    }

    private static void Validate(Guid id, string version, string name, string definitionsJson, Guid actorId)
    {
        if (id == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Catalog identifiers are required.");
        if (string.IsNullOrWhiteSpace(version) || version.Trim().Length > 100)
            throw new ArgumentException("Catalog version is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Catalog name is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(definitionsJson))
            throw new ArgumentException("Catalog definitions are required.");
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
