using System.Security.Claims;
using System.Text;
using Coaching.Application.Attachments;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.ExportCoachingData;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Coaching.Curriculum.IntegrationTests;

// Package K1 (coaching audit 31.10.2026): money, subscriptions and KVKK on PostgreSQL with the production retry strategy.
public sealed class CoachingMoneyFixesPostgresTests
{
    private static readonly Guid Admin = Guid.NewGuid();

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CancellingAnApprovedTransferRunsUnderTheRetryStrategy()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var plan = await Setup(f.Db, "Individual");
        var user = Guid.NewGuid();
        var transfer = await Approved(f.Db, plan, user, "EFT-K1-CANCEL-01");

        var cancelled = await Service(f.Db).CancelApprovedBankTransferAsync(transfer, "Banka iadesi yapıldı", Admin);

        Assert.Equal("Cancelled", cancelled!.Status);
        f.Db.ChangeTracker.Clear();
        Assert.Equal("Cancelled", (await f.Db.CoachingSubscriptions.SingleAsync(x => x.UserId == user)).Status);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ApprovingAWithdrawalCancelsThePaymentAndClosesTheRequestTogether()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(); using var files = new Files();
        var plan = await Setup(f.Db, "Individual");
        var user = Guid.NewGuid();
        var first = await Approved(f.Db, plan, user, "EFT-K1-CAYMA-01");
        await Sales(f.Db, files).RequestWithdrawalAsync(user, first, "Vazgeçtim", default);

        await Sales(f.Db, files).ReviewWithdrawalAsync(Admin, first, true, "İade edildi", default);

        f.Db.ChangeTracker.Clear();
        var stored = await f.Db.CoachingBankTransferRequests.SingleAsync(x => x.Id == first);
        Assert.Equal(("Cancelled", "Approved", "İade edildi"), (stored.Status, stored.WithdrawalStatus, stored.WithdrawalReviewNote));

        // Cancelling the payment from the other admin screen also settles an open withdrawal request.
        var other = Guid.NewGuid();
        var second = await Approved(f.Db, plan, other, "EFT-K1-CAYMA-02");
        await Sales(f.Db, files).RequestWithdrawalAsync(other, second, "Fikrim değişti", default);
        await Service(f.Db).CancelApprovedBankTransferAsync(second, "Cayma talebi üzerine iade edildi", Admin);
        f.Db.ChangeTracker.Clear();
        var settled = await f.Db.CoachingBankTransferRequests.SingleAsync(x => x.Id == second);
        Assert.Equal(("Cancelled", "Approved"), (settled.Status, settled.WithdrawalStatus));
        Assert.NotNull(settled.WithdrawalReviewedAt);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AReceiptCanBeSentAgainAfterItsNoticeWasWithdrawnOrRejected()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var plan = await Setup(f.Db, "Individual");
        var user = Guid.NewGuid();
        var first = await Create(f.Db, plan, user, "EFT-K1-RESEND-1", "resend-0001");
        await Service(f.Db).WithdrawMyBankTransferRequestAsync(user, first.Id);

        var second = await Create(f.Db, plan, user, "EFT-K1-RESEND-1", "resend-0002");
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("Pending", second.Status);
        await Service(f.Db).ReviewBankTransferRequestAsync(second.Id, new("Rejected", "Dekont okunmuyor"), Admin, "review-resend-02");

