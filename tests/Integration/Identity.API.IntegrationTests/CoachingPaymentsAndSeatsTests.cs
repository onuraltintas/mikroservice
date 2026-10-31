using EduPlatform.Shared.Contracts.Events.Coaching;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap Y5: paid periods, plan upgrade, seats that carry over and stay within capacity, and pending transfers
// the payer can correct or withdraw.
public sealed partial class CoachingCmsAndSubscriptionServiceTests
{
    private const string Iban = "TR33 0006 1005 1978 6457 8413 26";

    [Fact]
    public async Task CancellingTheFirstTransfer_KeepsTheRenewalThatExtendedIt()
    {
        await using var db = CreateDbContext();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var actor = Guid.NewGuid(); var user = Guid.NewGuid();
        var plan = (await subscriptions.CreatePlanAsync(Plan("periods", "Bireysel yıllık", "Individual", 1200m, null), actor))!.Value;
        await Settings(subscriptions, actor, "settings-periods-0001");
        var first = await Approve(subscriptions, user, plan, "EFT-PERIOD-1", actor, "periods-first-01");
        await Approve(subscriptions, user, plan, "EFT-PERIOD-2", actor, "periods-second-1");
        (await db.CoachingSubscriptionPeriods.CountAsync()).Should().Be(2);

        (await subscriptions.CancelApprovedBankTransferAsync(first, "İlk ödeme iade edildi", actor))!.Status.Should().Be("Cancelled");

        var subscription = await db.CoachingSubscriptions.SingleAsync();
        subscription.Status.Should().Be("Active");
        // The renewal's paid year now starts today instead of after the cancelled year.
        subscription.EndDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(365), TimeSpan.FromMinutes(1));
        (await subscriptions.GetMyAccessAsync(user)).HasAccess.Should().BeTrue();
        var periods = await db.CoachingSubscriptionPeriods.OrderBy(x => x.CreatedAt).ToListAsync();
        periods.Select(x => x.Status).Should().Equal("Cancelled", "Active");
        periods[1].EndDate.Should().BeCloseTo(subscription.EndDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ApprovalUsesTheDurationAndCurrencyFixedWhenTheTransferWasSent()
    {
        await using var db = CreateDbContext();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var actor = Guid.NewGuid(); var user = Guid.NewGuid();
        var plan = (await subscriptions.CreatePlanAsync(Plan("fixed-terms", "Bireysel yıllık", "Individual", 1200m, null), actor))!.Value;
        await Settings(subscriptions, actor, "settings-fixed-terms-1");
        var transfer = await subscriptions.CreateBankTransferRequestAsync(user, "Ada", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-FIXED-01", "Ada", null, true, AcceptSalesContract: true), "create-fixed-terms-01");
        transfer!.DurationDays.Should().Be(365);

        await subscriptions.UpdatePlanAsync(plan, Plan("fixed-terms", "Bireysel yıllık", "Individual", 1500m, null) with { DurationDays = 30 }, actor);
        await subscriptions.UpdateSettingsAsync(new CoachingSubscriptionSettingsRequest(true, "EUR", "Edu İvme", "Örnek Banka", Iban, null, true),
            actor, "settings-fixed-terms-2");
        await subscriptions.ReviewBankTransferRequestAsync(transfer.Id, new CoachingBankTransferReviewRequest("Approved", null), actor, "review-fixed-terms-01");

        (await db.CoachingSubscriptions.SingleAsync()).EndDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(365), TimeSpan.FromMinutes(1));
        var payment = await db.CoachingPaymentRecords.SingleAsync();
        (payment.Amount, payment.Currency).Should().Be((1200m, "TRY"));
    }

    [Fact]
    public async Task IndependentTeacherCanUpgradeToALargerPlanAndKeepsSeatsAndPaidDays()
    {
        await using var db = CreateDbContext();
        var identity = new TestIdentityAuthorizationClient();
        var subscriptions = new CoachingSubscriptionService(db, identity);
        var actor = Guid.NewGuid(); var teacher = Guid.NewGuid(); var student = Guid.NewGuid();
        identity.ActiveTeachers.Add(teacher); identity.TeacherAssignments.Add((teacher, student));
        var small = (await subscriptions.CreatePlanAsync(Plan("teacher-small", "Öğretmen 3", "Teacher", 500m, 3), actor))!.Value;
        var large = (await subscriptions.CreatePlanAsync(Plan("teacher-large", "Öğretmen 5", "Teacher", 1500m, 5), actor))!.Value;
        var tiny = (await subscriptions.CreatePlanAsync(Plan("teacher-tiny", "Öğretmen 1", "Teacher", 300m, 1), actor))!.Value;
        await Settings(subscriptions, actor, "settings-upgrade-0001");
        var current = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            small, teacher, "Öğretmen", "teacher@example.test", null, [], DateTime.UtcNow, null), actor);
        (await subscriptions.AssignMyTeacherStudentSeatAsync(teacher, student)).Should().BeTrue();

