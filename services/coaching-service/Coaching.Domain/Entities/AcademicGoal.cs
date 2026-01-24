using EduPlatform.Shared.Kernel.Primitives;
using Coaching.Domain.Enums;

namespace Coaching.Domain.Entities;

/// <summary>
/// Akademik Hedef - Aggregate Root
/// </summary>
public class AcademicGoal : AggregateRoot
{
    public Guid StudentId { get; private set; }
    public Guid? SetByTeacherId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public GoalCategory Category { get; private set; }

    public ExamType? TargetExamType { get; private set; } // LGS, YKS, etc.
    public string? TargetSubject { get; private set; }

    public decimal? TargetScore { get; private set; }
    public DateTime? TargetDate { get; private set; }

    public int CurrentProgress { get; private set; } // 0-100%
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private AcademicGoal() { }

    public static AcademicGoal Create(
        Guid studentId,
        string title,
        GoalCategory category,
        Guid? setByTeacherId = null)
    {
        var goal = new AcademicGoal
        {
            StudentId = studentId,
            Title = title ?? throw new ArgumentNullException(nameof(title)),
            Category = category,
            SetByTeacherId = setByTeacherId,
            CurrentProgress = 0,
            IsCompleted = false
        };

        return goal;
    }

    public void UpdateDetails(
        string? title = null,
        string? description = null,
        GoalCategory? category = null)
    {
        if (title != null) Title = title;
        if (description != null) Description = description;
        if (category.HasValue) Category = category.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTarget(
        DateTime? targetDate = null,
        decimal? targetScore = null,
        ExamType? targetExamType = null,
        string? targetSubject = null)
    {
        if (targetDate.HasValue) TargetDate = targetDate;
        if (targetScore.HasValue) TargetScore = targetScore;
        if (targetExamType.HasValue) TargetExamType = targetExamType;
        if (targetSubject != null) TargetSubject = targetSubject;

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProgress(int progress)
    {
        if (progress < 0 || progress > 100)
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100");

        CurrentProgress = progress;

        if (progress == 100 && !IsCompleted)
        {
            MarkAsCompleted();
        }
        else if (progress < 100 && IsCompleted)
        {
            IsCompleted = false;
            CompletedAt = null;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsCompleted()
    {
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
        CurrentProgress = 100;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        IsCompleted = false;
        CompletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
