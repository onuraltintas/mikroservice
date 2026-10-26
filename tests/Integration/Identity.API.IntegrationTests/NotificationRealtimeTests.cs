using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Notification.API.Hubs;
using Notification.API.Services;
using Notification.Application.Configuration;
using Notification.Application.Messaging;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Realtime pushes go through the outbox, so only committed notifications are shown,
/// and notification texts show Turkey time for the UTC timestamps services publish.
/// </summary>
public sealed class NotificationRealtimeTests
{
    [Fact]
    public async Task Manager_StoresTheNotification_AndOnlyRequestsAPush()
    {
        await using var db = InMemory();
        var published = new RecordingPublishEndpoint();
        var user = Guid.NewGuid();

        await new NotificationManager(db, published.Endpoint).SendNotificationAsync(user, "Başlık", "Mesaj", "Info");

        var stored = await db.Notifications.SingleAsync();
        published.Messages.Should().ContainSingle()
            .Which.Should().Be(new NotificationRealtimeRequested(stored.Id, user));
    }

    [Fact]
    public async Task Manager_RequestsNoPush_ForAnErasedRecipient()
    {
        await using var db = InMemory();
        var user = Guid.NewGuid();
        db.ErasedRecipients.Add(NotificationErasedRecipient.Create(user, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var published = new RecordingPublishEndpoint();

        await new NotificationManager(db, published.Endpoint).SendNotificationAsync(user, "Başlık", "Mesaj", "Info");

        published.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Consumer_PushesACommittedNotification_ToItsRecipient()
    {
        await using var db = InMemory();
        var item = NotificationItem.Create(Guid.NewGuid(), "Başlık", "Mesaj", "Info", null, null);
        db.Notifications.Add(item);
        await db.SaveChangesAsync();
        var hub = new RecordingHub();

        (await new NotificationRealtimeConsumer(db, hub).DeliverAsync(new(item.Id, item.UserId), CancellationToken.None))
            .Should().BeTrue();

        hub.Sends.Should().ContainSingle().Which.Should().Be((item.UserId.ToString(), "ReceiveNotification"));
    }

    [Fact]
    public async Task Consumer_PushesNothing_WhenTheRowIsMissingOrBelongsToSomeoneElse()
    {
        await using var db = InMemory();
        var item = NotificationItem.Create(Guid.NewGuid(), "Başlık", "Mesaj", "Info", null, null);
        db.Notifications.Add(item);
        await db.SaveChangesAsync();
        var hub = new RecordingHub();
        var consumer = new NotificationRealtimeConsumer(db, hub);

        (await consumer.DeliverAsync(new(Guid.NewGuid(), item.UserId), CancellationToken.None)).Should().BeFalse("rolled back");
        (await consumer.DeliverAsync(new(item.Id, Guid.NewGuid()), CancellationToken.None)).Should().BeFalse("wrong recipient");
        hub.Sends.Should().BeEmpty();
    }

    [Theory]
    [InlineData("2026-10-26T06:30:00Z", "26.10.2026 09:30")]
    [InlineData("2026-12-31T22:15:00Z", "01.01.2027 01:15")]
    public void TurkeyTime_ShowsUtcEventTimesInLocalTime(string utc, string expected)
    {
        var value = DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture);

        TurkeyTime.Format(value).Should().Be(expected);
        TurkeyTime.Format(value.UtcDateTime).Should().Be(expected);
        TurkeyTime.Format(DateTime.SpecifyKind(value.UtcDateTime, DateTimeKind.Unspecified))
            .Should().Be(expected, "services publish UTC; a kindless value is not the container's local time");
    }

    [Fact]
    public void NotificationTexts_DoNotFormatRawTimestamps()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "EduPlatform.sln"))) root = root.Parent!;
        var consumers = Path.Combine(root.FullName, "services", "notification-service", "Notification.Application", "Consumers");

        foreach (var file in Directory.GetFiles(consumers, "*.cs"))
        {
            File.ReadAllText(file).Should().NotContain(":dd.MM.yyyy HH:mm}", $"{Path.GetFileName(file)} must use TurkeyTime");
        }
    }

    private static NotificationDbContext InMemory() => new(new DbContextOptionsBuilder<NotificationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class RecordingHub : IHubContext<NotificationHub>
    {
        public ConcurrentQueue<(string User, string Method)> Sends { get; } = new();
        public IHubClients Clients { get; }
        public IGroupManager Groups => throw new NotSupportedException();

        public RecordingHub()
        {
            Clients = DispatchProxy.Create<IHubClients, ClientsProxy>();
            ((ClientsProxy)Clients).Hub = this;
        }

        public class ClientsProxy : DispatchProxy
        {
            public RecordingHub Hub = null!;

            protected override object? Invoke(MethodInfo? method, object?[]? args) =>
                method?.Name == "User" ? new Client(Hub, (string)args![0]!) : throw new NotSupportedException();
        }

        private sealed class Client(RecordingHub hub, string user) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                hub.Sends.Enqueue((user, method));
                return Task.CompletedTask;
            }
        }
    }
}

/// <summary>Captures messages passed to any IPublishEndpoint.Publish overload.</summary>
public sealed class RecordingPublishEndpoint
{
    public ConcurrentQueue<object> Messages { get; } = new();
    public IPublishEndpoint Endpoint { get; }

    public RecordingPublishEndpoint()
    {
        Endpoint = DispatchProxy.Create<IPublishEndpoint, Proxy>();
        ((Proxy)(object)Endpoint).Owner = this;
    }

    public class Proxy : DispatchProxy
    {
        public RecordingPublishEndpoint Owner = null!;

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name != "Publish" || args is not { Length: > 0 } || args[0] is null)
                throw new NotSupportedException(method?.Name);
            Owner.Messages.Enqueue(args[0]!);
            return Task.CompletedTask;
        }
    }
}
