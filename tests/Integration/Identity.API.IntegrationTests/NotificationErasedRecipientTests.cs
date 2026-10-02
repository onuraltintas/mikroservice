using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.DataProtection;
using Notification.Application.Commands.SubmitSupportRequest;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class NotificationErasedRecipientTests(PostgresFixture postgres)
{
    [Fact]
    public async Task HistoricalReceiptReplay_AddsMissingRecipientProtection()
    {
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var subject = Guid.NewGuid(); var requestId = Guid.NewGuid();
            db.ErasureExecutions.Add(NotificationErasureExecutionReceipt.Complete(requestId, 0, DateTime.UtcNow));
            await db.SaveChangesAsync();
            await new NotificationErasureExecutionService(db, TimeProvider.System).ExecuteAsync(
                new(Guid.NewGuid(), requestId, subject, DateTime.UtcNow, PersonalDataScope.Account), CancellationToken.None);
            Assert.True(await db.ErasedRecipients.AnyAsync(x => x.UserId == subject));
            Assert.Null(await NotificationRecipientWrites.PersistAsync(db, NotificationItem.Create(subject, "Late", "Late", "Info")));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task ConcurrentNotification_WaitsForErasureAndCannotRecreateSubjectData()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        await using var db = new NotificationDbContext(options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var subject = Guid.NewGuid();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var key = $"notification-recipient:{subject:N}";
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
            var writer = Task.Run(async () => {
                await using var concurrent = new NotificationDbContext(options);
                return await NotificationRecipientWrites.PersistAsync(concurrent, NotificationItem.Create(subject, "Late", "Late", "Info"));
            });
            var deadline = DateTime.UtcNow.AddSeconds(5); var waiting = 0;
            while (waiting == 0 && DateTime.UtcNow < deadline)
            {
                waiting = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted").SingleAsync();
                if (waiting == 0) await Task.Delay(20);
            }
            Assert.True(waiting > 0, "Concurrent writer must actually wait on the recipient lock.");
            await new NotificationErasureExecutionService(db, TimeProvider.System).ExecuteAsync(
                new(Guid.NewGuid(), Guid.NewGuid(), subject, DateTime.UtcNow, PersonalDataScope.Account), CancellationToken.None);
            await transaction.CommitAsync();
            Assert.Null(await writer);
            Assert.Equal(0, await db.Notifications.CountAsync(x => x.UserId == subject));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task Migration_RefusesRollbackThatWouldLoseErasedRecipientProtection()
    {
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var previous = migrations[^2]; var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(previous); await migrator.MigrateAsync();
            db.ErasedRecipients.Add(NotificationErasedRecipient.Create(Guid.NewGuid(), DateTime.UtcNow)); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync(previous));
            Assert.Equal(1, await db.ErasedRecipients.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task Erasure_PreventsLateQueuedNotificationsAndKeepsOtherRecipients()
    {
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var subject = Guid.NewGuid(); var other = Guid.NewGuid();
            Assert.NotNull(await NotificationRecipientWrites.PersistAsync(db, NotificationItem.Create(subject, "Old", "Old", "Info")));
            var erasure = new NotificationErasureExecutionService(db, TimeProvider.System);
            var request = new PersonalDataErasureExecutionRequestedV1(Guid.NewGuid(), Guid.NewGuid(), subject,
                DateTime.UtcNow, PersonalDataScope.Account);
            await erasure.ExecuteAsync(request, CancellationToken.None);
            await new EmailDeliveryQueue(db, new EphemeralDataProtectionProvider()).QueueAsync(
                Guid.NewGuid(), "LateEvent", "test@example.com", "Late", "Late", subject);
            Assert.False(await db.EmailDeliveries.AnyAsync(x => x.SubjectUserId == subject));
            await using var supportDb = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(postgres.ConnectionString).Options);
            var support = await new SubmitSupportRequestHandler(supportDb, new EmailDeliveryQueue(supportDb, new EphemeralDataProtectionProvider()))
                .Handle(new("Test", "User", "test@example.com", "Late support", "Late support message", "late-support-idempotency", subject), CancellationToken.None);
            Assert.False(support.IsSuccess);
            Assert.False(await db.SupportRequests.AnyAsync(x => x.SubjectUserId == subject));
            Assert.Null(await NotificationRecipientWrites.PersistAsync(db, NotificationItem.Create(subject, "Late", "Late", "StudyPlanPublished")));
            Assert.NotNull(await NotificationRecipientWrites.PersistAsync(db, NotificationItem.Create(other, "Keep", "Keep", "Info")));
            Assert.Equal(0, await db.Notifications.CountAsync(x => x.UserId == subject));
            Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == other));
            Assert.Equal(1, await db.ErasedRecipients.CountAsync());
            await erasure.ExecuteAsync(request, CancellationToken.None);
            Assert.Equal(1, await db.ErasedRecipients.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
