using System.Text;
using Coaching.Application.Attachments;
using Coaching.Application.Interfaces;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap O10: trials, coupons, sales documents, withdrawals and invoices on PostgreSQL with the migration's constraints.
public sealed class CoachingSalesPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task SalesToolsRunOnPostgres()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var root = Path.Combine(Path.GetTempPath(), "coaching-sales-pg-" + Guid.NewGuid().ToString("N"));
        try
        {
            var admin = Guid.NewGuid(); var user = Guid.NewGuid(); var other = Guid.NewGuid();
            CoachingSubscriptionService Subscriptions() => new(f.Db, new Identity());
            CoachingSalesService Sales() => new(f.Db, Subscriptions(),
                new LocalAssignmentAttachmentStorage(Options.Create(new AssignmentAttachmentOptions { RootPath = root })), new DevelopmentAttachmentScanner(), TimeProvider.System);
            await Subscriptions().UpdateSettingsAsync(new(false, "TRY", "Edu İvme", "Örnek Banka", "TR33 0006 1005 1978 6457 8413 26", null, true), admin, "settings-sales-pg-0001");
            var plan = (await Subscriptions().CreatePlanAsync(new("pg-yearly", "Bireysel yıllık", "Koçluk", "Individual", 1200m, false, "Annual", 365, null,
                ["Raporlar"], true, true, 0, TrialDays: 7), admin))!.Value;
            await Sales().UpdateSellerAsync(admin, new("Edu İvme Ltd. Şti.", "Kadıköy/İstanbul", "Kadıköy", "1234567890", null, "destek@example.test", "0216"), default);

            var trial = await Sales().StartTrialAsync(other, "Can", null, "Student", plan, default);
            Assert.Equal("Trial", (await f.Db.CoachingSubscriptionPeriods.SingleAsync(x => x.SubscriptionId == trial.SubscriptionId)).Source);

            await Sales().CreateCouponAsync(admin, new("PG20", "Percent", 20m, null, DateTime.UtcNow.AddDays(10), 5, [plan], true), default);
            Assert.Equal(960m, (await Sales().QuoteAsync(user, "pg20", plan, default)).Amount);
            var transfer = await Subscriptions().CreateBankTransferRequestAsync(user, "Ada", "ada@example.test",
                new CoachingBankTransferRequestCreate(plan, "EFT-PG-SALES-1", "Ada", null, true, "PG20", true), "create-sales-pg-0001");
            Assert.Equal((960m, "PG20"), (transfer!.Amount, transfer.CouponCode));
            await Subscriptions().ReviewBankTransferRequestAsync(transfer.Id, new("Approved", null), admin, "review-sales-pg-0001");
            f.Db.ChangeTracker.Clear();
            Assert.Equal(1, (await Sales().ListCouponsAsync(default)).Single().UsedCount);
            Assert.Contains("Edu İvme Ltd. Şti.", (await Sales().GetTransferDocumentsAsync(user, transfer.Id, default)).DistanceSales);

            var payment = await f.Db.CoachingPaymentRecords.SingleAsync();
            await Sales().AttachInvoiceAsync(admin, payment.Id, "PG2026000001", new DateOnly(2026, 10, 28), "fatura.pdf", new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4 pg")), default);
            f.Db.ChangeTracker.Clear();
            var mine = Assert.Single(await Sales().ListMyPaymentsAsync(user, default));
            Assert.Equal(("PG2026000001", 960m), (mine.InvoiceNumber, mine.Amount));
            Assert.NotNull(mine.WithdrawUntil);

            await Sales().RequestWithdrawalAsync(user, transfer.Id, "Vazgeçtim", default);
            Assert.Single(await Sales().ListWithdrawalsAsync(default));
            await Sales().ReviewWithdrawalAsync(admin, transfer.Id, true, "İade edildi", default);
            f.Db.ChangeTracker.Clear();
            Assert.Equal(("Cancelled", "Approved"), await f.Db.CoachingBankTransferRequests.Select(x => new ValueTuple<string, string?>(x.Status, x.WithdrawalStatus)).SingleAsync());
            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                f.Db.Add(new CoachingSubscriptionTrial { UserId = other, Audience = "Individual", PlanId = plan, SubscriptionId = Guid.NewGuid(), StartedAt = DateTime.UtcNow, EndsAt = DateTime.UtcNow });
                await f.Db.SaveChangesAsync();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class Identity : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(new(true, null));
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? institution, bool admin, CancellationToken ct) => Task.FromResult(institution);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) => Task.FromResult(studentIds);
    }
}
