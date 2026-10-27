using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap Y1: subscriptions that run out are marked expired and their owners are told in time.
public sealed class CoachingSubscriptionLifecyclePostgresTests
{
    private static readonly DateTime Start = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ExpiringSubscriptionsGetOneReminderPerStageAndAnExpiryNoticeOnce()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var clock = new Clock(Start); var events = new Publisher();
        var individual = Plan(pg.Db, "Individual", "Bireysel aylık"); var teacherPlan = Plan(pg.Db, "Teacher", "Öğretmen yıllık");
        var institutionPlan = Plan(pg.Db, "Institution", "Kurum yıllık");
        var student = Guid.NewGuid(); var teacher = Guid.NewGuid(); var institution = Guid.NewGuid();
        var studentSubscription = Add(pg.Db, individual, student, null, "ada@example.test", Start.AddDays(5));
        var teacherSubscription = Add(pg.Db, teacherPlan, teacher, null, "ogretmen@example.test", Start.AddDays(4).AddHours(18));
        var institutionSubscription = Add(pg.Db, institutionPlan, null, institution, "kurum@example.test", Start.AddDays(6));
        await pg.Db.SaveChangesAsync();

        await Run(pg, clock, events);
        await Run(pg, clock, events);
        Assert.Collection(events.Notices.OrderBy(x => x.Kind).ThenBy(x => x.Audience),
            x => { Assert.Equal((CoachingSubscriptionNoticeKinds.Expiring, "Individual", student, "ada@example.test"), (x.Kind, x.Audience, x.RecipientUserId, x.RecipientEmail));
                   Assert.Equal("Bireysel aylık", x.PlanName); Assert.Equal(Start.AddDays(5), x.EndsAt); },
            x => Assert.Equal((CoachingSubscriptionNoticeKinds.Expiring, "Teacher", teacher), (x.Kind, x.Audience, x.RecipientUserId)),
            x => Assert.Equal((CoachingSubscriptionNoticeKinds.InstitutionExpiring, (Guid?)null, "kurum@example.test"), (x.Kind, x.RecipientUserId, x.RecipientEmail)));

        // Inside the last day each owner gets one more reminder.
        clock.Now = Start.AddDays(4).AddHours(12);
        await Run(pg, clock, events); await Run(pg, clock, events);
        Assert.Equal(5, events.Notices.Count);
        Assert.Equal(new Guid?[] { student, teacher }.Order(), events.Notices.Skip(3).Select(x => x.RecipientUserId).Order());
        Assert.All(events.Notices.Skip(3), x => Assert.Equal(CoachingSubscriptionNoticeKinds.Expiring, x.Kind));

