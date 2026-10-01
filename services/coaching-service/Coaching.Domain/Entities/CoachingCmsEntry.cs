namespace Coaching.Domain.Entities;

public sealed class CoachingCmsEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "Page";
    public string? Group { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? Eyebrow { get; set; }
    public string? LinkLabel { get; set; }
    public string? LinkUrl { get; set; }
    public string? SecondaryLinkLabel { get; set; }
    public string? SecondaryLinkUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? Author { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool TestimonialConsentConfirmed { get; set; }
    public string TagsJson { get; set; } = "[]";
    public bool IsPublished { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public int SortOrder { get; set; }
    public int ViewCount { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class CoachingCmsRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntryId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int Version { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class CoachingCmsNavigationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Menu { get; set; } = "Main";
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool OpenInNewTab { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CoachingCmsMediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
