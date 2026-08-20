using FluentAssertions;
using Notification.Application.Interfaces;
using Notification.Application.Services;

namespace Identity.API.IntegrationTests;

public sealed class CoachingNotificationDispatcherTests
{
    [Fact]
    public async Task SendAsync_FansOutDistinctStableIdsPerRecipient()
    {
        var eventMessageId = Guid.NewGuid();
        var firstStudentId = Guid.NewGuid();
        var secondStudentId = Guid.NewGuid();
        var notifications = new RecordingNotificationService();
        var dispatcher = new CoachingNotificationDispatcher(notifications);

        await dispatcher.SendAsync(
            eventMessageId,
            new[] { firstStudentId, firstStudentId, secondStudentId, Guid.Empty },
            "Yeni ödev",
            "Bir ödev oluşturuldu.",
            "AssignmentCreated",
            "assignment-1",
            CancellationToken.None);

        notifications.Calls.Should().HaveCount(2);
        notifications.Calls.Select(call => call.UserId)
            .Should().BeEquivalentTo(new[] { firstStudentId, secondStudentId });

        var firstSourceId = notifications.Calls.Single(call => call.UserId == firstStudentId).SourceMessageId;
        var secondSourceId = notifications.Calls.Single(call => call.UserId == secondStudentId).SourceMessageId;

        firstSourceId.Should().NotBeNull();
        secondSourceId.Should().NotBeNull();
        firstSourceId.Should().NotBe(secondSourceId);

        await dispatcher.SendAsync(
            eventMessageId,
            new[] { firstStudentId },
            "Yeni ödev",
            "Bir ödev oluşturuldu.",
            "AssignmentCreated",
            "assignment-1",
            CancellationToken.None);

        notifications.Calls[^1].SourceMessageId.Should().Be(firstSourceId);
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<NotificationCall> Calls { get; } = [];

        public Task SendNotificationAsync(
            Guid userId,
            string title,
            string message,
            string type,
            string? relatedEntityId = null,
            Guid? sourceMessageId = null)
        {
            Calls.Add(new NotificationCall(userId, title, message, type, relatedEntityId, sourceMessageId));
            return Task.CompletedTask;
        }
    }

    private sealed record NotificationCall(
        Guid UserId,
        string Title,
        string Message,
        string Type,
        string? RelatedEntityId,
        Guid? SourceMessageId);
}
