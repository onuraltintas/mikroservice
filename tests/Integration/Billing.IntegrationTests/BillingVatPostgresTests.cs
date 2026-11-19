using Billing.Application;
using Billing.Domain;
using Billing.Infrastructure;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Billing.IntegrationTests;

/// <summary>
/// VAT on a net plan price and business sales (maxkampüs; user decisions 20.11.2026): the buyer pays net + VAT, reads the
/// business subscription contract, and has no withdrawal. Consumer plans whose price includes taxes do not change.
/// </summary>
public sealed class BillingVatPostgresTests : IAsyncLifetime
{
    private static readonly Guid Admin = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002");
    private readonly PostgresFixture _postgres = new();
    private readonly Clock _clock = new(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));
    private DbContextOptions<BillingDbContext> _options = null!;
    private Guid _workspacePlan;
    private Guid _consumerPlan;

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class NoEvents : IBillingEvents
    {
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    }

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _options = new DbContextOptionsBuilder<BillingDbContext>().UseBillingDatabase(_postgres.ConnectionString).Options;
        await using var db = Db();
        await db.Database.MigrateAsync();
        var catalog = new BillingCatalog(db, _clock);
        await catalog.UpdateSellerAsync(new SellerRequest("TEST Satıcı Ltd.", "Bir adres", "Daire", "1234567890", null, "satici@example.invalid", "0212", null), Admin);
        await catalog.CreateBankAccountAsync(new BankAccountRequest("Test Bankası", "TEST Satıcı Ltd.", "TR330006100519786457841326", null, null, true, 1), Admin);
        await catalog.UpdateProductSettingsAsync(BillingSeed.CourseManagement, new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin);
        await catalog.UpdateProductSettingsAsync(BillingSeed.SpeedReading, new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin);
        var entitlements = (await catalog.GetProductsAsync()).SelectMany(item => item.Entitlements).ToArray();
        var workspace = entitlements.Single(item => item.Key == "course-management.workspace").Id;
        var student = entitlements.Single(item => item.Key == "speed-reading.student").Id;
        _workspacePlan = (await catalog.CreatePlanAsync(new PlanRequest("TEST 10 öğrenci (aylık)", null, BuyerType.Individual, 500, 30, 0, null, null,
            false, true, true, 1, [], [new PlanEntitlementRequest(workspace, EntitlementHolder.Buyer, 10)], VatRate: 20), Admin)).Id;
        _consumerPlan = (await catalog.CreatePlanAsync(new PlanRequest("TEST Okuma", null, BuyerType.Individual, 749, 365, 0, null, null,
            false, true, true, 1, [], [new PlanEntitlementRequest(student, EntitlementHolder.Buyer, null)]), Admin)).Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync();

    private BillingDbContext Db() => new(_options);
    private BillingOrders Orders(BillingDbContext db) => new(db, new NoEvents(), _clock);
    private BillingExtras Extras(BillingDbContext db) => new(db, Orders(db), _clock);
    private static BillingUser Owner() => new(Guid.NewGuid(), "kurum@example.invalid", ["CourseInstitutionOwner"]);

    [Fact]
    public async Task Course_management_is_a_business_sale_by_default_and_its_catalog_says_so()
    {
        await using var db = Db();
        var product = (await new BillingCatalog(db, _clock).GetProductsAsync()).Single(item => item.Code == "course-management");
        Assert.True(product.BusinessSales);
        var catalog = await new BillingCatalog(db, _clock).GetPublicCatalogAsync("course-management");
        Assert.True(catalog!.BusinessSales);
        var plan = Assert.Single(catalog.Plans);
        Assert.Equal(20m, plan.VatRate);
        Assert.Equal(500m, plan.Price);
        Assert.Equal(600m, plan.PriceWithVat);
        Assert.False((await new BillingCatalog(db, _clock).GetPublicCatalogAsync("speed-reading"))!.BusinessSales);
    }

    [Fact]
    public async Task A_net_price_order_carries_vat_and_the_buyer_reads_the_business_contract()
    {
        var owner = Owner();
        await using var db = Db();
        var documents = await Orders(db).PreviewDocumentsAsync(owner, _workspacePlan, "TEST Kurs Sahibi");
        Assert.True(documents!.Business);
        Assert.Equal(SalesDocumentTexts.BusinessVersion, documents.Version);
        Assert.Contains("KURUMSAL ABONELİK SÖZLEŞMESİ", documents.DistanceSales);
        Assert.Contains("Net tutar 500,00 TRY, KDV (%20) 100,00 TRY, ödenecek toplam tutar 600,00 TRY.", documents.DistanceSales);
        Assert.DoesNotContain("Cayma hakkı", documents.DistanceSales);
        Assert.DoesNotContain("Tüketici Hakem Heyeti", documents.DistanceSales);

        // The consumer documents are not accepted for a business sale.
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Orders(db).CreateOrderAsync(owner,
            new CreateOrderRequest(_workspacePlan, SalesDocumentTexts.Version, "TEST Kurs Sahibi", null, null)));
        Assert.Contains("kurumsal abonelik sözleşmesini", refused.Message);

        var order = await Orders(db).CreateOrderAsync(owner, new CreateOrderRequest(_workspacePlan, SalesDocumentTexts.BusinessVersion, "TEST Kurs Sahibi", null, null));
        Assert.Equal(500m, order.ListPrice);
        Assert.Equal(20m, order.VatRate);
        Assert.Equal(100m, order.VatAmount);
        Assert.Equal(600m, order.Amount);
    }

    [Fact]
    public async Task A_business_sale_has_no_withdrawal()
    {
        var owner = Owner();
        OrderView order;
        await using (var db = Db())
        {
            order = await Orders(db).CreateOrderAsync(owner, new CreateOrderRequest(_workspacePlan, SalesDocumentTexts.BusinessVersion, "TEST Kurs Sahibi", null, null));
            var approved = (await Orders(db).ApproveOrderAsync(order.Id, Admin, null))!;
            Assert.Null(approved.Order.WithdrawUntil);
        }
        await using (var db = Db())
        {
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Extras(db).RequestWithdrawalAsync(owner.Id, order.Id, null));
            Assert.Equal("Billing.WithdrawalNotOffered", refused.Code);
            Assert.Empty(await db.Withdrawals.Where(item => item.OrderId == order.Id).ToListAsync());
        }
    }

    [Fact]
    public async Task A_price_with_taxes_included_keeps_the_consumer_documents_and_the_amount()
    {
        var reader = new BillingUser(Guid.NewGuid(), "okur@example.invalid", ["Student"]);
        await using var db = Db();
        var documents = await Orders(db).PreviewDocumentsAsync(reader, _consumerPlan, "TEST Okur");
        Assert.False(documents!.Business);
        Assert.Equal(SalesDocumentTexts.Version, documents.Version);
        Assert.Contains("Ödenecek toplam tutar 749,00 TRY (vergiler dahil).", documents.DistanceSales);
        var order = await Orders(db).CreateOrderAsync(reader, new CreateOrderRequest(_consumerPlan, SalesDocumentTexts.Version, "TEST Okur", null, null));
        Assert.Null(order.VatRate);
        Assert.Equal(0m, order.VatAmount);
        Assert.Equal(749m, order.Amount);
        var approved = (await Orders(db).ApproveOrderAsync(order.Id, Admin, null))!;
        Assert.NotNull(approved.Order.WithdrawUntil);
    }

    [Fact]
    public async Task The_last_end_of_a_product_stays_known_after_the_subscription_is_over()
    {
        var owner = Owner();
        await using (var db = Db())
        {
            Assert.Null((await Orders(db).GetAccessAsync(owner.Id, "course-management"))!.LastEndsAt);
            var order = await Orders(db).CreateOrderAsync(owner, new CreateOrderRequest(_workspacePlan, SalesDocumentTexts.BusinessVersion, "TEST Kurs Sahibi", null, null));
            await Orders(db).ApproveOrderAsync(order.Id, Admin, null);
        }
        var end = _clock.Now.UtcDateTime.AddDays(30);
        _clock.Now = _clock.Now.AddDays(45);
        await using (var db = Db())
        {
            var access = (await Orders(db).GetAccessAsync(owner.Id, "course-management"))!;
            Assert.Empty(access.Entitlements);
            Assert.Equal(end, access.LastEndsAt!.Value, TimeSpan.FromSeconds(1));
            Assert.Null((await Orders(db).GetAccessAsync(owner.Id, "speed-reading"))!.LastEndsAt);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(20.555)]
    public async Task A_vat_rate_must_be_a_real_percentage(double rate)
    {
        await using var db = Db();
        var workspace = (await new BillingCatalog(db, _clock).GetProductsAsync()).SelectMany(item => item.Entitlements)
            .Single(item => item.Key == "course-management.workspace").Id;
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => new BillingCatalog(db, _clock).CreatePlanAsync(new PlanRequest("TEST", null,
            BuyerType.Individual, 100, 30, 0, null, null, false, true, true, 1, [], [new PlanEntitlementRequest(workspace, EntitlementHolder.Buyer, 5)],
            VatRate: (decimal)rate), Admin));
        Assert.Contains("KDV oranı", refused.Message);
    }

    [Theory]
    [InlineData(500, 20, 100)]
    [InlineData(1_250, 20, 250)]
    [InlineData(99.99, 20, 20)]
    [InlineData(10.05, 18, 1.81)]
    public void Vat_is_rounded_to_the_kurus(double net, double rate, double expected) =>
        Assert.Equal((decimal)expected, BillingRules.Vat((decimal)net, (decimal)rate));

    [Fact]
    public void No_vat_is_added_to_a_price_with_taxes_included() => Assert.Equal(0m, BillingRules.Vat(749m, null));
}
