using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Programs;

public sealed class StudentProgramProgress : AggregateRoot
{
    private StudentProgramProgress()
    {
    }

    public Guid UserId { get; private set; }
    public Guid ProgramTemplateId { get; private set; }
    public DateTime AssignedDate { get; private set; }
    public int CurrentDay { get; private set; }
    public int CurrentWeek { get; private set; }
    public int CurrentDifficultyLevel { get; private set; }
    public int DaysCompleted { get; private set; }
    public int ExercisesCompleted { get; private set; }
    public DateTime? LastCompletionDate { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? CompletedDate { get; private set; }
    public decimal AverageSuccessRate { get; private set; }
    public int CurrentStreak { get; private set; }
    public int LongestStreak { get; private set; }

    public static StudentProgramProgress Import(
        Guid id,
        Guid userId,
        Guid programTemplateId,
        DateTime assignedDate,
        int currentDay,
        int currentWeek,
        int currentDifficultyLevel,
        int daysCompleted,
        int exercisesCompleted,
        DateTime? lastCompletionDate,
        bool isActive,
        DateTime? completedDate,
        decimal averageSuccessRate,
        int currentStreak,
        int longestStreak,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || userId == Guid.Empty || programTemplateId == Guid.Empty)
            throw new ArgumentException("Student program identifiers are required.");
        if (currentDay < 0 || currentWeek < 0 || daysCompleted < 0 || exercisesCompleted < 0
            || currentStreak < 0 || longestStreak < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentDay));
        }

        return new StudentProgramProgress
        {
            Id = id,
            UserId = userId,
            ProgramTemplateId = programTemplateId,
            AssignedDate = EnsureUtc(assignedDate),
            CurrentDay = currentDay,
            CurrentWeek = currentWeek,
            CurrentDifficultyLevel = currentDifficultyLevel,
            DaysCompleted = daysCompleted,
            ExercisesCompleted = exercisesCompleted,
            LastCompletionDate = lastCompletionDate.HasValue ? EnsureUtc(lastCompletionDate.Value) : null,
            IsActive = isActive,
            CompletedDate = completedDate.HasValue ? EnsureUtc(completedDate.Value) : null,
            AverageSuccessRate = averageSuccessRate,
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    public void Reset(Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Reset actor is required.", nameof(actorId));

        CurrentDay = 1;
        CurrentWeek = 1;
        DaysCompleted = 0;
        ExercisesCompleted = 0;
        AverageSuccessRate = 0;
        CurrentStreak = 0;
        LongestStreak = 0;
        LastCompletionDate = null;
        CompletedDate = null;
        IsActive = true;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
