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

    [Fact]
    public void LessonAndAssignmentRemindersAreScheduledTwentyFourHoursBeforeTarget()
    {
        var targetAt = new DateTimeOffset(2026, 11, 18, 15, 30, 0, TimeSpan.Zero);

        Assert.Equal(targetAt.AddHours(-24), CourseManagementReminderSchedule.LessonNotificationTime(targetAt));
        Assert.Equal(targetAt.AddHours(-24), CourseManagementReminderSchedule.AssignmentNotificationTime(targetAt));
    }

    [Fact]
    public void InstallmentReminderUsesThreeDaysBeforeAndDueDayAtNineInIstanbul()
    {
        var schedule = CourseManagementReminderSchedule.InstallmentTimes(new DateOnly(2026, 11, 18));

        Assert.Equal(new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.FromHours(3)), schedule.UpcomingAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(3)), schedule.DueAt);
    }

    [Fact]
    public void ReminderRequiresNotificationBeforeTarget()
    {
        Assert.Throws<ArgumentException>(() => CourseManagementReminder.Create(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), CourseManagementReminderType.Lesson,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void DueDayReminderCanBeQueuedAtItsTargetTime()
    {
        var dueAt = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(3));

        var reminder = CourseManagementReminder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            CourseManagementReminderType.InstallmentDue, dueAt, dueAt, dueAt.AddDays(-1));

        Assert.Equal(CourseManagementReminderType.InstallmentDue, reminder.Type);
    }

    [Fact]
    public void StaleLessonAndUpcomingInstallmentRemindersAreNotDelivered()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

        Assert.False(CourseManagementReminderSchedule.IsDeliverable(CourseManagementReminderType.Lesson,
            now.AddMinutes(-1), now));
        Assert.False(CourseManagementReminderSchedule.IsDeliverable(CourseManagementReminderType.InstallmentUpcoming,
            now, now));
    }

    [Fact]
    public void DueDayInstallmentReminderIsDeliveredOnlyOnItsIstanbulCalendarDay()
    {
        var dueAt = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(3));

        Assert.True(CourseManagementReminderSchedule.IsDeliverable(CourseManagementReminderType.InstallmentDue,
            dueAt, dueAt.AddHours(4)));
        Assert.False(CourseManagementReminderSchedule.IsDeliverable(CourseManagementReminderType.InstallmentDue,
            dueAt, dueAt.AddDays(1)));
    }

    private static CourseManagementReminder CreateReminder() => CourseManagementReminder.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CourseManagementReminderType.Lesson,
        DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
