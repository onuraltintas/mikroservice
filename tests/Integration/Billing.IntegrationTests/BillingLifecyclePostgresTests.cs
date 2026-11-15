using Billing.Application;
using Billing.Domain;
using Billing.Infrastructure;
using EduPlatform.Shared.Contracts.Events.Billing;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Billing.IntegrationTests;

/// <summary>Notices, reminders and the retention of an erased account's records against a real PostgreSQL database.</summary>
public sealed class BillingLifecyclePostgresTests : IAsyncLifetime
{
    private static readonly Guid Admin = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly byte[] Pdf = "%PDF-1.7 test"u8.ToArray();
    private readonly PostgresFixture _postgres = new();
    private readonly Clock _clock = new(new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero));
    private readonly Recorder _events = new();
    private DbContextOptions<BillingDbContext> _options = null!;
    private Guid _monthly;
    private Guid _teacherPlan;

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Recorder : IBillingEvents
    {
        public List<object> Messages { get; } = [];
        public IEnumerable<BillingNoticeV1> Notices => Messages.OfType<BillingNoticeV1>();
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _options = new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(_postgres.ConnectionString).Options;
        await using var db = Db();
        await db.Database.MigrateAsync();
        var catalog = new BillingCatalog(db, _clock);
        await SellerAsync("finans@example.invalid");
        await catalog.CreateBankAccountAsync(new BankAccountRequest("Test Bankası", "TEST Satıcı Ltd.", "TR330006100519786457841326", null, null, true, 1), Admin);
        await catalog.UpdateProductSettingsAsync(BillingSeed.Coaching, new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin);
        var entitlements = (await catalog.GetProductsAsync()).SelectMany(item => item.Entitlements).ToDictionary(item => item.Key, item => item.Id);
        _monthly = (await catalog.CreatePlanAsync(new PlanRequest("TEST Aylık", null, BuyerType.Individual, 200, 30, 0, null, null, false, true, true, 0, [],
            [new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.Buyer, null)]), Admin)).Id;
        _teacherPlan = (await catalog.CreatePlanAsync(new PlanRequest("TEST Öğretmen", null, BuyerType.Teacher, 900, 30, 0, 5, null, false, true, true, 0, [],
            [new PlanEntitlementRequest(entitlements["coaching.teacher"], EntitlementHolder.Buyer, null),
             new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.SeatHolder, null)]), Admin)).Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync();

    private BillingDbContext Db() => new(_options);
    private BillingOrders Orders(BillingDbContext db) => new(db, _events, _clock);
    private BillingExtras Extras(BillingDbContext db) => new(db, Orders(db), _clock);
    private BillingLifecycle Lifecycle(BillingDbContext db) => new(db, Orders(db), _events, _clock);
    private static BillingUser Student(string email = "ogrenci@example.invalid") => new(Guid.NewGuid(), email, ["Student"]);

    private async Task SellerAsync(string? alertEmail)
    {
        await using var db = Db();
        await new BillingCatalog(db, _clock).UpdateSellerAsync(
            new SellerRequest("TEST Satıcı Ltd.", "Bir adres", "Daire", "1234567890", null, "satici@example.invalid", "0212", alertEmail), Admin);
    }

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

    private async Task<int> RemindAsync()
    {
        await using var db = Db();
        return await Lifecycle(db).SendRemindersAsync();
    }

    [Fact]
    public async Task The_buyer_and_the_seller_are_told_what_happens_to_an_order()
    {
        var user = Student("ada@example.invalid");
        var order = await OrderAsync(user, _monthly);
        Assert.Empty(_events.Notices);

        await using (var db = Db())
        {
            await Orders(db).DeclarePaidAsync(user.Id, order.Id, null, null);
            // Saying it twice tells the seller once.
            await Orders(db).DeclarePaidAsync(user.Id, order.Id, "Tekrar", null);
        }
        var declared = Assert.Single(_events.Notices);
        Assert.Equal((BillingNoticeKinds.PaymentDeclared, "finans@example.invalid", null, 200m), (declared.Kind, declared.RecipientEmail, declared.RecipientUserId, declared.Amount));
        Assert.Contains(order.PaymentCode, declared.Detail);
        Assert.Contains("Deneme Öğrenci", declared.Detail);

        await ApproveAsync(order.Id);
        var approved = _events.Notices.Last();
        Assert.Equal((BillingNoticeKinds.OrderApproved, "ada@example.invalid", user.Id, _clock.Now.UtcDateTime.AddDays(30)),
            (approved.Kind, approved.RecipientEmail, approved.RecipientUserId, approved.EndsAt));
        Assert.Equal(["coaching"], approved.ProductCodes);

        await using (var db = Db())
        {
            await Extras(db).AttachInvoiceAsync(order.Id, new InvoiceUpload("ABC2026000000001", new DateOnly(2026, 11, 15), "fatura.pdf", Pdf), Admin);
            Assert.Equal((BillingNoticeKinds.InvoiceReady, "ABC2026000000001"), (_events.Notices.Last().Kind, _events.Notices.Last().Detail));
            await Extras(db).RequestWithdrawalAsync(user.Id, order.Id, null);
            Assert.Equal((BillingNoticeKinds.WithdrawalRequested, "finans@example.invalid"), (_events.Notices.Last().Kind, _events.Notices.Last().RecipientEmail));
            var request = Assert.Single(await Extras(db).GetWithdrawalsAsync(WithdrawalStatus.Requested));
            await Extras(db).ReviewWithdrawalAsync(request.Id, new ReviewWithdrawalRequest(true, null), Admin);
        }
        // Accepting a withdrawal is told as that, not as a reversal.
        Assert.Equal((BillingNoticeKinds.WithdrawalApproved, "ada@example.invalid"), (_events.Notices.Last().Kind, _events.Notices.Last().RecipientEmail));
        Assert.DoesNotContain(_events.Notices, item => item.Kind == BillingNoticeKinds.ApprovalReversed);

        var second = await OrderAsync(user, _monthly);
        await using (var db = Db())
        {
            await Orders(db).RejectOrderAsync(second.Id, Admin, "Ödeme hesabımıza ulaşmadı.");
            Assert.Equal((BillingNoticeKinds.OrderRejected, "Ödeme hesabımıza ulaşmadı."), (_events.Notices.Last().Kind, _events.Notices.Last().Detail));
        }
        var third = await OrderAsync(user, _monthly);
        await ApproveAsync(third.Id);
        await using (var db = Db())
        {
            await Orders(db).ReverseApprovalAsync(third.Id, Admin, "Ödeme bankadan geri döndü.");
            Assert.Equal(BillingNoticeKinds.ApprovalReversed, _events.Notices.Last().Kind);
        }
        var fourth = await OrderAsync(user, _monthly);
        _clock.Now = _clock.Now.AddDays(14);
        await using (var db = Db())
        {
            Assert.Equal(1, await Orders(db).ExpirePendingOrdersAsync());
            Assert.Equal((BillingNoticeKinds.OrderExpired, fourth.PaymentCode), (_events.Notices.Last().Kind, _events.Notices.Last().PaymentCode));
        }
    }

    [Fact]
    public async Task Without_an_alert_address_the_seller_gets_nothing_and_the_buyer_still_does()
    {
        await SellerAsync(null);
        var user = Student();
        var order = await OrderAsync(user, _monthly);
        await using (var db = Db()) await Orders(db).DeclarePaidAsync(user.Id, order.Id, null, null);
        Assert.Empty(_events.Notices);
        await ApproveAsync(order.Id);
        Assert.Equal(BillingNoticeKinds.OrderApproved, Assert.Single(_events.Notices).Kind);
    }

    [Fact]
    public async Task A_subscription_is_reminded_seven_days_and_one_day_before_it_ends_and_when_it_has_ended_each_once()
    {
        var user = Student("ada@example.invalid");
        await ApproveAsync((await OrderAsync(user, _monthly)).Id);
        var end = _clock.Now.UtcDateTime.AddDays(30);
        _events.Messages.Clear();

        Assert.Equal(0, await RemindAsync());
        _clock.Now = _clock.Now.AddDays(24);
        Assert.Equal(1, await RemindAsync());
        Assert.Equal(0, await RemindAsync());
        var expiring = Assert.Single(_events.Notices);
        Assert.Equal((BillingNoticeKinds.Expiring, "ada@example.invalid", user.Id, end, null), (expiring.Kind, expiring.RecipientEmail, expiring.RecipientUserId, expiring.EndsAt, expiring.Detail));

        _clock.Now = _clock.Now.AddDays(5).AddHours(12);
        Assert.Equal(1, await RemindAsync());
        Assert.Equal(BillingNoticeKinds.ExpiringTomorrow, _events.Notices.Last().Kind);
        _clock.Now = _clock.Now.AddDays(1);
        Assert.Equal(1, await RemindAsync());
        Assert.Equal(0, await RemindAsync());
        Assert.Equal(BillingNoticeKinds.Expired, _events.Notices.Last().Kind);

        // A renewal gives the subscription a new end, and that end its own reminders.
        await ApproveAsync((await OrderAsync(user, _monthly)).Id);
        _events.Messages.Clear();
        _clock.Now = _clock.Now.AddDays(25);
        Assert.Equal(1, await RemindAsync());
        Assert.Equal(BillingNoticeKinds.Expiring, Assert.Single(_events.Notices).Kind);
    }

    [Fact]
    public async Task Nobody_is_urged_to_renew_a_product_that_asks_for_no_subscription_and_a_seat_plan_is_reminded_of_its_seats()
    {
        var teacher = new BillingUser(Guid.NewGuid(), "ogretmen@example.invalid", ["Teacher"]);
        await ApproveAsync((await OrderAsync(teacher, _teacherPlan)).Id);
        _events.Messages.Clear();
        _clock.Now = _clock.Now.AddDays(25);
        await using (var db = Db())
            await new BillingCatalog(db, _clock).UpdateProductSettingsAsync(BillingSeed.Coaching, new ProductSettingsRequest(false, BankTransferApproval.Manual), Admin);
        Assert.Equal(0, await RemindAsync());
        await using (var db = Db())
            await new BillingCatalog(db, _clock).UpdateProductSettingsAsync(BillingSeed.Coaching, new ProductSettingsRequest(true, BankTransferApproval.Manual), Admin);
        Assert.Equal(1, await RemindAsync());
        Assert.Equal("Seats", Assert.Single(_events.Notices).Detail);
    }

    [Fact]
    public async Task An_erased_account_loses_access_at_once_keeps_its_financial_records_ten_years_and_is_then_stripped_of_names()
    {
        var teacher = new BillingUser(Guid.NewGuid(), "ogretmen@example.invalid", ["Teacher"]);
        await ApproveAsync((await OrderAsync(teacher, _teacherPlan)).Id);
        var user = Student("ada@example.invalid");
        var paid = await OrderAsync(user, _monthly);
        await using (var db = Db()) await Orders(db).DeclarePaidAsync(user.Id, paid.Id, "Gönderdim", new ReceiptUpload("dekont.pdf", Pdf));
        await ApproveAsync(paid.Id);
        await using (var db = Db())
        {
            await Extras(db).AttachInvoiceAsync(paid.Id, new InvoiceUpload("ABC2026000000009", new DateOnly(2026, 11, 15), "fatura.pdf", Pdf), Admin);
            await new BillingSeats(db, Orders(db), new FakeInstitutions(), _clock).AssignAsync(new SeatChangeRequest("coaching", teacher.Id, null, user.Id, teacher.Id, null));
        }
        var other = await OrderAsync(user, _monthly);

        await using (var db = Db())
        {
            var lifecycle = Lifecycle(db);
            var assessment = await lifecycle.AssessErasureAsync(user.Id);
            Assert.Equal((1, 1, 1, 1), (assessment.Payments, assessment.Subscriptions, assessment.Seats, assessment.Invoices));
            Assert.True(await lifecycle.EraseUserAsync(user.Id) > 0);
            Assert.Equal(0, await lifecycle.EraseUserAsync(user.Id));
        }
        await using (var db = Db())
        {
            Assert.Empty((await Orders(db).GetAccessAsync(user.Id, "coaching"))!.Entitlements);
            // The teacher's own access and pool are untouched; the seat is free again.
            Assert.Single((await Orders(db).GetAccessAsync(teacher.Id, "coaching"))!.Entitlements);
            Assert.True((await db.Seats.SingleAsync()).IsSuspended);
            Assert.Equal(OrderStatus.Withdrawn, (await db.Orders.SingleAsync(item => item.Id == other.Id)).Status);
            Assert.Equal(0, await db.OrderReceipts.CountAsync());
            // What was paid stays as it was, with the buyer's name, for the retention time.
            Assert.Equal(PaymentStatus.Approved, (await db.Payments.SingleAsync(item => item.OrderId == paid.Id)).Status);
            Assert.Equal("Deneme Öğrenci", (await db.Accounts.SingleAsync(item => item.UserId == user.Id)).DisplayName);
            Assert.Equal(1, await db.Invoices.CountAsync());
            Assert.Equal(_clock.Now.UtcDateTime.AddYears(10), (await db.ErasureReceipts.SingleAsync()).RedactAfter);
            Assert.Equal(0, await Lifecycle(db).RedactExpiredAsync());
        }

        _clock.Now = _clock.Now.AddYears(10).AddDays(1);
        await using (var db = Db())
        {
            Assert.Equal(1, await Lifecycle(db).RedactExpiredAsync());
            Assert.Equal(0, await Lifecycle(db).RedactExpiredAsync());
        }
        await using var check = Db();
        var account = await check.Accounts.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal((RetentionRules.RedactedName, null), (account.DisplayName, account.Email));
        var order = await check.Orders.SingleAsync(item => item.Id == paid.Id);
        Assert.DoesNotContain("Deneme Öğrenci", order.DocumentsJson);
        Assert.Null(order.PayerNote);
        // The bookkeeping facts remain.
        Assert.Equal((200m, OrderStatus.Paid), (order.Amount, order.Status));
        Assert.Equal(1, await check.Payments.CountAsync(item => item.OrderId == paid.Id));
        Assert.Equal(0, await check.Invoices.CountAsync());
        // The teacher is somebody else: nothing of theirs was touched.
        Assert.Equal("Deneme Öğrenci", (await check.Accounts.SingleAsync(item => item.UserId == teacher.Id)).DisplayName);
    }

    [Fact]
    public async Task Nothing_is_approved_for_an_erased_account()
    {
        var user = Student();
        var order = await OrderAsync(user, _monthly);
        await using var db = Db();
        // The receipt is written directly: the order must still be waiting when the approval is tried.
        db.ErasureReceipts.Add(new ErasureReceipt { UserId = user.Id, ErasedAt = _clock.Now.UtcDateTime, RedactAfter = _clock.Now.UtcDateTime.AddYears(10) });
        await db.SaveChangesAsync();
        Assert.Equal("Billing.UserErased", (await Assert.ThrowsAsync<BusinessRuleException>(() => Orders(db).ApproveOrderAsync(order.Id, Admin, null))).Code);
        await using var check = Db();
        Assert.Equal(0, await check.Payments.CountAsync());
        Assert.Equal(0, await check.Subscriptions.CountAsync());
    }

    [Fact]
    public async Task A_bank_line_tied_to_no_order_loses_its_sender_a_year_after_the_movement()
    {
        var now = _clock.Now.UtcDateTime;
        Guid old, recent, ignored;
        await using (var db = Db())
        {
            var account = new BankAccount { Id = Guid.NewGuid(), BankName = "Test Bankası", AccountHolder = "TEST Satıcı Ltd.", Iban = "TR610003200013900000326785",
                IsActive = false, SortOrder = 9, CreatedBy = Admin, CreatedAt = now };
            var import = new BankStatementImport { Id = Guid.NewGuid(), BankAccountId = account.Id, FileName = "test.csv", UploadedBy = Admin, UploadedAt = now, RowCount = 3, AddedCount = 3 };
            BankTransaction Line(DateTime bookedAt, BankTransactionStatus status, string fingerprint) => new()
            {
                Id = Guid.NewGuid(), BankAccountId = account.Id, ImportId = import.Id, BookedAt = bookedAt, Amount = 150m, Description = "TEST ilgisiz havale",
                Sender = "Deneme Gönderen", Fingerprint = fingerprint, Status = status, Note = "Ödeme kodu yok", CreatedAt = now
            };
            var lines = new[] { Line(now.AddYears(-1).AddDays(-1), BankTransactionStatus.Unmatched, "test-old"),
                Line(now.AddDays(-300), BankTransactionStatus.Unmatched, "test-recent"), Line(now.AddYears(-2), BankTransactionStatus.Ignored, "test-ignored") };
            (old, recent, ignored) = (lines[0].Id, lines[1].Id, lines[2].Id);
            db.AddRange(account, import);
            db.AddRange(lines);
            await db.SaveChangesAsync();
        }

        await using (var db = Db())
        {
            Assert.Equal(2, await Lifecycle(db).ClearUnmatchedBankLinesAsync());
            Assert.Equal(0, await Lifecycle(db).ClearUnmatchedBankLinesAsync());
        }

        await using var check = Db();
        foreach (var id in new[] { old, ignored })
        {
            var line = await check.BankTransactions.SingleAsync(item => item.Id == id);
            Assert.Equal((null, RetentionRules.RedactedText, null), (line.Sender, line.Description, line.Note));
            // The bookkeeping facts and the duplicate check remain.
            Assert.Equal(150m, line.Amount);
            Assert.StartsWith("test-", line.Fingerprint);
        }
        var kept = await check.BankTransactions.SingleAsync(item => item.Id == recent);
        Assert.Equal(("Deneme Gönderen", "TEST ilgisiz havale"), (kept.Sender, kept.Description));
    }
}
