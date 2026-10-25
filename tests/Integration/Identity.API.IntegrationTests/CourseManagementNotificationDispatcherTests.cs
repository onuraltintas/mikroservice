using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Application.Interfaces;
using Notification.Application.Services;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;

namespace Identity.API.IntegrationTests;

public sealed class CourseManagementNotificationDispatcherTests
{
    [Fact]
    public async Task SendAsync_UsesStableDistinctKeysForRecipientsAndEventRetries()
    {
        var eventMessageId = Guid.NewGuid();
        var firstStudentId = Guid.NewGuid();
        var secondStudentId = Guid.NewGuid();
        var notifications = new RecordingNotificationService();
        var dispatcher = new CourseManagementNotificationDispatcher(notifications);

        await dispatcher.SendAsync(eventMessageId,
            [firstStudentId, firstStudentId, secondStudentId, Guid.Empty],
            "Ders saati değişti", "Dersin yeni saati güncellendi.",
            "CourseManagement.LessonRescheduled", "lesson-1", CancellationToken.None);

        notifications.Calls.Should().HaveCount(2);
        notifications.Calls.Select(call => call.UserId)
            .Should().BeEquivalentTo([firstStudentId, secondStudentId]);

        var firstSourceId = notifications.Calls.Single(call => call.UserId == firstStudentId).SourceMessageId;
        var secondSourceId = notifications.Calls.Single(call => call.UserId == secondStudentId).SourceMessageId;
        firstSourceId.Should().NotBeNull();
        secondSourceId.Should().NotBeNull();
        Assert.NotEqual(firstSourceId, secondSourceId);

        await dispatcher.SendAsync(eventMessageId, [firstStudentId],
            "Ders saati değişti", "Dersin yeni saati güncellendi.",
            "CourseManagement.LessonRescheduled", "lesson-1", CancellationToken.None);

        notifications.Calls[^1].SourceMessageId.Should().Be(firstSourceId);
    }

    [Fact]
    public async Task ReplayedEvent_PersistsOnlyOneNotificationPerRecipient()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new NotificationDbContext(options);
        var eventMessageId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var dispatcher = new CourseManagementNotificationDispatcher(new PersistingNotificationService(db));

        await dispatcher.SendAsync(eventMessageId, [recipientId],
            "Yeni ders planlandı", "Dersin yarın planlandı.",
            "CourseManagement.LessonScheduled", "lesson-1", CancellationToken.None);
        await dispatcher.SendAsync(eventMessageId, [recipientId],
            "Yeni ders planlandı", "Dersin yarın planlandı.",
            "CourseManagement.LessonScheduled", "lesson-1", CancellationToken.None);

        (await db.Notifications.CountAsync()).Should().Be(1);
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<NotificationCall> Calls { get; } = [];

        public Task SendNotificationAsync(Guid userId, string title, string message, string type,
            string? relatedEntityId = null, Guid? sourceMessageId = null)
        {
            Calls.Add(new NotificationCall(userId, title, message, type, relatedEntityId, sourceMessageId));
            return Task.CompletedTask;
        }
    }

    private sealed record NotificationCall(Guid UserId, string Title, string Message,
        string Type, string? RelatedEntityId, Guid? SourceMessageId);

    private sealed class PersistingNotificationService(NotificationDbContext db) : INotificationService
    {
        public async Task SendNotificationAsync(Guid userId, string title, string message, string type,
            string? relatedEntityId = null, Guid? sourceMessageId = null)
        {
            await NotificationRecipientWrites.PersistAsync(db, NotificationItem.Create(
                userId, title, message, type, relatedEntityId, sourceMessageId));
        }
    }
}
