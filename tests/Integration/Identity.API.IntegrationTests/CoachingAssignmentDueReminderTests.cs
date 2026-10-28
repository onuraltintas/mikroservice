using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Coaching.Infrastructure.Reminders;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

// Roadmap O8: a student who has not handed in an assignment is reminded once, a day before it is due.
public sealed partial class CoachingReminderTests
{
    [Fact]
    public async Task AssignmentDueRemindsOnlyStudentsWhoHaveNotSubmittedOnceWithinTheLastDay()
    {
        var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(2).AddHours(6), TimeSpan.Zero) };
        var teacher = Guid.NewGuid(); var waiting = Guid.NewGuid(); var done = Guid.NewGuid(); var later = Guid.NewGuid();
        await using var provider = Services().BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var soon = Assignment.Create(teacher, "Çarpanlar testi", clock.Now.UtcDateTime.AddHours(20)); soon.AssignToStudents([waiting, done]); soon.SubmitAssignment(done);
            var next = Assignment.Create(teacher, "Üslü sayılar", clock.Now.UtcDateTime.AddHours(30)); next.AssignToStudents([later]);
            var cancelled = Assignment.Create(teacher, "İptal", clock.Now.UtcDateTime.AddHours(10)); cancelled.AssignToStudents([waiting]); cancelled.Cancel();
            db.AddRange(soon, next, cancelled); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var processor = new CoachingReminderProcessor(db, new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()), clock);

            Assert.Equal(1, await processor.ProcessAsync());
            var receipt = Assert.Single(await db.CoachingReminderReceipts.AsNoTracking().ToListAsync());
            Assert.Equal(("AssignmentDue", waiting, soon.Id), (receipt.Kind, receipt.RecipientUserId, receipt.SourceId));
            Assert.Contains("Çarpanlar testi", Assert.Single(await db.Set<OutboxMessage>().AsNoTracking().ToListAsync()).Body);
            Assert.Equal(0, await processor.ProcessAsync());

            clock.Now = clock.Now.AddHours(7);
            Assert.Equal(1, await processor.ProcessAsync());
            Assert.Contains(await db.CoachingReminderReceipts.AsNoTracking().ToListAsync(), x => x.RecipientUserId == later && x.SourceId == next.Id);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
