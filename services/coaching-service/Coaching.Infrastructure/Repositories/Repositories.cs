using Microsoft.EntityFrameworkCore;
using Coaching.Domain.Entities;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries;
using Coaching.Application.Queries.GetStudentProgress;
using Coaching.Application.Queries.GetInstitutionCoachingComparison;
using Coaching.Infrastructure.Data;

namespace Coaching.Infrastructure.Repositories;

public sealed class IdempotencyRepository(CoachingDbContext context) : IIdempotencyRepository
{
    public Task<IdempotencyRecord?> GetAsync(
        string scope,
        string key,
        CancellationToken cancellationToken = default)
    {
        return context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.Scope == scope && record.Key == key,
                cancellationToken);
    }

    public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        return context.IdempotencyRecords.AddAsync(record, cancellationToken).AsTask();
    }
}

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
                .ThenInclude(student => student.SubmissionAttachments)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<PagedRepositoryResult<Assignment>> GetByTeacherIdAsync(Guid teacherId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Assignments
            .Include(a => a.AssignedStudents)
                .ThenInclude(student => student.SubmissionAttachments)
            .Where(a => a.TeacherId == teacherId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<Assignment>(items, totalCount);
    }

    public async Task<PagedRepositoryResult<Assignment>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Assignments
            .Include(a => a.AssignedStudents.Where(s => s.StudentId == studentId))
                .ThenInclude(student => student.SubmissionAttachments)
            .Where(a => a.AssignedStudents.Any(s => s.StudentId == studentId))
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<Assignment>(items, totalCount);
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

    public async Task<PagedRepositoryResult<Exam>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Exams
            .Include(e => e.Results.Where(r => r.StudentId == studentId))
            .Where(e => e.Results.Any(r => r.StudentId == studentId))
            .OrderByDescending(e => e.ExamDate)
            .ThenByDescending(e => e.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<Exam>(items, totalCount);
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

    public async Task<PagedRepositoryResult<CoachingSession>> GetByTeacherIdAsync(Guid teacherId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.CoachingSessions
            .Include(s => s.Attendances)
            .Where(s => s.TeacherId == teacherId)
            .OrderByDescending(s => s.ScheduledDate)
            .ThenByDescending(s => s.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<CoachingSession>(items, totalCount);
    }

    public async Task<PagedRepositoryResult<CoachingSession>> GetByStudentIdAsync(
        Guid studentId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CoachingSessions
            .Include(session => session.Attendances.Where(attendance => attendance.StudentId == studentId))
            .Where(session => session.Attendances.Any(attendance => attendance.StudentId == studentId))
            .OrderByDescending(session => session.ScheduledDate)
            .ThenByDescending(session => session.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<CoachingSession>(items, totalCount);
    }

    public async Task<PagedRepositoryResult<CoachingSession>> GetUpcomingSessionsAsync(DateTime from, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.CoachingSessions
            .Include(s => s.Attendances)
            .Where(s => s.ScheduledDate >= from && s.Status == Domain.Enums.SessionStatus.Scheduled)
            .OrderBy(s => s.ScheduledDate)
            .ThenBy(s => s.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<CoachingSession>(items, totalCount);
    }

    public async Task<PagedRepositoryResult<CoachingSession>> GetUpcomingSessionsByTeacherIdAsync(
        Guid teacherId,
        DateTime from,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CoachingSessions
            .Include(s => s.Attendances)
            .Where(s => s.TeacherId == teacherId &&
                        s.ScheduledDate >= from &&
                        s.Status == Domain.Enums.SessionStatus.Scheduled)
            .OrderBy(s => s.ScheduledDate)
            .ThenBy(s => s.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<CoachingSession>(items, totalCount);
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

public sealed class CoachingCalendarRepository(CoachingDbContext context)
    : ICoachingCalendarRepository
{
    public Task<IReadOnlyCollection<CoachingCalendarSession>> GetByTeacherIdAsync(
        Guid teacherId,
        DateTime fromDate,
        DateTime toDate,
        int maxEvents,
        CancellationToken cancellationToken = default) =>
        GetCalendarSessionsAsync(
            context.CoachingSessions
                .AsNoTracking()
                .Include(session => session.Attendances)
                .Where(session => session.TeacherId == teacherId
                    && session.ScheduledDate >= fromDate
                    && session.ScheduledDate <= toDate),
            maxEvents,
            cancellationToken);

    public Task<IReadOnlyCollection<CoachingCalendarSession>> GetByStudentIdAsync(
        Guid studentId,
        DateTime fromDate,
        DateTime toDate,
        int maxEvents,
        CancellationToken cancellationToken = default) =>
        GetCalendarSessionsAsync(
            context.CoachingSessions
                .AsNoTracking()
                .Include(session => session.Attendances.Where(attendance => attendance.StudentId == studentId))
                .Where(session => session.ScheduledDate >= fromDate
                    && session.ScheduledDate <= toDate
                    && session.Attendances.Any(attendance => attendance.StudentId == studentId)),
            maxEvents,
            cancellationToken);

    private static async Task<IReadOnlyCollection<CoachingCalendarSession>> GetCalendarSessionsAsync(
        IQueryable<CoachingSession> query,
        int maxEvents,
        CancellationToken cancellationToken)
    {
        var sessions = await query
            .OrderBy(session => session.ScheduledDate)
            .ThenBy(session => session.Id)
            .Take(maxEvents)
            .ToListAsync(cancellationToken);

        return sessions
            .Select(session => new CoachingCalendarSession(
                session.Id,
                session.Title,
                session.ScheduledDate,
                session.DurationMinutes,
                session.Status.ToString(),
                session.MeetingLink,
                session.Attendances.Select(attendance => attendance.StudentId).ToArray()))
            .ToArray();
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

    public async Task<PagedRepositoryResult<AcademicGoal>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.AcademicGoals
            .Where(g => g.StudentId == studentId)
            .OrderByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<AcademicGoal>(items, totalCount);
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

public sealed class CoachingStudentProgressRepository(CoachingDbContext context)
    : ICoachingStudentProgressRepository
{
    public async Task<StudentProgressSummaryDto> GetStudentSummaryAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await context.AssignmentStudents
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && item.Assignment.Status != Domain.Enums.AssignmentStatus.Cancelled)
            .Select(item => new
            {
                item.SubmittedAt,
                item.Score,
                item.Status,
                item.Assignment.MaxScore
            })
            .ToListAsync(cancellationToken);

        var assignmentPercentages = assignments
            .Where(item => item.Score.HasValue && item.MaxScore.HasValue && item.MaxScore.Value > 0)
            .Select(item => (double)item.Score!.Value / (double)item.MaxScore!.Value * 100)
            .ToArray();

        var exams = await context.ExamResults
            .AsNoTracking()
            .Where(item => item.StudentId == studentId)
            .Select(item => new { item.Score, item.Exam.MaxScore })
            .ToListAsync(cancellationToken);
        var examPercentages = exams
            .Where(item => item.MaxScore > 0)
            .Select(item => (double)item.Score / (double)item.MaxScore * 100)
            .ToArray();

        var goals = await context.AcademicGoals
            .AsNoTracking()
            .Where(item => item.StudentId == studentId)
            .Select(item => new { item.CurrentProgress, item.IsCompleted })
            .ToListAsync(cancellationToken);

        var attendances = await context.SessionAttendances
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && item.Session.Status != Domain.Enums.SessionStatus.Cancelled)
            .Select(item => new
            {
                item.AttendanceStatus,
                item.Session.Status,
                item.Session.ScheduledDate
            })
            .ToListAsync(cancellationToken);

        var recordedAttendances = attendances
            .Where(item => item.AttendanceStatus != Domain.Enums.AttendanceStatus.NotRecorded)
            .ToArray();
        var attendedSessions = recordedAttendances.Count(item =>
            item.AttendanceStatus is Domain.Enums.AttendanceStatus.Present
                or Domain.Enums.AttendanceStatus.Late);

        return new StudentProgressSummaryDto(
            studentId,
            assignments.Count,
            assignments.Count(item => item.SubmittedAt.HasValue),
            assignments.Count(item => item.Status == Domain.Enums.StudentAssignmentStatus.Graded),
            AveragePercentage(assignmentPercentages),
            exams.Count,
            AveragePercentage(examPercentages),
            goals.Count,
            goals.Count(item => item.IsCompleted),
            goals.Count == 0 ? 0 : (int)Math.Round(goals.Average(item => item.CurrentProgress)),
            attendances.Count,
            attendances.Count(item => item.Status == Domain.Enums.SessionStatus.Scheduled
                && item.ScheduledDate >= DateTime.UtcNow),
            attendedSessions,
            recordedAttendances.Length == 0
                ? null
                : Math.Round((decimal)attendedSessions / recordedAttendances.Length * 100, 2));
    }

    private static decimal? AveragePercentage(IReadOnlyCollection<double> values) =>
        values.Count == 0 ? null : Math.Round((decimal)values.Average(), 2);
}

public sealed class CoachingComparativeReportRepository(CoachingDbContext context)
    : ICoachingComparativeReportRepository
{
    public async Task<InstitutionCoachingComparisonDto> GetInstitutionComparisonAsync(
        Guid institutionId,
        IReadOnlyCollection<Guid> studentIds,
        int? gradeLevel,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var studentIdArray = studentIds.Distinct().ToArray();

        var assignmentRows = await context.AssignmentStudents
            .AsNoTracking()
            .Where(item => studentIdArray.Contains(item.StudentId)
                && item.Assignment.InstitutionId == institutionId
                && item.Assignment.CreatedAt >= fromDate
                && item.Assignment.CreatedAt <= toDate
                && item.Assignment.Status != Domain.Enums.AssignmentStatus.Cancelled
                && (!gradeLevel.HasValue || item.Assignment.TargetGradeLevel == gradeLevel.Value))
            .Select(item => new
            {
                item.AssignmentId,
                item.SubmittedAt,
                item.Score,
                item.Status,
                item.Assignment.MaxScore
            })
            .ToListAsync(cancellationToken);

        var assignmentPercentages = assignmentRows
            .Where(item => item.Score.HasValue && item.MaxScore.HasValue && item.MaxScore.Value > 0)
            .Select(item => (double)item.Score!.Value / (double)item.MaxScore!.Value * 100)
            .ToArray();

        var examRows = await context.ExamResults
            .AsNoTracking()
            .Where(item => studentIdArray.Contains(item.StudentId)
                && item.Exam.InstitutionId == institutionId
                && item.Exam.ExamDate >= fromDate
                && item.Exam.ExamDate <= toDate
                && (!gradeLevel.HasValue || item.Exam.TargetGradeLevel == gradeLevel.Value))
            .Select(item => new
            {
                item.ExamId,
                item.Score,
                item.Exam.MaxScore
            })
            .ToListAsync(cancellationToken);
        var examPercentages = examRows
            .Where(item => item.MaxScore > 0)
            .Select(item => (double)item.Score / (double)item.MaxScore * 100)
            .ToArray();

        var sessionRows = await context.SessionAttendances
            .AsNoTracking()
            .Where(item => studentIdArray.Contains(item.StudentId)
                && item.Session.InstitutionId == institutionId
                && item.Session.ScheduledDate >= fromDate
                && item.Session.ScheduledDate <= toDate
                && item.Session.Status != Domain.Enums.SessionStatus.Cancelled)
            .Select(item => new
            {
                item.SessionId,
                item.AttendanceStatus
            })
            .ToListAsync(cancellationToken);
        var recordedSessionRows = sessionRows
            .Where(item => item.AttendanceStatus != Domain.Enums.AttendanceStatus.NotRecorded)
            .ToArray();
        var attendedSessionRows = recordedSessionRows.Count(item =>
            item.AttendanceStatus is Domain.Enums.AttendanceStatus.Present
                or Domain.Enums.AttendanceStatus.Late);

        var goals = await context.AcademicGoals
            .AsNoTracking()
            .Where(item => studentIdArray.Contains(item.StudentId)
                && item.CreatedAt >= fromDate
                && item.CreatedAt <= toDate)
            .Select(item => new { item.CurrentProgress, item.IsCompleted })
            .ToListAsync(cancellationToken);

        return new InstitutionCoachingComparisonDto(
            institutionId,
            gradeLevel,
            fromDate,
            toDate,
            studentIdArray.Length,
            assignmentRows.Select(item => item.AssignmentId).Distinct().Count(),
            assignmentRows.Count,
            assignmentRows.Count(item => item.SubmittedAt.HasValue),
            assignmentRows.Count(item => item.Status == Domain.Enums.StudentAssignmentStatus.Graded),
            AveragePercentage(assignmentPercentages),
            examRows.Select(item => item.ExamId).Distinct().Count(),
            examRows.Count,
            AveragePercentage(examPercentages),
            sessionRows.Select(item => item.SessionId).Distinct().Count(),
            recordedSessionRows.Length,
            attendedSessionRows,
            recordedSessionRows.Length == 0
                ? null
                : Math.Round((decimal)attendedSessionRows / recordedSessionRows.Length * 100, 2),
            goals.Count,
            goals.Count(item => item.IsCompleted),
            goals.Count == 0 ? 0 : (int)Math.Round(goals.Average(item => item.CurrentProgress)));
    }

    private static decimal? AveragePercentage(IReadOnlyCollection<double> values) =>
        values.Count == 0 ? null : Math.Round((decimal)values.Average(), 2);
}

public sealed class CoachingEarlyWarningRepository(CoachingDbContext context)
    : ICoachingEarlyWarningRepository
{
    public async Task<IReadOnlyCollection<CoachingStudentEarlyWarningMetrics>> GetStudentMetricsAsync(
        Guid institutionId,
        IReadOnlyCollection<Guid> studentIds,
        int? gradeLevel,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var studentIdArray = studentIds.Distinct().ToArray();
        if (studentIdArray.Length == 0)
        {
            return Array.Empty<CoachingStudentEarlyWarningMetrics>();
        }

        var accumulators = studentIdArray.ToDictionary(
            studentId => studentId,
            _ => new EarlyWarningAccumulator());

        var assignmentRows = await context.AssignmentStudents
            .AsNoTracking()
            .Where(item => studentIdArray.Contains(item.StudentId)
                && item.Assignment.InstitutionId == institutionId
                && item.Assignment.CreatedAt >= fromDate
                && item.Assignment.CreatedAt <= toDate
                && item.Assignment.Status != Domain.Enums.AssignmentStatus.Cancelled
                && (!gradeLevel.HasValue || item.Assignment.TargetGradeLevel == gradeLevel.Value))
            .Select(item => new
            {
                item.StudentId,
                item.SubmittedAt,
                item.Score,
                item.Status,
                item.Assignment.MaxScore,
                item.Assignment.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var row in assignmentRows)
        {
            if (!accumulators.TryGetValue(row.StudentId, out var accumulator))
            {
                continue;
            }

            accumulator.AssignmentCount++;
            accumulator.SubmittedAssignmentCount += row.SubmittedAt.HasValue ? 1 : 0;
            accumulator.GradedAssignmentCount += row.Status == Domain.Enums.StudentAssignmentStatus.Graded ? 1 : 0;
            if (row.Score.HasValue && row.MaxScore.HasValue && row.MaxScore.Value > 0)
            {
                accumulator.AssignmentPercentages.Add(
                    row.Score.Value / row.MaxScore.Value * 100);
            }

            accumulator.RecordActivity(row.CreatedAt);
        }

        var examRows = await context.ExamResults
            .AsNoTracking()
            .Where(item => studentIdArray.Contains(item.StudentId)
                && item.Exam.InstitutionId == institutionId
                && item.Exam.ExamDate >= fromDate
                && item.Exam.ExamDate <= toDate
                && (!gradeLevel.HasValue || item.Exam.TargetGradeLevel == gradeLevel.Value))
            .Select(item => new
            {
                item.StudentId,
                item.Score,
                item.Exam.MaxScore,
                item.Exam.ExamDate
            })
            .ToListAsync(cancellationToken);

        foreach (var row in examRows)
        {
            if (!accumulators.TryGetValue(row.StudentId, out var accumulator))
            {
                continue;
            }

            accumulator.RecordActivity(row.ExamDate);
        }

        var sessionRows = await context.SessionAttendances
            .AsNoTracking()
            .Where(item => studentIdArray.Contains(item.StudentId)
                && item.Session.InstitutionId == institutionId
                && item.Session.ScheduledDate >= fromDate
                && item.Session.ScheduledDate <= toDate
                && item.Session.Status != Domain.Enums.SessionStatus.Cancelled)
            .Select(item => new
            {
                item.StudentId,
                item.AttendanceStatus,
                item.Session.ScheduledDate
            })
            .ToListAsync(cancellationToken);

        foreach (var row in sessionRows)
        {
            if (!accumulators.TryGetValue(row.StudentId, out var accumulator))
            {
                continue;
            }

            if (row.AttendanceStatus != Domain.Enums.AttendanceStatus.NotRecorded)
            {
                accumulator.RecordedAttendanceCount++;
                accumulator.AttendedSessionCount += row.AttendanceStatus is
                    Domain.Enums.AttendanceStatus.Present or Domain.Enums.AttendanceStatus.Late
                    ? 1
                    : 0;
            }

            accumulator.RecordActivity(row.ScheduledDate);
        }

        var goalRows = await context.AcademicGoals
            .AsNoTracking()
            .Where(item => studentIdArray.Contains(item.StudentId)
                && item.CreatedAt >= fromDate
                && item.CreatedAt <= toDate)
            .Select(item => new
            {
                item.StudentId,
                item.CurrentProgress,
                item.IsCompleted,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var row in goalRows)
        {
            if (!accumulators.TryGetValue(row.StudentId, out var accumulator))
            {
                continue;
            }

            accumulator.GoalCount++;
            accumulator.CompletedGoalCount += row.IsCompleted ? 1 : 0;
            accumulator.GoalProgressTotal += row.CurrentProgress;
            accumulator.RecordActivity(row.CreatedAt);
        }

        return accumulators.Select(item => item.Value.ToMetrics(item.Key)).ToArray();
    }

    private sealed class EarlyWarningAccumulator
    {
        public int AssignmentCount { get; set; }
        public int SubmittedAssignmentCount { get; set; }
        public int GradedAssignmentCount { get; set; }
        public List<decimal> AssignmentPercentages { get; } = [];
        public int RecordedAttendanceCount { get; set; }
        public int AttendedSessionCount { get; set; }
        public int GoalCount { get; set; }
        public int CompletedGoalCount { get; set; }
        public int GoalProgressTotal { get; set; }
        public DateTime? LastActivityAt { get; private set; }

        public void RecordActivity(DateTime activityAt)
        {
            if (!LastActivityAt.HasValue || activityAt > LastActivityAt.Value)
            {
                LastActivityAt = activityAt;
            }
        }

        public CoachingStudentEarlyWarningMetrics ToMetrics(Guid studentId) =>
            new(
                studentId,
                AssignmentCount,
                SubmittedAssignmentCount,
                GradedAssignmentCount,
                AssignmentPercentages.Count == 0
                    ? null
                    : Math.Round(AssignmentPercentages.Average(), 2),
                RecordedAttendanceCount,
                AttendedSessionCount,
                GoalCount,
                CompletedGoalCount,
                GoalCount == 0 ? 0 : (int)Math.Round((decimal)GoalProgressTotal / GoalCount),
                LastActivityAt);
    }
}

public sealed class CoachingAdminRepository : ICoachingAdminRepository
{
    private readonly CoachingDbContext _context;

    public CoachingAdminRepository(CoachingDbContext context)
    {
        _context = context;
    }

    public async Task<CoachingAdminOverviewDto> GetOverviewAsync(
        int recentLimit,
        CancellationToken cancellationToken = default,
        Guid? institutionId = null,
        IReadOnlyCollection<Guid>? scopedStudentIds = null)
    {
        var assignments = _context.Assignments.AsNoTracking().AsQueryable();
        var sessions = _context.CoachingSessions.AsNoTracking().AsQueryable();
        var exams = _context.Exams.AsNoTracking().AsQueryable();
        var goals = _context.AcademicGoals.AsNoTracking().AsQueryable();

        if (institutionId.HasValue)
        {
            assignments = assignments.Where(item => item.InstitutionId == institutionId.Value);
            sessions = sessions.Where(item => item.InstitutionId == institutionId.Value);
            exams = exams.Where(item => item.InstitutionId == institutionId.Value);

            goals = scopedStudentIds is null
                ? goals.Where(_ => false)
                : goals.Where(item => scopedStudentIds.Contains(item.StudentId));
        }

        var assignmentIds = assignments.Select(item => item.Id);
        var examIds = exams.Select(item => item.Id);
        var assignmentStudents = _context.AssignmentStudents
            .AsNoTracking()
            .Where(item => assignmentIds.Contains(item.AssignmentId));
        var examResults = _context.ExamResults
            .AsNoTracking()
            .Where(item => examIds.Contains(item.ExamId));

        var totalAssignments = await assignments.CountAsync(cancellationToken);
        var activeAssignments = await assignments
            .CountAsync(item => item.Status == Domain.Enums.AssignmentStatus.Active, cancellationToken);
        var completedAssignments = await assignments
            .CountAsync(item => item.Status == Domain.Enums.AssignmentStatus.Completed, cancellationToken);
        var cancelledAssignments = await assignments
            .CountAsync(item => item.Status == Domain.Enums.AssignmentStatus.Cancelled, cancellationToken);
        var totalAssignmentStudents = await assignmentStudents.CountAsync(cancellationToken);
        var submittedAssignmentStudents = await assignmentStudents
            .CountAsync(item => item.Status == Domain.Enums.StudentAssignmentStatus.Submitted
                || item.Status == Domain.Enums.StudentAssignmentStatus.Graded,
                cancellationToken);
        var totalExams = await exams.CountAsync(cancellationToken);
        var totalExamResults = await examResults.CountAsync(cancellationToken);
        var totalSessions = await sessions.CountAsync(cancellationToken);
        var upcomingSessions = await sessions
            .CountAsync(item => item.Status == Domain.Enums.SessionStatus.Scheduled
                && item.ScheduledDate >= DateTime.UtcNow,
                cancellationToken);
        var totalGoals = await goals.CountAsync(cancellationToken);
        var completedGoals = await goals
            .CountAsync(item => item.IsCompleted, cancellationToken);

        var recentAssignments = await assignments
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(recentLimit)
            .Select(item => new
            {
                item.Id,
                item.TeacherId,
                item.InstitutionId,
                item.Title,
                item.Status,
                item.DueDate,
                item.CreatedAt,
                StudentCount = item.AssignedStudents.Count,
                SubmittedStudentCount = item.AssignedStudents.Count(student =>
                    student.Status == Domain.Enums.StudentAssignmentStatus.Submitted
                    || student.Status == Domain.Enums.StudentAssignmentStatus.Graded)
            })
            .ToListAsync(cancellationToken);

        return new CoachingAdminOverviewDto(
            totalAssignments,
            activeAssignments,
            completedAssignments,
            cancelledAssignments,
            totalAssignmentStudents,
            submittedAssignmentStudents,
            totalExams,
            totalExamResults,
            totalSessions,
            upcomingSessions,
            totalGoals,
            completedGoals,
            recentAssignments.Select(item => new CoachingAdminAssignmentDto(
                item.Id,
                item.TeacherId,
                item.InstitutionId,
                item.Title,
                item.Status.ToString(),
                item.DueDate,
                item.StudentCount,
                item.SubmittedStudentCount,
                item.CreatedAt)).ToList());
    }

    public async Task<PagedRepositoryResult<CoachingAdminAssignmentListDto>> GetAssignmentsAsync(
        int pageNumber,
        int pageSize,
        string? status,
        string? source,
        string? search,
        CancellationToken cancellationToken = default,
        Guid? institutionId = null)
    {
        var query = _context.Assignments.AsNoTracking().AsQueryable();

        if (institutionId.HasValue)
            query = query.Where(assignment => assignment.InstitutionId == institutionId.Value);

        if (Enum.TryParse<Domain.Enums.AssignmentStatus>(status, true, out var parsedStatus))
            query = query.Where(assignment => assignment.Status == parsedStatus);

        if (Enum.TryParse<Domain.Enums.AssignmentSource>(source, true, out var parsedSource))
            query = query.Where(assignment => assignment.Source == parsedSource);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(assignment =>
                EF.Functions.ILike(assignment.Title, $"%{term}%")
                || (assignment.Description != null && EF.Functions.ILike(assignment.Description, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(assignment => assignment.CreatedAt)
            .ThenByDescending(assignment => assignment.Id)
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .Select(assignment => new CoachingAdminAssignmentListDto(
                assignment.Id,
                assignment.TeacherId,
                assignment.InstitutionId,
                assignment.Title,
                assignment.Source.ToString(),
                assignment.BookTitle,
                assignment.BookStartPage,
                assignment.BookEndPage,
                assignment.Status,
                assignment.DueDate,
                assignment.AssignedStudents.Count,
                assignment.AssignedStudents.Count(student =>
                    student.Status == Domain.Enums.StudentAssignmentStatus.Submitted
                    || student.Status == Domain.Enums.StudentAssignmentStatus.Graded),
                assignment.AssignedStudents.SelectMany(student => student.SubmissionAttachments).Count(),
                assignment.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<CoachingAdminAssignmentListDto>(items, totalCount);
    }

    public async Task<PagedRepositoryResult<CoachingAdminSessionListDto>> GetSessionsAsync(
        int pageNumber,
        int pageSize,
        string? status,
        string? search,
        CancellationToken cancellationToken = default,
        Guid? institutionId = null)
    {
        var query = _context.CoachingSessions.AsNoTracking().AsQueryable();

        if (institutionId.HasValue)
            query = query.Where(session => session.InstitutionId == institutionId.Value);

        if (Enum.TryParse<Domain.Enums.SessionStatus>(status, true, out var parsedStatus))
            query = query.Where(session => session.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(session =>
                EF.Functions.ILike(session.Title, $"%{term}%")
                || (session.Description != null && EF.Functions.ILike(session.Description, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(session => session.ScheduledDate)
            .ThenByDescending(session => session.Id)
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .Select(session => new CoachingAdminSessionListDto(
                session.Id,
                session.TeacherId,
                session.InstitutionId,
                session.Title,
                session.SessionType,
                session.ScheduledDate,
                session.DurationMinutes,
                session.Status,
                session.Attendances.Count,
                session.Attendances.Count(attendance =>
                    attendance.AttendanceStatus == Domain.Enums.AttendanceStatus.Present),
                session.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<CoachingAdminSessionListDto>(items, totalCount);
    }

    public async Task<PagedRepositoryResult<CoachingAdminExamListDto>> GetExamsAsync(
        int pageNumber,
        int pageSize,
        string? examType,
        string? search,
        CancellationToken cancellationToken = default,
        Guid? institutionId = null)
    {
        var query = _context.Exams.AsNoTracking().AsQueryable();

        if (institutionId.HasValue)
            query = query.Where(exam => exam.InstitutionId == institutionId.Value);

        if (Enum.TryParse<Domain.Enums.ExamType>(examType, true, out var parsedExamType))
            query = query.Where(exam => exam.ExamType == parsedExamType);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(exam =>
                EF.Functions.ILike(exam.Title, $"%{term}%")
                || (exam.Description != null && EF.Functions.ILike(exam.Description, $"%{term}%"))
                || (exam.Subject != null && EF.Functions.ILike(exam.Subject, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(exam => exam.ExamDate)
            .ThenByDescending(exam => exam.Id)
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .Select(exam => new CoachingAdminExamListDto(
                exam.Id,
                exam.CreatedByTeacherId,
                exam.InstitutionId,
                exam.Title,
                exam.ExamType,
                exam.ExamDate,
                exam.MaxScore,
                exam.Results.Count,
                exam.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<CoachingAdminExamListDto>(items, totalCount);
    }

    public async Task<PagedRepositoryResult<CoachingAdminGoalListDto>> GetGoalsAsync(
        int pageNumber,
        int pageSize,
        bool? completed,
        string? search,
        CancellationToken cancellationToken = default,
        Guid? institutionId = null,
        IReadOnlyCollection<Guid>? scopedStudentIds = null)
    {
        var query = _context.AcademicGoals.AsNoTracking().AsQueryable();

        if (institutionId.HasValue)
        {
            query = scopedStudentIds is null
                ? query.Where(_ => false)
                : query.Where(goal => scopedStudentIds.Contains(goal.StudentId));
        }

        if (completed.HasValue)
            query = query.Where(goal => goal.IsCompleted == completed.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(goal =>
                EF.Functions.ILike(goal.Title, $"%{term}%")
                || (goal.Description != null && EF.Functions.ILike(goal.Description, $"%{term}%"))
                || (goal.TargetSubject != null && EF.Functions.ILike(goal.TargetSubject, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(goal => goal.CreatedAt)
            .ThenByDescending(goal => goal.Id)
            .Skip(CoachingPaging.GetSkip(pageNumber, pageSize))
            .Take(pageSize)
            .Select(goal => new CoachingAdminGoalListDto(
                goal.Id,
                goal.StudentId,
                goal.SetByTeacherId,
                goal.Title,
                goal.Category,
                goal.TargetDate,
                goal.CurrentProgress,
                goal.IsCompleted,
                goal.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedRepositoryResult<CoachingAdminGoalListDto>(items, totalCount);
    }
}
