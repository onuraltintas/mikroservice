using Billing.Application;
using Billing.Domain;
using Billing.Infrastructure;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Billing.IntegrationTests;

/// <summary>Coupons, free trials, withdrawal and invoices against a real PostgreSQL database.</summary>
public sealed class BillingExtrasPostgresTests : IAsyncLifetime
{
    private static readonly Guid Admin = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly byte[] Pdf = "%PDF-1.7 fatura"u8.ToArray();
    private readonly PostgresFixture _postgres = new();
    private readonly Clock _clock = new(new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero));
    private DbContextOptions<BillingDbContext> _options = null!;
    private Guid _monthly;
    private Guid _yearly;
    private Guid _trialPlan;

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
        _options = new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(_postgres.ConnectionString).Options;
        await using var db = Db();
        await db.Database.MigrateAsync();
        var catalog = new BillingCatalog(db, _clock);
        await catalog.UpdateSellerAsync(new SellerRequest("TEST Satıcı Ltd.", "Bir adres", "Daire", "1234567890", null, "satici@example.invalid", "0212", null), Admin);
        await catalog.CreateBankAccountAsync(new BankAccountRequest("Test Bankası", "TEST Satıcı Ltd.", "TR330006100519786457841326", null, null, true, 1), Admin);
        await catalog.UpdateProductSettingsAsync(BillingSeed.SpeedReading, new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin);
        var student = (await catalog.GetProductsAsync()).SelectMany(item => item.Entitlements).Single(item => item.Key == "speed-reading.student").Id;
        PlanRequest Plan(string name, decimal price, int days, int trial) => new(name, null, BuyerType.Individual, price, days, trial, null, null, false, true, true, 0, [],
            [new PlanEntitlementRequest(student, EntitlementHolder.Buyer, null)]);
        _monthly = (await catalog.CreatePlanAsync(Plan("TEST Aylık", 200, 30, 0), Admin)).Id;
        _yearly = (await catalog.CreatePlanAsync(Plan("TEST Yıllık", 1_500, 365, 0), Admin)).Id;
        _trialPlan = (await catalog.CreatePlanAsync(Plan("TEST Denemeli", 300, 30, 7), Admin)).Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync();

    private BillingDbContext Db() => new(_options);
    private BillingOrders Orders(BillingDbContext db) => new(db, new NoEvents(), _clock);
    private BillingExtras Extras(BillingDbContext db) => new(db, Orders(db), _clock);
    private static BillingUser Student() => new(Guid.NewGuid(), "ogrenci@example.invalid", ["Student"]);

    private async Task<OrderView> OrderAsync(BillingUser user, Guid plan, string? coupon = null)
    {
        await using var db = Db();
        return await Orders(db).CreateOrderAsync(user, new CreateOrderRequest(plan, SalesDocumentTexts.Version, "Deneme Öğrenci", null, coupon));
    }

    private async Task ApproveAsync(Guid orderId)
    {
        await using var db = Db();
        await Orders(db).ApproveOrderAsync(orderId, Admin, null);
    }

    private async Task<CouponView> CouponAsync(string code, CouponKind kind, decimal value, int? maxUses = null, Guid[]? plans = null, DateTime? endsAt = null)
    {
        await using var db = Db();
        return await Extras(db).CreateCouponAsync(new CouponRequest(code, kind, value, null, endsAt, maxUses, plans, true), Admin);
    }

    private async Task<string> RefusedAsync(BillingUser user, Guid plan, string coupon) =>
        (await Assert.ThrowsAsync<BusinessRuleException>(() => OrderAsync(user, plan, coupon))).Message;

    [Theory]
    [InlineData(CouponKind.Percent, 25, 200, 50)]
    [InlineData(CouponKind.Percent, 33, 199.99, 66.00)]
    [InlineData(CouponKind.Amount, 75, 200, 75)]
    [InlineData(CouponKind.Amount, 500, 200, 200)]
    public void A_discount_is_a_percent_or_an_amount_and_never_more_than_the_price(CouponKind kind, double value, double price, double expected) =>
        Assert.Equal((decimal)expected, ExtrasRules.Discount(kind, (decimal)value, (decimal)price));

    [Fact]
    public async Task A_coupon_lowers_the_amount_of_the_order_and_the_documents_say_what_is_paid()
    {
        await CouponAsync(" okul-25 ", CouponKind.Percent, 25);
        var user = Student();
        await using (var db = Db())
        {
            var quote = await Extras(db).QuoteCouponAsync(user, new CouponQuoteRequest(_monthly, "okul-25"));
            Assert.Equal(("OKUL-25", 200m, 50m, 150m), (quote.Code, quote.ListPrice, quote.Discount, quote.Amount));
        }

        var order = await OrderAsync(user, _monthly, "Okul-25");

        Assert.Equal((200m, 50m, 150m, "OKUL-25"), (order.ListPrice, order.Discount, order.Amount, order.CouponCode));
        await using var check = Db();
        Assert.Contains("150,00 TRY", (await Orders(check).GetAcceptedDocumentsAsync(user.Id, order.Id))!.PreInformation);
        Assert.Equal(1, Assert.Single(await Extras(check).GetCouponsAsync()).UsedCount);
    }

    [Fact]
    public async Task A_coupon_is_refused_outside_its_time_plans_limit_or_when_the_account_used_it_and_a_dropped_order_gives_the_use_back()
    {
        await CouponAsync("TEKLIK", CouponKind.Amount, 50, maxUses: 1);
        await CouponAsync("YILLIK", CouponKind.Percent, 10, plans: [_yearly]);
        await CouponAsync("BITTI", CouponKind.Percent, 10, endsAt: _clock.Now.UtcDateTime.AddDays(-1));
        await CouponAsync("BEDAVA", CouponKind.Amount, 200);
        var first = Student();
        var second = Student();

        Assert.Equal("Kupon kodu geçersiz ya da süresi dolmuş.", await RefusedAsync(first, _monthly, "YOK-BOYLE"));
        Assert.Equal("Kupon kodu geçersiz ya da süresi dolmuş.", await RefusedAsync(first, _monthly, "BITTI"));
        Assert.Equal("Bu kupon seçtiğiniz planda geçerli değil.", await RefusedAsync(first, _monthly, "YILLIK"));
        Assert.Equal("Bu kupon seçtiğiniz planın tamamını karşılıyor; bu planda kullanılamaz.", await RefusedAsync(first, _monthly, "BEDAVA"));

        // A waiting order counts as a use: the limit of one is reached before anything is paid.
        var order = await OrderAsync(first, _monthly, "TEKLIK");
        Assert.Equal("Bu kuponun kullanım sınırı doldu.", await RefusedAsync(second, _monthly, "TEKLIK"));
        // The buyer takes the order back: the use is free again, for somebody else.
        await using (var db = Db()) await Orders(db).WithdrawOrderAsync(first.Id, order.Id);
        var other = await OrderAsync(second, _monthly, "TEKLIK");
        await ApproveAsync(other.Id);
        Assert.Equal("Bu kuponun kullanım sınırı doldu.", await RefusedAsync(first, _yearly, "TEKLIK"));
        // The approval is reversed: the use comes back again.
        await using (var db = Db()) await Orders(db).ReverseApprovalAsync(other.Id, Admin, "Ödeme bankadan geri döndü.");
        Assert.Equal(150m, (await OrderAsync(first, _monthly, "TEKLIK")).Amount);

        // An account uses a code once, whatever the plan.
        await CouponAsync("HERKESE", CouponKind.Percent, 10);
        await OrderAsync(second, _monthly, "HERKESE");
        Assert.Equal("Bu kuponu daha önce kullandınız.", await RefusedAsync(second, _yearly, "HERKESE"));

        await using var admin = Db();
        Assert.Equal("Billing.CouponExists", (await Assert.ThrowsAsync<BusinessRuleException>(
            () => Extras(admin).CreateCouponAsync(new CouponRequest("teklik", CouponKind.Percent, 5, null, null, null, null, true), Admin))).Code);
        Assert.Equal("Billing.CouponInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(
            () => Extras(admin).CreateCouponAsync(new CouponRequest("YUZELLI", CouponKind.Percent, 150, null, null, null, null, true), Admin))).Code);
    }

    [Fact]
    public async Task Two_orders_at_once_cannot_take_the_last_use_of_a_coupon()
    {
        await CouponAsync("SONBIR", CouponKind.Amount, 50, maxUses: 1);
        async Task<bool> Try()
        {
            try { await OrderAsync(Student(), _monthly, "SONBIR"); return true; }
            catch (BusinessRuleException) { return false; }
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(Try)));
        Assert.Equal(1, results.Count(taken => taken));
    }

    [Fact]
    public async Task A_free_trial_runs_once_for_somebody_new_to_the_product_and_a_payment_follows_it()
    {
        var user = Student();
        await using (var db = Db())
        {
            var extras = Extras(db);
            Assert.Equal("Billing.TrialUnavailable", (await Assert.ThrowsAsync<BusinessRuleException>(() => extras.StartTrialAsync(user, new StartTrialRequest(_monthly, null)))).Code);
            var trial = await extras.StartTrialAsync(user, new StartTrialRequest(_trialPlan, "Deneme Öğrenci"));
            Assert.Equal((true, _clock.Now.UtcDateTime.AddDays(7)), (trial.IsCurrent, trial.EndsAt));
            Assert.Equal("speed-reading.student", Assert.Single((await Orders(db).GetAccessAsync(user.Id, "speed-reading"))!.Entitlements).Key);
            Assert.Equal(0, await db.Payments.CountAsync());
        }
        await using (var db = Db())
            Assert.Equal("Billing.TrialUsed", (await Assert.ThrowsAsync<BusinessRuleException>(() => Extras(db).StartTrialAsync(user, new StartTrialRequest(_trialPlan, null)))).Code);

        // Paying during the trial does not cut it short: the paid month starts when the trial ends.
        var order = await OrderAsync(user, _trialPlan);
        await ApproveAsync(order.Id);
        await using (var db = Db())
            Assert.Equal(_clock.Now.UtcDateTime.AddDays(37), Assert.Single((await Orders(db).GetAccessAsync(user.Id, "speed-reading"))!.Entitlements).EndsAt);

        // Somebody who already bought the product gets no trial.
        var customer = Student();
        var paid = await OrderAsync(customer, _monthly);
        await ApproveAsync(paid.Id);
        await using var check = Db();
        Assert.Equal("Billing.TrialUsed", (await Assert.ThrowsAsync<BusinessRuleException>(() => Extras(check).StartTrialAsync(customer, new StartTrialRequest(_trialPlan, null)))).Code);
    }

    [Fact]
    public async Task Withdrawal_is_open_for_fourteen_days_once_and_accepting_it_ends_the_access_and_keeps_the_payment()
    {
        var user = Student();
        var order = await OrderAsync(user, _monthly);
        await using (var db = Db())
            Assert.Equal("Billing.WithdrawalInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() => Extras(db).RequestWithdrawalAsync(user.Id, order.Id, null))).Code);
        await ApproveAsync(order.Id);
        var approvedAt = _clock.Now.UtcDateTime;

        await using (var db = Db())
        {
            var extras = Extras(db);
            Assert.Equal(approvedAt.AddDays(14), (await Orders(db).GetMyOrdersAsync(user.Id)).Single().WithdrawUntil);
            Assert.Null(await extras.RequestWithdrawalAsync(Guid.NewGuid(), order.Id, null));
            var requested = await extras.RequestWithdrawalAsync(user.Id, order.Id, " Vazgeçtim ");
            Assert.Equal((WithdrawalStatus.Requested, null), (requested!.Withdrawal, requested.WithdrawUntil));
            Assert.Equal("Billing.WithdrawalExists", (await Assert.ThrowsAsync<BusinessRuleException>(() => extras.RequestWithdrawalAsync(user.Id, order.Id, null))).Code);

            // A refusal needs a reason; after it the buyer may ask again.
            var waiting = Assert.Single(await extras.GetWithdrawalsAsync(WithdrawalStatus.Requested));
            Assert.Equal(("Deneme Öğrenci", "Vazgeçtim", 200m), (waiting.AccountName, waiting.Reason, waiting.Amount));
            Assert.Equal("Billing.NoteRequired", (await Assert.ThrowsAsync<BusinessRuleException>(() => extras.ReviewWithdrawalAsync(waiting.Id, new(false, " "), Admin))).Code);
            Assert.Equal(WithdrawalStatus.Rejected, (await extras.ReviewWithdrawalAsync(waiting.Id, new(false, "Hizmet tamamen kullanıldı."), Admin))!.Status);
            Assert.Equal("Billing.WithdrawalReviewed", (await Assert.ThrowsAsync<BusinessRuleException>(() => extras.ReviewWithdrawalAsync(waiting.Id, new(true, null), Admin))).Code);
            Assert.NotNull((await Orders(db).GetMyOrdersAsync(user.Id)).Single().WithdrawUntil);
        }

        _clock.Now = _clock.Now.AddDays(5);
        await using (var db = Db())
        {
            var extras = Extras(db);
            await extras.RequestWithdrawalAsync(user.Id, order.Id, null);
            var second = Assert.Single(await extras.GetWithdrawalsAsync(WithdrawalStatus.Requested));
            var accepted = await extras.ReviewWithdrawalAsync(second.Id, new(true, null), Admin);
            Assert.Equal(WithdrawalStatus.Approved, accepted!.Status);
            Assert.Contains("iade", accepted.ReviewNote);
            Assert.Empty((await Orders(db).GetAccessAsync(user.Id, "speed-reading"))!.Entitlements);
            var mine = (await Orders(db).GetMyOrdersAsync(user.Id)).Single();
            Assert.Equal((OrderStatus.Cancelled, WithdrawalStatus.Approved), (mine.Status, mine.Withdrawal));
            var payment = await db.Payments.SingleAsync();
            Assert.Equal(PaymentStatus.Reversed, payment.Status);
        }

        // After fourteen days the right is over.
        var late = Student();
        var lateOrder = await OrderAsync(late, _monthly);
        await ApproveAsync(lateOrder.Id);
        _clock.Now = _clock.Now.AddDays(14);
        await using var check = Db();
        Assert.Equal("Billing.WithdrawalExpired", (await Assert.ThrowsAsync<BusinessRuleException>(() => Extras(check).RequestWithdrawalAsync(late.Id, lateOrder.Id, null))).Code);
        Assert.Null((await Orders(check).GetMyOrdersAsync(late.Id)).Single().WithdrawUntil);
    }

    [Fact]
    public async Task Reversing_an_approval_settles_a_waiting_withdrawal()
    {
        var user = Student();
        var order = await OrderAsync(user, _monthly);
        await ApproveAsync(order.Id);
        await using var db = Db();
        await Extras(db).RequestWithdrawalAsync(user.Id, order.Id, null);
        await Orders(db).ReverseApprovalAsync(order.Id, Admin, "Cayma talebi üzerine iade edildi.");
        var settled = Assert.Single(await Extras(db).GetWithdrawalsAsync(null));
        Assert.Equal((WithdrawalStatus.Approved, "Cayma talebi üzerine iade edildi."), (settled.Status, settled.ReviewNote));
    }

    [Fact]
    public async Task An_invoice_is_a_pdf_for_an_approved_payment_with_its_own_number_and_only_the_buyer_and_the_admin_open_it()
    {
        var user = Student();
        var order = await OrderAsync(user, _monthly);
        var upload = new InvoiceUpload(" ABC2026000000001 ", new DateOnly(2026, 11, 15), "C:\\faturalar\\kasim.pdf", Pdf);
        await using var db = Db();
        var extras = Extras(db);
        Assert.Equal("Billing.InvoiceInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() => extras.AttachInvoiceAsync(order.Id, upload, Admin))).Code);
        await ApproveAsync(order.Id);
        Assert.Equal("Billing.InvoiceInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(
            () => extras.AttachInvoiceAsync(order.Id, upload with { Content = "not a pdf"u8.ToArray() }, Admin))).Code);
        Assert.Equal("Billing.InvoiceInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() => extras.AttachInvoiceAsync(order.Id, upload with { Number = "A" }, Admin))).Code);
        Assert.Null(await extras.AttachInvoiceAsync(Guid.NewGuid(), upload, Admin));

        var attached = await extras.AttachInvoiceAsync(order.Id, upload, Admin);
        Assert.True(attached!.Order.HasInvoice);
        Assert.Equal("Billing.InvoiceExists", (await Assert.ThrowsAsync<BusinessRuleException>(() => extras.AttachInvoiceAsync(order.Id, upload with { Number = "ABC2026000000002" }, Admin))).Code);

        var other = Student();
        var otherOrder = await OrderAsync(other, _monthly);
        await ApproveAsync(otherOrder.Id);
        Assert.Equal("Billing.InvoiceExists", (await Assert.ThrowsAsync<BusinessRuleException>(() => extras.AttachInvoiceAsync(otherOrder.Id, upload, Admin))).Code);

        var file = await extras.GetInvoiceAsync(user.Id, order.Id);
        Assert.Equal(("ABC2026000000001", "kasim.pdf", Pdf.Length), (file!.Number, file.FileName, file.Content.Length));
        Assert.Null(await extras.GetInvoiceAsync(other.Id, order.Id));
        Assert.NotNull(await extras.GetInvoiceAsync(null, order.Id));
        Assert.True((await Orders(db).GetMyOrdersAsync(user.Id)).Single().HasInvoice);

        Assert.True(await extras.RemoveInvoiceAsync(order.Id));
        Assert.False(await extras.RemoveInvoiceAsync(order.Id));
        Assert.Null(await extras.GetInvoiceAsync(null, order.Id));
    }
}