        var third = await Create(f.Db, plan, user, "EFT-K1-RESEND-1", "resend-0003");
        Assert.NotEqual(second.Id, third.Id);
        Assert.Equal("Pending", third.Status);
        // A notice still waiting with the same receipt is the same notice.
        Assert.Equal(third.Id, (await Create(f.Db, plan, user, "EFT-K1-RESEND-1", "resend-0004")).Id);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(3, await f.Db.CoachingBankTransferRequests.CountAsync(x => x.UserId == user));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheServerRequiresTheContractAndRejectsASecondNoticeForAPlanStillUnderReview()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var plan = await Setup(f.Db, "Individual");
        var user = Guid.NewGuid();
        var withoutContract = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(f.Db).CreateBankTransferRequestAsync(user, "Ada", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-K1-NOCONTRACT", "Ada", null, true), "k1-contract-0001"));
        Assert.Equal("Sales.ContractRequired", withoutContract.Code);

        await Create(f.Db, plan, user, "EFT-K1-PENDING-1", "pending-0001");
        var second = await Assert.ThrowsAsync<BusinessRuleException>(() => Create(f.Db, plan, user, "EFT-K1-PENDING-2", "pending-0002"));
        Assert.Equal("Subscription.TransferPending", second.Code);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ACouponCannotBeUsedMoreOftenThanItsLimitAndACancelledPurchaseGivesTheUseBack()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(); using var files = new Files();
        var plan = await Setup(f.Db, "Individual");
        var coupon = await Sales(f.Db, files).CreateCouponAsync(Admin, new("K1TEK", "Percent", 10m, null, null, 2, [], true), default);
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var first = await Create(f.Db, plan, a, "EFT-K1-COUPON-A", "coupon-a-0001", "K1TEK");
        var second = await Create(f.Db, plan, b, "EFT-K1-COUPON-B", "coupon-b-0001", "K1TEK");
        // Two notices wait with the code, so the limit of two is reached.
        await Assert.ThrowsAsync<ArgumentException>(() => Create(f.Db, plan, c, "EFT-K1-COUPON-C", "coupon-c-0001", "K1TEK"));

        await Sales(f.Db, files).UpdateCouponAsync(Admin, coupon.Id, new("K1TEK", "Percent", 10m, null, null, 1, [], true), default);
        await Service(f.Db).ReviewBankTransferRequestAsync(first.Id, new("Approved", null), Admin, "review-coupon-a1");
        var over = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(f.Db).ReviewBankTransferRequestAsync(second.Id, new("Approved", null), Admin, "review-coupon-b1"));
        Assert.Equal("Sales.CouponLimit", over.Code);

        await Service(f.Db).CancelApprovedBankTransferAsync(first.Id, "Ödeme iade edildi", Admin);
        await Service(f.Db).WithdrawMyBankTransferRequestAsync(b, second.Id);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(0, (await f.Db.Set<CoachingCoupon>().SingleAsync()).UsedCount);
        Assert.Equal(1080m, (await Sales(f.Db, files).QuoteAsync(a, "K1TEK", plan, default)).Amount);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CancellingAnUpgradePutsTheTeacherBackOnThePlanTheyHadBefore()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var small = await Setup(f.Db, "Teacher", seats: 5);
        var large = (await Service(f.Db).CreatePlanAsync(new("k1-teacher-large", "Öğretmen büyük", "Koçluk", "Teacher", 2000m, false, "Annual", 365, 20,
            ["Raporlar"], true, true, 1), Admin))!.Value;
        var teacher = Guid.NewGuid();
        await Approved(f.Db, small, teacher, "EFT-K1-TEACHER-1");
        var upgrade = await Approved(f.Db, large, teacher, "EFT-K1-TEACHER-2");
        f.Db.ChangeTracker.Clear();
        Assert.Equal(large, (await f.Db.CoachingSubscriptions.SingleAsync(x => x.UserId == teacher)).PlanId);

        await Service(f.Db).CancelApprovedBankTransferAsync(upgrade, "Yükseltme iade edildi", Admin);