        // A smaller plan cannot be bought while a plan runs: the payer would pay for seats they lose.
        (await subscriptions.CreateTeacherBankTransferRequestAsync(teacher, "Öğretmen", "teacher@example.test",
            new CoachingBankTransferRequestCreate(tiny, "TEACHER-DOWN-01", "Öğretmen", null, true, AcceptSalesContract: true), "create-downgrade-0001")).Should().BeNull();
        var upgrade = await subscriptions.CreateTeacherBankTransferRequestAsync(teacher, "Öğretmen", "teacher@example.test",
            new CoachingBankTransferRequestCreate(large, "TEACHER-UP-0001", "Öğretmen", null, true, AcceptSalesContract: true), "create-upgrade-00001");
        (await subscriptions.ReviewBankTransferRequestAsync(upgrade!.Id, new CoachingBankTransferReviewRequest("Approved", null), actor,
            "review-upgrade-00001"))!.Status.Should().Be("Approved");

        var subscription = await db.CoachingSubscriptions.SingleAsync();
        subscription.Id.Should().Be(current!.Id);
        subscription.PlanId.Should().Be(large);
        subscription.EndDate.Should().BeCloseTo(current.EndDate.AddDays(365), TimeSpan.FromMinutes(1));
        var summary = (await subscriptions.GetMyTeacherSubscriptionAsync(teacher))!;
        summary.IncludedStudentSeats.Should().Be(5);
        summary.Students.Should().ContainSingle(x => x.StudentId == student && !x.IsSuspended);
    }

    [Fact]
    public async Task InstitutionRenewal_CarriesTheSeatsOfCurrentMembers()
    {
        await using var db = CreateDbContext();
        var identity = new TestIdentityAuthorizationClient();
        var subscriptions = new CoachingSubscriptionService(db, identity);
        var actor = Guid.NewGuid(); var institution = Guid.NewGuid();
        var stays = Guid.NewGuid(); var left = Guid.NewGuid(); var paused = Guid.NewGuid();
        identity.InstitutionStudents.Add((institution, stays)); identity.InstitutionStudents.Add((institution, paused));
        var plan = (await subscriptions.CreatePlanAsync(Plan("institution-renewal", "Kurum yıllık", "Institution", 9000m, 3), actor))!.Value;
        var current = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            plan, null, null, null, institution, [stays, left, paused], DateTime.UtcNow.AddDays(-300), null), actor);
        await subscriptions.ChangeStudentSeatAsync(current!.Id, paused, new CoachingSubscriptionSeatChangeRequest(true, "Ara verdi"), actor);

        var renewal = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            plan, null, null, null, institution, null, current.EndDate, null, "KURUM-YENILEME-1"), actor);

        renewal!.UsedSeatCount.Should().Be(1);
        (await db.CoachingSubscriptionSeats.Where(x => x.SubscriptionId == renewal.Id).Select(x => x.StudentId).ToListAsync())
            .Should().Equal(stays);
    }

    [Fact]
    public async Task AdminSeatReactivation_StaysWithinThePlanCapacity()
    {
        await using var db = CreateDbContext();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var actor = Guid.NewGuid(); var institution = Guid.NewGuid(); var seated = Guid.NewGuid(); var waiting = Guid.NewGuid();
        var plan = (await subscriptions.CreatePlanAsync(Plan("institution-capacity", "Kurum tek koltuk", "Institution", 900m, 1), actor))!.Value;
        var subscription = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            plan, null, null, null, institution, [seated], DateTime.UtcNow, null), actor);
        db.CoachingSubscriptionSeats.Add(new CoachingSubscriptionSeat { SubscriptionId = subscription!.Id, StudentId = waiting, IsSuspended = true, AssignedBy = actor });
        await db.SaveChangesAsync();

        (await subscriptions.ChangeStudentSeatAsync(subscription.Id, waiting, new CoachingSubscriptionSeatChangeRequest(false, null), actor))
            .Should().BeFalse();
        await subscriptions.ChangeStudentSeatAsync(subscription.Id, seated, new CoachingSubscriptionSeatChangeRequest(true, "Ayrıldı"), actor);
        (await subscriptions.ChangeStudentSeatAsync(subscription.Id, waiting, new CoachingSubscriptionSeatChangeRequest(false, null), actor))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ManualLicenses_RecordTheirPaymentAndPeriod()
    {
        await using var db = CreateDbContext();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var actor = Guid.NewGuid(); var user = Guid.NewGuid(); var institution = Guid.NewGuid();
        var individual = (await subscriptions.CreatePlanAsync(Plan("manual-individual", "Bireysel yıllık", "Individual", 1200m, null), actor))!.Value;
        var institutional = (await subscriptions.CreatePlanAsync(Plan("manual-institution", "Kurum yıllık", "Institution", 9000m, 10), actor))!.Value;
        await Settings(subscriptions, actor, "settings-manual-00001");

        var granted = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            individual, user, "Ada", "ada@example.test", null, [], DateTime.UtcNow, "Elden ödeme", null, 900m), actor);
        var licensed = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            institutional, null, null, null, institution, [], DateTime.UtcNow, null, "KURUM-FATURA-77"), actor);

        var payments = await db.CoachingPaymentRecords.ToListAsync();
        payments.Should().HaveCount(2).And.OnlyContain(x => x.Provider == "Manual" && x.Status == "Succeeded" && x.Currency == "TRY");
        payments.Single(x => x.SubscriptionId == granted!.Id).Should().Match<CoachingPaymentRecord>(x => x.UserId == user && x.Amount == 900m);
        payments.Single(x => x.SubscriptionId == licensed!.Id).Should().Match<CoachingPaymentRecord>(x =>
            x.UserId == null && x.InstitutionId == institution && x.Amount == 9000m && x.Reference == "KURUM-FATURA-77");
        (await db.CoachingSubscriptionPeriods.CountAsync(x => x.Source == "Manual")).Should().Be(2);
        (await subscriptions.GetPaymentsAsync(null, null, 1, 25)).TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task PendingTransfer_CanBeCorrectedAndWithdrawnOnlyByItsPayer()
    {
        await using var db = CreateDbContext();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var actor = Guid.NewGuid(); var user = Guid.NewGuid(); var stranger = Guid.NewGuid();
        var plan = (await subscriptions.CreatePlanAsync(Plan("pending-edit", "Bireysel yıllık", "Individual", 1200m, null), actor))!.Value;
        await Settings(subscriptions, actor, "settings-pending-001");
        var transfer = await subscriptions.CreateBankTransferRequestAsync(user, "Ada", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-TYPO-001", "Ada", null, true, AcceptSalesContract: true), "create-pending-0001");

        (await subscriptions.UpdateMyBankTransferRequestAsync(stranger, transfer!.Id,
            new CoachingBankTransferRequestUpdate("EFT-OTHER-01", null, null))).Should().BeNull();
        var corrected = await subscriptions.UpdateMyBankTransferRequestAsync(user, transfer.Id,
            new CoachingBankTransferRequestUpdate("eft fixed 001", "Ada Yılmaz", "Annem gönderdi"));
        (corrected!.PaymentReference, corrected.PayerName, corrected.Note, corrected.Status)
            .Should().Be(("EFT-FIXED-001", "Ada Yılmaz", "Annem gönderdi", "Pending"));

        (await subscriptions.WithdrawMyBankTransferRequestAsync(stranger, transfer.Id)).Should().BeNull();
        (await subscriptions.WithdrawMyBankTransferRequestAsync(user, transfer.Id))!.Status.Should().Be("Withdrawn");
        var late = () => subscriptions.UpdateMyBankTransferRequestAsync(user, transfer.Id, new CoachingBankTransferRequestUpdate("EFT-LATE-001", null, null));
        await late.Should().ThrowAsync<BusinessRuleException>().Where(x => x.Code == "Subscription.TransferNotPending");
        (await subscriptions.ReviewBankTransferRequestAsync(transfer.Id, new CoachingBankTransferReviewRequest("Approved", null), actor,
            "review-withdrawn-001")).Should().BeNull();

        // Withdrawing frees the plan for a new notice instead of returning the withdrawn one.
        var again = await subscriptions.CreateBankTransferRequestAsync(user, "Ada", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-NEW-0001", "Ada", null, true, AcceptSalesContract: true), "create-pending-0002");
        again!.Id.Should().NotBe(transfer.Id); again.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task SameReceiptFromAnotherPayer_WarnsTheAdmin()
    {
        await using var db = CreateDbContext();
        var events = new CapturingPublisher();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient(), events);
        var actor = Guid.NewGuid();
        var plan = (await subscriptions.CreatePlanAsync(Plan("duplicate-receipt", "Bireysel yıllık", "Individual", 1200m, null), actor))!.Value;
        await Settings(subscriptions, actor, "settings-duplicate-01");
        await subscriptions.CreateBankTransferRequestAsync(Guid.NewGuid(), "Ada", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-SAME-0001", "Ada", null, true, AcceptSalesContract: true), "create-duplicate-0001");
        await subscriptions.CreateBankTransferRequestAsync(Guid.NewGuid(), "Can", "can@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-SAME-0001", "Can", null, true, AcceptSalesContract: true), "create-duplicate-0002");

        var notices = events.Of<CoachingSubscriptionNoticeV1>().Where(x => x.Kind == CoachingSubscriptionNoticeKinds.TransferSubmitted).ToArray();
        notices.Should().HaveCount(2);
        notices[0].Detail.Should().NotContain("başka");
        notices[1].Detail.Should().Contain("başka bir kullanıcının bildiriminde de var");
        var listed = (await subscriptions.GetBankTransferRequestsAsync(null, null, 1, 25)).Items;
        listed.Should().HaveCount(2).And.OnlyContain(x => x.DuplicateReferenceCount == 1);
    }

    private static Task Settings(CoachingSubscriptionService subscriptions, Guid actor, string key) =>
        subscriptions.UpdateSettingsAsync(new CoachingSubscriptionSettingsRequest(true, "TRY", "Edu İvme", "Örnek Banka", Iban, null, true), actor, key);

    private static async Task<Guid> Approve(CoachingSubscriptionService subscriptions, Guid user, Guid plan, string reference, Guid actor, string key)
    {
        var transfer = await subscriptions.CreateBankTransferRequestAsync(user, "Ada", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, reference, "Ada", null, true, AcceptSalesContract: true), "create-" + key + "-key");
        (await subscriptions.ReviewBankTransferRequestAsync(transfer!.Id, new CoachingBankTransferReviewRequest("Approved", null), actor,
            "review-" + key + "-key"))!.Status.Should().Be("Approved");
        return transfer.Id;
    }
}
