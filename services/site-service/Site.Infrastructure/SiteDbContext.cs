using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Site.Infrastructure;

/// <summary>
/// One piece of site content. Each entry belongs to a site (<see cref="SiteRules.Sites"/>: eduivme.com and, since
/// 19.11.2026, maxkampus.com) and is built from typed entries (see <see cref="SiteKinds"/>): the home page entries have an
/// empty <see cref="Group"/>, an eduivme.com product page's own entries carry the page's slug as their group.
/// </summary>
public sealed class SiteEntry
{
    public Guid Id { get; set; }
    public string Site { get; set; } = SiteRules.Eduivme;
    public string Kind { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Body { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = [];
    public string? Brand { get; set; }
    public string? Icon { get; set; }
    public string? LinkLabel { get; set; }
    public string? LinkUrl { get; set; }
    public string? SecondaryLinkLabel { get; set; }
    public string? SecondaryLinkUrl { get; set; }
    public Guid? ImageId { get; set; }
    public string? CtaTitle { get; set; }
    public string? CtaBody { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    /// <summary>A blog post's topic, or a menu item's place ("header", "footer"), or "featured" on a plan note.</summary>
    public string? Category { get; set; }
    /// <summary>Who a panel notice is for: "all", "owners" or "teachers".</summary>
    public string? Audience { get; set; }
    /// <summary>After this the entry is no longer shown (panel notices).</summary>
    public DateTime? EndsAt { get; set; }
    /// <summary>A blog post's author as shown to readers.</summary>
    public string? Author { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishAt { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>An entry as it was before a change; the admin can bring it back.</summary>
public sealed class SiteRevision
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public int Version { get; set; }
    public string Snapshot { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>An image uploaded for the site (PNG, JPEG or WebP; kept in the database).</summary>
public sealed class SiteMedia
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public byte[] Content { get; set; } = [];
    public string Sha256 { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A message sent with a site's contact form (maxkampus.com). Kept until answered and then for a while (see SiteRetention).</summary>
public sealed class SiteContactMessage
{
    public Guid Id { get; set; }
    public string Site { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ConsentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public Guid? AnsweredByUserId { get; set; }
}

public enum SiteSubscriberStatus
{
    Pending = 1,
    Confirmed = 2,
    Unsubscribed = 3
}

/// <summary>
/// A newsletter address (double opt-in: it counts only after the link in the confirmation e-mail is opened). An address
/// that left stays as "Unsubscribed" so it is not written to again; a confirmation never opened is deleted after a week.
/// </summary>
public sealed class SiteSubscriber
{
    public Guid Id { get; set; }
    public string Site { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public SiteSubscriberStatus Status { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? UnsubscribedAt { get; set; }
}

/// <summary>
/// The sites' own database (schema "site"). Content holds no personal data; contact messages and newsletter addresses do,
/// and they are deleted on a schedule (SiteRetention) or by the admin.
/// </summary>
public sealed class SiteDbContext(DbContextOptions<SiteDbContext> options) : DbContext(options)
{
    public DbSet<SiteEntry> Entries => Set<SiteEntry>();
    public DbSet<SiteRevision> Revisions => Set<SiteRevision>();
    public DbSet<SiteMedia> Media => Set<SiteMedia>();
    public DbSet<SiteContactMessage> ContactMessages => Set<SiteContactMessage>();
    public DbSet<SiteSubscriber> Subscribers => Set<SiteSubscriber>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SiteDatabase.Schema);

        modelBuilder.Entity<SiteEntry>(entity =>
        {
            entity.ToTable("entries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Site).HasMaxLength(20).IsRequired().HasDefaultValue(SiteRules.Eduivme);
            entity.Property(item => item.Kind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Group).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(80);
            entity.Property(item => item.Title).HasMaxLength(SiteRules.TitleLength).IsRequired();
            entity.Property(item => item.Subtitle).HasMaxLength(SiteRules.SubtitleLength);
            entity.Property(item => item.Body).HasMaxLength(SiteRules.LongBodyLength).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(60);
            entity.Property(item => item.Audience).HasMaxLength(20);
            entity.Property(item => item.Author).HasMaxLength(120);
            entity.Property(item => item.Brand).HasMaxLength(20);
            entity.Property(item => item.Icon).HasMaxLength(30);
            entity.Property(item => item.LinkLabel).HasMaxLength(SiteRules.LabelLength);
            entity.Property(item => item.LinkUrl).HasMaxLength(SiteRules.UrlLength);
            entity.Property(item => item.SecondaryLinkLabel).HasMaxLength(SiteRules.LabelLength);
            entity.Property(item => item.SecondaryLinkUrl).HasMaxLength(SiteRules.UrlLength);
            entity.Property(item => item.CtaTitle).HasMaxLength(SiteRules.TitleLength);
            entity.Property(item => item.CtaBody).HasMaxLength(SiteRules.SubtitleLength);
            entity.Property(item => item.SeoTitle).HasMaxLength(SiteRules.SeoTitleLength);
            entity.Property(item => item.SeoDescription).HasMaxLength(SiteRules.SubtitleLength);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new { item.Site, item.Group, item.Kind, item.SortOrder });
            entity.HasIndex(item => new { item.Site, item.Kind, item.Group, item.Slug }).IsUnique().HasFilter("\"Slug\" IS NOT NULL");
        });

        modelBuilder.Entity<SiteRevision>(entity =>
        {
            entity.ToTable("revisions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Snapshot).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => new { item.EntryId, item.Version });
        });

        modelBuilder.Entity<SiteMedia>(entity =>
        {
            entity.ToTable("media");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FileName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.AltText).HasMaxLength(SiteRules.SubtitleLength);
        });

        modelBuilder.Entity<SiteContactMessage>(entity =>
        {
            entity.ToTable("contact_messages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Site).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Organization).HasMaxLength(200);
            entity.Property(item => item.Email).HasMaxLength(320).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(40);
            entity.Property(item => item.Message).HasMaxLength(SiteRules.MessageLength).IsRequired();
            entity.HasIndex(item => new { item.Site, item.CreatedAt });
        });

        modelBuilder.Entity<SiteSubscriber>(entity =>
        {
            entity.ToTable("subscribers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Site).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(320).IsRequired();
            entity.Property(item => item.Token).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(40);
            entity.HasIndex(item => new { item.Site, item.Email }).IsUnique();
            entity.HasIndex(item => item.Token).IsUnique();
            entity.HasIndex(item => new { item.Site, item.Status });
        });
    }
}

public static class SiteDatabase
{
    public const string Schema = "site";
    public const string HistoryTable = "__EFMigrationsHistory";

    public static DbContextOptionsBuilder UseSiteDatabase(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(HistoryTable, Schema));

    public static DbContextOptionsBuilder<SiteDbContext> UseSiteDatabase(this DbContextOptionsBuilder<SiteDbContext> options, string connectionString)
    {
        ((DbContextOptionsBuilder)options).UseSiteDatabase(connectionString);
        return options;
    }
}

/// <summary>Lets the EF tools build the model without the web host (migrations and the CI drift check).</summary>
public sealed class SiteDbContextFactory : IDesignTimeDbContextFactory<SiteDbContext>
{
    public SiteDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SITE_CONNECTION_STRING")
            ?? "Host=127.0.0.1;Database=site_design;Username=site;Password=design-time-only";
        return new SiteDbContext(new DbContextOptionsBuilder<SiteDbContext>().UseSiteDatabase(connectionString).Options);
    }
}
