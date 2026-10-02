using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

public enum StudyPlanStatus { Draft = 0, Active = 1, Archived = 2 }

public sealed class StudyPlanRevision : AggregateRoot
{
    public Guid StudentId { get; private set; }
    public Guid PlanId { get; private set; }
    public int RevisionNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public StudyPlanStatus Status { get; private set; }
    public bool IsActive { get; private set; }
    public int? AutomaticAvailabilityVersion { get; private set; }
    public Guid? AutomaticSourceRevisionId { get; private set; }
    public int? AutomaticSourceRevisionVersion { get; private set; }
    private StudyPlanRevision() { }

    public static StudyPlanRevision Create(Guid studentId, Guid planId, int revisionNumber, string title)
    {
        if (studentId == Guid.Empty || planId == Guid.Empty) throw new ArgumentException("Plan ownership is required.");
        if (revisionNumber < 1) throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        return new StudyPlanRevision { StudentId = studentId, PlanId = planId,
            RevisionNumber = revisionNumber, Title = StudyCatalogText.Require(title, 200) };
    }

    public void Activate()
    {
        if (Status == StudyPlanStatus.Archived) throw new InvalidOperationException("Archived revisions cannot be activated.");
        Status = StudyPlanStatus.Active;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAutomaticSource(int availabilityVersion, Guid? revisionId, int? revisionVersion)
    {
        if (Status != StudyPlanStatus.Draft || availabilityVersion < 0 || revisionId == Guid.Empty
            || revisionVersion < 0 || revisionId.HasValue != revisionVersion.HasValue)
            throw new ArgumentException("Valid automatic plan source is required.");
        AutomaticAvailabilityVersion = availabilityVersion;
        AutomaticSourceRevisionId = revisionId;
        AutomaticSourceRevisionVersion = revisionVersion;
    }

    public void RenameDraft(string title)
    {
        if (Status != StudyPlanStatus.Draft) throw new InvalidOperationException("Only drafts can be renamed.");
        Title = StudyCatalogText.Require(title, 200);
        UpdatedAt = DateTime.UtcNow;
    }

    public void CorrectTitle(string title)
    {
        if (Status == StudyPlanStatus.Archived) throw new InvalidOperationException("Archived revisions cannot be corrected.");
        Title = StudyCatalogText.Require(title, 200);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = StudyPlanStatus.Archived;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordTaskChange()
    {
        if (Status != StudyPlanStatus.Active) throw new InvalidOperationException("Only active plan tasks can be changed.");
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class StudyPlanTask : AggregateRoot
{
    public Guid RevisionId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid? TopicId { get; private set; }
    public DateOnly PlannedDate { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int PlannedMinutes { get; private set; }
    public int? ActualMinutes { get; private set; }
    public bool IsPinned { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    private StudyPlanTask() { }

    public static StudyPlanTask Create(StudyPlanRevision revision, DateOnly plannedDate, string title,
        int plannedMinutes, Guid? topicId = null, bool isPinned = false)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.Status == StudyPlanStatus.Archived) throw new InvalidOperationException("Cannot add tasks to archived revisions.");
        if (plannedMinutes is < 1 or > 1440) throw new ArgumentOutOfRangeException(nameof(plannedMinutes));
        if (topicId == Guid.Empty || plannedDate == default) throw new ArgumentException("Valid task date and topic are required.");
        return new StudyPlanTask { RevisionId = revision.Id, StudentId = revision.StudentId,
            PlannedDate = plannedDate, Title = StudyCatalogText.Require(title, 200), PlannedMinutes = plannedMinutes,
            TopicId = topicId, IsPinned = isPinned };
    }

    public void Complete(int actualMinutes)
    {
        if (actualMinutes is < 1 or > 1440) throw new ArgumentOutOfRangeException(nameof(actualMinutes));
        if (IsCompleted)
        {
            if (ActualMinutes != actualMinutes) throw new InvalidOperationException("Completed work cannot be silently overwritten.");
            return;
        }
        ActualMinutes = actualMinutes;
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = CompletedAt;
    }

    public void Reschedule(DateOnly plannedDate)
    {
        if (IsCompleted) throw new InvalidOperationException("Completed work cannot be rescheduled.");
        if (plannedDate == default) throw new ArgumentException("Valid task date is required.");
        PlannedDate = plannedDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CorrectPlanning(string title, DateOnly plannedDate, int plannedMinutes)
    {
        if (IsCompleted) throw new InvalidOperationException("Completed work cannot be corrected.");
        var validatedTitle = StudyCatalogText.Require(title, 200);
        if (plannedDate == default || plannedMinutes is < 1 or > 1440) throw new ArgumentException("Valid planning date and minutes are required.");
        Title = validatedTitle; PlannedDate = plannedDate; PlannedMinutes = plannedMinutes;
        UpdatedAt = DateTime.UtcNow;
    }
}
