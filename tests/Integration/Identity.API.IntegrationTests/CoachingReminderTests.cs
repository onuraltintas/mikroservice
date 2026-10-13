using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Coaching.Infrastructure.Reminders;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingReminderTests(PostgresFixture postgres)
{
    [Fact]
    public async Task RemindersRespectLocalNineAndSessionHourDeduplicateAndFollowRescheduledWork()
    {
        var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(1).AddHours(6), TimeSpan.Zero) };
        await using var provider = Services().BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var student = Guid.NewGuid(); var teacher = Guid.NewGuid(); var date = DateOnly.FromDateTime(clock.Now.UtcDateTime);
            var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Private plan title"); plan.Activate();
            var first = StudyPlanTask.Create(plan, date, "Private task title", 30); var second = StudyPlanTask.Create(plan, date, "Second", 30);
            var hours = StudyAvailability.Create(student, "Europe/Istanbul");
            var session = CoachingSession.Create(teacher, "Private session title", clock.Now.UtcDateTime.AddMinutes(30), SessionType.OneOnOne); session.AddStudent(student);
            var cancelled = CoachingSession.Create(teacher, "Cancelled", clock.Now.UtcDateTime.AddMinutes(45), SessionType.OneOnOne); cancelled.AddStudent(student); cancelled.Cancel();
            db.AddRange(plan, first, second, hours, session, cancelled); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var processor = new CoachingReminderProcessor(db, new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()), clock);
            clock.Now = clock.Now.AddMinutes(-1);
            Assert.Equal(2, await processor.ProcessAsync()); // Session starts within an hour, but local study morning has not begun.
            Assert.Equal(2, await db.CoachingReminderReceipts.CountAsync());
            clock.Now = clock.Now.AddMinutes(1); Assert.Equal(1, await processor.ProcessAsync());
            Assert.Equal(0, await processor.ProcessAsync());
            Assert.Equal(3, await db.Set<OutboxMessage>().CountAsync());
            foreach (var message in await db.Set<OutboxMessage>().AsNoTracking().ToListAsync())
            {
                Assert.Contains("CoachingReminderDueEvent", message.MessageType);
                Assert.DoesNotContain("Private", message.Body);
            }
            var export = await new CoachingDataExportRepository(db).ExportStudentDataAsync(student, clock.Now, default);
            Assert.Equal(2, export.Reminders!.Count);
            var pending = await db.StudyPlanTasks.Where(x => x.RevisionId == plan.Id).ToListAsync();
            foreach (var task in pending) task.Reschedule(date.AddDays(1)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            clock.Now = clock.Now.AddDays(1); Assert.Equal(1, await processor.ProcessAsync()); Assert.Equal(0, await processor.ProcessAsync());
            var requestId = Guid.NewGuid(); var eventId = Guid.NewGuid();
            await new CoachingErasureAssessmentService(db, clock).AssessAsync(new(eventId, requestId, student, clock.Now.UtcDateTime, true, PersonalDataScope.Coaching), default);
            await new CoachingErasureExecutionService(db, new CoachingStudyPlanningPrivacyTests.NoAttachments(), clock)
                .ExecuteAsync(new(eventId, requestId, student, clock.Now.UtcDateTime, PersonalDataScope.Coaching), default);
            Assert.False(await db.CoachingReminderReceipts.AnyAsync(x => x.RecipientUserId == student));
            var remaining = Assert.Single(await db.CoachingReminderReceipts.AsNoTracking().ToListAsync()); Assert.Equal(teacher, remaining.RecipientUserId);
            Assert.Single(await db.Set<OutboxMessage>().AsNoTracking().ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task FailedSaveDoesNotMarkReminderSentOrLeaveOutboxMessagesForLaterWrites()
    {
        var fault = new ReminderFault(); var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(1).AddHours(6), TimeSpan.Zero) };
        await using var provider = Services(fault).BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan"); plan.Activate();
            db.AddRange(plan, StudyPlanTask.Create(plan, DateOnly.FromDateTime(clock.Now.UtcDateTime), "Task", 30)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var processor = new CoachingReminderProcessor(db, new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()), clock);
            fault.Enabled = true; await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
            Assert.Empty(await db.CoachingReminderReceipts.AsNoTracking().ToListAsync()); Assert.Empty(await db.Set<OutboxMessage>().AsNoTracking().ToListAsync());
            fault.Enabled = false; Assert.Equal(1, await processor.ProcessAsync()); Assert.Equal(0, await processor.ProcessAsync());
            Assert.Single(await db.CoachingReminderReceipts.AsNoTracking().ToListAsync()); Assert.Single(await db.Set<OutboxMessage>().AsNoTracking().ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private ServiceCollection Services(ReminderFault? fault = null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddDbContext<CoachingDbContext>(options => {
            options.UseNpgsql(postgres.ConnectionString, pg => pg.EnableRetryOnFailure());
            if (fault is not null) options.AddInterceptors(fault);
        });
        services.AddMassTransit(bus => {
            bus.AddEntityFrameworkOutbox<CoachingDbContext>(outbox => { outbox.UsePostgres(); outbox.UseBusOutbox(); });
            bus.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });
        return services;
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class ReminderFault : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<CoachingReminderReceipt>().Any(x => x.State == EntityState.Added)) throw new InvalidOperationException("Simulated reminder save failure.");
            return ValueTask.FromResult(result);
        }
    }
}
