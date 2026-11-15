using Billing.Application;
using Billing.Domain;
using Billing.Infrastructure;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Billing.IntegrationTests;

/// <summary>The admin's overview and hand-given subscriptions against a real PostgreSQL database.</summary>
public sealed class BillingAdministrationPostgresTests : IAsyncLifetime
{
    private static readonly Guid Admin = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private readonly PostgresFixture _postgres = new();
    private readonly Clock _clock = new(new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero));
    private readonly Recorder _events = new();
    private DbContextOptions<BillingDbContext> _options = null!;
    private Guid _monthly;
    private Guid _institutionPlan;
    private Guid _readingPlan;

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Recorder : IBillingEvents
    {
        public List<object> Messages { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
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
        await catalog.UpdateProductSettingsAsync(BillingSeed.Coaching, new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin);
        await catalog.UpdateProductSettingsAsync(BillingSeed.SpeedReading, new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin);
        var entitlements = (await catalog.GetProductsAsync()).SelectMany(item => item.Entitlements).ToDictionary(item => item.Key, item => item.Id);
        _monthly = (await catalog.CreatePlanAsync(new PlanRequest("TEST Aylık", null, BuyerType.Individual, 200, 30, 0, null, null, false, true, true, 0, [],
            [new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.Buyer, null)]), Admin)).Id;
        _institutionPlan = (await catalog.CreatePlanAsync(new PlanRequest("TEST Kurum", null, BuyerType.Institution, 5000, 365, 0, 20, null, false, true, true, 0, [],
            [new PlanEntitlementRequest(entitlements["coaching.institution"], EntitlementHolder.Buyer, null),
             new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.SeatHolder, null)]), Admin)).Id;
        _readingPlan = (await catalog.CreatePlanAsync(new PlanRequest("TEST Okuma", null, BuyerType.Individual, 100, 30, 0, null, null, false, true, true, 0, [],
            [new PlanEntitlementRequest(entitlements["speed-reading.student"], EntitlementHolder.Buyer, null)]), Admin)).Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync();

    private BillingDbContext Db() => new(_options);
    private BillingOrders Orders(BillingDbContext db) => new(db, _events, _clock);
    private BillingAdministration Administration(BillingDbContext db) => new(db, Orders(db), _clock);
    private static BillingUser Student() => new(Guid.NewGuid(), "ogrenci@example.invalid", ["Student"]);

    private async Task<OrderView> OrderAsync(BillingUser user, Guid plan)
    {
        await using var db = Db();
        return await Orders(db).CreateOrderAsync(user, new CreateOrderRequest(plan, SalesDocumentTexts.Version, "Deneme Öğrenci"));
    }

    private async Task ApproveAsync(Guid orderId)
    {
        await using var db = Db();
        await Orders(db).ApproveOrderAsync(orderId, Admin, null);
    }

    [Fact]
    public async Task A_subscription_given_by_hand_opens_access_without_a_payment_and_is_recorded_with_who_and_why()
    {
        var user = Guid.NewGuid();
        await using (var db = Db())
        {
            var given = await Administration(db).GrantAsync(new GrantSubscriptionRequest(user, null, "Deneme Öğrenci", "ogrenci@example.invalid", _monthly, null, "TEST: tanıtım aboneliği"), Admin);
            Assert.Equal((true, _clock.Now.UtcDateTime.AddDays(30), "Deneme Öğrenci"), (given.Subscription.IsCurrent, given.Subscription.EndsAt, given.AccountName));
        }
        await using (var db = Db())
        {
            Assert.Equal("coaching.student", Assert.Single((await Orders(db).GetAccessAsync(user, "coaching"))!.Entitlements).Key);
            var period = await db.SubscriptionPeriods.SingleAsync();
            Assert.Equal((PeriodSource.Manual, Admin, "TEST: tanıtım aboneliği", null), (period.Source, period.ActorId, period.Note, period.PaymentId));
            Assert.Equal(0, await db.Payments.CountAsync());
            Assert.Equal(0, await db.Orders.CountAsync());

            // Given again while it runs: the days are added to the end, in the same subscription.
            var again = await Administration(db).GrantAsync(new GrantSubscriptionRequest(user, null, "Deneme Öğrenci", null, _monthly, 10, "TEST: ek süre"), Admin);
            Assert.Equal(_clock.Now.UtcDateTime.AddDays(40), again.Subscription.EndsAt);
            Assert.Equal(1, await db.Subscriptions.CountAsync());
        }
    }

    [Fact]
    public async Task A_grant_is_refused_without_a_reason_for_the_wrong_kind_of_buyer_and_for_an_erased_account()
    {
        var user = Guid.NewGuid();
        await using var db = Db();
        var administration = Administration(db);
        async Task<string> CodeAsync(GrantSubscriptionRequest request) =>
            (await Assert.ThrowsAsync<BusinessRuleException>(() => administration.GrantAsync(request, Admin))).Code;
        Assert.Equal("Billing.NoteRequired", await CodeAsync(new(user, null, "Deneme Öğrenci", null, _monthly, null, " ")));
        Assert.Equal("Billing.GrantInvalid", await CodeAsync(new(null, null, "Deneme Öğrenci", null, _monthly, null, "TEST")));
        Assert.Equal("Billing.GrantInvalid", await CodeAsync(new(user, Guid.NewGuid(), "Deneme Öğrenci", null, _monthly, null, "TEST")));
        Assert.Equal("Billing.GrantInvalid", await CodeAsync(new(user, null, "D", null, _monthly, null, "TEST")));
        Assert.Equal("Billing.GrantInvalid", await CodeAsync(new(user, null, "Deneme Öğrenci", null, _monthly, 0, "TEST")));
        Assert.Equal("Billing.GrantInvalid", await CodeAsync(new(user, null, "Deneme Öğrenci", "adres-degil", _monthly, null, "TEST")));
        Assert.Equal("Billing.PlanNotForBuyer", await CodeAsync(new(user, null, "Deneme Öğrenci", null, _institutionPlan, null, "TEST")));
        Assert.Equal("Billing.PlanNotForBuyer", await CodeAsync(new(null, Guid.NewGuid(), "TEST Kurum A.Ş.", null, _monthly, null, "TEST")));
        Assert.Equal("Billing.PlanNotFound", await CodeAsync(new(user, null, "Deneme Öğrenci", null, Guid.NewGuid(), null, "TEST")));
        db.ErasureReceipts.Add(new ErasureReceipt { UserId = user, ErasedAt = _clock.Now.UtcDateTime, RedactAfter = _clock.Now.UtcDateTime.AddYears(10) });
        await db.SaveChangesAsync();
        Assert.Equal("Billing.UserErased", await CodeAsync(new(user, null, "Deneme Öğrenci", null, _monthly, null, "TEST")));
        Assert.Equal(0, await db.Subscriptions.CountAsync());
        Assert.Equal(0, await db.Accounts.CountAsync());
    }

    [Fact]
    public async Task An_institution_is_given_its_plan_with_the_seat_pool()
    {
        var institution = Guid.NewGuid();
        await using var db = Db();
        var given = await Administration(db).GrantAsync(new GrantSubscriptionRequest(null, institution, "TEST Kurum A.Ş.", null, _institutionPlan, null, "TEST: sözleşmeyle satış"), Admin);
        Assert.Equal((20, institution, _clock.Now.UtcDateTime.AddDays(365)), (given.Subscription.SeatCount, given.BeneficiaryUserId, given.Subscription.EndsAt));
        var account = await db.Accounts.SingleAsync();
        Assert.Equal((AccountType.Organization, institution, null), (account.Type, account.OrganizationId, account.UserId));
        var pool = await new BillingSeats(db, Orders(db), new FakeInstitutions(), _clock).GetPoolAsync(new SeatOwner(null, institution), "coaching");
        Assert.Equal(20, pool!.Capacity);
    }

    [Fact]
    public async Task A_paid_subscription_is_extended_and_ended_by_hand_and_the_payment_stays_as_it_was()
    {
        var user = Student();
        var order = await OrderAsync(user, _monthly);
        await ApproveAsync(order.Id);
        Guid id;
        await using (var db = Db())
        {
            id = (await db.Subscriptions.SingleAsync()).Id;
            var administration = Administration(db);
            Assert.Null(await administration.ExtendAsync(Guid.NewGuid(), new ExtendSubscriptionRequest(5, "TEST"), Admin));
            Assert.Equal("Billing.NoteRequired", (await Assert.ThrowsAsync<BusinessRuleException>(() => administration.ExtendAsync(id, new ExtendSubscriptionRequest(5, null), Admin))).Code);
            var extended = await administration.ExtendAsync(id, new ExtendSubscriptionRequest(5, "TEST: kesinti telafisi"), Admin);
            Assert.Equal(_clock.Now.UtcDateTime.AddDays(35), extended!.Subscription.EndsAt);
        }
        _clock.Now = _clock.Now.AddDays(10);
        await using (var db = Db())
        {
            var ended = await Administration(db).CancelAsync(id, new CancelSubscriptionRequest("TEST: kullanıcı istedi"), Admin);
            Assert.Equal((false, SubscriptionStatus.Cancelled, _clock.Now.UtcDateTime), (ended!.Subscription.IsCurrent, ended.Subscription.Status, ended.Subscription.EndsAt));
            Assert.Equal("Billing.SubscriptionNotRunning",
                (await Assert.ThrowsAsync<BusinessRuleException>(() => Administration(db).CancelAsync(id, new CancelSubscriptionRequest("TEST: yine"), Admin))).Code);
        }
        await using (var db = Db())
        {
            Assert.Empty((await Orders(db).GetAccessAsync(user.Id, "coaching"))!.Entitlements);
            Assert.Equal(PaymentStatus.Approved, (await db.Payments.SingleAsync()).Status);
            Assert.All(await db.SubscriptionPeriods.ToListAsync(), period =>
                Assert.Equal((PeriodStatus.Cancelled, Admin, "TEST: kullanıcı istedi"), (period.Status, period.ActorId, period.Note)));
            // The money of an ended subscription can still be given back: reversing the approval does not fail on it.
            await Orders(db).ReverseApprovalAsync(order.Id, Admin, "TEST: iade edildi");
            Assert.Equal(PaymentStatus.Reversed, (await db.Payments.SingleAsync()).Status);
        }
        // Extending an ended subscription starts it again from now.
        await using (var db = Db())
        {
            var revived = await Administration(db).ExtendAsync(id, new ExtendSubscriptionRequest(7, "TEST: yeniden açıldı"), Admin);
            Assert.Equal((true, _clock.Now.UtcDateTime, _clock.Now.UtcDateTime.AddDays(7)), (revived!.Subscription.IsCurrent, revived.Subscription.StartsAt, revived.Subscription.EndsAt));
            Assert.Single((await Orders(db).GetAccessAsync(user.Id, "coaching"))!.Entitlements);
        }
    }

    [Fact]
    public async Task The_overview_counts_what_waits_and_what_was_approved_by_month_and_by_product()
    {
        // Last month (Turkey time): approved on 31 October 23:30 UTC is already November there.
        _clock.Now = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
        var early = Student();
        await ApproveAsync((await OrderAsync(early, _monthly)).Id);
        _clock.Now = new DateTimeOffset(2026, 10, 31, 23, 30, 0, TimeSpan.Zero);
        await ApproveAsync((await OrderAsync(Student(), _readingPlan)).Id);
        _clock.Now = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
        var buyer = Student();
        var paid = await OrderAsync(buyer, _monthly);
        await ApproveAsync(paid.Id);
        var declared = await OrderAsync(Student(), _monthly);
        await OrderAsync(Student(), _readingPlan);
        var stale = await OrderAsync(Student(), _monthly);
        await using (var db = Db())
        {
            await Orders(db).DeclarePaidAsync((await db.Orders.SingleAsync(item => item.Id == declared.Id)).OrderedByUserId, declared.Id, null, null);
            await new BillingExtras(db, Orders(db), _clock).RequestWithdrawalAsync(buyer.Id, paid.Id, null);
            // An order past its time waits for nobody, even before the hourly sweep closes it.
            await db.Orders.Where(item => item.Id == stale.Id).ExecuteUpdateAsync(set => set.SetProperty(item => item.ExpiresAt, _clock.Now.UtcDateTime.AddMinutes(-1)));
        }

        await using var read = Db();
        var all = await Administration(read).GetOverviewAsync(null);
        Assert.Equal((2, 1, 1, 0, 3), (all.WaitingOrders, all.DeclaredOrders, all.WithdrawalRequests, all.UnmatchedTransactions, all.CurrentSubscriptions));
        Assert.Equal((300m, 200m, "TRY"), (all.ApprovedThisMonth, all.ApprovedLastMonth, all.Currency));
        // Only the October subscription (until 19 November) ends within 14 days; the reading one runs until 30 November.
        Assert.Equal([early.Id], all.EndingSoon.Select(item => item.BeneficiaryUserId));

        var coaching = await Administration(read).GetOverviewAsync("coaching");
        Assert.Equal((1, 1, 1, 2, 200m, 200m), (coaching.WaitingOrders, coaching.DeclaredOrders, coaching.WithdrawalRequests, coaching.CurrentSubscriptions,
            coaching.ApprovedThisMonth, coaching.ApprovedLastMonth));
        Assert.Single(coaching.EndingSoon);
        Assert.Empty((await Administration(read).GetOverviewAsync("speed-reading")).EndingSoon);
        var reading = await Administration(read).GetOverviewAsync("speed-reading");
        Assert.Equal((1, 0, 0, 1, 100m, 0m), (reading.WaitingOrders, reading.DeclaredOrders, reading.WithdrawalRequests, reading.CurrentSubscriptions,
            reading.ApprovedThisMonth, reading.ApprovedLastMonth));
    }
}
