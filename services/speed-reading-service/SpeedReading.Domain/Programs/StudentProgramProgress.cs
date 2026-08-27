using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Programs;

public sealed record StudentProgramCompletionResult(
    bool DayCompleted,
    bool WeekChanged,
    bool DifficultyIncreased,
    bool ProgramCompleted,
    int OldDay,
    int OldWeek,
    int OldDifficultyLevel);

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

    public StudentProgramCompletionResult ApplyExerciseCompletion(
        decimal averageSuccessRate,
        bool wasPreviouslyPassed,
        int completedCount,
        int expectedCount,
        ProgramTemplate template,
        Guid actorId,
        DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Completion actor is required.", nameof(actorId));
        if (template is null)
            throw new ArgumentNullException(nameof(template));
        if (CurrentDay < 1 || CurrentWeek < 1)
            throw new InvalidOperationException("Program progress must have a positive day and week.");
        if (completedCount < 0 || expectedCount < 0)
            throw new ArgumentOutOfRangeException(nameof(completedCount));

        var now = EnsureUtc(at);
        var oldDay = CurrentDay;
        var oldWeek = CurrentWeek;
        var oldDifficultyLevel = CurrentDifficultyLevel;
        var previousCompletionDate = LastCompletionDate;

        if (!wasPreviouslyPassed)
            ExercisesCompleted++;

        AverageSuccessRate = averageSuccessRate;
        LastCompletionDate = now;
        UpdatedAt = now;
        UpdatedBy = actorId.ToString();

        var dayCompleted = expectedCount > 0 && completedCount >= expectedCount;
        var weekChanged = false;
        var difficultyIncreased = false;
        var programCompleted = false;

        if (dayCompleted && oldDay == CurrentDay && oldWeek == CurrentWeek)
        {
            DaysCompleted++;
            CurrentStreak = CalculateNextStreak(previousCompletionDate, now, CurrentStreak);
            LongestStreak = Math.Max(LongestStreak, CurrentStreak);

            var completedCumulativeDay = ((CurrentWeek - 1) * 7) + CurrentDay;
            CurrentDay++;

            if (template.TotalDays > 0 && completedCumulativeDay >= template.TotalDays)
            {
                programCompleted = true;
                CompletedDate = now;
                IsActive = false;
            }
            else if (CurrentDay > 7)
            {
                CurrentWeek++;
                CurrentDay = 1;
                weekChanged = true;

                if (template.WeeksPerDifficultyIncrease > 0
                    && CurrentWeek % template.WeeksPerDifficultyIncrease == 0
                    && CurrentDifficultyLevel < template.MaxDifficultyLevel)
                {
                    CurrentDifficultyLevel++;
                    difficultyIncreased = true;
                }
            }
        }

        return new StudentProgramCompletionResult(
            dayCompleted,
            weekChanged,
            difficultyIncreased,
            programCompleted,
            oldDay,
            oldWeek,
            oldDifficultyLevel);
    }

    private static int CalculateNextStreak(
        DateTime? lastCompletionDate,
        DateTime completionDate,
        int currentStreak)
    {
        if (!lastCompletionDate.HasValue)
            return 1;

        var previousDate = lastCompletionDate.Value.ToUniversalTime().Date;
        var currentDate = completionDate.ToUniversalTime().Date;
        if (previousDate == currentDate)
            return Math.Max(currentStreak, 1);

        return previousDate == currentDate.AddDays(-1)
            ? Math.Max(currentStreak, 1) + 1
            : 1;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
