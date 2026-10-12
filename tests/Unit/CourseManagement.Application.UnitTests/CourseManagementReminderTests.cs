using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

public sealed class CourseManagementReminderTests
{
    [Fact]
    public void ReminderCanBeCancelledBeforeQueueingAndThenCannotBePublished()
    {
        var reminder = CreateReminder();

        Assert.True(reminder.Cancel());
        Assert.Equal(CourseManagementReminderStatus.Cancelled, reminder.Status);
        Assert.False(reminder.MarkQueued(DateTimeOffset.UtcNow));
        Assert.Null(reminder.QueuedAt);
    }

    [Fact]
    public void QueuedReminderCannotBeCancelledOrQueuedTwice()
    {
        var reminder = CreateReminder();
        var queuedAt = DateTimeOffset.UtcNow;

        Assert.True(reminder.MarkQueued(queuedAt));
        Assert.Equal(CourseManagementReminderStatus.Queued, reminder.Status);
        Assert.Equal(queuedAt, reminder.QueuedAt);
        Assert.False(reminder.Cancel());
        Assert.False(reminder.MarkQueued(queuedAt.AddMinutes(1)));
        Assert.Equal(queuedAt, reminder.QueuedAt);
    }

    private static CourseManagementReminder CreateReminder() => CourseManagementReminder.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CourseManagementReminderType.Lesson,
        DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
