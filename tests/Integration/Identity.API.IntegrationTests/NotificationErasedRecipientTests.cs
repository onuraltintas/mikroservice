using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class NotificationErasedRecipientTests(PostgresFixture postgres)
{
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
