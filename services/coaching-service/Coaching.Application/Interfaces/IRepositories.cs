using Coaching.Domain.Entities;

namespace Coaching.Application.Interfaces;

/// <summary>
/// Assignment repository interface
/// </summary>
public interface IAssignmentRepository
{
    Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Assignment>> GetByTeacherIdAsync(Guid teacherId, CancellationToken cancellationToken = default);
    Task<List<Assignment>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<Assignment> AddAsync(Assignment assignment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Assignment assignment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Assignment assignment, CancellationToken cancellationToken = default);
}

public interface IExamRepository
{
    Task<Exam?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Exam>> GetByInstitutionIdAsync(Guid institutionId, CancellationToken cancellationToken = default);
    Task<List<Exam>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<Exam> AddAsync(Exam exam, CancellationToken cancellationToken = default);
    Task UpdateAsync(Exam exam, CancellationToken cancellationToken = default);
    Task DeleteAsync(Exam exam, CancellationToken cancellationToken = default);
}

public interface ICoachingSessionRepository
{
    Task<CoachingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<CoachingSession>> GetByTeacherIdAsync(Guid teacherId, CancellationToken cancellationToken = default);
    Task<List<CoachingSession>> GetUpcomingSessionsAsync(DateTime from, CancellationToken cancellationToken = default);
    Task<CoachingSession> AddAsync(CoachingSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(CoachingSession session, CancellationToken cancellationToken = default);
    Task DeleteAsync(CoachingSession session, CancellationToken cancellationToken = default);
}

public interface IAcademicGoalRepository
{
    Task<AcademicGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<AcademicGoal>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<AcademicGoal> AddAsync(AcademicGoal goal, CancellationToken cancellationToken = default);
    Task UpdateAsync(AcademicGoal goal, CancellationToken cancellationToken = default);
    Task DeleteAsync(AcademicGoal goal, CancellationToken cancellationToken = default);
}

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
