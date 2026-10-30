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
    public int AdaptiveDifficultyOffset { get; private set; }
    public string? ScheduleJson { get; private set; }
    public bool IsStaffTraining { get; private set; }
    public int EarlyAdvanceDays { get; private set; }
    public int DaysCompleted { get; private set; }
    public int ExercisesCompleted { get; private set; }
    public DateTime? LastCompletionDate { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? CompletedDate { get; private set; }
    public decimal AverageSuccessRate { get; private set; }
    public int CurrentStreak { get; private set; }
    public int LongestStreak { get; private set; }

    public static StudentProgramProgress Start(
        Guid id,
        Guid userId,
        ProgramTemplate template,
        int previousCurrentStreak,
        int previousLongestStreak,
        Guid actorId,
        DateTime assignedAt,
        bool isStaffTraining = false)
    {
        if (id == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Student program identifiers are required.");
        if (template is null)
            throw new ArgumentNullException(nameof(template));
        if (actorId == Guid.Empty)
            throw new ArgumentException("Student program actor is required.", nameof(actorId));

        return new StudentProgramProgress
        {
            Id = id,
            UserId = userId,
            ProgramTemplateId = template.Id,
            AssignedDate = EnsureUtc(assignedAt),
            IsStaffTraining = isStaffTraining,
            CurrentDay = 1,
            CurrentWeek = 1,
            CurrentDifficultyLevel = template.InitialDifficultyLevel,
            AdaptiveDifficultyOffset = 0,
            DaysCompleted = 0,
            ExercisesCompleted = 0,
            AverageSuccessRate = 0,
            CurrentStreak = Math.Max(previousCurrentStreak, 0),
            LongestStreak = Math.Max(previousLongestStreak, 0),
            IsActive = true,
            CreatedAt = EnsureUtc(assignedAt),
            CreatedBy = actorId.ToString()
        };
    }

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
        string? updatedBy,
        int adaptiveDifficultyOffset = 0)
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
            AdaptiveDifficultyOffset = adaptiveDifficultyOffset,
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

    public void Reset(ProgramTemplate template, Guid actorId, DateTime at)
    {
        if (template is null)
            throw new ArgumentNullException(nameof(template));
        if (actorId == Guid.Empty)
            throw new ArgumentException("Reset actor is required.", nameof(actorId));

        CurrentDay = 1;
        CurrentWeek = 1;
        EarlyAdvanceDays = 0;
        AdaptiveDifficultyOffset = 0;
        // H18 (finding 10): a reset starts again at the program's own level.
        CurrentDifficultyLevel = template.InitialDifficultyLevel;
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

    public void Deactivate(Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Student program actor is required.", nameof(actorId));

        IsActive = false;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    public void SetSchedule(string scheduleJson, Guid actorId, DateTime at)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson) || actorId == Guid.Empty)
            throw new ArgumentException("A program schedule and actor are required.");
        if (ScheduleJson is not null)
            return;

        ScheduleJson = scheduleJson;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    /// <summary>Replaces the schedule after a difficulty change; the caller keeps finished weeks unchanged.</summary>
    public void ReplaceSchedule(string scheduleJson, Guid actorId, DateTime at)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson) || actorId == Guid.Empty)
            throw new ArgumentException("A program schedule and actor are required.");
        ScheduleJson = scheduleJson;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    public bool ApplyAdaptiveDifficultyAdjustment(
        int difficultyAdjustment,
        ProgramTemplate template,
        Guid actorId,
        DateTime at)
    {
        if (template is null)
            throw new ArgumentNullException(nameof(template));
        if (actorId == Guid.Empty)
            throw new ArgumentException("Student program actor is required.", nameof(actorId));

        // H15 (finding D): the offset stays inside the program's real range (level 1 … max), so a step down after earlier
        // steps up is never swallowed. H18 (findings 2 and 7): the step starts from the stored offset brought into that range
        // (rows from before H15 may hold −5 … +5), and the shown level is always initial + offset — the level the schedule
        // chooses exercises at — so it can neither jump nor drift.
        var adjustedOffset = ClampOffset(template, ClampOffset(template, AdaptiveDifficultyOffset) + difficultyAdjustment);
        var adjustedDifficulty = LevelFor(template, adjustedOffset);
        if (adjustedDifficulty == CurrentDifficultyLevel
            && adjustedOffset == AdaptiveDifficultyOffset)
            return false;

        CurrentDifficultyLevel = adjustedDifficulty;
        AdaptiveDifficultyOffset = adjustedOffset;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
        return true;
    }

    public StudentProgramCompletionResult ApplyExerciseCompletion(
        decimal averageSuccessRate,
        bool wasPreviouslyPassed,
        int completedCount,
        int expectedCount,
        ProgramTemplate template,
        Guid actorId,
        DateTime at,
        int? assignedTotalDays = null)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Completion actor is required.", nameof(actorId));
        if (template is null)
            throw new ArgumentNullException(nameof(template));
        if (CurrentDay < 1 || CurrentWeek < 1)
            throw new InvalidOperationException("Program progress must have a positive day and week.");
        if (completedCount < 0 || expectedCount < 0)
            throw new ArgumentOutOfRangeException(nameof(completedCount));
        if (CompletedDate.HasValue || !IsActive)
            return new StudentProgramCompletionResult(false, false, false, false,
                CurrentDay, CurrentWeek, CurrentDifficultyLevel);

        var now = EnsureUtc(at);
        var oldDay = CurrentDay;
        var oldWeek = CurrentWeek;
        var oldDifficultyLevel = CurrentDifficultyLevel;
        var previousCompletionDate = LastCompletionDate;

        if (!wasPreviouslyPassed)
            ExercisesCompleted++;

        AverageSuccessRate = averageSuccessRate;
        // H18 (finding 7): rows whose shown level drifted (calendar increases before H18) show the real level again.
        if (template.InitialDifficultyLevel > 0)
            CurrentDifficultyLevel = LevelFor(template, ClampOffset(template, AdaptiveDifficultyOffset));
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
            if ((assignedTotalDays ?? template.TotalDays) > 0
                && completedCumulativeDay >= (assignedTotalDays ?? template.TotalDays))
            {
                programCompleted = true;
                CompletedDate = now;
                IsActive = false;
            }
            else
            {
                CurrentDay++;
                if (CurrentDay <= 7)
                    return new StudentProgramCompletionResult(dayCompleted, false, false, false,
                        oldDay, oldWeek, oldDifficultyLevel);
                CurrentWeek++;
                CurrentDay = 1;
                weekChanged = true;

                // H18 (finding 7): no calendar increase. The schedule is built at initial + offset only, so a calendar
                // step changed nothing but the shown level; the weekly result (AdaptAfterWeekAsync) moves the level.
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

    public void GrantEarlyDays(int days, Guid actorId, DateTime at)
    {
        if (days <= 0 || actorId == Guid.Empty)
            throw new ArgumentException("Positive early days and a valid actor are required.");
        EarlyAdvanceDays = checked(EarlyAdvanceDays + days);
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    private static int ClampOffset(ProgramTemplate template, int offset) => Math.Clamp(
        offset,
        Math.Min(0, 1 - template.InitialDifficultyLevel),
        Math.Max(0, template.MaxDifficultyLevel - template.InitialDifficultyLevel));

    private static int LevelFor(ProgramTemplate template, int offset) =>
        Math.Clamp(template.InitialDifficultyLevel + offset, 1, Math.Max(1, template.MaxDifficultyLevel));

    private static int CalculateNextStreak(
        DateTime? lastCompletionDate,
        DateTime completionDate,
        int currentStreak) =>
        SpeedReading.Domain.Gamification.GamificationRules.CalculateNextStreak(lastCompletionDate, completionDate, currentStreak);

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
