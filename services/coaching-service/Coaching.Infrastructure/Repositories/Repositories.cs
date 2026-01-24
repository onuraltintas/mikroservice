using Microsoft.EntityFrameworkCore;
using Coaching.Domain.Entities;
using Coaching.Application.Interfaces;
using Coaching.Infrastructure.Data;

namespace Coaching.Infrastructure.Repositories;

/// <summary>
/// Assignment Repository Implementation
/// </summary>
public class AssignmentRepository : IAssignmentRepository
{
    private readonly CoachingDbContext _context;

    public AssignmentRepository(CoachingDbContext context)
    {
        _context = context;
    }

    public async Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Assignments
            .Include(a => a.AssignedStudents)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<List<Assignment>> GetByTeacherIdAsync(Guid teacherId, CancellationToken cancellationToken = default)
    {
        return await _context.Assignments
            .Include(a => a.AssignedStudents)
            .Where(a => a.TeacherId == teacherId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Assignment>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Assignments
            .Include(a => a.AssignedStudents)
            .Where(a => a.AssignedStudents.Any(s => s.StudentId == studentId))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Assignment> AddAsync(Assignment assignment, CancellationToken cancellationToken = default)
    {
        await _context.Assignments.AddAsync(assignment, cancellationToken);
        return assignment;
    }

    public Task UpdateAsync(Assignment assignment, CancellationToken cancellationToken = default)
    {
        _context.Assignments.Update(assignment);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Assignment assignment, CancellationToken cancellationToken = default)
    {
        _context.Assignments.Remove(assignment);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Exam Repository Implementation
/// </summary>
public class ExamRepository : IExamRepository
{
    private readonly CoachingDbContext _context;

    public ExamRepository(CoachingDbContext context)
    {
        _context = context;
    }

    public async Task<Exam?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Exams
            .Include(e => e.Results)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<List<Exam>> GetByInstitutionIdAsync(Guid institutionId, CancellationToken cancellationToken = default)
    {
        return await _context.Exams
            .Include(e => e.Results)
            .Where(e => e.InstitutionId == institutionId)
            .OrderByDescending(e => e.ExamDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Exam>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Exams
            .Include(e => e.Results)
            .Where(e => e.Results.Any(r => r.StudentId == studentId))
            .OrderByDescending(e => e.ExamDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Exam> AddAsync(Exam exam, CancellationToken cancellationToken = default)
    {
        await _context.Exams.AddAsync(exam, cancellationToken);
        return exam;
    }

    public Task UpdateAsync(Exam exam, CancellationToken cancellationToken = default)
    {
        _context.Exams.Update(exam);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Exam exam, CancellationToken cancellationToken = default)
    {
        _context.Exams.Remove(exam);
        return Task.CompletedTask;
    }
}

/// <summary>
/// CoachingSession Repository Implementation
/// </summary>
public class CoachingSessionRepository : ICoachingSessionRepository
{
    private readonly CoachingDbContext _context;

    public CoachingSessionRepository(CoachingDbContext context)
    {
        _context = context;
    }

    public async Task<CoachingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CoachingSessions
            .Include(s => s.Attendances)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<List<CoachingSession>> GetByTeacherIdAsync(Guid teacherId, CancellationToken cancellationToken = default)
    {
        return await _context.CoachingSessions
            .Include(s => s.Attendances)
            .Where(s => s.TeacherId == teacherId)
            .OrderByDescending(s => s.ScheduledDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CoachingSession>> GetUpcomingSessionsAsync(DateTime from, CancellationToken cancellationToken = default)
    {
        return await _context.CoachingSessions
            .Include(s => s.Attendances)
            .Where(s => s.ScheduledDate >= from && s.Status == Domain.Enums.SessionStatus.Scheduled)
            .OrderBy(s => s.ScheduledDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<CoachingSession> AddAsync(CoachingSession session, CancellationToken cancellationToken = default)
    {
        await _context.CoachingSessions.AddAsync(session, cancellationToken);
        return session;
    }

    public Task UpdateAsync(CoachingSession session, CancellationToken cancellationToken = default)
    {
        _context.CoachingSessions.Update(session);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CoachingSession session, CancellationToken cancellationToken = default)
    {
        _context.CoachingSessions.Remove(session);
        return Task.CompletedTask;
    }
}

/// <summary>
/// AcademicGoal Repository Implementation
/// </summary>
public class AcademicGoalRepository : IAcademicGoalRepository
{
    private readonly CoachingDbContext _context;

    public AcademicGoalRepository(CoachingDbContext context)
    {
        _context = context;
    }

    public async Task<AcademicGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AcademicGoals
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<List<AcademicGoal>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.AcademicGoals
            .Where(g => g.StudentId == studentId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AcademicGoal> AddAsync(AcademicGoal goal, CancellationToken cancellationToken = default)
    {
        await _context.AcademicGoals.AddAsync(goal, cancellationToken);
        return goal;
    }

    public Task UpdateAsync(AcademicGoal goal, CancellationToken cancellationToken = default)
    {
        _context.AcademicGoals.Update(goal);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AcademicGoal goal, CancellationToken cancellationToken = default)
    {
        _context.AcademicGoals.Remove(goal);
        return Task.CompletedTask;
    }
}
