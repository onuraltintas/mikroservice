using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Notification.Application.Commands.ReplyToSupportRequest;
using Notification.Application.Interfaces;
using Notification.Application.Services;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Derived message ids are idempotency keys already stored in production; merging the
/// helpers must not change a single one.
/// </summary>
public sealed class NotificationMessageIdTests
{
    private static readonly Guid Event = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly Guid Recipient = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

    [Fact]
    public void DispatcherIds_MatchTheIdsStoredBeforeTheMerge()
    {
        new CoachingNotificationDispatcher(new NoNotifications()).RecipientMessageId(Event, Recipient)
            .Should().Be(Legacy($"coaching-notification:{Event:N}:{Recipient:N}"));
        new CourseManagementNotificationDispatcher(new NoNotifications()).RecipientMessageId(Event, Recipient)
            .Should().Be(Legacy($"course-management-notification:{Event:N}:{Recipient:N}"));
    }

    [Fact]
    public void ReplyIds_MatchTheIdsStoredBeforeTheMerge()
    {
        ReplyToSupportRequestHandler.ReplyMessageId(Event, "Sorununuz çözüldü.")
            .Should().Be(Legacy($"{Event:N}:Sorununuz çözüldü."));
    }

    [Fact]
    public async Task Dispatcher_SkipsEmptyAndRepeatedRecipients()
    {
        var notifications = new NoNotifications();
        await new CoachingNotificationDispatcher(notifications).SendAsync(
            Event, [Recipient, Guid.Empty, Recipient], "t", "m", "Type", null, CancellationToken.None);

        notifications.Recipients.Should().Equal(Recipient);
    }

    private static Guid Legacy(string canonical) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)).AsSpan(0, 16));

    private sealed class NoNotifications : INotificationService
    {
        public List<Guid> Recipients { get; } = [];

        public Task SendNotificationAsync(Guid userId, string title, string message, string type,
            string? relatedEntityId = null, Guid? sourceMessageId = null)
        {
            Recipients.Add(userId);
            return Task.CompletedTask;
        }
    }
}
