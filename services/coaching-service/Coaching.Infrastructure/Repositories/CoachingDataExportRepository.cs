using Coaching.Application.Interfaces;
using Coaching.Application.Queries.ExportCoachingData;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Repositories;

public sealed class CoachingDataExportRepository(CoachingDbContext context)
    : ICoachingDataExportRepository
{
    public async Task<CoachingDataExportDto> ExportStudentDataAsync(
        Guid studentId,
        DateTimeOffset exportedAt,
        CancellationToken cancellationToken)
    {
        var assignmentRows = await context.AssignmentStudents
            .AsNoTracking()
            .Where(row => row.StudentId == studentId)
            .Include(row => row.Assignment)
            .OrderBy(row => row.CreatedAt)
            .ToListAsync(cancellationToken);
        var assignments = assignmentRows.Select(row => new CoachingDataAssignmentDto(
            row.AssignmentId, row.Assignment.Title, row.Assignment.Subject, row.Assignment.DueDate,
            row.Status.ToString(), row.SubmittedAt, row.Score, row.StudentNote, row.TeacherFeedback)).ToArray();

        var examRows = await context.ExamResults
            .AsNoTracking()
            .Where(row => row.StudentId == studentId)
            .Include(row => row.Exam)
            .OrderBy(row => row.CreatedAt)
            .ToListAsync(cancellationToken);
        var exams = examRows.Select(row => new CoachingDataExamDto(
            row.ExamId, row.Exam.Title, row.Exam.Subject, row.Exam.ExamDate, row.Exam.MaxScore,
            row.Score, row.CorrectAnswers, row.WrongAnswers, row.EmptyAnswers,
            row.SubjectScoresJson, row.Ranking)).ToArray();

        var goalRows = await context.AcademicGoals.AsNoTracking()
            .Where(goal => goal.StudentId == studentId)
            .OrderBy(goal => goal.CreatedAt)
            .ToListAsync(cancellationToken);
        var goals = goalRows.Select(goal => new CoachingDataGoalDto(
            goal.Id, goal.Title, goal.Description, goal.Category.ToString(), goal.CurrentProgress,
            goal.IsCompleted, goal.TargetDate, goal.TargetScore)).ToArray();

        var sessionRows = await context.SessionAttendances.AsNoTracking()
            .Where(attendance => attendance.StudentId == studentId)
            .Include(attendance => attendance.Session)
            .OrderBy(attendance => attendance.CreatedAt)
            .ToListAsync(cancellationToken);
        var sessions = sessionRows.Select(attendance => new CoachingDataSessionDto(
            attendance.SessionId, attendance.Session.Title, attendance.Session.ScheduledDate,
            attendance.Session.DurationMinutes, attendance.Session.Status.ToString(),
            attendance.AttendanceStatus.ToString(), attendance.StudentNote,
            attendance.Session.TeacherNotesVisibility == CoachingNoteVisibility.StudentVisible
                ? attendance.Session.TeacherNotes
                : null)).ToArray();

        var agreementRows = await context.CoachingAgreementAcknowledgements.AsNoTracking()
            .Where(acknowledgement => acknowledgement.SubjectStudentId == studentId)
            .Include(acknowledgement => acknowledgement.AgreementDocument)
            .OrderBy(acknowledgement => acknowledgement.AcknowledgedAt)
            .ToListAsync(cancellationToken);
        var agreements = agreementRows.Select(acknowledgement => new CoachingDataAgreementDto(
            acknowledgement.Id, acknowledgement.AgreementDocumentId,
            acknowledgement.AgreementDocument.DocumentVersion,
            acknowledgement.AgreementDocument.Locale,
            acknowledgement.AgreementDocument.Title,
            acknowledgement.AgreementDocument.DocumentReference,
            acknowledgement.AgreementDocument.ContentSha256,
            acknowledgement.PartyRole.ToString(),
            acknowledgement.AcknowledgedAt,
            acknowledgement.WithdrawnAt)).ToArray();

        return new CoachingDataExportDto(
            "1.0", studentId, exportedAt, assignments, exams, goals, sessions, agreements);
    }
}
