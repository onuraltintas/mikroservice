namespace Identity.Domain.Entities;

public sealed class PlatformLegalPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public int Version { get; set; } = 1;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class PlatformLegalPageRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public bool IsArchived { get; set; }
    public int Version { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum RegistrationLegalDocumentAction
{
    Accepted = 1,
    Acknowledged = 2
}

public sealed class RegistrationLegalDocumentAcceptance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Identity.Domain.Enums.PlatformProduct Product { get; set; }
    public string DocumentSlug { get; set; } = string.Empty;
    public int DocumentVersion { get; set; }
    public RegistrationLegalDocumentAction Action { get; set; }
    public string RegistrationMethod { get; set; } = string.Empty;
    public DateTimeOffset AcceptedAt { get; set; } = DateTimeOffset.UtcNow;
}
