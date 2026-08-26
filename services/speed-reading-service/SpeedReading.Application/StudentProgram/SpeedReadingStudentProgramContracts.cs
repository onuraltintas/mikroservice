namespace SpeedReading.Application.StudentProgram;

public interface ISpeedReadingStudentProgram
{
    Task<StudentProgramInfo?> GetMyProgramAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentProgramInfo>> GetMyProgramsAsync(Guid userId, CancellationToken cancellationToken);

    Task<StartStudentProgramResult> StartProgramAsync(
        Guid userId,
        Guid templateId,
        CancellationToken cancellationToken);
}

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
    DateTime? CompletedDate);

public sealed record StartStudentProgramResult(
    bool Success,
    Guid ProgramId,
    string ProgramName,
    string Message);
