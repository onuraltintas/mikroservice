using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Sessions;

/// <summary>
/// Aggregate root for a student exercise attempt. It owns the lifecycle and
/// answer-count invariants used by the new Speed Reading data store.
/// </summary>
public sealed class ExerciseSession : AggregateRoot
{
    private readonly List<ExerciseSessionAnswer> _answers = [];

    private ExerciseSession()
    {
    }

    public Guid StudentId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public Guid? ReadingTextId { get; private set; }
    public Guid? StudentAssignmentId { get; private set; }
    public ExerciseSessionStatus Status { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public int TotalPausedSeconds { get; private set; }
    public DateTime? PausedAt { get; private set; }
    public int? TimeLimitSeconds { get; private set; }
    public int CurrentStep { get; private set; }
    public int TotalSteps { get; private set; }
    public int CorrectCount { get; private set; }
    public int IncorrectCount { get; private set; }
    public string SessionDataJson { get; private set; } = "{}";
    public string? CustomDataJson { get; private set; }
    public string ProcessedActionsJson { get; private set; } = "{}";
    public IReadOnlyCollection<ExerciseSessionAnswer> Answers => _answers.AsReadOnly();

    public static ExerciseSession Start(
        Guid studentId,
        Guid exerciseId,
        Guid? readingTextId,
        int totalSteps,
        DateTime startedAt,
        int? timeLimitSeconds,
        Guid? studentAssignmentId = null)
    {
        if (studentId == Guid.Empty)
            throw new ArgumentException("Student is required.", nameof(studentId));
        if (exerciseId == Guid.Empty)
            throw new ArgumentException("Exercise is required.", nameof(exerciseId));
        if (totalSteps <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalSteps));
        if (timeLimitSeconds is <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds));

