using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Coaching.Infrastructure.Reminders;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

// User decision 15.11.2026: a plan made for a week (or up to a chosen date) is ending and nothing follows it: whoever made
// it (the teacher, else the student) is reminded once.
public sealed partial class CoachingReminderTests
{
    [Fact]
    public async Task APlanMadeForPartOfTheWayRemindsItsMakerOnceWhenItEnds()
    {
        // 07:00 UTC is 10:00 in Istanbul: after the 09:00 reminder hour.
        var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(2).AddHours(7), TimeSpan.Zero) };
        var today = DateOnly.FromDateTime(clock.Now.UtcDateTime);
        var teacher = Guid.NewGuid(); var taught = Guid.NewGuid(); var own = Guid.NewGuid(); var yearly = Guid.NewGuid(); var later = Guid.NewGuid();
        await using var provider = Services().BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            StudyPlanRevision Plan(Guid student, DateOnly end, bool partial, Guid? madeBy = null)
            {
                var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "TEST plan", madeBy);
                plan.SetWeeklySource(new WeeklyPlanSource(1, null, today.AddDays(-5), end, true, "test-fingerprint", null, null, Partial: partial));
                plan.Activate();
                return plan;
            }
            var byTeacher = Plan(taught, today.AddDays(1), true, teacher);
            var byStudent = Plan(own, today, true);
            db.AddRange(byTeacher, byStudent, Plan(yearly, today.AddDays(1), false), Plan(later, today.AddDays(4), true));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var processor = new CoachingReminderProcessor(db, new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()), clock);

            Assert.Equal(2, await processor.ProcessAsync());
            var receipts = await db.CoachingReminderReceipts.AsNoTracking().OrderBy(x => x.SourceId).ToListAsync();
            Assert.All(receipts, receipt => Assert.Equal("PlanEnding", receipt.Kind));
            Assert.Equal(new[] { (teacher, byTeacher.Id), (own, byStudent.Id) }.ToHashSet(),
                receipts.Select(x => (x.RecipientUserId, x.SourceId)).ToHashSet());
            // The same plan does not remind again, neither later that day nor the next.
            Assert.Equal(0, await processor.ProcessAsync());
            clock.Now = clock.Now.AddDays(1);
            Assert.Equal(0, await processor.ProcessAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
