using Billing.Application;
using Billing.Domain;
using Billing.Infrastructure;
using EduPlatform.Shared.Contracts.Events.Billing;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Billing.IntegrationTests;

/// <summary>Buying by bank transfer against a real PostgreSQL database: order, review, periods, grants, events.</summary>
public sealed class BillingOrdersPostgresTests : IAsyncLifetime
{
    private static readonly Guid Admin = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly byte[] Pdf = "%PDF-1.7 test"u8.ToArray();
    private readonly PostgresFixture _postgres = new();
    private readonly Clock _clock = new(new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero));
    private readonly RecordingEvents _events = new();
    private DbContextOptions<BillingDbContext> _options = null!;
    private Guid _monthly;
    private Guid _bundle;
    private Guid _teacherPlan;

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingEvents : IBillingEvents
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
        await using var db = new BillingDbContext(_options);
        await db.Database.MigrateAsync();
        var catalog = new BillingCatalog(db, _clock, _events);
        var entitlements = (await catalog.GetProductsAsync()).SelectMany(item => item.Entitlements).ToDictionary(item => item.Key, item => item.Id);
        PlanRequest Plan(string name, BuyerType buyer, decimal price, int days, int? seats, params PlanEntitlementRequest[] gives) =>
            new(name, null, buyer, price, days, 0, seats, null, false, true, true, 0, [], gives);
        _monthly = (await catalog.CreatePlanAsync(Plan("TEST Aylık", BuyerType.Individual, 200, 30, null,
            new PlanEntitlementRequest(entitlements["speed-reading.student"], EntitlementHolder.Buyer, null)), Admin)).Id;
        _bundle = (await catalog.CreatePlanAsync(Plan("TEST Paket", BuyerType.Individual, 500, 90, null,
            new(entitlements["speed-reading.student"], EntitlementHolder.Buyer, null),
            new(entitlements["coaching.student"], EntitlementHolder.Buyer, null)), Admin)).Id;
        _teacherPlan = (await catalog.CreatePlanAsync(Plan("TEST Öğretmen", BuyerType.Teacher, 900, 30, 10,
            new(entitlements["coaching.teacher"], EntitlementHolder.Buyer, null),
            new(entitlements["coaching.student"], EntitlementHolder.SeatHolder, null)), Admin)).Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync();

    private BillingDbContext Db() => new(_options);
    private BillingOrders Orders(BillingDbContext db) => new(db, _events, _clock);
    private static BillingUser Student(Guid? id = null) => new(id ?? Guid.NewGuid(), "ogrenci@example.invalid", ["Student"]);
    private static CreateOrderRequest Request(Guid plan) => new(plan, SalesDocumentTexts.Version, "Deneme Öğrenci");

    private async Task OpenSalesAsync(bool requireSpeedReading = true)
    {
        await using var db = Db();
        var catalog = new BillingCatalog(db, _clock, _events);
        await catalog.UpdateSellerAsync(new SellerRequest("TEST Satıcı Ltd.", "Bir adres", "Daire", "1234567890", null, "satici@example.invalid", "0212", null), Admin);
        await catalog.CreateBankAccountAsync(new BankAccountRequest("Test Bankası", "TEST Satıcı Ltd.", "TR330006100519786457841326", null, null, true, 1), Admin);
        if (requireSpeedReading)
            await catalog.UpdateProductSettingsAsync(BillingSeed.SpeedReading, new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin);
    }

    private async Task<OrderView> OrderAsync(BillingUser user, Guid plan)
    {
        await using var db = Db();
        return await Orders(db).CreateOrderAsync(user, Request(plan));
    }

    private async Task<AdminOrderView> ApproveAsync(Guid orderId)
    {
        await using var db = Db();
        return (await Orders(db).ApproveOrderAsync(orderId, Admin, null))!;
    }

    private async Task<ProductAccess> AccessAsync(Guid userId, string product)
    {
        await using var db = Db();
        return (await Orders(db).GetAccessAsync(userId, product))!;
    }

    [Fact]
    public async Task An_order_needs_open_sales_a_product_that_asks_for_a_subscription_and_accepted_documents()
    {
        var user = Student();
        await using (var db = Db())
        {
            var closed = await Assert.ThrowsAsync<BusinessRuleException>(() => Orders(db).CreateOrderAsync(user, Request(_monthly)));
            Assert.Equal("Billing.SubscriptionNotRequired", closed.Code);
        }
        await OpenSalesAsync();
        Assert.Contains(_events.Messages.OfType<BillingProductSettingsChangedV1>(), item => item is { ProductCode: "speed-reading", RequireSubscription: true });

        await using (var db = Db())
        {
            var orders = Orders(db);
            Assert.Equal("Billing.DocumentsNotAccepted",
                (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.CreateOrderAsync(user, new(_monthly, "2020-01-01", "Deneme Öğrenci")))).Code);
            Assert.Equal("Billing.BuyerNameInvalid",
                (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.CreateOrderAsync(user, new(_monthly, SalesDocumentTexts.Version, " ")))).Code);
            Assert.Equal("Billing.PlanNotForBuyer",
                (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.CreateOrderAsync(user, Request(_teacherPlan)))).Code);
            Assert.Equal("Billing.PlanNotFound",
                (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.CreateOrderAsync(user, Request(Guid.NewGuid())))).Code);
        }

        var order = await OrderAsync(user, _monthly);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Matches("^EDU-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{6}$", order.PaymentCode);
        Assert.Equal(200m, order.Amount);
        Assert.Equal(_clock.Now.UtcDateTime.AddDays(14), order.ExpiresAt);
        Assert.Equal(["speed-reading"], order.ProductCodes);

        await using (var db = Db())
        {
            var orders = Orders(db);
            // One waiting order per plan; another plan is fine.
            Assert.Equal("Billing.OrderPending", (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.CreateOrderAsync(user, Request(_monthly)))).Code);
            var documents = await orders.GetAcceptedDocumentsAsync(user.Id, order.Id);
            Assert.Contains("TEST Satıcı Ltd.", documents!.DistanceSales);
            Assert.Contains("Deneme Öğrenci", documents.PreInformation);
            Assert.Contains("Master Hızlı Okuma TEST Aylık aboneliği", documents.PreInformation);
            Assert.Contains("200,00 TL", documents.PreInformation);
            Assert.Null(await orders.GetAcceptedDocumentsAsync(Guid.NewGuid(), order.Id));
            Assert.Single(await db.Accounts.Where(item => item.UserId == user.Id).ToListAsync());
        }
    }

    [Fact]
    public async Task The_buyer_declares_payment_with_a_receipt_and_can_take_a_waiting_order_back()
    {
        await OpenSalesAsync();
        var user = Student();
        var order = await OrderAsync(user, _monthly);
        await using var db = Db();
        var orders = Orders(db);

        Assert.Equal("Billing.ReceiptInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(
            () => orders.DeclarePaidAsync(user.Id, order.Id, null, new ReceiptUpload("x.exe", "MZ not a receipt"u8.ToArray())))).Code);
        Assert.Null(await orders.DeclarePaidAsync(Guid.NewGuid(), order.Id, null, null));

        var declared = await orders.DeclarePaidAsync(user.Id, order.Id, " Halkbank'tan gönderdim ", new ReceiptUpload("C:\\Belgeler\\dekont.pdf", Pdf));
        Assert.NotNull(declared!.PaidDeclaredAt);
        Assert.True(declared.HasReceipt);
        Assert.Equal("Halkbank'tan gönderdim", declared.PayerNote);
        var receipt = await orders.GetReceiptAsync(user.Id, order.Id);
        Assert.Equal(("dekont.pdf", "application/pdf"), (receipt!.FileName, receipt.ContentType));
        Assert.Null(await orders.GetReceiptAsync(Guid.NewGuid(), order.Id));
        Assert.NotNull(await orders.GetReceiptAsync(null, order.Id));

        var withdrawn = await orders.WithdrawOrderAsync(user.Id, order.Id);
        Assert.Equal(OrderStatus.Withdrawn, withdrawn!.Status);
        Assert.Null(await orders.GetReceiptAsync(null, order.Id));
        Assert.Equal("Billing.OrderNotPending", (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.WithdrawOrderAsync(user.Id, order.Id))).Code);
        Assert.Equal("Billing.OrderNotPending", (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.ApproveOrderAsync(order.Id, Admin, null))).Code);
    }

    [Fact]
    public async Task Approval_opens_the_subscription_gives_the_entitlement_and_is_single_shot()
    {
        await OpenSalesAsync();
        var user = Student();
        var order = await OrderAsync(user, _monthly);
        Assert.Empty((await AccessAsync(user.Id, "speed-reading")).Entitlements);
        _events.Messages.Clear();

        var approved = await ApproveAsync(order.Id);

        Assert.Equal(OrderStatus.Paid, approved.Order.Status);
        Assert.Equal(Admin, approved.ReviewedBy);
        var access = await AccessAsync(user.Id, "speed-reading");
        Assert.True(access.RequireSubscription);
        var entitlement = Assert.Single(access.Entitlements);
        Assert.Equal("speed-reading.student", entitlement.Key);
        Assert.Equal(_clock.Now.UtcDateTime.AddDays(30), entitlement.EndsAt);
        Assert.Empty((await AccessAsync(user.Id, "coaching")).Entitlements);
        Assert.Empty((await AccessAsync(Guid.NewGuid(), "speed-reading")).Entitlements);
        var changed = Assert.Single(_events.Messages.OfType<BillingEntitlementsChangedV1>());
        Assert.Equal((user.Id, "speed-reading"), (changed.UserId, Assert.Single(changed.ProductCodes)));

        await using var db = Db();
        Assert.Equal("Billing.OrderNotPending", (await Assert.ThrowsAsync<BusinessRuleException>(() => Orders(db).ApproveOrderAsync(order.Id, Admin, null))).Code);
        Assert.Equal("Billing.OrderNotPending", (await Assert.ThrowsAsync<BusinessRuleException>(() => Orders(db).RejectOrderAsync(order.Id, Admin, "Ödeme gelmedi."))).Code);
        var payment = Assert.Single(await db.Payments.ToListAsync());
        Assert.Equal((200m, PaymentStatus.Approved, PaymentMethod.BankTransfer), (payment.Amount, payment.Status, payment.Method));

        var mine = await Orders(db).GetMyBillingAsync(user.Id, "speed-reading");
        Assert.True(Assert.Single(mine!.Subscriptions).IsCurrent);
        Assert.Empty(mine.PendingOrders);
        Assert.True(mine.SalesOpen);

        // The entitlement ends with the period.
        _clock.Now = _clock.Now.AddDays(30);
        Assert.Empty((await AccessAsync(user.Id, "speed-reading")).Entitlements);
    }

    [Fact]
    public async Task Two_approvals_at_once_pay_an_order_once()
    {
        await OpenSalesAsync();
        var order = await OrderAsync(Student(), _monthly);

        async Task<bool> TryApprove()
        {
            await using var db = Db();
            try { return await Orders(db).ApproveOrderAsync(order.Id, Admin, null) is not null; }
            catch (BusinessRuleException) { return false; }
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(TryApprove)));

        Assert.Equal(1, results.Count(approved => approved));
        await using var check = Db();
        Assert.Equal(1, await check.Payments.CountAsync());
        Assert.Equal(1, await check.SubscriptionPeriods.CountAsync());
    }

    [Fact]
    public async Task A_renewal_starts_when_the_running_period_ends_and_a_reversal_gives_back_only_its_own_time()
    {
        await OpenSalesAsync();
        var user = Student();
        var start = _clock.Now.UtcDateTime;
        var first = await OrderAsync(user, _monthly);
        await ApproveAsync(first.Id);
        _clock.Now = _clock.Now.AddDays(10);
        var second = await OrderAsync(user, _monthly);
        await ApproveAsync(second.Id);

        // 30 + 30 days from the first approval, not from the second.
        Assert.Equal(start.AddDays(60), Assert.Single((await AccessAsync(user.Id, "speed-reading")).Entitlements).EndsAt);

        // The first payment is reversed on day 10: its 20 unused days go, the renewal keeps its 30 and moves forward.
        await using (var db = Db())
        {
            var orders = Orders(db);
            Assert.Equal("Billing.NoteRequired", (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.ReverseApprovalAsync(first.Id, Admin, " "))).Code);
            var reversed = await orders.ReverseApprovalAsync(first.Id, Admin, "Ödeme bankadan geri döndü.");
            Assert.Equal(OrderStatus.Cancelled, reversed!.Order.Status);
            Assert.Contains("Ödeme bankadan geri döndü.", reversed.Order.ReviewNote);
            Assert.Equal("Billing.OrderNotPaid", (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.ReverseApprovalAsync(first.Id, Admin, "İkinci kez."))).Code);
        }
        Assert.Equal(start.AddDays(40), Assert.Single((await AccessAsync(user.Id, "speed-reading")).Entitlements).EndsAt);

        // The second is reversed too: nothing paid is left, access ends now, and the payments stay as financial records.
        await using (var db = Db())
        {
            await Orders(db).ReverseApprovalAsync(second.Id, Admin, "Ödeme bankadan geri döndü.");
            Assert.Empty((await Orders(db).GetAccessAsync(user.Id, "speed-reading"))!.Entitlements);
            Assert.Equal(2, await db.Payments.CountAsync(item => item.Status == PaymentStatus.Reversed));
            Assert.Equal(SubscriptionStatus.Cancelled, (await db.Subscriptions.SingleAsync()).Status);
        }

        // A new payment after that starts a fresh period on the same subscription.
        var third = await OrderAsync(user, _monthly);
        await ApproveAsync(third.Id);
        Assert.Equal(_clock.Now.UtcDateTime.AddDays(30), Assert.Single((await AccessAsync(user.Id, "speed-reading")).Entitlements).EndsAt);
        await using var check = Db();
        Assert.Equal(1, await check.Subscriptions.CountAsync());
    }

    [Fact]
    public async Task A_bundle_gives_access_in_each_of_its_products_and_what_was_bought_does_not_change_with_the_plan()
    {
        await OpenSalesAsync();
        var user = Student();
        var order = await OrderAsync(user, _bundle);
        Assert.Equal(["coaching", "speed-reading"], order.ProductCodes);

        // The admin changes the plan while the order waits: price, time and entitlements of the order stay.
        await using (var db = Db())
        {
            var catalog = new BillingCatalog(db, _clock, _events);
            var speedReading = (await catalog.GetProductsAsync()).Single(item => item.Code == "speed-reading").Entitlements.Single(item => item.Key == "speed-reading.student");
            await catalog.UpdatePlanAsync(_bundle, new PlanRequest("TEST Paket", null, BuyerType.Individual, 999, 7, 0, null, null, false, true, true, 0, [],
                [new PlanEntitlementRequest(speedReading.Id, EntitlementHolder.Buyer, null)]), Admin);
        }
        _events.Messages.Clear();
        var approved = await ApproveAsync(order.Id);

        Assert.Equal(500m, approved.Order.Amount);
        var end = _clock.Now.UtcDateTime.AddDays(90);
        Assert.Equal(end, Assert.Single((await AccessAsync(user.Id, "speed-reading")).Entitlements).EndsAt);
        Assert.Equal(end, Assert.Single((await AccessAsync(user.Id, "coaching")).Entitlements).EndsAt);
        Assert.False((await AccessAsync(user.Id, "coaching")).RequireSubscription);
        Assert.Equal(["coaching", "speed-reading"], Assert.Single(_events.Messages.OfType<BillingEntitlementsChangedV1>()).ProductCodes);

        await using var check = Db();
        var listed = await Orders(check).GetSubscriptionsAsync("coaching");
        Assert.Equal("Deneme Öğrenci", Assert.Single(listed).AccountName);
        Assert.Empty(await Orders(check).GetSubscriptionsAsync("course-management"));
    }

    [Fact]
    public async Task A_teacher_buys_a_teacher_plan_and_only_the_buyer_side_entitlement_is_granted()
    {
        await OpenSalesAsync(requireSpeedReading: false);
        await using (var db = Db())
            await new BillingCatalog(db, _clock, _events).UpdateProductSettingsAsync(BillingSeed.Coaching, new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin);
        var teacher = new BillingUser(Guid.NewGuid(), "ogretmen@example.invalid", ["Teacher"]);
        var order = await OrderAsync(teacher, _teacherPlan);
        await ApproveAsync(order.Id);

        // Seat holders get theirs when seats are given out (stage 4).
        Assert.Equal("coaching.teacher", Assert.Single((await AccessAsync(teacher.Id, "coaching")).Entitlements).Key);
        await using var check = Db();
        Assert.Equal(10, (await check.Subscriptions.SingleAsync()).SeatCount);
    }

    [Fact]
    public async Task A_waiting_order_drops_after_fourteen_days_and_the_queue_lists_declared_payments_first()
    {
        await OpenSalesAsync();
        var early = await OrderAsync(Student(), _monthly);
        _clock.Now = _clock.Now.AddDays(1);
        var declaredUser = Student();
        var declared = await OrderAsync(declaredUser, _monthly);
        await using (var db = Db())
        {
            var orders = Orders(db);
            await orders.DeclarePaidAsync(declaredUser.Id, declared.Id, null, new ReceiptUpload("dekont.pdf", Pdf));
            var queue = await orders.GetOrdersAsync(OrderStatus.Pending, "speed-reading");
            Assert.Equal([declared.Id, early.Id], queue.Select(item => item.Order.Id));
            Assert.Empty(await orders.GetOrdersAsync(OrderStatus.Pending, "coaching"));
            Assert.Equal(0, await orders.ExpirePendingOrdersAsync());
        }

        _clock.Now = _clock.Now.AddDays(13);
        await using (var db = Db())
        {
            var orders = Orders(db);
            Assert.Equal("Billing.OrderExpired", (await Assert.ThrowsAsync<BusinessRuleException>(() => orders.ApproveOrderAsync(early.Id, Admin, null))).Code);
            Assert.Equal(1, await orders.ExpirePendingOrdersAsync());
            Assert.Equal(OrderStatus.Expired, (await orders.GetOrdersAsync(OrderStatus.Expired, null)).Single().Order.Status);
            Assert.Single(await orders.GetOrdersAsync(OrderStatus.Pending, null));
        }
        _clock.Now = _clock.Now.AddDays(1);
        await using (var db = Db())
        {
            Assert.Equal(1, await Orders(db).ExpirePendingOrdersAsync());
            // The receipt of an order that dropped is not kept.
            Assert.Equal(0, await db.OrderReceipts.CountAsync());
        }
    }

    [Fact]
    public async Task An_entitlement_in_use_cannot_be_removed_from_the_catalog()
    {
        await OpenSalesAsync();
        var order = await OrderAsync(Student(), _monthly);
        await ApproveAsync(order.Id);
        await using var db = Db();
        var catalog = new BillingCatalog(db, _clock, _events);
        var entitlement = (await catalog.GetProductsAsync()).SelectMany(item => item.Entitlements).Single(item => item.Key == "speed-reading.student");
        Assert.Equal("Billing.EntitlementInUse", (await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.RemoveEntitlementAsync(entitlement.Id))).Code);
    }
}
