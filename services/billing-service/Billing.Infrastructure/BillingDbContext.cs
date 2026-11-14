using Billing.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Billing.Infrastructure;

/// <summary>
/// The billing service's own database (schema "billing"). Stage 1 holds the catalog and the settings; orders,
/// subscriptions and the rest come with the later stages of billing-service-plan.md.
/// </summary>
public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public const string Schema = "billing";

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Entitlement> Entitlements => Set<Entitlement>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        // Messages (entitlement changes, notices) leave with the transaction that causes them.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.BankTransferApproval).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(item => item.Code).IsUnique();
            entity.HasData(BillingSeed.Products);
        });

        modelBuilder.Entity<Entitlement>(entity =>
        {
            entity.ToTable("entitlements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Key).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => item.Key).IsUnique();
            entity.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasData(BillingSeed.Entitlements);
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("plans");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2_000).IsRequired();
            entity.Property(item => item.BuyerType).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Price).HasPrecision(12, 2);
            entity.Property(item => item.ExtraSeatPrice).HasPrecision(12, 2);
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.FeaturesJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => new { item.IsActive, item.IsPublic, item.SortOrder });
        });

        modelBuilder.Entity<PlanEntitlement>(entity =>
        {
            entity.ToTable("plan_entitlements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Holder).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(item => new { item.PlanId, item.EntitlementId, item.Holder }).IsUnique();
            entity.HasOne<Plan>().WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Entitlement>().WithMany().HasForeignKey(item => item.EntitlementId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Seller>(entity =>
        {
            entity.ToTable("seller");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Ignore(item => item.IsComplete);
            entity.Property(item => item.Title).HasMaxLength(200);
            entity.Property(item => item.Address).HasMaxLength(500);
            entity.Property(item => item.TaxOffice).HasMaxLength(100);
            entity.Property(item => item.TaxNumber).HasMaxLength(11);
            entity.Property(item => item.MersisNumber).HasMaxLength(16);
            entity.Property(item => item.Email).HasMaxLength(320);
            entity.Property(item => item.Phone).HasMaxLength(40);
            entity.Property(item => item.AlertEmail).HasMaxLength(320);
        });

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.ToTable("bank_accounts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.BankName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.AccountHolder).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Iban).HasMaxLength(34).IsRequired();
            entity.Property(item => item.Instructions).HasMaxLength(2_000);
            entity.Property(item => item.StatementTemplate).HasMaxLength(40);
            entity.HasIndex(item => item.Iban).IsUnique();
        });
    }
}

/// <summary>
/// The products and entitlements every environment starts with. Plans are not seeded: what is sold, to whom and with
/// how many seats is the admin's choice (user decision 14.11.2026). Every product starts with "no subscription needed".
/// </summary>
public static class BillingSeed
{
    private static readonly DateTime SeededAt = new(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Guid Coaching = Guid.Parse("b1110000-0000-4000-8000-000000000001");
    public static readonly Guid SpeedReading = Guid.Parse("b1110000-0000-4000-8000-000000000002");
    public static readonly Guid CourseManagement = Guid.Parse("b1110000-0000-4000-8000-000000000003");

    public static Product[] Products =>
    [
        new() { Id = Coaching, Code = "coaching", Name = "Eduİvme Koçluk", SortOrder = 1, CreatedAt = SeededAt },
        new() { Id = SpeedReading, Code = "speed-reading", Name = "Master Hızlı Okuma", SortOrder = 2, CreatedAt = SeededAt },
        new() { Id = CourseManagement, Code = "course-management", Name = "Kurs Yönetimi", SortOrder = 3, CreatedAt = SeededAt }
    ];

    public static Entitlement[] Entitlements =>
    [
        Entitlement("b1120000-0000-4000-8000-000000000001", Coaching, "coaching.student", "Koçluk öğrenci erişimi", false),
        Entitlement("b1120000-0000-4000-8000-000000000002", Coaching, "coaching.teacher", "Koçluk öğretmen erişimi", false),
        Entitlement("b1120000-0000-4000-8000-000000000003", Coaching, "coaching.institution", "Koçluk kurum erişimi", false),
        Entitlement("b1120000-0000-4000-8000-000000000004", SpeedReading, "speed-reading.student", "Hızlı Okuma öğrenci erişimi", false),
        Entitlement("b1120000-0000-4000-8000-000000000005", SpeedReading, "speed-reading.institution", "Hızlı Okuma kurum erişimi", false),
        Entitlement("b1120000-0000-4000-8000-000000000006", CourseManagement, "course-management.workspace", "Kurs Yönetimi çalışma alanı (etkin öğrenci sınırı)", true)
    ];

    private static Entitlement Entitlement(string id, Guid productId, string key, string name, bool hasLimit) =>
        new() { Id = Guid.Parse(id), ProductId = productId, Key = key, Name = name, HasLimit = hasLimit, CreatedAt = SeededAt };
}

/// <summary>Lets the EF tools build the model without the web host (migrations and the CI drift check).</summary>
public sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BILLING_CONNECTION_STRING")
            ?? "Host=127.0.0.1;Database=billing_design;Username=billing;Password=design-time-only";
        return new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(connectionString).Options);
    }
}
