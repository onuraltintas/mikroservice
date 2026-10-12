namespace SpeedReading.Application.StudentProgram;

public interface ISpeedReadingStudentProgram
{
    Task<StartStudentProgramResult> StartStaffTrainingAsync(Guid userId, Guid templateId, CancellationToken cancellationToken);
    Task<NextStudentProgramRecommendation?> GetNextProgramRecommendationAsync(Guid userId, CancellationToken cancellationToken);
    Task<StartStudentProgramResult> ConfirmNextProgramAsync(Guid userId, ConfirmNextStudentProgramRequest request, CancellationToken cancellationToken);
    Task RequestNextProgramAsync(Guid userId, ConfirmNextStudentProgramRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    Task<StartStudentProgramResult> ApproveNextProgramAsync(Guid actorId, Guid userId, ConfirmNextStudentProgramRequest request,
        Guid? institutionId, CancellationToken cancellationToken);
    Task<StudentProgramInfo?> GetMyProgramAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentProgramInfo>> GetMyProgramsAsync(Guid userId, CancellationToken cancellationToken);

    Task<StartStudentProgramResult> StartProgramAsync(
        Guid userId,
        Guid templateId,
        CancellationToken cancellationToken);
}

public sealed record NextStudentProgramRecommendation(
    Guid SourceProgressId,
    Guid AssessmentAttemptId,
    Guid TemplateId,
    string TemplateName,
    int TotalDays,
    bool RequiresStaffApproval)
{
    public int RecommendedTrainingLevel { get; init; }
    public int MeasuredLevel { get; init; }
    public string RuleVersion { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public decimal AverageWpm { get; init; }
    public decimal Comprehension { get; init; }
    public decimal? WpmDelta { get; init; }
    public decimal? ComprehensionDelta { get; init; }
    public decimal TrainingSuccessRate { get; init; }
    public NextProgramOption? HigherOption { get; init; }
    public Guid? RequestedTemplateId { get; init; }
    public DateTime? RequestedAt { get; init; }
}

public sealed record NextProgramOption(Guid TemplateId, string TemplateName, int TotalDays, int Level);

public sealed record ConfirmNextStudentProgramRequest(Guid TemplateId, Guid SourceProgressId, Guid AssessmentAttemptId,
    bool OverrideAcknowledged = false);

public sealed record StudentProgramInfo(
    Guid ProgressId,
    Guid TemplateId,
    string TemplateName,
    string TemplateDescription,
    int ProgramType,
    string ProgramTypeName,
    string? ExamType,
    Guid TargetAgeGroupId,
    string TargetAgeGroupName,
    int MinAssessmentScore,
    int MaxAssessmentScore,
    int CurrentWeek,
    int CurrentDay,
    int CurrentDifficultyLevel,
    int MaxDifficultyLevel,
    int TotalDaysCompleted,
    int TotalExercisesCompleted,
    decimal AverageSuccessRate,
    int CurrentStreak,
    int LongestStreak,
    DateTime AssignedDate,
    DateTime? LastCompletionDate,
    bool IsActive,
    DateTime? CompletedDate)
{
    public int? MeasuredLevel { get; init; }
    public int? RecommendedTrainingLevel { get; init; }
    public int? SelectedTrainingLevel { get; init; }
    public string? SelectionPolicyVersion { get; init; }
}

public sealed record StartStudentProgramResult(
    bool Success,
    Guid ProgramId,
    string ProgramName,
    string Message);
