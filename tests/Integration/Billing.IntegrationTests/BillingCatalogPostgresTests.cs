using Billing.Application;
using Billing.Domain;
using Billing.Infrastructure;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Billing.IntegrationTests;

/// <summary>The catalog against a real PostgreSQL database built by the migrations.</summary>
public sealed class BillingCatalogPostgresTests : IAsyncLifetime
{
    private static readonly Guid Admin = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private readonly PostgresFixture _postgres = new();
    private DbContextOptions<BillingDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _options = new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(_postgres.ConnectionString).Options;
        await using var db = new BillingDbContext(_options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync();

    private BillingCatalog Catalog(BillingDbContext db) => new(db, TimeProvider.System);

    private static PlanRequest Plan(string name, BuyerType buyer, decimal price, int? seats, params PlanEntitlementRequest[] entitlements) =>
        new(name, null, buyer, price, 30, 0, seats, null, false, true, true, 0, ["Bir özellik"], entitlements);

    [Fact]
    public async Task Migrations_create_the_three_products_with_subscription_not_required_and_manual_approval()
    {
        await using var db = new BillingDbContext(_options);
        var products = await Catalog(db).GetProductsAsync();

        Assert.Equal(["coaching", "speed-reading", "course-management"], products.Select(item => item.Code));
        Assert.All(products, item =>
        {
            Assert.False(item.RequireSubscription);
            Assert.Equal(BankTransferApproval.Manual, item.BankTransferApproval);
            Assert.NotEmpty(item.Entitlements);
            Assert.All(item.Entitlements, entitlement => Assert.StartsWith(item.Code + ".", entitlement.Key));
        });
        Assert.Empty(await Catalog(db).GetPlansAsync());
        Assert.Equal("billing", db.Model.GetDefaultSchema());
    }

    [Fact]
    public async Task Product_settings_change_per_product_and_leave_the_others_alone()
    {
        await using var db = new BillingDbContext(_options);
        var catalog = Catalog(db);

        var updated = await catalog.UpdateProductSettingsAsync(BillingSeed.SpeedReading,
            new ProductSettingsRequest(true, BankTransferApproval.Automatic), Admin);

        Assert.True(updated!.RequireSubscription);
        Assert.Equal(BankTransferApproval.Automatic, updated.BankTransferApproval);
        var coaching = (await catalog.GetProductsAsync()).Single(item => item.Code == "coaching");
        Assert.False(coaching.RequireSubscription);
        Assert.Equal(BankTransferApproval.Manual, coaching.BankTransferApproval);
        Assert.Null(await catalog.UpdateProductSettingsAsync(Guid.NewGuid(), new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin));
    }

    [Fact]
    public async Task A_new_product_gets_entitlements_under_its_own_code_only()
    {
        await using var db = new BillingDbContext(_options);
        var catalog = Catalog(db);

        var product = await catalog.CreateProductAsync(new ProductRequest("Exam-Prep", "Deneme Sınavı", true, 9), Admin);
        Assert.Equal("exam-prep", product.Code);
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.CreateProductAsync(new ProductRequest("exam-prep", "İkinci", true, 9), Admin));
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.AddEntitlementAsync(product.Id, new EntitlementRequest("coaching.student", "Yanlış", false)));