        // After the end date every subscription is marked expired and its owner is told once.
        clock.Now = Start.AddDays(7);
        await Run(pg, clock, events); await Run(pg, clock, events);
        var expired = events.Notices.Skip(5).ToArray();
        Assert.Equal(3, expired.Length);
        Assert.Equal(2, expired.Count(x => x.Kind == CoachingSubscriptionNoticeKinds.Expired));
        Assert.Contains(expired, x => x.Kind == CoachingSubscriptionNoticeKinds.InstitutionExpired && x.RecipientEmail == "kurum@example.test");
        pg.Db.ChangeTracker.Clear();
        Guid[] ids = [studentSubscription, teacherSubscription, institutionSubscription];
        Assert.All(await pg.Db.CoachingSubscriptions.Where(x => Enumerable.Contains(ids, x.Id)).ToListAsync(),
            x => Assert.Equal("Expired", x.Status));
        Assert.Equal(events.Notices.Count, events.Notices.Select(x => x.NoticeId).Distinct().Count());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RenewedCancelledAndLongLapsedSubscriptionsStayQuiet()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var clock = new Clock(Start); var events = new Publisher();
        var individual = Plan(pg.Db, "Individual", "Bireysel aylık"); var other = Plan(pg.Db, "Individual", "Bireysel yıllık");
        var renewed = Guid.NewGuid(); var cancelled = Guid.NewGuid(); var lapsed = Guid.NewGuid();
        var current = Add(pg.Db, individual, renewed, null, "renewed@example.test", Start.AddDays(3));
        Add(pg.Db, other, renewed, null, "renewed@example.test", Start.AddDays(368), Start.AddDays(3));
        var cancelledId = Add(pg.Db, individual, cancelled, null, "cancelled@example.test", Start.AddDays(-1));
        var lapsedId = Add(pg.Db, individual, lapsed, null, "lapsed@example.test", Start.AddDays(-90));
        await pg.Db.SaveChangesAsync();
        await pg.Db.CoachingSubscriptions.Where(x => x.Id == cancelledId).ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, "Cancelled"));

        await Run(pg, clock, events);
        Assert.Empty(events.Notices);
        pg.Db.ChangeTracker.Clear();
        Assert.Equal("Cancelled", (await pg.Db.CoachingSubscriptions.SingleAsync(x => x.Id == cancelledId)).Status);
        Assert.Equal("Expired", (await pg.Db.CoachingSubscriptions.SingleAsync(x => x.Id == lapsedId)).Status);

        clock.Now = Start.AddDays(4);
        await Run(pg, clock, events);
        Assert.Empty(events.Notices);
        Assert.Equal("Expired", (await pg.Db.CoachingSubscriptions.AsNoTracking().SingleAsync(x => x.Id == current)).Status);
    }

    // Roadmap Y5: a payment notice nobody could verify does not wait forever; the payer is told and can send a new one.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PendingTransfersExpireAfterTwoWeeksAndThePayerIsToldOnce()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var clock = new Clock(Start); var events = new Publisher();
        var plan = Plan(pg.Db, "Individual", "Bireysel aylık");
        var stale = Transfer(pg.Db, plan, "EFT-STALE-001", Start.AddDays(-15));
        var fresh = Transfer(pg.Db, plan, "EFT-FRESH-001", Start.AddDays(-13));
        await pg.Db.SaveChangesAsync();

        await Run(pg, clock, events); await Run(pg, clock, events);

        pg.Db.ChangeTracker.Clear();
        Assert.Equal("Expired", (await pg.Db.CoachingBankTransferRequests.SingleAsync(x => x.Id == stale)).Status);
        Assert.Equal("Pending", (await pg.Db.CoachingBankTransferRequests.SingleAsync(x => x.Id == fresh)).Status);
        var notice = Assert.Single(events.Notices);
        Assert.Equal((CoachingSubscriptionNoticeKinds.TransferExpired, "payer@example.test", "Bireysel aylık"),
            (notice.Kind, notice.RecipientEmail, notice.PlanName));
        Assert.Contains("EFT-STALE-001", notice.Detail);
    }

    private static Guid Transfer(CoachingDbContext db, Guid plan, string reference, DateTime createdAt)
    {
        var transfer = new CoachingBankTransferRequest { UserId = Guid.NewGuid(), UserName = "Ödeyen", UserEmail = "payer@example.test",
            PlanId = plan, Amount = 250, PaymentReference = reference, CreatedAt = createdAt, UpdatedAt = createdAt, DurationDays = 30 };
        db.Add(transfer);
        return transfer.Id;
    }

    private static async Task Run(CurriculumPostgresTests.Pg pg, Clock clock, Publisher events)
    {
        await using var db = new CoachingDbContext(pg.Options);
        await new CoachingSubscriptionLifecycleProcessor(db, events, clock).ProcessAsync();
    }

    private static Guid Plan(CoachingDbContext db, string audience, string name)
    {
        var plan = new CoachingSubscriptionPlan { Slug = "plan-" + Guid.NewGuid().ToString("N")[..8], Name = name, Audience = audience,
            Price = 250, DurationDays = 30, IncludedStudentSeats = audience == "Individual" ? null : 5, CreatedBy = Guid.NewGuid() };
        db.Add(plan);
        return plan.Id;
    }

    private static Guid Add(CoachingDbContext db, Guid plan, Guid? user, Guid? institution, string email, DateTime end, DateTime? start = null)
    {
        var subscription = new CoachingSubscription { PlanId = plan, UserId = user, InstitutionId = institution, UserName = "Kişi",
            UserEmail = email, StartDate = start ?? end.AddDays(-30), EndDate = end, CreatedBy = Guid.NewGuid() };
        db.Add(subscription);
        return subscription.Id;
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

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
