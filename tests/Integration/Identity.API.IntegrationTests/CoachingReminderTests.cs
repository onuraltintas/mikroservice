using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Application.Interfaces;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Coaching.Infrastructure.Reminders;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingReminderTests(PostgresFixture postgres)
{
    [Fact]
    public async Task OverdueFeesProduceOnePrivateStudentReminderPerLocalDayAndStopAfterSettlement()
    {
        var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(1).AddHours(6), TimeSpan.Zero) };
        var today = DateOnly.FromDateTime(clock.Now.UtcDateTime); var student = Guid.NewGuid(); var other = Guid.NewGuid(); var actor = Guid.NewGuid();
        await using var provider = Services().BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try {
            CoachingFeeAgreement Fee(Guid owner, DateOnly due) => CoachingFeeAgreement.Create(Guid.NewGuid(), owner, Guid.NewGuid(), null, actor,
                "Private fee title", 100m, [new(due, 100m)], clock.Now.UtcDateTime);
            var first = Fee(student, today.AddDays(-1)); var second = Fee(student, today.AddDays(-2));
            var future = Fee(other, today.AddDays(1)); var cancelled = Fee(other, today.AddDays(-1)); cancelled.Cancel(actor, "Cancelled", clock.Now.UtcDateTime);
            db.AddRange(first, second, future, cancelled); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var processor = new CoachingReminderProcessor(db, new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()), clock);
            clock.Now = clock.Now.AddMinutes(-1); Assert.Equal(0, await processor.ProcessAsync());
            clock.Now = clock.Now.AddMinutes(1); Assert.Equal(1, await processor.ProcessAsync()); Assert.Equal(0, await processor.ProcessAsync());
            var receipt = Assert.Single(await db.CoachingReminderReceipts.AsNoTracking().ToListAsync());
            Assert.Equal("FeeOverdue", receipt.Kind); Assert.Equal(student, receipt.RecipientUserId); Assert.Equal(student, receipt.SourceId);
            var message = Assert.Single(await db.Set<OutboxMessage>().AsNoTracking().ToListAsync()); Assert.DoesNotContain("Private fee title", message.Body);
            foreach (var fee in await db.CoachingFeeAgreements.Where(x => x.StudentId == student).ToListAsync())
                db.CoachingFeePayments.Add(fee.AddPayment(Guid.NewGuid(), actor, fee.Balance, CoachingFeePaymentMethod.Cash, today, clock.Now.UtcDateTime, null));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear(); clock.Now = clock.Now.AddDays(1);
            Assert.Equal(0, await processor.ProcessAsync()); Assert.Single(await db.CoachingReminderReceipts.AsNoTracking().ToListAsync());
        } finally { await db.Database.EnsureDeletedAsync(); }
    }
    [Fact]
    public async Task ConcurrentEarlierPlanDoesNotShiftAQueuedReminderOntoTheNextPage()
    {
        var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(1).AddHours(6), TimeSpan.Zero) };
        var date = DateOnly.FromDateTime(clock.Now.UtcDateTime);
        await using var provider = Services().BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            for (var i = 1; i <= 100; i++)
            {
                var student = Guid.Parse($"10000000-0000-0000-0000-{i:D12}"); var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Plan"); plan.Activate();
                db.AddRange(plan, StudyPlanTask.Create(plan, date, "Task", 30), StudyAvailability.Create(student, i == 1 ? "Europe/London" : "Europe/Istanbul"));
            }
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var publisher = new PageInsertPublisher(new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()), async () => {
                await using var concurrent = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
                var plan = StudyPlanRevision.Create(Guid.Parse("00000001-0000-0000-0000-000000000001"), Guid.NewGuid(), 1, "Concurrent plan"); plan.Activate();
                concurrent.AddRange(plan, StudyPlanTask.Create(plan, date, "Task", 30)); await concurrent.SaveChangesAsync();
            });
            var processor = new CoachingReminderProcessor(db, publisher, clock);
            Assert.Equal(99, await processor.ProcessAsync()); Assert.Equal(1, await processor.ProcessAsync());
            Assert.Equal(100, await db.CoachingReminderReceipts.CountAsync()); Assert.Equal(100, await db.Set<OutboxMessage>().CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task AlreadyQueuedStudyRemindersDoNotLockStudentsAgain()
    {
        var counter = new StudyLockCounter(); var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(1).AddHours(6), TimeSpan.Zero) };
        await using var provider = Services(counter: counter).BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan"); plan.Activate();
            db.AddRange(plan, StudyPlanTask.Create(plan, DateOnly.FromDateTime(clock.Now.UtcDateTime), "Task", 30)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var processor = new CoachingReminderProcessor(db, new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()), clock);
            Assert.Equal(1, await processor.ProcessAsync()); Assert.Equal(1, counter.Count); counter.Count = 0;
            Assert.Equal(0, await processor.ProcessAsync()); Assert.Equal(0, counter.Count);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TransientCommitFailureQueuesExactlyOneReminder(bool beforeCommit)
    {
        var ack = new ReminderAckFault { BeforeCommit = beforeCommit };
        var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(1).AddHours(6), TimeSpan.Zero) };
        await using var provider = Services(ack: ack).BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan"); plan.Activate();
            db.AddRange(plan, StudyPlanTask.Create(plan, DateOnly.FromDateTime(clock.Now.UtcDateTime), "Task", 30)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var processor = new CoachingReminderProcessor(db, new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()), clock);
            ack.Enabled = true; Assert.Equal(1, await processor.ProcessAsync()); Assert.Equal(0, await processor.ProcessAsync());
            Assert.Single(await db.CoachingReminderReceipts.AsNoTracking().ToListAsync()); Assert.Single(await db.Set<OutboxMessage>().AsNoTracking().ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task WorkerLogsFailureAndConcurrentRestartsRecoverWithoutDuplicateNotifications()
    {
        var fault = new ReminderFault(); var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(1).AddHours(6), TimeSpan.Zero) };
        await using var provider = Services(fault, clock: clock).BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan"); plan.Activate();
            db.AddRange(plan, StudyPlanTask.Create(plan, DateOnly.FromDateTime(clock.Now.UtcDateTime), "Task", 30)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var logger = new WorkerLogger(); fault.Enabled = true;
            using (var worker = new CoachingReminderWorker(provider.GetRequiredService<IServiceScopeFactory>(), clock, logger))
            {
                try { await worker.StartAsync(default); await logger.Logged.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
                finally { await worker.StopAsync(default); }
            }
            Assert.Empty(await db.CoachingReminderReceipts.AsNoTracking().ToListAsync()); fault.Enabled = false;
            using var first = new CoachingReminderWorker(provider.GetRequiredService<IServiceScopeFactory>(), clock, NullLogger<CoachingReminderWorker>.Instance);
            using var second = new CoachingReminderWorker(provider.GetRequiredService<IServiceScopeFactory>(), clock, NullLogger<CoachingReminderWorker>.Instance);
            try
            {
                await first.StartAsync(default); await second.StartAsync(default);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!await db.CoachingReminderReceipts.AnyAsync() && DateTime.UtcNow < deadline) await Task.Delay(20);
            }
            finally { await first.StopAsync(default); await second.StopAsync(default); }
            Assert.Single(await db.CoachingReminderReceipts.AsNoTracking().ToListAsync()); Assert.Single(await db.Set<OutboxMessage>().AsNoTracking().ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task SessionBatchContinuesToLastRecipientOnTheNextCycle()
    {
        var clock = new Clock { Now = new(DateTime.UtcNow.Date.AddDays(1).AddHours(6), TimeSpan.Zero) };
        await using var provider = Services().BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var session = CoachingSession.Create(Guid.NewGuid(), "Group", clock.Now.UtcDateTime.AddMinutes(30), SessionType.Group);
            session.AddStudents(Enumerable.Range(0, 100).Select(_ => Guid.NewGuid())); db.Add(session); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var processor = new CoachingReminderProcessor(db, new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()), clock);
            Assert.Equal(100, await processor.ProcessAsync()); Assert.Equal(1, await processor.ProcessAsync()); Assert.Equal(0, await processor.ProcessAsync());
            Assert.Equal(101, await db.CoachingReminderReceipts.CountAsync()); Assert.Equal(101, await db.Set<OutboxMessage>().CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
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

    [Fact]
    public async Task OverdueWorkProducesOneDailySummaryAndStopsAfterCompletion()
    {
        var clock = new Clock { Now = new(new DateTime(2026, 10, 23, 6, 0, 0, DateTimeKind.Utc)) };
        await using var provider = Services(clock: clock).BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
        await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var student = Guid.NewGuid(); var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Geciken test"); plan.Activate();
            var task = StudyPlanTask.Create(plan, new(2026, 10, 22), "Bekleyen", 30);
            db.AddRange(plan, task, StudyAvailability.Create(student, "Europe/Istanbul")); await db.SaveChangesAsync();
            var processor = scope.ServiceProvider.GetRequiredService<CoachingReminderProcessor>();
            Assert.Equal(1, await processor.ProcessAsync());
            Assert.Equal("StudyOverdue", (await db.CoachingReminderReceipts.SingleAsync()).Kind);
            Assert.Equal(0, await processor.ProcessAsync());
            task.Complete(25, 8); await db.SaveChangesAsync(); clock.Now = clock.Now.AddDays(1);
            Assert.Equal(0, await processor.ProcessAsync());
            Assert.True(task.IsCompleted); Assert.Equal(25, task.ActualMinutes);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private ServiceCollection Services(ReminderFault? fault = null, ReminderAckFault? ack = null, TimeProvider? clock = null, StudyLockCounter? counter = null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddDbContext<CoachingDbContext>(options => {
            options.UseNpgsql(postgres.ConnectionString, pg => pg.EnableRetryOnFailure());
            if (fault is not null) options.AddInterceptors(fault);
            if (ack is not null) options.AddInterceptors(ack);
            if (counter is not null) options.AddInterceptors(counter);
        });
        services.AddSingleton<TimeProvider>(clock ?? TimeProvider.System);
        services.AddScoped<ICoachingEventPublisher, MassTransitCoachingEventPublisher>(); services.AddScoped<CoachingReminderProcessor>();
        services.AddMassTransit(bus => {
            bus.AddEntityFrameworkOutbox<CoachingDbContext>(outbox => { outbox.UsePostgres(); outbox.UseBusOutbox(); });
            bus.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });
        return services;
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class PageInsertPublisher(ICoachingEventPublisher inner, Func<Task> insert) : ICoachingEventPublisher
    {
        private int count;
        public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
        {
            await inner.PublishAsync(message, ct);
            if (message is CoachingReminderDueEvent { Kind: "StudyDay" } && ++count == 99) await insert();
        }
    }
    private sealed class StudyLockCounter : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(System.Data.Common.DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock") && command.Parameters.Cast<System.Data.Common.DbParameter>().Any(x => x.Value is string key && key.StartsWith("coaching-study-plan:"))) Count++;
            return ValueTask.FromResult(result);
        }
    }
    private sealed class WorkerLogger : ILogger<CoachingReminderWorker>
    {
        public TaskCompletionSource Logged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        { if (level == LogLevel.Error && error is not null) Logged.TrySetResult(); }
    }
    private sealed class ReminderAckFault : DbTransactionInterceptor
    {
        public bool Enabled; public bool BeforeCommit;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(System.Data.Common.DbTransaction transaction, TransactionEventData data, InterceptionResult result, CancellationToken ct = default)
        {
            if (Enabled && BeforeCommit) { Enabled = false; throw new Npgsql.NpgsqlException("Simulated pre-commit connection loss.", new IOException("Connection interrupted.")); }
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(System.Data.Common.DbTransaction transaction, TransactionEndEventData data, CancellationToken ct = default)
        {
            if (Enabled && !BeforeCommit) { Enabled = false; throw new Npgsql.NpgsqlException("Simulated lost COMMIT acknowledgement.", new IOException("Connection interrupted.")); }
            return Task.CompletedTask;
        }
    }
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