        var withEntitlement = await catalog.AddEntitlementAsync(product.Id, new EntitlementRequest("exam-prep.student", "Öğrenci erişimi", false));
        var entitlement = Assert.Single(withEntitlement!.Entitlements);
        Assert.True(await catalog.RemoveEntitlementAsync(entitlement.Id));
        Assert.False(await catalog.RemoveEntitlementAsync(entitlement.Id));
    }

    [Fact]
    public async Task A_plan_gives_entitlements_to_the_buyer_or_to_seat_holders_and_a_bundle_spans_products()
    {
        await using var db = new BillingDbContext(_options);
        var catalog = Catalog(db);
        var entitlements = (await catalog.GetProductsAsync()).SelectMany(item => item.Entitlements).ToDictionary(item => item.Key, item => item.Id);

        var bundle = await catalog.CreatePlanAsync(Plan("Koçluk + Hızlı Okuma", BuyerType.Individual, 500, null,
            new(entitlements["coaching.student"], EntitlementHolder.Buyer, null),
            new(entitlements["speed-reading.student"], EntitlementHolder.Buyer, null)), Admin);
        Assert.Equal(["coaching", "speed-reading"], bundle.ProductCodes);

        var teacher = await catalog.CreatePlanAsync(Plan("Öğretmen 10", BuyerType.Teacher, 900, 10,
            new(entitlements["coaching.teacher"], EntitlementHolder.Buyer, null),
            new(entitlements["coaching.student"], EntitlementHolder.SeatHolder, null)), Admin);
        Assert.Equal(10, teacher.SeatCount);
        Assert.Contains(teacher.Entitlements, item => item is { Key: "coaching.student", Holder: EntitlementHolder.SeatHolder });

        // Seat holders need seats; a limit is written exactly when the entitlement has one; a plan gives something.
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.CreatePlanAsync(Plan("Koltuksuz", BuyerType.Teacher, 100, null,
            new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.SeatHolder, null)), Admin));
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.CreatePlanAsync(Plan("Sınırsız", BuyerType.Institution, 100, null,
            new PlanEntitlementRequest(entitlements["course-management.workspace"], EntitlementHolder.Buyer, null)), Admin));
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.CreatePlanAsync(Plan("Gereksiz sınır", BuyerType.Individual, 100, null,
            new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.Buyer, 5)), Admin));
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.CreatePlanAsync(Plan("Boş", BuyerType.Individual, 100, null), Admin));

        var workspace = await catalog.CreatePlanAsync(Plan("Kurs 100", BuyerType.Institution, 2_000, null,
            new PlanEntitlementRequest(entitlements["course-management.workspace"], EntitlementHolder.Buyer, 100)), Admin);
        Assert.Equal(100, Assert.Single(workspace.Entitlements).LimitValue);

        // Editing replaces the entitlement list; an entitlement a plan gives cannot be removed from the catalog.
        var edited = await catalog.UpdatePlanAsync(bundle.Id, Plan("Yalnız Koçluk", BuyerType.Individual, 300, null,
            new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.Buyer, null)), Admin);
        Assert.Equal(["coaching"], edited!.ProductCodes);
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.RemoveEntitlementAsync(entitlements["coaching.student"]));
    }

    [Fact]
    public async Task Public_catalog_shows_only_open_public_plans_of_the_product_and_opens_sales_with_seller_and_bank_account()
    {
        await using var db = new BillingDbContext(_options);
        var catalog = Catalog(db);
        var entitlements = (await catalog.GetProductsAsync()).SelectMany(item => item.Entitlements).ToDictionary(item => item.Key, item => item.Id);
        var student = new PlanEntitlementRequest(entitlements["speed-reading.student"], EntitlementHolder.Buyer, null);
        await catalog.CreatePlanAsync(Plan("Aylık", BuyerType.Individual, 200, null, student), Admin);
        await catalog.CreatePlanAsync(Plan("Gizli", BuyerType.Individual, 200, null, student) with { IsPublic = false }, Admin);
        await catalog.CreatePlanAsync(Plan("Kapalı", BuyerType.Individual, 200, null, student) with { IsActive = false }, Admin);
        await catalog.CreatePlanAsync(Plan("Koçluk", BuyerType.Individual, 200, null,
            new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.Buyer, null)), Admin);

        var before = await catalog.GetPublicCatalogAsync("speed-reading");
        Assert.Equal(["Aylık"], before!.Plans.Select(item => item.Name));
        Assert.False(before.SalesOpen);

        await catalog.UpdateSellerAsync(new SellerRequest("Deneme Ltd.", "Bir adres", "Bir daire", "1234567890", null, "satici@example.com", "0212 000 00 00", null), Admin);
        Assert.False((await catalog.GetPublicCatalogAsync("speed-reading"))!.SalesOpen);
        var first = await catalog.CreateBankAccountAsync(new BankAccountRequest("Birinci Banka", "Deneme Ltd.", "TR33 0006 1005 1978 6457 8413 26", null, "Halkbank", true, 1), Admin);
        await catalog.CreateBankAccountAsync(new BankAccountRequest("İkinci Banka", "Deneme Ltd.", "TR320010009999901234567890", "Açıklamaya ödeme kodunu yazın.", null, true, 2), Admin);
        await catalog.CreateBankAccountAsync(new BankAccountRequest("Kapalı Banka", "Deneme Ltd.", "TR610003200013900000326785", null, null, false, 3), Admin);
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.CreateBankAccountAsync(
            new BankAccountRequest("Yine Birinci", "Deneme Ltd.", "TR330006100519786457841326", null, null, true, 4), Admin));

        var after = await catalog.GetPublicCatalogAsync("speed-reading");
        Assert.True(after!.SalesOpen);
        Assert.Equal(["Birinci Banka", "İkinci Banka"], after.BankAccounts.Select(item => item.BankName));
        Assert.Equal("TR33 0006 1005 1978 6457 8413 26", after.BankAccounts[0].IbanFormatted);

        Assert.True(await catalog.RemoveBankAccountAsync(first.Id));
        Assert.Null(await catalog.GetPublicCatalogAsync("unknown"));
        await catalog.UpdateProductAsync(BillingSeed.SpeedReading, new ProductRequest("speed-reading", "Master Hızlı Okuma", false, 2), Admin);
        Assert.Null(await catalog.GetPublicCatalogAsync("speed-reading"));
    }

    [Fact]
    public async Task Seller_numbers_are_checked()
    {
        await using var db = new BillingDbContext(_options);
        var catalog = Catalog(db);
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.UpdateSellerAsync(new SellerRequest("A", null, null, "12345", null, null, null, null), Admin));
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.UpdateSellerAsync(new SellerRequest("A", null, null, null, "123", null, null, null), Admin));
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.UpdateSellerAsync(new SellerRequest("A", null, null, null, null, "eposta-degil", null, null), Admin));
        var seller = await catalog.UpdateSellerAsync(new SellerRequest("A", null, null, "12345678901", "0123456789012345", null, null, "uyari@example.com"), Admin);
        Assert.False(seller.IsComplete);
        Assert.Equal("uyari@example.com", (await catalog.GetSellerAsync()).AlertEmail);
    }
}
