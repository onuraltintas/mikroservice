using System.Reflection;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Notification.API.Hubs;
using Notification.API.Services;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class NotificationRealtimeErasureTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ErasureBetweenPersistenceAndHubDispatch_PreventsRealtimeDelivery()
    {
        var subject = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        var hook = new AfterCommit(async () =>
        {
            await using var erasureDb = new NotificationDbContext(options);
            await new NotificationErasureExecutionService(erasureDb, TimeProvider.System).ExecuteAsync(
                new(Guid.NewGuid(), Guid.NewGuid(), subject, DateTime.UtcNow, PersonalDataScope.Account), CancellationToken.None);
        });
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(postgres.ConnectionString).AddInterceptors(hook).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            hook.Enabled = true;
            var hub = new Hub();
            await new NotificationManager(db, hub).SendNotificationAsync(subject, "Plan", "Plan", "Info");
            Assert.Equal(0, hub.Client.Calls);
            Assert.False(await db.Notifications.AnyAsync(x => x.UserId == subject));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class AfterCommit(Func<Task> action) : DbTransactionInterceptor
    {
        public bool Enabled;
        public override async Task TransactionCommittedAsync(System.Data.Common.DbTransaction transaction,
            TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (!Enabled) return;
            Enabled = false; await action();
        }
    }
    private sealed class Client : IClientProxy
    {
        public int Calls;
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        { Calls++; return Task.CompletedTask; }
    }
    public class ClientsProxy : DispatchProxy
    {
        public IClientProxy Client = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "User" ? Client : throw new NotSupportedException();
    }
    private sealed class Hub : IHubContext<NotificationHub>
    {
        public Client Client { get; } = new();
        public IHubClients Clients { get; }
        public IGroupManager Groups => throw new NotSupportedException();
        public Hub()
        {
            Clients = DispatchProxy.Create<IHubClients, ClientsProxy>();
            ((ClientsProxy)Clients).Client = Client;
        }
    }
}
