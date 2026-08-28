using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Assessment;

public enum AssessmentAttemptPhase
{
    Baseline = 1,
    PostTraining = 2,
    Retention = 3,
    Transfer = 4
}

public enum AssessmentAttemptStatus
{
    InProgress = 1,
    Completed = 2,
    Abandoned = 3
}

/// <summary>
/// A versioned measurement window. Exercise results are linked to this
/// aggregate so baseline, post-training, retention, and transfer scores are
/// never mixed implicitly by recency.
/// </summary>
public sealed class AssessmentAttempt : AggregateRoot
{
    private AssessmentAttempt()
    {
    }

    public Guid StudentId { get; private set; }
    public AssessmentAttemptPhase Phase { get; private set; }
    public AssessmentAttemptStatus Status { get; private set; }
    public string FormVersion { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public Guid? AgeGroupConfigurationId { get; private set; }
    public int ExpectedExerciseCount { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public static AssessmentAttempt Start(
        Guid id,
        Guid studentId,
        AssessmentAttemptPhase phase,
        string formVersion,
        string language,
        Guid? ageGroupConfigurationId,
        int expectedExerciseCount,
        DateTime startedAt,
        string? createdBy)
    {
        if (id == Guid.Empty || studentId == Guid.Empty)
            throw new ArgumentException("Assessment attempt identifiers are required.");
        if (!Enum.IsDefined(phase))
            throw new ArgumentOutOfRangeException(nameof(phase));
        if (string.IsNullOrWhiteSpace(formVersion) || formVersion.Trim().Length > 100)
            throw new ArgumentException("Assessment form version is required and must not exceed 100 characters.", nameof(formVersion));
        if (string.IsNullOrWhiteSpace(language) || language.Trim().Length > 20)
            throw new ArgumentException("Assessment language is required and must not exceed 20 characters.", nameof(language));
        if (expectedExerciseCount is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(expectedExerciseCount));

        return new AssessmentAttempt
        {
            Id = id,
            StudentId = studentId,
            Phase = phase,
            Status = AssessmentAttemptStatus.InProgress,
            FormVersion = formVersion.Trim(),
            Language = language.Trim(),
            AgeGroupConfigurationId = ageGroupConfigurationId,
            ExpectedExerciseCount = expectedExerciseCount,
            StartedAt = EnsureUtc(startedAt),
            CreatedAt = EnsureUtc(startedAt),
            CreatedBy = createdBy
        };
    }

    public void Complete(DateTime completedAt)
    {
        if (Status == AssessmentAttemptStatus.Completed)
            return;
        if (Status != AssessmentAttemptStatus.InProgress)
            throw new InvalidOperationException("Only an in-progress assessment attempt can be completed.");

        CompletedAt = EnsureUtc(completedAt);
        Status = AssessmentAttemptStatus.Completed;
        UpdatedAt = CompletedAt;
    }

    public void Abandon(DateTime abandonedAt)
    {
        if (Status == AssessmentAttemptStatus.Abandoned)
            return;
        if (Status != AssessmentAttemptStatus.InProgress)
            throw new InvalidOperationException("Only an in-progress assessment attempt can be abandoned.");

        Status = AssessmentAttemptStatus.Abandoned;
        UpdatedAt = EnsureUtc(abandonedAt);
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
