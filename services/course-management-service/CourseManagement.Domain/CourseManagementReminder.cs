namespace CourseManagement.Domain;

public enum CourseManagementReminderType
{
    Lesson = 1,
    Assignment = 2,
    InstallmentUpcoming = 3,
    InstallmentDue = 4
}

public enum CourseManagementReminderStatus
{
    Pending = 1,
    Cancelled = 2,
    Queued = 3
}

public sealed class CourseManagementReminder
{
    private CourseManagementReminder() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid RelatedEntityId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public CourseManagementReminderType Type { get; private set; }
    public DateTimeOffset TargetAt { get; private set; }
    public DateTimeOffset NotifyAt { get; private set; }
    public CourseManagementReminderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? QueuedAt { get; private set; }

    public static CourseManagementReminder Create(Guid id, Guid workspaceId, Guid relatedEntityId, Guid recipientUserId,
        CourseManagementReminderType type, DateTimeOffset targetAt, DateTimeOffset notifyAt, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || relatedEntityId == Guid.Empty || recipientUserId == Guid.Empty)
            throw new ArgumentException("Reminder identifiers are required.");
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (targetAt == default || notifyAt == default || createdAt == default || notifyAt > targetAt
            || notifyAt == targetAt && type != CourseManagementReminderType.InstallmentDue)
            throw new ArgumentException("Reminder target and notification times are invalid.");

        return new CourseManagementReminder
        {
            Id = id,
            WorkspaceId = workspaceId,
            RelatedEntityId = relatedEntityId,
            RecipientUserId = recipientUserId,
            Type = type,
            TargetAt = targetAt.ToUniversalTime(),
            NotifyAt = notifyAt.ToUniversalTime(),
            Status = CourseManagementReminderStatus.Pending,
            CreatedAt = createdAt.ToUniversalTime()
        };
    }

    public bool Cancel()
    {
        if (Status != CourseManagementReminderStatus.Pending) return false;
        Status = CourseManagementReminderStatus.Cancelled;
        return true;
    }

    public bool MarkQueued(DateTimeOffset queuedAt)
    {
        if (Status != CourseManagementReminderStatus.Pending || queuedAt == default) return false;
        Status = CourseManagementReminderStatus.Queued;
        QueuedAt = queuedAt.ToUniversalTime();
        return true;
    }
}

public readonly record struct InstallmentReminderTimes(DateTimeOffset UpcomingAt, DateTimeOffset DueAt);

public static class CourseManagementReminderSchedule
{
    private static readonly TimeZoneInfo IstanbulTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static DateTimeOffset LessonNotificationTime(DateTimeOffset startsAt) => startsAt.ToUniversalTime().AddHours(-24);

    public static DateTimeOffset AssignmentNotificationTime(DateTimeOffset dueAt) => dueAt.ToUniversalTime().AddHours(-24);

    public static InstallmentReminderTimes InstallmentTimes(DateOnly dueOn)
    {
        if (dueOn == default || dueOn < new DateOnly(1, 1, 4))
            throw new ArgumentOutOfRangeException(nameof(dueOn));

        return new InstallmentReminderTimes(AtIstanbulNine(dueOn.AddDays(-3)), AtIstanbulNine(dueOn));
    }

    public static bool IsDeliverable(CourseManagementReminderType type, DateTimeOffset targetAt, DateTimeOffset now)
    {
        if (type != CourseManagementReminderType.InstallmentDue) return targetAt > now;

        var targetDay = TimeZoneInfo.ConvertTime(targetAt, IstanbulTimeZone).Date;
        var today = TimeZoneInfo.ConvertTime(now, IstanbulTimeZone).Date;
        return targetDay == today;
    }

    private static DateTimeOffset AtIstanbulNine(DateOnly date)
    {
        var localTime = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(9, 0)), DateTimeKind.Unspecified);
        return new DateTimeOffset(localTime, IstanbulTimeZone.GetUtcOffset(localTime));
    }
}
