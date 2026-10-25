using EduPlatform.Shared.Contracts.Events.CourseManagement;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;
using Notification.Application.Services;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;
using Shared.IntegrationTests.Fixtures;
using Testcontainers.RabbitMq;

namespace CourseManagement.IntegrationTests;

public sealed class CourseManagementNotificationRabbitMqPostgresTests
{
    [Fact]
    public async Task BusOutboxAndReminderConsumer_RollBackAndRetryAcrossRabbitMqAndPostgres()
    {
        var postgres = new PostgresFixture();
        var rabbitMq = new RabbitMqBuilder("rabbitmq:4.3.4-management-alpine")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithPortBinding(5673, 5672)
            .WithCleanUp(true)
            .Build();
        await postgres.InitializeAsync();

        try
        {
            await rabbitMq.StartAsync();
            var courseManagementConnection = await CreateDatabaseConnectionStringAsync(postgres.ConnectionString,
                $"course_management_{Guid.NewGuid():N}");
            var notificationConnection = await CreateDatabaseConnectionStringAsync(postgres.ConnectionString,
                $"notification_{Guid.NewGuid():N}");
            var courseManagementOptions = new DbContextOptionsBuilder<CourseManagementDbContext>()
                .UseNpgsql(courseManagementConnection)
                .Options;
            var notificationOptions = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseNpgsql(notificationConnection)
                .Options;
            await using (var migrationDb = new CourseManagementDbContext(courseManagementOptions))
                await migrationDb.Database.MigrateAsync();
            await using (var migrationDb = new NotificationDbContext(notificationOptions))
                await migrationDb.Database.MigrateAsync();

            var retryProbe = new RetryProbe();
            var consumerServices = new ServiceCollection();
            consumerServices.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(notificationConnection));
            consumerServices.AddSingleton(retryProbe);
            consumerServices.AddScoped<INotificationService, FailOnceAfterPersistNotificationService>();
            consumerServices.AddScoped<ICourseManagementNotificationDispatcher, CourseManagementNotificationDispatcher>();
            consumerServices.AddMassTransitTestHarness(configurator =>
            {
                configurator.SetTestTimeouts(testTimeout: TimeSpan.FromSeconds(45));
                configurator.AddConsumer<CourseManagementReminderDueConsumer>();
                configurator.AddEntityFrameworkOutbox<NotificationDbContext>(options =>
                {
                    options.UsePostgres();
                    options.UseBusOutbox();
                });
                configurator.UsingRabbitMq((context, bus) =>
                {
                    bus.Host(new Uri(rabbitMq.GetConnectionString()));
                    bus.ReceiveEndpoint("course-management-reminder-due", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Immediate(1));
                        endpoint.UseEntityFrameworkOutbox<NotificationDbContext>(context);
                        endpoint.ConfigureConsumer<CourseManagementReminderDueConsumer>(context);
                    });
                });
            });

            var producerServices = new ServiceCollection();
            producerServices.AddDbContext<CourseManagementDbContext>(options => options.UseNpgsql(courseManagementConnection));
            producerServices.AddMassTransitTestHarness(configurator =>
            {
                configurator.AddEntityFrameworkOutbox<CourseManagementDbContext>(options =>
                {
                    options.UsePostgres();
                    options.UseBusOutbox();
                    options.QueryDelay = TimeSpan.FromMilliseconds(100);
                });
                configurator.UsingRabbitMq((_, bus) => bus.Host(new Uri(rabbitMq.GetConnectionString())));
            });

            await using var consumerProvider = consumerServices.BuildServiceProvider(validateScopes: true);
            await using var producerProvider = producerServices.BuildServiceProvider(validateScopes: true);
            var consumerHarness = consumerProvider.GetRequiredService<ITestHarness>();
            var producerHarness = producerProvider.GetRequiredService<ITestHarness>();
            await consumerHarness.Start();
            await producerHarness.Start();
            try
            {
                var rolledBackMessageId = Guid.NewGuid();
                await using (var rollbackScope = producerProvider.CreateAsyncScope())
                {
                    var db = rollbackScope.ServiceProvider.GetRequiredService<CourseManagementDbContext>();
                    var publisher = rollbackScope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
                    await using var transaction = await db.Database.BeginTransactionAsync();
                    db.Workspaces.Add(CreateWorkspace());
                    await publisher.Publish(CreateReminder(Guid.NewGuid()),
                        publish => publish.MessageId = rolledBackMessageId);
                    await db.SaveChangesAsync();

                    var pendingOutboxCount = await db.Database.SqlQuery<int>(
                        $"SELECT COUNT(*)::int AS \"Value\" FROM \"OutboxMessage\" WHERE \"MessageId\" = {rolledBackMessageId}")
                        .SingleAsync();
                    pendingOutboxCount.Should().Be(1);
                    await transaction.RollbackAsync();
                }

                await using (var verifyRollbackDb = new CourseManagementDbContext(courseManagementOptions))
                {
                    (await verifyRollbackDb.Workspaces.CountAsync()).Should().Be(0);
                    var rolledBackOutboxCount = await verifyRollbackDb.Database.SqlQuery<int>(
                        $"SELECT COUNT(*)::int AS \"Value\" FROM \"OutboxMessage\" WHERE \"MessageId\" = {rolledBackMessageId}")
                        .SingleAsync();
                    rolledBackOutboxCount.Should().Be(0);
                }

                await rabbitMq.StopAsync();
                var recipientId = Guid.NewGuid();
                var eventMessageId = Guid.NewGuid();
                await using (var publishScope = producerProvider.CreateAsyncScope())
                {
                    var db = publishScope.ServiceProvider.GetRequiredService<CourseManagementDbContext>();
                    var publisher = publishScope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
                    db.Workspaces.Add(CreateWorkspace());
                    await publisher.Publish(CreateReminder(recipientId), publish => publish.MessageId = eventMessageId);
                    await db.SaveChangesAsync();
                }

                await using (var pendingOutboxDb = new CourseManagementDbContext(courseManagementOptions))
                {
                    var pendingOutboxCount = await pendingOutboxDb.Database.SqlQuery<int>(
                        $"SELECT COUNT(*)::int AS \"Value\" FROM \"OutboxMessage\" WHERE \"MessageId\" = {eventMessageId}")
                        .SingleAsync();
                    pendingOutboxCount.Should().Be(1);
                }
                await rabbitMq.StartAsync();

                await retryProbe.Completed.Task.WaitAsync(TimeSpan.FromSeconds(45));
                retryProbe.Attempts.Should().Be(2);
                retryProbe.RetrySawPersistedNotification.Should().BeFalse();

                await using var verifyDb = new NotificationDbContext(notificationOptions);
                await verifyDb.Database.OpenConnectionAsync();
                await using var inboxCountCommand = verifyDb.Database.GetDbConnection().CreateCommand();
                inboxCountCommand.CommandText = "SELECT COUNT(*) FROM \"InboxState\"";

                var notificationCount = 0;
                long inboxCount = 0;
                var persistenceDeadline = DateTimeOffset.UtcNow.AddSeconds(10);
                while (DateTimeOffset.UtcNow < persistenceDeadline)
                {
                    notificationCount = await verifyDb.Notifications.AsNoTracking().CountAsync();
                    inboxCount = (long)(await inboxCountCommand.ExecuteScalarAsync())!;
                    if (notificationCount == 1 && inboxCount == 1)
                        break;

                    await Task.Delay(100);
                }

                notificationCount.Should().Be(1, "the consumer transaction commits after the handler returns");
                inboxCount.Should().Be(1);
                var notifications = await verifyDb.Notifications.AsNoTracking().ToListAsync();
                notifications.Should().ContainSingle();
                notifications[0].UserId.Should().Be(recipientId);
                notifications[0].Type.Should().Be("CourseManagement.LessonReminder");

                await using var verifyCourseManagementDb = new CourseManagementDbContext(courseManagementOptions);
                (await verifyCourseManagementDb.Workspaces.CountAsync()).Should().Be(1);
            }
            finally
            {
                await producerHarness.Stop();
                await consumerHarness.Stop();
            }
        }
        finally
        {
            await rabbitMq.DisposeAsync();
            await postgres.DisposeAsync();
        }
    }

    private static Workspace CreateWorkspace()
        => Workspace.Create(Guid.NewGuid(), Guid.NewGuid(), "RabbitMQ test", WorkspaceType.IndependentTeacher);

    private static CourseManagementReminderDueV1 CreateReminder(Guid recipientId)
        => new(Guid.NewGuid(), Guid.NewGuid(), recipientId, "Lesson", DateTimeOffset.UtcNow.AddHours(24));

    private static async Task<string> CreateDatabaseConnectionStringAsync(string adminConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
        return builder.ConnectionString;
    }

    private sealed class RetryProbe
    {
        private int _attempts;
        private int _retrySawPersistedNotification;

        public int Attempts => Volatile.Read(ref _attempts);
        public bool RetrySawPersistedNotification => Volatile.Read(ref _retrySawPersistedNotification) != 0;
        public TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int BeginAttempt() => Interlocked.Increment(ref _attempts);
        public void RecordRetryFoundNotification() => Interlocked.Exchange(ref _retrySawPersistedNotification, 1);
    }

    private sealed class FailOnceAfterPersistNotificationService(
        NotificationDbContext db,
        RetryProbe retryProbe) : INotificationService
    {
        public async Task SendNotificationAsync(
            Guid userId,
            string title,
            string message,
            string type,
            string? relatedEntityId = null,
            Guid? sourceMessageId = null)
        {
            if (sourceMessageId is not { } notificationId)
                throw new InvalidOperationException("A stable source message id is required for retry safety.");

            var attempt = retryProbe.BeginAttempt();
            if (attempt > 1 && await db.Notifications.AsNoTracking()
                    .AnyAsync(notification => notification.Id == notificationId && notification.UserId == userId))
                retryProbe.RecordRetryFoundNotification();

            await NotificationRecipientWrites.PersistAsync(db, NotificationItem.Create(
                userId, title, message, type, relatedEntityId, notificationId));

            if (attempt == 1)
                throw new InvalidOperationException("Simulated transient failure after notification persistence.");

            retryProbe.Completed.TrySetResult(true);
        }
    }
}
