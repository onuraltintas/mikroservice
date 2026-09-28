using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Application.Queries;
using Coaching.Application.Queries.GetStudentProgress;
using Coaching.Application.Queries.GetInstitutionCoachingComparison;

namespace Coaching.Application.Interfaces;

/// <summary>
/// Assignment repository interface
/// </summary>
public interface IAssignmentRepository
{
    Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<Assignment>> GetByTeacherIdAsync(
        Guid teacherId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        AssignmentStatus? status = null);
    Task<PagedRepositoryResult<Assignment>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Assignment> AddAsync(Assignment assignment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Assignment assignment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Assignment assignment, CancellationToken cancellationToken = default);
}

public interface IExamRepository
{
    Task<Exam?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Exam?> GetMetadataByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> GetResultStudentIdsByExamIdAsync(Guid examId, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<ExamResult>> GetResultsByExamIdAsync(
        Guid examId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<Guid>? scopedStudentIds = null);
    Task<List<Exam>> GetByInstitutionIdAsync(Guid institutionId, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<Exam>> GetByTeacherIdAsync(Guid teacherId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<Exam>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Exam> AddAsync(Exam exam, CancellationToken cancellationToken = default);
    Task UpdateAsync(Exam exam, CancellationToken cancellationToken = default);
    Task DeleteAsync(Exam exam, CancellationToken cancellationToken = default);
}

public interface ICoachingSessionRepository
{
    Task<CoachingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<CoachingSession>> GetByTeacherIdAsync(Guid teacherId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<CoachingSession>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<CoachingSession>> GetUpcomingSessionsAsync(DateTime from, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<CoachingSession>> GetUpcomingSessionsByTeacherIdAsync(Guid teacherId, DateTime from, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<CoachingSession> AddAsync(CoachingSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(CoachingSession session, CancellationToken cancellationToken = default);
    Task DeleteAsync(CoachingSession session, CancellationToken cancellationToken = default);
}

public interface IAcademicGoalRepository
{
    Task<AcademicGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> GetStudentIdsByTeacherIdAsync(Guid teacherId, CancellationToken cancellationToken = default);
    Task<PagedRepositoryResult<AcademicGoal>> GetByTeacherIdAsync(
        Guid teacherId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<Guid>? scopedStudentIds = null);
    Task<PagedRepositoryResult<AcademicGoal>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AcademicGoal> AddAsync(AcademicGoal goal, CancellationToken cancellationToken = default);
    Task UpdateAsync(AcademicGoal goal, CancellationToken cancellationToken = default);
    Task DeleteAsync(AcademicGoal goal, CancellationToken cancellationToken = default);
}

public interface ICoachingAgreementRepository
{
    Task<CoachingAgreementDocument?> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<CoachingAgreementDocument?> GetCurrentAsync(
        string locale,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);

    Task<CoachingAgreementAcknowledgement?> GetActiveSelfAcknowledgementAsync(
        Guid documentId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<CoachingAgreementAcknowledgement?> GetActiveAcknowledgementForStudentAsync(
        Guid documentId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<CoachingAgreementAcknowledgement?> GetActiveAcknowledgementAsync(
        Guid documentId,
        Guid studentId,
        Guid acknowledgedByUserId,
        Coaching.Domain.Enums.CoachingAgreementPartyRole partyRole,
        CancellationToken cancellationToken = default);

    Task<CoachingAgreementAcknowledgement?> GetAcknowledgementAsync(
        Guid acknowledgementId,
        CancellationToken cancellationToken = default);

    Task AddDocumentAsync(
        CoachingAgreementDocument document,
        CancellationToken cancellationToken = default);

    Task AddAcknowledgementAsync(
        CoachingAgreementAcknowledgement acknowledgement,
        CancellationToken cancellationToken = default);
}

public interface ICoachingStudentProgressRepository
{
    Task<StudentProgressSummaryDto> GetStudentSummaryAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);
}

public interface ICoachingComparativeReportRepository
{
    Task<InstitutionCoachingComparisonDto> GetInstitutionComparisonAsync(
        Guid institutionId,
        IReadOnlyCollection<Guid> studentIds,
        int? gradeLevel,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);
}

public sealed record PagedRepositoryResult<T>(IReadOnlyList<T> Items, int TotalCount);

public enum CoachingStudentHistoryType
{
    Assignments,
    Exams,
    Sessions,
    Goals
}

public sealed record CoachingStudentHistoryFilter(DateTime? FromDate, DateTime? ToDate, string? Status, string? Search);

public interface ICoachingStudentHistoryRepository
{
    Task<PagedRepositoryResult<CoachingAdminStudentHistoryItemDto>> GetStudentHistoryAsync(
        Guid studentId,
        CoachingStudentHistoryType type,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        CoachingStudentHistoryFilter? filter = null);
}

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

public interface ICoachingAdminRepository : ICoachingStudentHistoryRepository
{
    Task<CoachingAdminStudentDetailDto> GetStudentDetailAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<TeacherCoachingAnalyticsDto> GetTeacherAnalyticsAsync(
        Guid teacherId,
        Guid? institutionId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    Task<TeacherCoachingOverviewDto> GetTeacherOverviewAsync(
        Guid teacherId,
        Guid? institutionId,
        CancellationToken cancellationToken = default);

    Task<CoachingAdminOverviewDto> GetOverviewAsync(
        int recentLimit,
        CancellationToken cancellationToken = default,
        Guid? institutionId = null,
        IReadOnlyCollection<Guid>? scopedStudentIds = null);

    Task<PagedRepositoryResult<CoachingAdminAssignmentListDto>> GetAssignmentsAsync(
        int pageNumber,
        int pageSize,
        string? status,
        string? source,
        string? search,
        CancellationToken cancellationToken = default,
        Guid? institutionId = null);

    Task<PagedRepositoryResult<CoachingAdminSessionListDto>> GetSessionsAsync(
        int pageNumber,
        int pageSize,
        string? status,
        string? search,
        CancellationToken cancellationToken = default,
        Guid? institutionId = null);

    Task<PagedRepositoryResult<CoachingAdminExamListDto>> GetExamsAsync(
        int pageNumber,
        int pageSize,
        string? examType,
        string? search,
        CancellationToken cancellationToken = default,
        Guid? institutionId = null);

    Task<PagedRepositoryResult<CoachingAdminGoalListDto>> GetGoalsAsync(
        int pageNumber,
        int pageSize,
        bool? completed,
        string? search,
        CancellationToken cancellationToken = default,
        Guid? institutionId = null,
        IReadOnlyCollection<Guid>? scopedStudentIds = null);
}

public sealed record CoachingAdminStudentDetailDto(
    Guid StudentId,
    int TotalAssignments,
    int SubmittedAssignments,
    int TotalExams,
    int TotalSessions,
    int TotalGoals,
    IReadOnlyList<CoachingAdminStudentAssignmentDto> Assignments,
    IReadOnlyList<CoachingAdminStudentExamDto> Exams);

public sealed record CoachingAdminStudentAssignmentDto(
    Guid Id, string Title, string Status, DateTime DueDate, decimal? Score);

public sealed record CoachingAdminStudentExamDto(
    Guid Id, string Title, decimal Score, decimal MaxScore, DateTime ExamDate);

public sealed record CoachingAdminStudentHistoryItemDto(
    Guid Id,
    string Type,
    string Title,
    DateTime EventDate,
    string Status,
    decimal? Score = null,
    decimal? MaxScore = null,
    int? Progress = null,
    string? Category = null);

public sealed record TeacherCoachingAnalyticsDto(
    Guid TeacherId,
    TeacherCoachingPeriodDto CurrentPeriod,
    TeacherCoachingPeriodDto PreviousPeriod,
    int LowResults,
    int MediumResults,
    int HighResults);

public sealed record TeacherCoachingPeriodDto(int Assignments, int Exams, int Sessions);

public sealed record TeacherCoachingOverviewDto(
    Guid TeacherId,
    int TotalAssignments,
    int TotalAssignmentStudents,
    int SubmittedAssignmentStudents,
    int TotalExams,
    int TotalSessions);

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
