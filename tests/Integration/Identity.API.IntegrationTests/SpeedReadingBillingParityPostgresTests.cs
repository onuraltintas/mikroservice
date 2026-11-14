using System.Reflection;
using System.Text;
using EduPlatform.Shared.Contracts.Events.SpeedReading;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Kernel.Exceptions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Hızlı Okuma billing parity (user decision 14.11.2026): payment decisions, withdrawal answers and expiry reach the payer
/// and the admin as they do in Koçluk, and an approved payment carries its invoice.
/// </summary>
[Collection("Database")]
public sealed class SpeedReadingBillingParityPostgresTests(PostgresFixture postgres)
{
    private static readonly Guid SettingsId = Guid.Parse("4c18b5a8-96e8-4af6-9195-0d4fdde3dcf2");
    private static readonly Assembly Infrastructure = typeof(OwnedSpeedReadingDbContext).Assembly;

    [Fact]
    public async Task Payment_and_withdrawal_decisions_tell_the_payer_and_the_admin_and_an_approved_payment_takes_an_invoice()
    {
        await using var db = await CreateAsync();
        try
        {
            var buyer = Guid.NewGuid(); var stranger = Guid.NewGuid(); var admin = Guid.NewGuid();
            var (plan, _) = await SeedAsync(db, sellerComplete: true);
            var notices = new RecordingNotices();
            var directory = new Directory();
            var subscriptions = (ISpeedReadingSubscription)Activator.CreateInstance(
                Infrastructure.GetType("SpeedReading.Infrastructure.Legacy.LegacySpeedReadingSubscription")!,
                BindingFlags.Instance | BindingFlags.NonPublic, null, [db, db, null, null, directory], null)!;
            var sales = (ISpeedReadingSales)Activator.CreateInstance(
                Infrastructure.GetType("SpeedReading.Infrastructure.Legacy.SpeedReadingSalesService")!,
                BindingFlags.Instance | BindingFlags.NonPublic, null, [db, directory], null)!;
            foreach (var service in new object[] { subscriptions, sales })
                service.GetType().GetProperty("Notices", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, notices);
            CreateBankTransferPaymentRequest Notice(string reference) => new(plan, reference, "Veli", null, SalesDocumentsVersion: SpeedReadingSalesDocumentTexts.Version);

            // A new payment notice goes to the admin only.
            var first = await subscriptions.CreateBankTransferPaymentRequestAsync(buyer, Notice("EFT-PAR-01"), "parity-create-key-0001");
            var submitted = Assert.Single(notices.Items);
            Assert.Equal((SpeedReadingSubscriptionNoticeKinds.TransferSubmitted, (Guid?)null, "EFT-PAR-01"), (submitted.Kind, submitted.UserId, submitted.Detail));
            db.ChangeTracker.Clear(); notices.Items.Clear();

            // No invoice before the approval.
            Assert.Equal("Sales.InvoiceNotAllowed", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sales.AttachInvoiceAsync(admin, first!.Id, "ONL2026000001", DateOnly.FromDateTime(DateTime.UtcNow), "fatura.pdf", Pdf("a")))).Code);
            db.ChangeTracker.Clear();

            // A rejection tells the payer why.
            var second = await subscriptions.CreateBankTransferPaymentRequestAsync(buyer, Notice("EFT-PAR-02"), "parity-create-key-0002");
            db.ChangeTracker.Clear(); notices.Items.Clear();
            await subscriptions.ReviewBankTransferPaymentRequestAsync(second!.Id, new ReviewBankTransferPaymentRequest("Rejected", "Banka hareketi bulunamadı"), admin, "parity-review-key-0002");
            var rejected = Assert.Single(notices.Items);
            Assert.Equal((SpeedReadingSubscriptionNoticeKinds.TransferRejected, (Guid?)buyer, "Banka hareketi bulunamadı"), (rejected.Kind, rejected.UserId, rejected.Detail));
            Assert.EndsWith("@example.invalid", rejected.Email);
            db.ChangeTracker.Clear(); notices.Items.Clear();

            // An approval tells the payer until when the access runs.
            var approved = await subscriptions.ReviewBankTransferPaymentRequestAsync(first!.Id, new ReviewBankTransferPaymentRequest("Approved", null), admin, "parity-review-key-0001");
            var approval = Assert.Single(notices.Items);
            Assert.Equal((SpeedReadingSubscriptionNoticeKinds.TransferApproved, (Guid?)buyer, "Yıllık plan"), (approval.Kind, approval.UserId, approval.PlanName));
            Assert.InRange(approval.EndsAt!.Value, DateTime.UtcNow.AddDays(364), DateTime.UtcNow.AddDays(366));
            Assert.Null(approved!.InvoiceNumber);
            db.ChangeTracker.Clear(); notices.Items.Clear();

