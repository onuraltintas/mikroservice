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
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderReceipt> OrderReceipts => Set<OrderReceipt>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPeriod> SubscriptionPeriods => Set<SubscriptionPeriod>();
    public DbSet<EntitlementGrant> EntitlementGrants => Set<EntitlementGrant>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Withdrawal> Withdrawals => Set<Withdrawal>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<NoticeReceipt> NoticeReceipts => Set<NoticeReceipt>();
    public DbSet<ErasureReceipt> ErasureReceipts => Set<ErasureReceipt>();
    public DbSet<BankStatementImport> BankStatementImports => Set<BankStatementImport>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();

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

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(320);
            // One account per user and one per organization, whatever the product.
            entity.HasIndex(item => item.UserId).IsUnique();
            entity.HasIndex(item => item.OrganizationId).IsUnique();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PlanName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.BuyerType).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
            entity.HasOne<Subscription>().WithMany().HasForeignKey(item => item.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.ListPrice).HasPrecision(12, 2);
            entity.Property(item => item.Discount).HasPrecision(12, 2);
            entity.Property(item => item.Amount).HasPrecision(12, 2);
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.EntitlementsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.PaymentCode).HasMaxLength(20).IsRequired();
            entity.Property(item => item.CouponCode).HasMaxLength(30);
            entity.HasIndex(item => item.CouponCode);
            entity.Property(item => item.DocumentsVersion).HasMaxLength(20).IsRequired();
            entity.Property(item => item.DocumentsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.PayerNote).HasMaxLength(500);
            entity.Property(item => item.ReviewNote).HasMaxLength(2_000);
            entity.HasIndex(item => item.PaymentCode).IsUnique();
            // An account waits for one payment per plan at a time.
            entity.HasIndex(item => new { item.AccountId, item.PlanId, item.BeneficiaryUserId, item.Kind }).IsUnique()
                .HasFilter("\"Status\" = 'Pending'").HasDatabaseName("IX_orders_one_pending_per_plan");
            entity.HasIndex(item => new { item.Status, item.ExpiresAt });
            entity.HasIndex(item => item.OrderedByUserId);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Plan>().WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderReceipt>(entity =>
        {
            entity.ToTable("order_receipts");
            entity.HasKey(item => item.OrderId);
            entity.Property(item => item.FileName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
            entity.HasOne<Order>().WithOne().HasForeignKey<OrderReceipt>(item => item.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Method).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Amount).HasPrecision(12, 2);
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.ReversalReason).HasMaxLength(1_000);
            // An order is paid once.
            entity.HasIndex(item => item.OrderId).IsUnique();
            entity.HasOne<Order>().WithMany().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("subscriptions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PlanName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.EntitlementsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => new { item.AccountId, item.PlanId, item.BeneficiaryUserId }).IsUnique();
            entity.HasIndex(item => item.BeneficiaryUserId);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Plan>().WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SubscriptionPeriod>(entity =>
        {
            entity.ToTable("subscription_periods");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Source).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Note).HasMaxLength(500);
            entity.HasIndex(item => item.SubscriptionId);
            entity.HasIndex(item => item.PaymentId).IsUnique();
            entity.HasOne<Subscription>().WithMany().HasForeignKey(item => item.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Payment>().WithMany().HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EntitlementGrant>(entity =>
        {
            entity.ToTable("entitlement_grants");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Key).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.ProductId, item.EndsAt });
            entity.HasIndex(item => item.SubscriptionId);
            entity.HasOne<Subscription>().WithMany().HasForeignKey(item => item.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Entitlement>().WithMany().HasForeignKey(item => item.EntitlementId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.ToTable("seats");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SuspensionReason).HasMaxLength(500);
            // A user holds one seat of a subscription.
            entity.HasIndex(item => new { item.SubscriptionId, item.UserId }).IsUnique();
            entity.HasIndex(item => item.UserId);
            entity.HasOne<Subscription>().WithMany().HasForeignKey(item => item.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.ToTable("coupons");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Value).HasPrecision(12, 2);
            entity.Property(item => item.PlanIdsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<Withdrawal>(entity =>
        {
            entity.ToTable("withdrawals");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Reason).HasMaxLength(1_000);
            entity.Property(item => item.ReviewNote).HasMaxLength(1_000);
            entity.HasIndex(item => new { item.Status, item.RequestedAt });
            // An order has at most one request that is open or was accepted; a refused one may be followed by another.
            entity.HasIndex(item => item.OrderId).IsUnique().HasFilter("\"Status\" <> 'Rejected'").HasDatabaseName("IX_withdrawals_one_live_per_order");
            entity.HasOne<Order>().WithMany().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("invoices");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Number).HasMaxLength(40).IsRequired();
            entity.Property(item => item.FileName).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => item.Number).IsUnique();
            entity.HasIndex(item => item.PaymentId).IsUnique();
            entity.HasIndex(item => item.OrderId);
            entity.HasOne<Payment>().WithMany().HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NoticeReceipt>(entity =>
        {
            entity.ToTable("notice_receipts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).HasMaxLength(30).IsRequired();
            entity.HasIndex(item => new { item.SubscriptionId, item.Kind, item.PeriodEnd }).IsUnique();
            entity.HasOne<Subscription>().WithMany().HasForeignKey(item => item.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ErasureReceipt>(entity =>
        {
            entity.ToTable("erasure_receipts");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).ValueGeneratedNever();
            entity.HasIndex(item => new { item.RedactedAt, item.RedactAfter });
        });

        modelBuilder.Entity<BankStatementImport>(entity =>
        {
            entity.ToTable("bank_statement_imports");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FileName).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => item.UploadedAt);
            entity.HasOne<BankAccount>().WithMany().HasForeignKey(item => item.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BankTransaction>(entity =>
        {
            entity.ToTable("bank_transactions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(12, 2);
            entity.Property(item => item.Description).HasMaxLength(1_000).IsRequired();
            entity.Property(item => item.Sender).HasMaxLength(200);
            entity.Property(item => item.Reference).HasMaxLength(100);
            entity.Property(item => item.PaymentCode).HasMaxLength(20);
            entity.Property(item => item.Fingerprint).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Note).HasMaxLength(1_000);
            // The same statement line is stored once, and a line pays one order.
            entity.HasIndex(item => item.Fingerprint).IsUnique();
            entity.HasIndex(item => item.PaymentId).IsUnique();
            entity.HasIndex(item => new { item.Status, item.BookedAt });
            entity.HasIndex(item => item.OrderId);
            entity.HasOne<BankAccount>().WithMany().HasForeignKey(item => item.BankAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<BankStatementImport>().WithMany().HasForeignKey(item => item.ImportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Payment>().WithMany().HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
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