        return new ExerciseSession
        {
            StudentId = studentId,
            ExerciseId = exerciseId,
            ReadingTextId = readingTextId,
            StudentAssignmentId = studentAssignmentId,
            Status = ExerciseSessionStatus.Active,
            StartTime = EnsureUtc(startedAt),
            TotalSteps = totalSteps,
            TimeLimitSeconds = timeLimitSeconds,
            SessionDataJson = "{}",
            ProcessedActionsJson = "{}"
        };
    }

    public static ExerciseSession Import(
        Guid id,
        Guid studentId,
        Guid exerciseId,
        Guid? readingTextId,
        Guid? studentAssignmentId,
        ExerciseSessionStatus status,
        DateTime startTime,
        DateTime? endTime,
        int totalPausedSeconds,
        DateTime? pausedAt,
        int? timeLimitSeconds,
        int currentStep,
        int totalSteps,
        int correctCount,
        int incorrectCount,
        string sessionDataJson,
        string? customDataJson,
        string processedActionsJson,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || studentId == Guid.Empty || exerciseId == Guid.Empty)
            throw new ArgumentException("Session identifiers are required.");
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (totalSteps <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalSteps));
        if (totalPausedSeconds < 0 || currentStep < 0 || correctCount < 0 || incorrectCount < 0)
            throw new ArgumentOutOfRangeException(nameof(totalPausedSeconds));
        if (timeLimitSeconds is <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds));

        return new ExerciseSession
        {
            Id = id,
            StudentId = studentId,
            ExerciseId = exerciseId,
            ReadingTextId = readingTextId,
            StudentAssignmentId = studentAssignmentId,
            Status = status,
            StartTime = EnsureUtc(startTime),
            EndTime = endTime.HasValue ? EnsureUtc(endTime.Value) : null,
            TotalPausedSeconds = totalPausedSeconds,
            PausedAt = pausedAt.HasValue ? EnsureUtc(pausedAt.Value) : null,
            TimeLimitSeconds = timeLimitSeconds,
            CurrentStep = Math.Min(Math.Max(currentStep, 0), totalSteps),
            TotalSteps = totalSteps,
            CorrectCount = correctCount,
            IncorrectCount = incorrectCount,
            SessionDataJson = string.IsNullOrWhiteSpace(sessionDataJson) ? "{}" : sessionDataJson,
            CustomDataJson = customDataJson,
            ProcessedActionsJson = string.IsNullOrWhiteSpace(processedActionsJson) ? "{}" : processedActionsJson,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    public void ImportAnswer(ExerciseSessionAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (answer.SessionId != Id)
            throw new InvalidOperationException("Answer belongs to another session.");
        if (_answers.Any(item => item.QuestionId == answer.QuestionId))
            return;
        _answers.Add(answer);
    }

    public void Pause(DateTime at)
    {
        EnsureStatus(ExerciseSessionStatus.Active, "Only an active session can be paused.");
        PausedAt = EnsureUtc(at);
        Status = ExerciseSessionStatus.Paused;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Resume(DateTime at)
    {
        EnsureStatus(ExerciseSessionStatus.Paused, "Only a paused session can be resumed.");
        var resumedAt = EnsureUtc(at);
        TotalPausedSeconds += CalculateNonNegativeSeconds(PausedAt, resumedAt);
        PausedAt = null;
        Status = ExerciseSessionStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordAnswer(
        Guid questionId,
        string answer,
        bool isCorrect,
        int timeSpentSeconds,
        int bloomLevel)
    {
        EnsureStatus(ExerciseSessionStatus.Active, "Answers can only be recorded for an active session.");
        if (questionId == Guid.Empty)
            throw new ArgumentException("Question is required.", nameof(questionId));
        if (string.IsNullOrWhiteSpace(answer))
            throw new ArgumentException("Answer is required.", nameof(answer));
        if (timeSpentSeconds < 0 || bloomLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(timeSpentSeconds));
        if (_answers.Any(item => item.QuestionId == questionId))
            throw new InvalidOperationException("A question cannot be answered more than once.");

        _answers.Add(new ExerciseSessionAnswer(
            Guid.NewGuid(),
            Id,
            questionId,
            answer.Trim(),
            isCorrect,
            timeSpentSeconds,
            bloomLevel));

        if (isCorrect) CorrectCount++;
        else IncorrectCount++;
        CurrentStep = Math.Min(TotalSteps, CurrentStep + 1);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Advance(bool isCorrect = true)
    {
        EnsureStatus(ExerciseSessionStatus.Active, "Only an active session can advance.");
        CurrentStep = Math.Min(TotalSteps, CurrentStep + 1);
        if (isCorrect) CorrectCount++;
        else IncorrectCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCurrentStep(int step)
    {
        EnsureStatus(ExerciseSessionStatus.Active, "Only an active session can change progress.");
        CurrentStep = Math.Clamp(step, 0, TotalSteps);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetState(string sessionDataJson, string? customDataJson = null)
    {
        SessionDataJson = string.IsNullOrWhiteSpace(sessionDataJson) ? "{}" : sessionDataJson;
        CustomDataJson = customDataJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetProcessedActions(string processedActionsJson)
    {
        ProcessedActionsJson = string.IsNullOrWhiteSpace(processedActionsJson) ? "{}" : processedActionsJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkTimedOut(DateTime at)
    {
        if (Status is ExerciseSessionStatus.Completed or ExerciseSessionStatus.Timeout)
            return;

        Status = ExerciseSessionStatus.Timeout;
        EndTime = EnsureUtc(at);
        if (PausedAt.HasValue)
        {
            TotalPausedSeconds += CalculateNonNegativeSeconds(PausedAt, EndTime.Value);
            PausedAt = null;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(DateTime at)
    {
        if (Status is not (ExerciseSessionStatus.Active or ExerciseSessionStatus.Paused))
            throw new InvalidOperationException("Only an active or paused session can be completed.");

        var completedAt = EnsureUtc(at);
        if (PausedAt.HasValue)
        {
            TotalPausedSeconds += CalculateNonNegativeSeconds(PausedAt, completedAt);
            PausedAt = null;
        }

        Status = ExerciseSessionStatus.Completed;
        EndTime = completedAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsTimedOut(DateTime now)
    {
        if (TimeLimitSeconds is not > 0 || Status is ExerciseSessionStatus.Completed or ExerciseSessionStatus.Timeout)
            return false;

        var end = EnsureUtc(now);
        var elapsed = Math.Max(0, (end - StartTime).TotalSeconds);
        elapsed -= Math.Max(0, TotalPausedSeconds);
        if (Status == ExerciseSessionStatus.Paused && PausedAt.HasValue)
            elapsed -= Math.Max(0, (end - PausedAt.Value).TotalSeconds);

        return elapsed > TimeLimitSeconds.Value;
    }

    private void EnsureStatus(ExerciseSessionStatus expected, string message)
    {
        if (Status != expected)
            throw new InvalidOperationException(message);
    }

    private static int CalculateNonNegativeSeconds(DateTime? from, DateTime to) =>
        from.HasValue ? Math.Max(0, (int)Math.Round((to - from.Value).TotalSeconds)) : 0;

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