        f.Db.ChangeTracker.Clear();
        var subscription = await f.Db.CoachingSubscriptions.SingleAsync(x => x.UserId == teacher);
        Assert.Equal((small, "Active"), (subscription.PlanId, subscription.Status));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ATeacherInAnInstitutionCannotStartATeacherPlanTrial()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(); using var files = new Files();
        var plan = (await Service(f.Db).CreatePlanAsync(new("k1-teacher-trial", "Öğretmen deneme", "Koçluk", "Teacher", 900m, false, "Annual", 365, 5,
            ["Raporlar"], true, true, 0, TrialDays: 7), Admin))!.Value;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Sales(f.Db, files, new Identity(institutionTeacher: true)).StartTrialAsync(Guid.NewGuid(), "Öğretmen", null, "Teacher", plan, default));
        Assert.Equal("Authorization.Forbidden", error.Code);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AnErasedUsersPendingTransferCannotBeApproved()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(); using var files = new Files();
        var plan = await Setup(f.Db, "Individual");
        var user = Guid.NewGuid();
        var pending = await Create(f.Db, plan, user, "EFT-K1-ERASED-1", "erased-0001");
        await Erase(f.Db, files, user);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(f.Db).ReviewBankTransferRequestAsync(pending.Id, new("Approved", null), Admin, "review-erased-01"));
        Assert.Equal("Subscription.UserErased", error.Code);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheAdminNoticeShowsTheAmountInTurkishFormat()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var plan = await Setup(f.Db, "Individual");
        var events = new Publisher();
        await new CoachingSubscriptionService(f.Db, new Identity(), events).CreateBankTransferRequestAsync(Guid.NewGuid(), "Ada", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-K1-FORMAT-1", "Ada", null, true, AcceptSalesContract: true), "k1-format-notice-0001");
        Assert.Contains("1.200,00 TRY", Assert.Single(events.Notices).Detail);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheSeatPriceNeverExceedsTheFullPeriodPriceAfterALicenceWasExtended()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var institution = Guid.NewGuid();
        var plan = new CoachingSubscriptionPlan { Slug = "k1-kurum", Name = "Kurum yıllık", Audience = "Institution", Price = 5000m, IncludedStudentSeats = 1,
            DurationDays = 365, ExtraSeatPrice = 365m };
        var license = new CoachingSubscription { PlanId = plan.Id, InstitutionId = institution, StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow.AddDays(500) };
        f.Db.AddRange(plan, license, new CoachingSubscriptionSettings { Currency = "TRY", BankTransferEnabled = true, Iban = "TR000000000000000000000001" });
        await f.Db.SaveChangesAsync();
        var requests = new CoachingSeatRequestService(f.Db, new Scope(new(false, institution, [])), new Actor(), TimeProvider.System);

        var quote = await requests.QuoteAsync(license.Id, 2, default);

        Assert.Equal((365, 730m), (quote.RemainingDays, quote.Amount));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ABuyerCanOpenTheInvoiceOfAnOldPayment()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(); using var files = new Files();
        var plan = await Setup(f.Db, "Individual");
        var user = Guid.NewGuid();
        var oldest = new CoachingPaymentRecord { UserId = user, UserName = "Ada", UserEmail = "ada@example.test", PlanId = plan, PlanName = "Bireysel yıllık",
            Amount = 10m, CreatedAt = DateTime.UtcNow.AddYears(-2) };
        f.Db.Add(oldest);
        for (var i = 0; i < 101; i++)
            f.Db.Add(new CoachingPaymentRecord { UserId = user, UserName = "Ada", UserEmail = "ada@example.test", PlanId = plan, PlanName = "Bireysel yıllık",
                Amount = 10m, CreatedAt = DateTime.UtcNow.AddDays(-i) });
        await f.Db.SaveChangesAsync();
        await Sales(f.Db, files).AttachInvoiceAsync(Admin, oldest.Id, "K1-2024-0001", new DateOnly(2024, 10, 1), "fatura.pdf",
            new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4 k1")), default);

        await using var invoice = await Sales(f.Db, files).OpenInvoiceAsync(user, oldest.Id, default);
        Assert.Equal("fatura.pdf", invoice.FileName);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sales(f.Db, files).OpenInvoiceAsync(Guid.NewGuid(), oldest.Id, default));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AnonymizationAfterFiveYearsAlsoClearsDocumentsWithdrawalsTrialsAndWhatAParentSent()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(); using var files = new Files();
        var plan = await Setup(f.Db, "Individual");
        var parent = Guid.NewGuid(); var child = Guid.NewGuid(); var student = Guid.NewGuid();
        var sent = await new CoachingSubscriptionService(f.Db, new Identity()).CreateChildBankTransferRequestAsync(parent, "Veli Ayşe", "veli@example.test",
            child, "Çocuk Can", "can@example.test", new CoachingBankTransferRequestCreate(plan, "EFT-K1-PARENT-1", null, "Annesi ödedi", true,
                AcceptSalesContract: true), "parent-sent-0001");
        var own = await Create(f.Db, plan, student, "EFT-K1-ANON-01", "anon-0001", name: "Öğrenci Ece", email: "ece@example.test");
        await Service(f.Db).ReviewBankTransferRequestAsync(own.Id, new("Approved", null), Admin, "review-anon-0001");
        await Sales(f.Db, files).RequestWithdrawalAsync(student, own.Id, "Ece'nin özel gerekçesi", default);
        await Sales(f.Db, files).ReviewWithdrawalAsync(Admin, own.Id, false, "Ece ile görüşüldü", default);
        f.Db.Add(new CoachingSubscriptionTrial { UserId = student, Audience = "Individual", PlanId = plan, SubscriptionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow, EndsAt = DateTime.UtcNow.AddDays(7) });
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var clock = new Clock(DateTime.UtcNow);
        var studentExecution = await Erase(f.Db, files, student, clock);
        var parentExecution = await Erase(f.Db, files, parent, clock);
        Assert.Equal<DateTime?>(studentExecution.CompletedAt.AddYears(5), studentExecution.FinancialRetentionUntil);
        Assert.True(parentExecution.RetainedFinancialRecordCount >= 1);

        clock.Now = DateTimeOffset.UtcNow.AddYears(5).AddDays(1);
        await new CoachingFinancialRetentionProcessor(f.Db, clock, NullLogger<CoachingFinancialRetentionProcessor>.Instance).AnonymizeDueAsync(default);

        f.Db.ChangeTracker.Clear();
        var anonymized = await f.Db.CoachingBankTransferRequests.SingleAsync(x => x.Id == own.Id);
        Assert.Null(anonymized.WithdrawalReason);
        Assert.Null(anonymized.WithdrawalReviewNote);
        Assert.DoesNotContain("Öğrenci Ece", anonymized.SalesDocumentsJson ?? string.Empty);
        Assert.DoesNotContain("ece@example.test", anonymized.SalesDocumentsJson ?? string.Empty);
        Assert.False(await f.Db.Set<CoachingSubscriptionTrial>().AnyAsync(x => x.UserId == student));
        var bySentParent = await f.Db.CoachingBankTransferRequests.SingleAsync(x => x.Id == sent!.Id);
        Assert.NotEqual(parent, bySentParent.SubmittedByUserId);
        Assert.Null(bySentParent.PayerName);
        Assert.DoesNotContain("Veli Ayşe", bySentParent.SalesDocumentsJson ?? string.Empty);
        Assert.Equal(child, bySentParent.UserId);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ErasingAStudentAlsoRemovesTheTeacherParentConversationsAboutThem()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(); using var files = new Files();
        var (teacher, parent, student) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var conversation = CoachingConversation.Create(teacher, student, parent, CoachingConversationKind.TeacherParent, null, DateTime.UtcNow);
        f.Db.Add(conversation);
        f.Db.Add(CoachingMessage.Create(conversation.Id, parent, "Öğrenci hakkında soru", false, DateTime.UtcNow));
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var requestId = Guid.NewGuid();
        await new CoachingErasureAssessmentService(f.Db, TimeProvider.System)
            .AssessAsync(new(Guid.NewGuid(), requestId, student, DateTime.UtcNow, DryRun: true), default);
        f.Db.ChangeTracker.Clear();
        var execution = await new CoachingErasureExecutionService(f.Db, files.Storage, TimeProvider.System)
            .ExecuteAsync(new(Guid.NewGuid(), requestId, student, DateTime.UtcNow, PersonalDataScope.Account), default);
        Assert.True(execution.DeletedRecordCount >= 2);

        f.Db.ChangeTracker.Clear();
        Assert.False(await f.Db.Set<CoachingConversation>().AnyAsync());
        Assert.False(await f.Db.Set<CoachingMessage>().AnyAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheExportCarriesTheFullPurchaseRecordAndTheStudentsGoalsAndFiles()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(); using var files = new Files();
        var plan = await Setup(f.Db, "Individual");
        var (student, teacher) = (Guid.NewGuid(), Guid.NewGuid());
        await Sales(f.Db, files).CreateCouponAsync(Admin, new("K1EXPORT", "Amount", 100m, null, null, null, [], true), default);
        var transfer = await Create(f.Db, plan, student, "EFT-K1-EXPORT-1", "export-0001", "K1EXPORT");
        await Service(f.Db).ReviewBankTransferRequestAsync(transfer.Id, new("Approved", null), Admin, "review-export-01");
        await Sales(f.Db, files).RequestWithdrawalAsync(student, transfer.Id, "Deneme gerekçesi", default);
        var payment = await f.Db.CoachingPaymentRecords.SingleAsync(x => x.BankTransferRequestId == transfer.Id);
        await Sales(f.Db, files).AttachInvoiceAsync(Admin, payment.Id, "K1-EXP-0001", DateOnly.FromDateTime(DateTime.UtcNow), "fatura.pdf",
            new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4 export")), default);
        f.Db.Add(new CoachingSubscriptionTrial { UserId = student, Audience = "Individual", PlanId = plan, SubscriptionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddDays(-40), EndsAt = DateTime.UtcNow.AddDays(-33) });
        var goal = AcademicGoal.Create(student, "TYT netim", Coaching.Domain.Enums.GoalCategory.Other);
        goal.SetNetTarget("TYT", 80m);
        f.Db.Add(goal);
        f.Db.Add(StudentPrimaryGoal.Create(student, goal.Id));
        var assignment = Assignment.Create(teacher, "Dosyalı ödev", DateTime.UtcNow.AddDays(3));
        assignment.AssignToStudent(student);
        assignment.AssignedStudents.Single().AddSubmissionAttachment($"assignments/{assignment.Id:N}/k1", "cevap.png", "image/png", 11,
            new string('A', 64));
        f.Db.Add(assignment);
        var conversation = CoachingConversation.Create(teacher, student, student, CoachingConversationKind.TeacherStudent, null, DateTime.UtcNow);
        f.Db.Add(conversation);
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var export = await new CoachingDataExportRepository(f.Db).ExportStudentDataAsync(student, DateTimeOffset.UtcNow, default);

        Assert.Equal("1.11", export.SchemaVersion);
        var exported = Assert.Single(export.BankTransfers!);
        Assert.Equal((1200m, "K1EXPORT", 100m, CoachingSalesDocumentTexts.Version, "Requested", "Deneme gerekçesi", 365),
            (exported.ListPrice, exported.CouponCode, exported.DiscountAmount, exported.SalesContractVersion, exported.WithdrawalStatus,
                exported.WithdrawalReason, exported.DurationDays));
        Assert.NotNull(exported.SalesContractAcceptedAt);
        Assert.NotNull(exported.AdultPayerDeclaredAt);
        Assert.Single(export.Trials!);
        Assert.Single(export.SubscriptionPeriods!);
        Assert.Equal("K1-EXP-0001", Assert.Single(export.Invoices!).InvoiceNumber);
        Assert.Equal(80m, Assert.Single(Assert.Single(export.Goals).NetTargets!).TargetNet);
        Assert.Equal(goal.Id, export.PrimaryGoalId);
        Assert.Equal("cevap.png", Assert.Single(Assert.Single(export.Assignments).Attachments!).FileName);

        // A teacher's export carries the teacher's own conversations.
        var teacherExport = await new CoachingDataExportRepository(f.Db).ExportStudentDataAsync(teacher, DateTimeOffset.UtcNow, default);
        Assert.Equal(conversation.Id, Assert.Single(teacherExport.Conversations!).Id);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AParentExportsOnlyWhatTheyThemselvesCreated()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var plan = await Setup(f.Db, "Individual");
        var (parent, child, teacher) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await new CoachingSubscriptionService(f.Db, new Identity()).CreateChildBankTransferRequestAsync(parent, "Veli Ayşe", "veli@example.test",
            child, "Çocuk Can", "can@example.test", new CoachingBankTransferRequestCreate(plan, "EFT-K1-PEXPORT-1", null, null, true,
                AcceptSalesContract: true), "parent-export-0001");
        var conversation = CoachingConversation.Create(teacher, child, parent, CoachingConversationKind.TeacherParent, null, DateTime.UtcNow);
        f.Db.Add(conversation);
        f.Db.Add(CoachingMessage.Create(conversation.Id, parent, "Merhaba hocam", false, DateTime.UtcNow));
        f.Db.Add(AcademicGoal.Create(child, "Çocuğun hedefi", Coaching.Domain.Enums.GoalCategory.Other));
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var handler = new ExportCoachingDataQueryHandler(new CoachingDataExportRepository(f.Db), new CoachingAccessPolicy(new Viewer(parent, "Parent")),
            new Identity(), TimeProvider.System);
        var export = await handler.Handle(new ExportCoachingDataQuery(), default);

        Assert.Equal(parent, export.StudentId);
        Assert.Single(export.BankTransfers!);
        Assert.Equal("Merhaba hocam", Assert.Single(Assert.Single(export.Conversations!).Messages).Body);
        Assert.Empty(export.Goals);
    }

    [Fact]
    public void TheContractExplainsWhenARenewalStartsAndIsDatedInTurkey()
    {
        var documents = CoachingSalesDocumentTexts.Render(new(new CoachingSubscriptionSettings(), "Ada", null, "Bireysel yıllık", 365, 1200m, 0, 1200m,
            "TRY", null, new DateTime(2026, 10, 31, 22, 30, 0, DateTimeKind.Utc), string.Empty));
        Assert.Contains("01.11.2026", documents.DistanceSales);
        Assert.Contains("yenileme", documents.DistanceSales, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("yenileme", documents.PreInformation, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("2026-10-28", documents.Version);
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------------

    private static CoachingSubscriptionService Service(CoachingDbContext db) => new(db, new Identity());

    private static CoachingSalesService Sales(CoachingDbContext db, Files files, Identity? identity = null) =>
        new(db, new CoachingSubscriptionService(db, identity ?? new Identity()), files.Storage, new DevelopmentAttachmentScanner(), TimeProvider.System);

    private static async Task<Guid> Setup(CoachingDbContext db, string audience, int? seats = null)
    {
        await Service(db).UpdateSettingsAsync(new(false, "TRY", "Edu İvme", "Örnek Banka", "TR33 0006 1005 1978 6457 8413 26", null, true), Admin,
            "settings-k1-" + Guid.NewGuid().ToString("N")[..12]);
        return (await Service(db).CreatePlanAsync(new("k1-" + audience.ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N")[..6],
            audience == "Teacher" ? "Öğretmen küçük" : "Bireysel yıllık", "Koçluk", audience, audience == "Teacher" ? 1000m : 1200m, false, "Annual", 365,
            seats, ["Raporlar"], true, true, 0), Admin))!.Value;
    }

    private static async Task<CoachingBankTransferRequestSummary> Create(CoachingDbContext db, Guid plan, Guid user, string reference, string key,
        string? coupon = null, string name = "Ada", string email = "ada@example.test")
    {
        var result = await Service(db).CreateBankTransferRequestAsync(user, name, email,
            new CoachingBankTransferRequestCreate(plan, reference, name, null, true, coupon, AcceptSalesContract: true), "k1-create-" + key);
        Assert.NotNull(result);
        return result!;
    }

    private static async Task<Guid> Approved(CoachingDbContext db, Guid plan, Guid user, string reference)
    {
        var audience = (await db.CoachingSubscriptionPlans.AsNoTracking().SingleAsync(x => x.Id == plan)).Audience;
        var key = reference.ToLowerInvariant();
        var created = audience == "Teacher"
            ? await Service(db).CreateTeacherBankTransferRequestAsync(user, "Öğretmen", "ogretmen@example.test",
                new CoachingBankTransferRequestCreate(plan, reference, "Öğretmen", null, true, AcceptSalesContract: true), key)
            : await Create(db, plan, user, reference, key);
        Assert.NotNull(created);
        Assert.Equal("Approved", (await Service(db).ReviewBankTransferRequestAsync(created!.Id, new("Approved", null), Admin, "review-" + key))!.Status);
        db.ChangeTracker.Clear();
        return created.Id;
    }

    private static async Task<CoachingErasureExecution> Erase(CoachingDbContext db, Files files, Guid subject, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        var requestId = Guid.NewGuid();
        await new CoachingErasureAssessmentService(db, clock).AssessAsync(new(Guid.NewGuid(), requestId, subject, DateTime.UtcNow, DryRun: true), default);
        db.ChangeTracker.Clear();
        var execution = await new CoachingErasureExecutionService(db, files.Storage, clock)
            .ExecuteAsync(new(Guid.NewGuid(), requestId, subject, DateTime.UtcNow, PersonalDataScope.Account), default);
        db.ChangeTracker.Clear();
        return execution;
    }

    private sealed class Identity(bool institutionTeacher = false) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(new(true, null));
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? institution, bool admin, CancellationToken ct) =>
            Task.FromResult(institutionTeacher ? Guid.NewGuid() : institution);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) => Task.FromResult(studentIds);
    }

    private sealed class Publisher : ICoachingEventPublisher
    {
        public List<CoachingSubscriptionNoticeV1> Notices { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            if (message is CoachingSubscriptionNoticeV1 notice) Notices.Add(notice);
            return Task.CompletedTask;
        }
    }

    private sealed class Scope(CoachingAdminScope scope) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(scope);
    }

    private sealed class Actor : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public IEnumerable<string> Roles => ["InstitutionAdmin"];
        public string? Email => null; public string? FullName => null;
        public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
    }

    private sealed class Viewer(Guid id, string role) : ICurrentUserService
    {
        public Guid? UserId => id;
        public IEnumerable<string> Roles => [role];
        public string? Email => null; public string? FullName => null;
        public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
    }

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Files : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "coaching-k1-" + Guid.NewGuid().ToString("N"));
        public LocalAssignmentAttachmentStorage Storage { get; }
        public Files() { Directory.CreateDirectory(root); Storage = new(Options.Create(new AssignmentAttachmentOptions { RootPath = root })); }
        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
