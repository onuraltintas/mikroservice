using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Site.Infrastructure;

/// <summary>
/// One piece of eduivme.com content. The site is built from typed entries (see <see cref="SiteKinds"/>): the home page
/// entries have an empty <see cref="Group"/>, a product page's own entries carry the page's slug as their group.
/// </summary>
public sealed class SiteEntry
{
    public Guid Id { get; set; }
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

/// <summary>The eduivme.com site's own database (schema "site"). It holds no personal data.</summary>
public sealed class SiteDbContext(DbContextOptions<SiteDbContext> options) : DbContext(options)
{
    public DbSet<SiteEntry> Entries => Set<SiteEntry>();
    public DbSet<SiteRevision> Revisions => Set<SiteRevision>();
    public DbSet<SiteMedia> Media => Set<SiteMedia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SiteDatabase.Schema);

        modelBuilder.Entity<SiteEntry>(entity =>
        {
            entity.ToTable("entries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Group).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(80);
            entity.Property(item => item.Title).HasMaxLength(SiteRules.TitleLength).IsRequired();
            entity.Property(item => item.Subtitle).HasMaxLength(SiteRules.SubtitleLength);
            entity.Property(item => item.Body).HasMaxLength(SiteRules.BodyLength).IsRequired();
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
            entity.HasIndex(item => new { item.Group, item.Kind, item.SortOrder });
            entity.HasIndex(item => new { item.Kind, item.Group, item.Slug }).IsUnique().HasFilter("\"Slug\" IS NOT NULL");
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