            // The invoice: a real PDF, a sane number and date, once per payment and once per number.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            foreach (var (number, issuedOn, file, content) in new (string, DateOnly, string, Stream)[]
                     {
                         ("x", today, "fatura.pdf", Pdf("a")), ("ONL 2026", today, "fatura.pdf", Pdf("a")),
                         ("ONL2026000001", today.AddDays(3), "fatura.pdf", Pdf("a")), ("ONL2026000001", today, "fatura.docx", Pdf("a")),
                         ("ONL2026000001", today, "fatura.pdf", new MemoryStream(Encoding.ASCII.GetBytes("<html>not a pdf</html>")))
                     })
                Assert.Equal("Sales.InvoiceInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    sales.AttachInvoiceAsync(admin, first.Id, number, issuedOn, file, content))).Code);
            Assert.Null(await sales.AttachInvoiceAsync(admin, Guid.NewGuid(), "ONL2026000001", today, "fatura.pdf", Pdf("a")));
            db.ChangeTracker.Clear();
            var invoiced = await sales.AttachInvoiceAsync(admin, first.Id, " onl2026000001 ", today, "C:\\faturalar\\fatura.pdf", Pdf("first"));
            Assert.Equal(("ONL2026000001", (DateOnly?)today), (invoiced!.InvoiceNumber, invoiced.InvoiceIssuedOn));
            db.ChangeTracker.Clear();
            Assert.Equal("Sales.InvoiceExists", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sales.AttachInvoiceAsync(admin, first.Id, "ONL2026000002", today, "fatura.pdf", Pdf("b")))).Code);
            db.ChangeTracker.Clear();
            Assert.Equal("ONL2026000001", Assert.Single(await subscriptions.GetMyBankTransferPaymentRequestsAsync(buyer), item => item.Id == first.Id).InvoiceNumber);

            // Only the buyer (or an admin) opens it.
            var mine = await sales.OpenInvoiceAsync(buyer, first.Id, asAdmin: false);
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(mine!.Content));
            Assert.EndsWith("first", Encoding.ASCII.GetString(mine.Content));
            Assert.False(mine.FileName.Contains('\\'));
            Assert.Null(await sales.OpenInvoiceAsync(stranger, first.Id, asAdmin: false));
            Assert.NotNull(await sales.OpenInvoiceAsync(admin, first.Id, asAdmin: true));
            Assert.Null(await sales.OpenInvoiceAsync(buyer, second.Id, asAdmin: false));

            // Removing frees the payment and the number.
            Assert.Null((await sales.RemoveInvoiceAsync(first.Id))!.InvoiceNumber);
            db.ChangeTracker.Clear();
            Assert.Null(await sales.RemoveInvoiceAsync(first.Id));
            Assert.Null(await sales.OpenInvoiceAsync(buyer, first.Id, asAdmin: false));
            Assert.Equal("ONL2026000001", (await sales.AttachInvoiceAsync(admin, first.Id, "ONL2026000001", today, "fatura.pdf", Pdf("again")))!.InvoiceNumber);
            db.ChangeTracker.Clear(); notices.Items.Clear();

            // Withdrawal: the request goes to the admin, the answer to the payer.
            await sales.RequestWithdrawalAsync(buyer, first.Id, "Vazgeçtim");
            var asked = Assert.Single(notices.Items);
            Assert.Equal((SpeedReadingSubscriptionNoticeKinds.WithdrawalRequested, (Guid?)null, "EFT-PAR-01"), (asked.Kind, asked.UserId, asked.Detail));
            db.ChangeTracker.Clear(); notices.Items.Clear();
            await sales.ReviewWithdrawalAsync(first.Id, new(false, "Süre doldu"), admin);
            var refused = Assert.Single(notices.Items);
            Assert.Equal((SpeedReadingSubscriptionNoticeKinds.WithdrawalRejected, (Guid?)buyer, "Süre doldu"), (refused.Kind, refused.UserId, refused.Detail));
            db.ChangeTracker.Clear(); notices.Items.Clear();
            await sales.RequestWithdrawalAsync(buyer, first.Id, null);
            db.ChangeTracker.Clear(); notices.Items.Clear();
            await sales.ReviewWithdrawalAsync(first.Id, new(true, null), admin);
            var accepted = Assert.Single(notices.Items);
            Assert.Equal((SpeedReadingSubscriptionNoticeKinds.WithdrawalApproved, (Guid?)buyer), (accepted.Kind, accepted.UserId));
            Assert.Null(accepted.Detail);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task Subscribers_are_reminded_once_before_and_after_the_end_and_only_while_a_renewal_can_be_bought()
    {
        await using var db = await CreateAsync();
        try
        {
            var (plan, product) = await SeedAsync(db, sellerComplete: false);
            var now = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
            var clock = new Clock(now);
            var week = Guid.NewGuid(); var day = Guid.NewGuid(); var lapsed = Guid.NewGuid(); var far = Guid.NewGuid();
            var covered = Guid.NewGuid(); var licensed = Guid.NewGuid(); var longAgo = Guid.NewGuid(); var cancelled = Guid.NewGuid();
            Subscription(week, now.AddDays(5)); Subscription(day, now.AddHours(20)); Subscription(lapsed, now.AddDays(-2));
            Subscription(far, now.AddDays(30)); Subscription(longAgo, now.AddDays(-10)); Subscription(cancelled, now.AddDays(3), status: "Cancelled");
            Subscription(covered, now.AddDays(2)); Subscription(covered, now.AddDays(200));
            Subscription(licensed, now.AddDays(2), license: Guid.NewGuid());
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var notices = new RecordingNotices();
            var lifecycle = (ISpeedReadingSubscriptionLifecycle)Activator.CreateInstance(
                Infrastructure.GetType("SpeedReading.Infrastructure.Legacy.SpeedReadingSubscriptionLifecycle")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db, notices, clock], null)!;

            // Hızlı Okuma is not on sale yet (the seller details are missing): nobody is told to renew.
            Assert.Equal(0, await lifecycle.ProcessAsync());
            Assert.Empty(notices.Items);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE speed_reading.bank_transfer_payment_settings SET \"SellerTitle\"='ONAL', \"SellerAddress\"='Yozgat', \"SellerTaxOffice\"='YOZGAT', \"SellerTaxNumber\"='6421076848', \"SellerEmail\"='s@example.invalid', \"SellerPhone\"='0505'");

            Assert.Equal(3, await lifecycle.ProcessAsync());
            var kinds = notices.Items.ToDictionary(item => item.UserId!.Value, item => item.Kind);
            Assert.Equal(SpeedReadingSubscriptionNoticeKinds.Expiring, kinds[week]);
            Assert.Equal(SpeedReadingSubscriptionNoticeKinds.Expiring, kinds[day]);
            Assert.Equal(SpeedReadingSubscriptionNoticeKinds.Expired, kinds[lapsed]);
            Assert.All(notices.Items, item =>
            {
                Assert.Equal("Yıllık plan", item.PlanName);
                Assert.NotNull(item.EndsAt);
                Assert.NotNull(item.NoticeId);
                Assert.EndsWith("@example.invalid", item.Email);
            });
            db.ChangeTracker.Clear(); notices.Items.Clear();

            // The same hour again: nothing twice.
            Assert.Equal(0, await lifecycle.ProcessAsync());
            db.ChangeTracker.Clear();

            // Five days later the 7-day subscriber is inside the last day and the one-day subscriber has lapsed.
            clock.Now = now.AddDays(4).AddHours(10);
            Assert.Equal(2, await lifecycle.ProcessAsync());
            Assert.Equal(SpeedReadingSubscriptionNoticeKinds.Expiring, notices.Items.Single(item => item.UserId == week).Kind);
            Assert.Equal(SpeedReadingSubscriptionNoticeKinds.Expired, notices.Items.Single(item => item.UserId == day).Kind);
            Assert.Equal(5, await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM speed_reading.subscription_notice_receipts").SingleAsync());

            void Subscription(Guid user, DateTimeOffset end, string status = "Active", Guid? license = null) =>
                Add(db, "LegacyUserSubscription", new
                {
                    Id = Guid.NewGuid(), UserId = user, UserEmail = $"{user:N}@example.invalid", PlanId = plan, ProductId = product, Status = status,
                    StartDate = end.UtcDateTime.AddDays(-365), EndDate = (DateTime?)end.UtcDateTime, InstitutionAccessLicenseId = license,
                    CreatedBy = user, CreatedAt = now.UtcDateTime.AddDays(-365)
                });
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task A_notice_becomes_an_in_app_notification_for_the_payer_and_an_e_mail_message_for_the_notification_service()
    {
        await using var db = await CreateAsync();
        try
        {
            var published = new List<object>();
            var publisher = DispatchProxy.Create<IPublishEndpoint, RecordingPublisher>();
            ((RecordingPublisher)(object)publisher).Published = published;
            var notices = (ISpeedReadingSubscriptionNotices)Activator.CreateInstance(
                Infrastructure.GetType("SpeedReading.Infrastructure.Legacy.SpeedReadingSubscriptionNotices")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db, publisher], null)!;
            var payer = Guid.NewGuid();
            var at = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

            await notices.StageAsync(new(payer, " veli@example.invalid ", SpeedReadingSubscriptionNoticeKinds.TransferApproved, "Yıllık plan",
                new DateTime(2027, 11, 20, 21, 30, 0, DateTimeKind.Utc), null, at));
            await notices.StageAsync(new(payer, "veli@example.invalid", SpeedReadingSubscriptionNoticeKinds.TransferSubmitted, "Yıllık plan", null, "EFT-1", at));
            await db.SaveChangesAsync();

            var row = (await db.Database.SqlQuery<string>(
                $"SELECT user_id::text || '|' || title || '|' || action_url || '|' || message AS \"Value\" FROM speed_reading.notifications").SingleAsync()).Split('|');
            Assert.Equal((payer.ToString(), "Ödemeniz onaylandı", "/student/subscription"), (row[0], row[1], row[2]));
            // 21:30 UTC is 00:30 the next day in Turkey.
            Assert.Contains("21.11.2027 00:30", row[3]);

            var messages = published.Cast<SpeedReadingSubscriptionNoticeV1>().ToArray();
            Assert.Equal(2, messages.Length);
            Assert.Equal(((Guid?)payer, "veli@example.invalid"), (messages[0].RecipientUserId, messages[0].RecipientEmail));
            // The admin notice names nobody: the notification service sends it to the configured alert address.
            Assert.Equal(((Guid?)null, (string?)null, "EFT-1"), (messages[1].RecipientUserId, messages[1].RecipientEmail, messages[1].Detail));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private async Task<OwnedSpeedReadingDbContext> CreateAsync()
    {
        var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<(Guid Plan, Guid Product)> SeedAsync(OwnedSpeedReadingDbContext db, bool sellerComplete)
    {
        var product = Guid.NewGuid(); var plan = Guid.NewGuid(); var now = DateTime.UtcNow;
        Add(db, "LegacyProduct", new { Id = product, Slug = "hizliokuma", Name = "Hızlı Okuma", IsActive = true, IsPublic = true, CreatedAt = now, UpdatedAt = now });
        Add(db, "LegacySubscriptionPlan", new { Id = plan, ProductId = product, Slug = "annual", Name = "Yıllık plan", Price = 499m, IsActive = true, IsPublic = true, BillingPeriod = "Annual", DurationDays = (int?)365, CreatedAt = now });
        Add(db, "LegacyBankTransferPaymentSettings", sellerComplete
            ? new { Id = SettingsId, AccountHolder = "Test", BankName = "Bank", Iban = "TR330006100519786457841326", IsEnabled = true, CreatedAt = now,
                SellerTitle = (string?)"ONAL LTD", SellerAddress = (string?)"Yozgat", SellerTaxOffice = (string?)"YOZGAT", SellerTaxNumber = (string?)"6421076848", SellerEmail = (string?)"satici@example.invalid", SellerPhone = (string?)"0505 000 00 00" }
            : new { Id = SettingsId, AccountHolder = "Test", BankName = "Bank", Iban = "TR330006100519786457841326", IsEnabled = true, CreatedAt = now,
                SellerTitle = (string?)null, SellerAddress = (string?)null, SellerTaxOffice = (string?)null, SellerTaxNumber = (string?)null, SellerEmail = (string?)null, SellerPhone = (string?)null });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (plan, product);
    }

    private static void Add(OwnedSpeedReadingDbContext db, string name, object values)
    {
        var type = Infrastructure.GetType($"SpeedReading.Infrastructure.Legacy.{name}")!;
        var entity = Activator.CreateInstance(type)!;
        foreach (var property in values.GetType().GetProperties())
            type.GetProperty(property.Name)!.SetValue(entity, property.GetValue(values));
        db.Add(entity);
    }

    private static MemoryStream Pdf(string tail) => new(Encoding.ASCII.GetBytes("%PDF-1.7\n" + tail));

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingNotices : ISpeedReadingSubscriptionNotices
    {
        public List<SpeedReadingSubscriptionNotice> Items { get; } = [];
        public Task StageAsync(SpeedReadingSubscriptionNotice notice, CancellationToken cancellationToken = default)
        {
            Items.Add(notice);
            return Task.CompletedTask;
        }
    }

    public class RecordingPublisher : DispatchProxy
    {
        public List<object> Published { get; set; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IPublishEndpoint.Publish) && args is { Length: > 0 } && args[0] is not null) Published.Add(args[0]!);
            return Task.CompletedTask;
        }
    }

    private sealed class Directory : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse(userIds.Select(id => new SpeedReadingUserDirectoryItem(id, "Test", "Veli", true) { Email = $"{id:N}@example.invalid" }).ToArray()));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
