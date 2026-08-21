using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Application.Queries;

namespace Coaching.Application.Interfaces;

/// <summary>
/// Assignment repository interface
/// </summary>
public interface IAssignmentRepository
{
    Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<Assignment>> GetByTeacherIdAsync(Guid teacherId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<Assignment>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Assignment> AddAsync(Assignment assignment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Assignment assignment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Assignment assignment, CancellationToken cancellationToken = default);
}

public interface IExamRepository
{
    Task<Exam?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Exam>> GetByInstitutionIdAsync(Guid institutionId, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<Exam>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Exam> AddAsync(Exam exam, CancellationToken cancellationToken = default);
    Task UpdateAsync(Exam exam, CancellationToken cancellationToken = default);
    Task DeleteAsync(Exam exam, CancellationToken cancellationToken = default);
}

public interface ICoachingSessionRepository
{
    Task<CoachingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<CoachingSession>> GetByTeacherIdAsync(Guid teacherId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<CoachingSession>> GetUpcomingSessionsAsync(DateTime from, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<CoachingSession>> GetUpcomingSessionsByTeacherIdAsync(Guid teacherId, DateTime from, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<CoachingSession> AddAsync(CoachingSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(CoachingSession session, CancellationToken cancellationToken = default);
    Task DeleteAsync(CoachingSession session, CancellationToken cancellationToken = default);
}

public interface IAcademicGoalRepository
{
    Task<AcademicGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<AcademicGoal>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AcademicGoal> AddAsync(AcademicGoal goal, CancellationToken cancellationToken = default);
    Task UpdateAsync(AcademicGoal goal, CancellationToken cancellationToken = default);
    Task DeleteAsync(AcademicGoal goal, CancellationToken cancellationToken = default);
}

public sealed record PagedRepositoryResult<T>(IReadOnlyList<T> Items, int TotalCount);

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetAsync(
        string scope,
        string key,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        IdempotencyRecord record,
        CancellationToken cancellationToken = default);
}

public interface ICoachingAdminRepository
{
    Task<CoachingAdminOverviewDto> GetOverviewAsync(
        int recentLimit,
        CancellationToken cancellationToken = default);

    Task<PagedRepositoryResult<CoachingAdminAssignmentListDto>> GetAssignmentsAsync(
        int pageNumber,
        int pageSize,
        string? status,
        string? source,
        string? search,
        CancellationToken cancellationToken = default);

    Task<PagedRepositoryResult<CoachingAdminSessionListDto>> GetSessionsAsync(
        int pageNumber,
        int pageSize,
        string? status,
        string? search,
        CancellationToken cancellationToken = default);

    Task<PagedRepositoryResult<CoachingAdminExamListDto>> GetExamsAsync(
        int pageNumber,
        int pageSize,
        string? examType,
        string? search,
        CancellationToken cancellationToken = default);

    Task<PagedRepositoryResult<CoachingAdminGoalListDto>> GetGoalsAsync(
        int pageNumber,
        int pageSize,
        bool? completed,
        string? search,
        CancellationToken cancellationToken = default);
}

public sealed record CoachingAdminOverviewDto(
    int TotalAssignments,
    int ActiveAssignments,
    int CompletedAssignments,
    int CancelledAssignments,
    int TotalAssignmentStudents,
    int SubmittedAssignmentStudents,
    int TotalExams,
    int TotalExamResults,
    int TotalSessions,
    int UpcomingSessions,
    int TotalGoals,
    int CompletedGoals,
    IReadOnlyList<CoachingAdminAssignmentDto> RecentAssignments);

public sealed record CoachingAdminAssignmentDto(
    Guid Id,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    string Status,
    DateTime DueDate,
    int StudentCount,
    int SubmittedStudentCount,
    DateTime CreatedAt);

public sealed record CoachingAdminAssignmentListDto(
    Guid Id,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    string Source,
    string? BookTitle,
    int? BookStartPage,
    int? BookEndPage,
    AssignmentStatus Status,
    DateTime DueDate,
    int StudentCount,
    int SubmittedStudentCount,
    int AttachmentCount,
    DateTime CreatedAt);

public sealed record CoachingAdminSessionListDto(
    Guid Id,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    SessionType SessionType,
    DateTime ScheduledDate,
    int DurationMinutes,
    SessionStatus Status,
    int StudentCount,
    int PresentCount,
    DateTime CreatedAt);

public sealed record CoachingAdminExamListDto(
    Guid Id,
    Guid CreatedByTeacherId,
    Guid? InstitutionId,
    string Title,
    ExamType ExamType,
    DateTime ExamDate,
    decimal MaxScore,
    int ResultCount,
    DateTime CreatedAt);

public sealed record CoachingAdminGoalListDto(
    Guid Id,
    Guid StudentId,
    Guid? SetByTeacherId,
    string Title,
    GoalCategory Category,
    DateTime? TargetDate,
    int CurrentProgress,
    bool IsCompleted,
    DateTime CreatedAt);

/// <summary>
/// Unit of Work for transaction management
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
