using System.Security.Cryptography;
using Coaching.Application.Attachments;
using Coaching.Application.Privacy;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Coaching.Curriculum.IntegrationTests;

// Package 6 of claude/coaching-fix-plan.md; retention period five years since the coaching audit of 31.10.2026.
public sealed class CoachingPrivacyRetentionPostgresTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ErasureKeepsFinancialRecordsForFiveYearsThenRemovesOnlyThePersonalFields()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); await using var files = new Files();
        var clock = new Clock(Start); var subject = Guid.NewGuid(); var other = Guid.NewGuid();
        var planId = SeedPlan(pg.Db);
        var mine = SeedFinance(pg.Db, planId, subject, "Ayşe Yılmaz", "ayse@example.test");
        var theirs = SeedFinance(pg.Db, planId, other, "Başka Kişi", "baska@example.test");
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();

        var requestId = Guid.NewGuid();
        var assessment = await new CoachingErasureAssessmentService(pg.Db, clock)
            .AssessAsync(new(Guid.NewGuid(), requestId, subject, Start, DryRun: true), default);
        Assert.Equal(4, assessment.RetainedFinancialRecordCount);
        pg.Db.ChangeTracker.Clear();
        var execution = await new CoachingErasureExecutionService(pg.Db, files.Storage, clock)
            .ExecuteAsync(new(Guid.NewGuid(), requestId, subject, Start, PersonalDataScope.Account), default);
        Assert.Equal(4, execution.RetainedFinancialRecordCount);
        Assert.Equal<DateTime?>(Start.AddYears(5), execution.FinancialRetentionUntil);

        // During the retention period everything stays as it was; only the institution seat is released.
        pg.Db.ChangeTracker.Clear();
        var keptTransfer = await pg.Db.CoachingBankTransferRequests.SingleAsync(x => x.Id == mine.Transfer);
        Assert.Equal<(string, string, string?, string?)>(("Ayşe Yılmaz", "ayse@example.test", "Ayşe Yılmaz (veli)", "Açıklama"),
            (keptTransfer.UserName, keptTransfer.UserEmail, keptTransfer.PayerName, keptTransfer.Note));
        Assert.Equal("ayse@example.test", (await pg.Db.CoachingSubscriptions.SingleAsync(x => x.Id == mine.Subscription)).UserEmail);
        var seat = await pg.Db.CoachingSubscriptionSeats.SingleAsync(x => x.Id == mine.Seat);
        Assert.True(seat.IsSuspended); Assert.Equal(subject, seat.StudentId);

        clock.Now = Start.AddYears(5).AddMinutes(-1);
        Assert.Equal(0, await Processor(pg.Db, clock).AnonymizeDueAsync(default));
        clock.Now = Start.AddYears(5).AddMinutes(1);
        Assert.Equal(1, await Processor(pg.Db, clock).AnonymizeDueAsync(default));
        Assert.Equal(0, await Processor(pg.Db, clock).AnonymizeDueAsync(default));

        pg.Db.ChangeTracker.Clear();
        Assert.False(await pg.Db.CoachingSubscriptions.AnyAsync(x => x.UserId == subject));
        Assert.False(await pg.Db.CoachingSubscriptionSeats.AnyAsync(x => x.StudentId == subject));
        Assert.False(await pg.Db.CoachingBankTransferRequests.AnyAsync(x => x.UserId == subject));
        Assert.False(await pg.Db.CoachingPaymentRecords.AnyAsync(x => x.UserId == subject));
        var subscription = await pg.Db.CoachingSubscriptions.SingleAsync(x => x.Id == mine.Subscription);
        Assert.Equal<(string?, string?, string?, string?)>((null, null, null, "EFT-Ayşe Yılmaz"), (subscription.UserName, subscription.UserEmail, subscription.Notes, subscription.PaymentReference));
        var transfer = await pg.Db.CoachingBankTransferRequests.SingleAsync(x => x.Id == mine.Transfer);
        Assert.Equal<(string, string, string?, string?, string?)>((CoachingFinancialRetention.AnonymizedName, "", null, null, null),
            (transfer.UserName, transfer.UserEmail, transfer.PayerName, transfer.Note, transfer.ReviewNote));
        Assert.Equal((250m, "EFT-Ayşe Yılmaz", "Approved"), (transfer.Amount, transfer.PaymentReference, transfer.Status));
        var payment = await pg.Db.CoachingPaymentRecords.SingleAsync(x => x.Id == mine.Payment);
        Assert.Equal<(string, string, decimal, string?)>((CoachingFinancialRetention.AnonymizedName, "", 250m, "EFT-Ayşe Yılmaz"),
            (payment.UserName, payment.UserEmail, payment.Amount, payment.Reference));
        Assert.Equal(subscription.UserId, transfer.UserId);
        Assert.NotEqual(execution.Id, transfer.UserId);

        var untouched = await pg.Db.CoachingBankTransferRequests.SingleAsync(x => x.Id == theirs.Transfer);
        Assert.Equal<(Guid, string, string?)>((other, "baska@example.test", "Başka Kişi (veli)"), (untouched.UserId, untouched.UserEmail, untouched.PayerName));
        var done = await pg.Db.CoachingErasureExecutions.SingleAsync();
        Assert.Null(done.SubjectUserId); Assert.Equal<DateTime?>(clock.Now.UtcDateTime, done.FinancialAnonymizedAt);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ALegalHoldPlacedDuringRetentionPostponesAnonymization()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); await using var files = new Files();
        var clock = new Clock(Start); var subject = Guid.NewGuid();
        var mine = SeedFinance(pg.Db, SeedPlan(pg.Db), subject, "Ayşe Yılmaz", "ayse@example.test");
        await pg.Db.SaveChangesAsync();
        await Erase(pg.Db, files, clock, subject);

        var hold = CoachingLegalHold.Place(subject, "Vergi incelemesi", Guid.NewGuid(), Start.AddMonths(6));
        pg.Db.Add(hold); await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        clock.Now = Start.AddYears(5).AddDays(1);
        Assert.Equal(0, await Processor(pg.Db, clock).AnonymizeDueAsync(default));
        Assert.Equal("ayse@example.test", (await pg.Db.CoachingPaymentRecords.SingleAsync(x => x.Id == mine.Payment)).UserEmail);

        var stored = await pg.Db.CoachingLegalHolds.SingleAsync();
        stored.Release(Guid.NewGuid(), Start.AddYears(5).AddDays(2)); await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        clock.Now = Start.AddYears(5).AddDays(3);
        Assert.Equal(1, await Processor(pg.Db, clock).AnonymizeDueAsync(default));
        pg.Db.ChangeTracker.Clear();
        Assert.Equal("", (await pg.Db.CoachingPaymentRecords.SingleAsync(x => x.Id == mine.Payment)).UserEmail);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task BlockedErasureSaysWhyAndChangesNothing()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); await using var files = new Files();
        var clock = new Clock(Start); var subject = Guid.NewGuid();
        pg.Db.Add(AcademicGoal.Create(subject, "Hedef", Coaching.Domain.Enums.GoalCategory.Other)); await pg.Db.SaveChangesAsync();

        var missing = await Assert.ThrowsAsync<CoachingErasureBlockedException>(() =>
            new CoachingErasureExecutionService(pg.Db, files.Storage, clock)
                .ExecuteAsync(new(Guid.NewGuid(), Guid.NewGuid(), subject, Start, PersonalDataScope.Coaching), default));
        Assert.Equal(CoachingErasureBlockedException.AssessmentMissing, missing.ReasonCode);

        pg.Db.Add(CoachingLegalHold.Place(subject, "Dava", Guid.NewGuid(), Start.AddDays(-1))); await pg.Db.SaveChangesAsync();
        var requestId = Guid.NewGuid();
        await new CoachingErasureAssessmentService(pg.Db, clock).AssessAsync(new(Guid.NewGuid(), requestId, subject, Start, DryRun: true), default);
        pg.Db.ChangeTracker.Clear();
        var held = await Assert.ThrowsAsync<CoachingErasureBlockedException>(() =>
            new CoachingErasureExecutionService(pg.Db, files.Storage, clock)
                .ExecuteAsync(new(Guid.NewGuid(), requestId, subject, Start, PersonalDataScope.Coaching), default));
        Assert.Equal(CoachingErasureBlockedException.LegalHold, held.ReasonCode);
        pg.Db.ChangeTracker.Clear();
        Assert.Equal(1, await pg.Db.AcademicGoals.CountAsync());
        Assert.Empty(await pg.Db.CoachingErasureExecutions.ToArrayAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task FilesAreOnlyDeletedAfterTheErasureHasCommitted()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); await using var files = new Files();
        var clock = new Clock(Start); var subject = Guid.NewGuid();
        var key = await SeedSubmissionFile(pg.Db, files, subject);
        var requestId = Guid.NewGuid();
        await new CoachingErasureAssessmentService(pg.Db, clock).AssessAsync(new(Guid.NewGuid(), requestId, subject, Start, DryRun: true), default);
        pg.Db.ChangeTracker.Clear();
        var message = new PersonalDataErasureExecutionRequestedV1(Guid.NewGuid(), requestId, subject, Start, PersonalDataScope.Coaching);
        var outbox = new CapturingPublisher();

        // The consumer outbox owns the transaction (inside the retry strategy, as the bus does); here it fails to commit.
        await pg.Db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await pg.Db.Database.BeginTransactionAsync();
            await new CoachingErasureExecutionService(pg.Db, files.Storage, clock, outbox).ExecuteAsync(message, default);
            await transaction.RollbackAsync();
        });
        pg.Db.ChangeTracker.Clear();
        Assert.True(File.Exists(files.PathOf(key)));
        Assert.Equal(1, await pg.Db.AssignmentSubmissionAttachments.CountAsync());

        // Committed: the file goes with the outbox message, which is only delivered after the commit.
        outbox.Messages.Clear();
        await pg.Db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await pg.Db.Database.BeginTransactionAsync();
            await new CoachingErasureExecutionService(pg.Db, files.Storage, clock, outbox).ExecuteAsync(message, default);
            await transaction.CommitAsync();
        });
        pg.Db.ChangeTracker.Clear();
        Assert.Equal(0, await pg.Db.AssignmentSubmissionAttachments.CountAsync());
        Assert.True(File.Exists(files.PathOf(key)));
        Assert.Equal(key, Assert.IsType<AssignmentAttachmentDeletionRequested>(Assert.Single(outbox.Messages)).StorageKey);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task WithoutTheOutboxTheServiceDeletesFilesAfterItsOwnCommit()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); await using var files = new Files();
        var clock = new Clock(Start); var subject = Guid.NewGuid();
        var key = await SeedSubmissionFile(pg.Db, files, subject);
        await Erase(pg.Db, files, clock, subject);
        Assert.False(File.Exists(files.PathOf(key)));
        Assert.Equal(0, await pg.Db.AssignmentSubmissionAttachments.CountAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ExportContainsTheUsersOwnSubscriptionsTransfersAndPayments()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var subject = Guid.NewGuid(); var planId = SeedPlan(pg.Db);
        var mine = SeedFinance(pg.Db, planId, subject, "Ayşe Yılmaz", "ayse@example.test");
        SeedFinance(pg.Db, planId, Guid.NewGuid(), "Başka Kişi", "baska@example.test");
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();

        var export = await new CoachingDataExportRepository(pg.Db).ExportStudentDataAsync(subject, Start, default);

        Assert.Equal("1.10", export.SchemaVersion);
        var subscription = Assert.Single(export.Subscriptions!);
        Assert.Equal<(Guid, string?)>((mine.Subscription, "Bireysel aylık"), (subscription.Id, subscription.PlanName));
        Assert.Equal(mine.Seat, Assert.Single(export.SubscriptionSeats!).Id);
        var transfer = Assert.Single(export.BankTransfers!);
        Assert.Equal<(decimal, string?, string?)>((250m, "Ayşe Yılmaz (veli)", "Açıklama"), (transfer.Amount, transfer.PayerName, transfer.Note));
        Assert.Equal(mine.Payment, Assert.Single(export.Payments!).Id);
    }

    private static CoachingFinancialRetentionProcessor Processor(CoachingDbContext db, TimeProvider clock) =>
        new(db, clock, NullLogger<CoachingFinancialRetentionProcessor>.Instance);

    private static async Task Erase(CoachingDbContext db, Files files, TimeProvider clock, Guid subject)
    {
        var requestId = Guid.NewGuid();
        await new CoachingErasureAssessmentService(db, clock).AssessAsync(new(Guid.NewGuid(), requestId, subject, Start, DryRun: true), default);
        db.ChangeTracker.Clear();
        await new CoachingErasureExecutionService(db, files.Storage, clock)
            .ExecuteAsync(new(Guid.NewGuid(), requestId, subject, Start, PersonalDataScope.Account), default);
        db.ChangeTracker.Clear();
    }

    private static Guid SeedPlan(CoachingDbContext db)
    {
        var plan = new CoachingSubscriptionPlan { Slug = "bireysel-" + Guid.NewGuid().ToString("N")[..8], Name = "Bireysel aylık", Price = 250, DurationDays = 30, CreatedBy = Guid.NewGuid() };
        db.Add(plan);
        return plan.Id;
    }

    private static (Guid Subscription, Guid Seat, Guid Transfer, Guid Payment) SeedFinance(CoachingDbContext db, Guid planId, Guid user, string name, string email)
    {
        var institution = new CoachingSubscription { PlanId = planId, InstitutionId = Guid.NewGuid(), StartDate = Start.AddMonths(-1), EndDate = Start.AddMonths(11), CreatedBy = Guid.NewGuid() };
        var own = new CoachingSubscription
        {
            PlanId = planId, UserId = user, UserName = name, UserEmail = email, StartDate = Start.AddDays(-10), EndDate = Start.AddDays(20),
            Notes = "Yönetici notu", PaymentReference = "EFT-" + name, CreatedBy = user
        };
        var transfer = new CoachingBankTransferRequest
        {
            UserId = user, UserName = name, UserEmail = email, PlanId = planId, Amount = 250, PaymentReference = "EFT-" + name,
            PayerName = name + " (veli)", Note = "Açıklama", Status = "Approved", SubscriptionId = own.Id, ReviewNote = "Onaylandı"
        };
        own.BankTransferRequestId = transfer.Id;
        var payment = new CoachingPaymentRecord
        {
            UserId = user, UserName = name, UserEmail = email, PlanId = planId, SubscriptionId = own.Id, BankTransferRequestId = transfer.Id,
            PlanName = "Bireysel aylık", Amount = 250, Reference = "EFT-" + name
        };
        var seat = new CoachingSubscriptionSeat { SubscriptionId = institution.Id, StudentId = user, AssignedBy = Guid.NewGuid() };
        db.AddRange(institution, own, transfer, payment, seat);
        return (own.Id, seat.Id, transfer.Id, payment.Id);
    }

    private static async Task<string> SeedSubmissionFile(CoachingDbContext db, Files files, Guid student)
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var assignment = Assignment.Create(Guid.NewGuid(), "Dosyalı ödev", Start.AddDays(3));
        assignment.AssignToStudent(student);
        var key = $"assignments/{assignment.Id:N}/{Guid.NewGuid():N}";
        await using (var content = new MemoryStream(bytes)) await files.Storage.StoreAsync(key, content, "image/png", bytes.Length, hash);
        assignment.AssignedStudents.Single().AddSubmissionAttachment(key, "cevap.png", "image/png", bytes.Length, hash);
        db.Add(assignment); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        return key;
    }

    private sealed class CapturingPublisher : Coaching.Application.Interfaces.ICoachingEventPublisher
    {
        public List<object> Messages { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Files : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "coaching-erasure-" + Guid.NewGuid().ToString("N"));
        public LocalAssignmentAttachmentStorage Storage { get; }
        public Files() { Directory.CreateDirectory(Root); Storage = new(Options.Create(new AssignmentAttachmentOptions { RootPath = Root })); }
        public string PathOf(string key) => Path.Combine(Root, key);
        public ValueTask DisposeAsync() { Directory.Delete(Root, true); return ValueTask.CompletedTask; }
    }
}
