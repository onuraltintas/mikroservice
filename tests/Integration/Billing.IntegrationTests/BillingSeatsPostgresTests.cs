using Billing.Application;
using Billing.Domain;
using Billing.Infrastructure;
using EduPlatform.Shared.Contracts.Events.Billing;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Billing.IntegrationTests;

internal sealed class FakeInstitutions : IBillingInstitutions
{
    public Dictionary<Guid, List<Institution>> Administered { get; } = [];
    public Task<IReadOnlyList<Institution>> GetAdministeredAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Institution>>(Administered.GetValueOrDefault(userId) ?? []);
}

/// <summary>Institution accounts, seat pools and extra seats against a real PostgreSQL database.</summary>
public sealed class BillingSeatsPostgresTests : IAsyncLifetime
{
    private static readonly Guid Admin = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Institution School = new(Guid.Parse("cccccccc-0000-4000-8000-000000000001"), "TEST Kurum");
    private readonly PostgresFixture _postgres = new();
    private readonly Clock _clock = new(new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero));
    private readonly Recorder _events = new();
    private readonly FakeInstitutions _institutions = new();
    private readonly BillingUser _manager = new(Guid.NewGuid(), "mudur@example.invalid", ["InstitutionAdmin"]);
    private readonly BillingUser _teacher = new(Guid.NewGuid(), "ogretmen@example.invalid", ["Teacher"]);
    private DbContextOptions<BillingDbContext> _options = null!;
    private Guid _teacherPlan;
    private Guid _institutionPlan;
    private Guid _individualPlan;

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
        _institutions.Administered[_manager.Id] = [School];
        await using var db = Db();
        await db.Database.MigrateAsync();
        var catalog = new BillingCatalog(db, _clock);
        await catalog.UpdateSellerAsync(new SellerRequest("TEST Satıcı Ltd.", "Bir adres", "Daire", "1234567890", null, "satici@example.invalid", "0212", null), Admin);
        await catalog.CreateBankAccountAsync(new BankAccountRequest("Test Bankası", "TEST Satıcı Ltd.", "TR330006100519786457841326", null, null, true, 1), Admin);
        await catalog.UpdateProductSettingsAsync(BillingSeed.Coaching, new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin);
        var entitlements = (await catalog.GetProductsAsync()).SelectMany(item => item.Entitlements).ToDictionary(item => item.Key, item => item.Id);
        var seatSide = new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.SeatHolder, null);
        _teacherPlan = (await catalog.CreatePlanAsync(new PlanRequest("TEST Öğretmen 2", null, BuyerType.Teacher, 900, 30, 0, 2, null, false, true, true, 0, [],
            [new PlanEntitlementRequest(entitlements["coaching.teacher"], EntitlementHolder.Buyer, null), seatSide]), Admin)).Id;
        // 365 TL per extra seat for 365 days: one lira a day, so the prorated price is easy to read.
        _institutionPlan = (await catalog.CreatePlanAsync(new PlanRequest("TEST Kurum 3", null, BuyerType.Institution, 20_000, 365, 0, 3, 365, false, true, true, 0, [],
            [new PlanEntitlementRequest(entitlements["coaching.institution"], EntitlementHolder.Buyer, null), seatSide]), Admin)).Id;
        _individualPlan = (await catalog.CreatePlanAsync(new PlanRequest("TEST Bireysel", null, BuyerType.Individual, 100, 30, 0, null, null, false, true, true, 0, [],
            [new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.Buyer, null)]), Admin)).Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync();

    private BillingDbContext Db() => new(_options);
    private BillingOrders Orders(BillingDbContext db) => new(db, _events, _clock, _institutions);
    private BillingSeats Seats(BillingDbContext db) => new(db, Orders(db), _institutions, _clock);

    private async Task<OrderView> BuyAsync(BillingUser user, Guid plan, Guid? organization = null)
    {
        await using var db = Db();
        var order = await Orders(db).CreateOrderAsync(user, new CreateOrderRequest(plan, SalesDocumentTexts.Version, "Deneme Yetkili", organization));
        await Orders(db).ApproveOrderAsync(order.Id, Admin, null);
        return order;
    }

    private async Task<SeatChangeResult> AssignAsync(Guid user, Guid? ownerUser = null, Guid? organization = null, string product = "coaching")
    {
        // Seats line up by the moment they were given; no two are given at the same instant here.
        _clock.Now = _clock.Now.AddSeconds(1);
        await using var db = Db();
        return await Seats(db).AssignAsync(new SeatChangeRequest(product, ownerUser, organization, user, Admin, null));
    }

    private async Task<string[]> KeysAsync(Guid holder)
    {
        await using var db = Db();
        return (await Orders(db).GetAccessAsync(holder, "coaching"))!.Entitlements.Select(item => item.Key).ToArray();
    }

    private async Task<Guid> SubscriptionIdAsync()
    {
        await using var db = Db();
        return (await db.Subscriptions.SingleAsync()).Id;
    }

    [Fact]
    public async Task An_institution_plan_is_bought_only_by_an_admin_of_the_institution_and_belongs_to_the_institution()
    {
        var outsider = new BillingUser(Guid.NewGuid(), "baska@example.invalid", ["InstitutionAdmin"]);
        await using (var db = Db())
        {
            var orders = Orders(db);
            CreateOrderRequest Request(Guid plan, Guid? organization) => new(plan, SalesDocumentTexts.Version, "Deneme Yetkili", organization);
            Assert.Equal("Billing.OrganizationRequired", (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.CreateOrderAsync(_manager, Request(_institutionPlan, null)))).Code);
            Assert.Equal("Billing.OrganizationNotAdministered", (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.CreateOrderAsync(outsider, Request(_institutionPlan, School.Id)))).Code);
            Assert.Equal("Billing.PlanNotForBuyer", (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.CreateOrderAsync(_manager, Request(_individualPlan, School.Id)))).Code);
        }

        var order = await BuyAsync(_manager, _institutionPlan, School.Id);

        await using (var db = Db())
        {
            var account = await db.Accounts.SingleAsync();
            Assert.Equal((AccountType.Organization, School.Id, "TEST Kurum"), (account.Type, account.OrganizationId, account.DisplayName));
            var documents = await Orders(db).GetAcceptedDocumentsAsync(_manager.Id, order.Id);
            Assert.Contains("TEST Kurum (yetkili: Deneme Yetkili)", documents!.DistanceSales);
        }
        // What the plan gives the buyer belongs to the institution, not to the admin who paid.
        Assert.Equal(["coaching.institution"], await KeysAsync(School.Id));
        Assert.Empty(await KeysAsync(_manager.Id));

        // Another admin of the same institution renews: one account, one subscription, a year more.
        var colleague = new BillingUser(Guid.NewGuid(), "yardimci@example.invalid", ["InstitutionAdmin"]);
        _institutions.Administered[colleague.Id] = [School];
        await BuyAsync(colleague, _institutionPlan, School.Id);
        await using var check = Db();
        Assert.Equal(1, await check.Accounts.CountAsync());
        Assert.Equal(_clock.Now.UtcDateTime.AddDays(730), (await check.Subscriptions.SingleAsync()).EndsAt);

        var mine = Assert.Single(await Seats(check).GetMyOrganizationsAsync(colleague));
        Assert.Equal(("TEST Kurum", 1, 0), (mine.Name, mine.Subscriptions.Count, mine.PendingOrders.Count));
        Assert.Empty(await Seats(check).GetMyOrganizationsAsync(outsider));
    }

    [Fact]
    public async Task A_teacher_pool_gives_its_seats_to_students_up_to_the_count_and_a_released_seat_frees_one()
    {
        await BuyAsync(_teacher, _teacherPlan);
        var (first, second, third) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(["coaching.teacher"], await KeysAsync(_teacher.Id));
        _events.Messages.Clear();

        Assert.Equal("Assigned", (await AssignAsync(first, _teacher.Id)).Code);
        Assert.Equal("AlreadyAssigned", (await AssignAsync(first, _teacher.Id)).Code);
        var full = await AssignAsync(second, _teacher.Id);
        Assert.Equal((true, 2, 2), (full.Done, full.Pool!.Capacity, full.Pool.Used));
        var refused = await AssignAsync(third, _teacher.Id);
        Assert.Equal((false, "PoolFull"), (refused.Done, refused.Code));

        Assert.Equal(["coaching.student"], await KeysAsync(first));
        Assert.Equal(["coaching.student"], await KeysAsync(second));
        Assert.Empty(await KeysAsync(third));
        Assert.Contains(_events.Messages.OfType<BillingEntitlementsChangedV1>(), item => item.UserId == first);
        Assert.Contains(_events.Messages.OfType<BillingEntitlementsChangedV1>(), item => item.UserId == second);

        // Other owners, other products and missing owners have no pool.
        Assert.Equal("NoPool", (await AssignAsync(third, Guid.NewGuid())).Code);
        Assert.Equal("NoPool", (await AssignAsync(third, _teacher.Id, product: "speed-reading")).Code);
        await using (var db = Db())
            Assert.Equal("Billing.SeatRequestInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(
                () => Seats(db).AssignAsync(new SeatChangeRequest("coaching", null, null, third, Admin, null)))).Code);

        await using (var db = Db())
        {
            var released = await Seats(db).ReleaseAsync(new SeatChangeRequest("coaching", _teacher.Id, null, first, _teacher.Id, "Öğretmen koltuğu kaldırdı."));
            Assert.Equal(("Released", 1), (released.Code, released.Pool!.Used));
            Assert.Equal("NotAssigned", (await Seats(db).ReleaseAsync(new SeatChangeRequest("coaching", _teacher.Id, null, first, _teacher.Id, null))).Code);
        }
        Assert.Empty(await KeysAsync(first));
        Assert.Equal("Assigned", (await AssignAsync(third, _teacher.Id)).Code);
        // The pool is full again: the student who lost the seat does not get it back by asking.
        Assert.Equal("PoolFull", (await AssignAsync(first, _teacher.Id)).Code);

        // The owner sees the pool; a stranger does not. The seat that was taken back is kept on record.
        await using var view = Db();
        var subscription = await view.Subscriptions.SingleAsync();
        var pool = await Seats(view).GetMySeatsAsync(_teacher, subscription.Id);
        Assert.Equal(2, pool!.Used);
        Assert.Null(await Seats(view).GetMySeatsAsync(new BillingUser(Guid.NewGuid(), null, ["Teacher"]), subscription.Id));
        // A teacher plan sells no extra seats.
        Assert.Equal("Billing.SeatOrderInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(
            () => Seats(view).QuoteSeatOrderAsync(_teacher, subscription.Id, 1, "Deneme Öğretmen"))).Code);
    }

    [Fact]
    public async Task Two_requests_for_the_last_seat_give_it_once()
    {
        await BuyAsync(_teacher, _teacherPlan);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => AssignAsync(Guid.NewGuid(), _teacher.Id))));
        Assert.Equal(2, results.Count(item => item.Done));
        await using var db = Db();
        Assert.Equal(2, await db.Seats.CountAsync(item => !item.IsSuspended));
    }

    [Fact]
    public async Task Extra_seats_are_priced_for_the_days_left_join_the_pool_on_approval_and_leave_it_when_reversed()
    {
        await BuyAsync(_manager, _institutionPlan, School.Id);
        var subscriptionId = await SubscriptionIdAsync();
        var students = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var student in students.Take(3)) Assert.True((await AssignAsync(student, organization: School.Id)).Done);
        Assert.Equal("PoolFull", (await AssignAsync(students[3], organization: School.Id)).Code);

        // 100 days in, 265 days are left: two seats cost 2 x 265 lira.
        _clock.Now = _clock.Now.AddDays(100);
        OrderView order;
        await using (var db = Db())
        {
            var seats = Seats(db);
            var quote = await seats.QuoteSeatOrderAsync(_manager, subscriptionId, 2, "Deneme Yetkili");
            Assert.Equal((530m, 265), (quote!.Amount, quote.DaysLeft));
            Assert.Contains("TEST Kurum 3 — 2 ek koltuk", quote.Documents.PreInformation);
            Assert.Null(await seats.QuoteSeatOrderAsync(new BillingUser(Guid.NewGuid(), null, []), subscriptionId, 2, "Yabancı Kişi"));
            Assert.Equal("Billing.SeatOrderInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() => seats.QuoteSeatOrderAsync(_manager, subscriptionId, 0, "Deneme Yetkili"))).Code);
            Assert.Equal("Billing.DocumentsNotAccepted", (await Assert.ThrowsAsync<BusinessRuleException>(
                () => seats.CreateSeatOrderAsync(_manager, subscriptionId, new CreateSeatOrderRequest(2, null, "Deneme Yetkili")))).Code);
            order = (await seats.CreateSeatOrderAsync(_manager, subscriptionId, new CreateSeatOrderRequest(2, SalesDocumentTexts.Version, "Deneme Yetkili")))!;
            Assert.Equal((OrderKind.ExtraSeats, 2, 530m), (order.Kind, order.ExtraSeats, order.Amount));
            Assert.Equal("Billing.OrderPending", (await Assert.ThrowsAsync<BusinessRuleException>(
                () => seats.CreateSeatOrderAsync(_manager, subscriptionId, new CreateSeatOrderRequest(1, SalesDocumentTexts.Version, "Deneme Yetkili")))).Code);
            // Nothing changes until the money is approved.
            Assert.Equal(3, (await seats.GetSeatsAsync(subscriptionId))!.Capacity);
            await Orders(db).ApproveOrderAsync(order.Id, Admin, null);
            var pool = await seats.GetSeatsAsync(subscriptionId);
            Assert.Equal((5, 2, 365m), (pool!.Capacity, pool.ExtraSeats, pool.ExtraSeatPrice));
            // The subscription itself is not extended by extra seats.
            Assert.Equal(1, await db.SubscriptionPeriods.CountAsync());
        }
        foreach (var student in students.Skip(3).Take(2)) Assert.Equal("Assigned", (await AssignAsync(student, organization: School.Id)).Code);
        Assert.Equal("PoolFull", (await AssignAsync(students[5], organization: School.Id)).Code);
        Assert.Equal(["coaching.student"], await KeysAsync(students[4]));

        // The payment for the extra seats is reversed: the pool is three again; the last two keep their seat rows but not the access.
        await using (var db = Db())
        {
            await Orders(db).ReverseApprovalAsync(order.Id, Admin, "Ödeme bankadan geri döndü.");
            var pool = await Seats(db).GetSeatsAsync(subscriptionId);
            Assert.Equal((3, 5), (pool!.Capacity, pool.Used));
            Assert.Equal([true, true, true, false, false], pool.Seats.Select(item => item.IsCovered));
        }
        Assert.Equal(["coaching.student"], await KeysAsync(students[2]));
        Assert.Empty(await KeysAsync(students[3]));
        Assert.Empty(await KeysAsync(students[4]));

        // The admin takes a seat back: the next in line is covered.
        await using (var db = Db())
        {
            var pool = await Seats(db).AdminReleaseAsync(subscriptionId, students[0], Admin);
            Assert.Equal([true, true, true, false], pool!.Seats.Where(item => !item.IsSuspended).Select(item => item.IsCovered));
            Assert.Null(await Seats(db).AdminReleaseAsync(subscriptionId, students[0], Admin));
        }
        Assert.Empty(await KeysAsync(students[0]));
        Assert.Equal(["coaching.student"], await KeysAsync(students[3]));
    }

    [Fact]
    public async Task Extra_seats_are_not_carried_into_a_renewal()
    {
        await BuyAsync(_manager, _institutionPlan, School.Id);
        var subscriptionId = await SubscriptionIdAsync();
        var firstEnd = _clock.Now.UtcDateTime.AddDays(365);
        await using (var db = Db())
        {
            var order = await Seats(db).CreateSeatOrderAsync(_manager, subscriptionId, new CreateSeatOrderRequest(1, SalesDocumentTexts.Version, "Deneme Yetkili"));
            Assert.Equal(365m, order!.Amount);
            await Orders(db).ApproveOrderAsync(order.Id, Admin, null);
        }
        var students = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var student in students) Assert.True((await AssignAsync(student, organization: School.Id)).Done);
        await BuyAsync(_manager, _institutionPlan, School.Id);

        // The fourth seat stands on the extra seat: its access ends with the first year, the others run on.
        await using (var db = Db())
        {
            var grants = await db.EntitlementGrants.AsNoTracking().Where(item => item.Key == "coaching.student").ToDictionaryAsync(item => item.UserId, item => item.EndsAt);
            Assert.Equal(firstEnd.AddDays(365), grants[students[0]]);
            Assert.Equal(firstEnd, grants[students[3]]);
        }
        _clock.Now = _clock.Now.AddDays(366);
        Assert.Equal(["coaching.student"], await KeysAsync(students[0]));
        Assert.Empty(await KeysAsync(students[3]));
        await using var check = Db();
        var pool = await Seats(check).GetSeatsAsync(subscriptionId);
        Assert.Equal((3, 0, 4), (pool!.Capacity, pool.ExtraSeats, pool.Used));
        Assert.Equal("PoolFull", (await AssignAsync(Guid.NewGuid(), organization: School.Id)).Code);
    }
}
