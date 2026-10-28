using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Coaching.Infrastructure.Reminders;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

// Roadmap D1: once the morning study scan of a time zone has nothing left to send, it is not repeated that day.
public sealed partial class CoachingReminderTests
{
    [Fact]
    public async Task FinishedStudyScanIsNotRepeatedTheSameLocalDay()
    {
        var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(1).AddHours(7), TimeSpan.Zero) };
        await using var provider = Services().BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var date = DateOnly.FromDateTime(clock.Now.UtcDateTime); var state = new CoachingReminderScanState();
            StudyPlanRevision Plan(Guid student, DateOnly day)
            {
                var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Plan"); plan.Activate();
                db.AddRange(plan, StudyPlanTask.Create(plan, day, "Görev", 30)); return plan;
            }
            Plan(Guid.NewGuid(), date); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var processor = new CoachingReminderProcessor(db, new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()), clock, state);
            Assert.Equal(1, await processor.ProcessAsync());

            var late = Guid.NewGuid(); Plan(late, date); Plan(late, date.AddDays(1)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            clock.Now = clock.Now.AddMinutes(5);
            Assert.Equal(0, await processor.ProcessAsync());
            Assert.False(await db.CoachingReminderReceipts.AnyAsync(x => x.RecipientUserId == late));

            clock.Now = clock.Now.AddDays(1);
            Assert.True(await processor.ProcessAsync() >= 1);
            Assert.True(await db.CoachingReminderReceipts.AnyAsync(x => x.RecipientUserId == late && x.Kind == "StudyDay"));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
