using SpeedReading.Application.Content;

namespace SpeedReading.Application.Assignments;

public sealed record CreateAssignmentRequest(
    Guid ExerciseId,
    Guid? ReadingTextId,
    IReadOnlyList<Guid>? StudentIds,
    string Title,
    string? Description,
    DateTime? DueDate);

public sealed record AssignmentSummary(
    Guid Id,
    Guid TeacherId,
    Guid ExerciseId,
    Guid? ReadingTextId,
    string Title,
    string Description,
    string? ExerciseTitle,
    string? ExerciseTypeName,
    string? ReadingTextTitle,
    DateTime DueDate,
    bool IsActive,
    DateTime CreatedAt,
    int StudentCount,
    int CompletedCount);

public sealed record StudentAssignmentSummary(
    Guid Id,
    Guid AssignmentId,
    string Title,
    string Description,
    DateTime DueDate,
    bool IsCompleted,
    DateTime? CompletionDate,
    Guid ExerciseId,
    string? ExerciseTitle,
    decimal? Score);

public sealed record AssignmentStudentSummary(
    Guid StudentId,
    string FirstName,
    string LastName,
    string FullName,
    bool IsCompleted,
    DateTime? CompletionDate,
    decimal? Score);

public sealed record AssignmentDetails(
    Guid Id,
    Guid TeacherId,
    Guid ExerciseId,
    Guid? ReadingTextId,
    string Title,
    string Description,
    string? ExerciseTitle,
    DateTime DueDate,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<AssignmentStudentSummary> Students);

public enum AssignmentStudentMutationStatus
{
    Success,
    AssignmentNotFound,
    StudentNotFound,
    AlreadyAssigned,
    StudentAssignmentNotFound
}

public interface ISpeedReadingAssignments
{
    Task<Guid?> CreateAsync(
        Guid teacherId,
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentAssignmentSummary>> GetMyAssignmentsAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<AssignmentSummary>> GetTeacherAssignmentsAsync(
        Guid teacherId,
        int pageNumber,
        int pageSize,
        string? searchTerm,
        bool? isActive,
        Guid? exerciseTypeId,
        CancellationToken cancellationToken = default);

    Task<AssignmentDetails?> GetDetailsAsync(
        Guid teacherId,
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid teacherId,
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task<AssignmentStudentMutationStatus> AddStudentAsync(
        Guid teacherId,
        Guid assignmentId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<AssignmentStudentMutationStatus> RemoveStudentAsync(
        Guid teacherId,
        Guid assignmentId,
        Guid studentId,
        CancellationToken cancellationToken = default);
}

public static class SpeedReadingAssignmentRules
{
    public static IReadOnlyList<Guid> NormalizeStudentIds(IEnumerable<Guid>? studentIds) =>
        (studentIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

    public static DateTime NormalizeDueDate(DateTime? dueDate)
    {
        var value = dueDate ?? DateTime.UtcNow.AddDays(7);
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
