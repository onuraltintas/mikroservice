using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingStudentStudyReportService(CoachingDbContext db, ICoachingAccessPolicy access,
    CoachingStudyPlanAccess? staffAccess = null) : IStudentStudyReportService
{
    public async Task<StudentStudyReport> GetForStudentAsync(Guid studentId, DateOnly fromDate, DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (staffAccess is null) throw new BusinessRuleException("Authorization.Forbidden", "Personel erişimi doğrulanamadı.");
        await staffAccess.RequireAsync(studentId, cancellationToken);
        return await ReadAsync(db, studentId, fromDate, toDate, cancellationToken);
    }
    public async Task<StudentStudyReport> GetAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu rapor yalnız öğrencinin kendi hesabında kullanılabilir.");
        return await ReadAsync(db, student, fromDate, toDate, cancellationToken);
    }

    public const int MaxWeeks = 12;
    public async Task<IReadOnlyList<StudyWeekTotals>> GetWeeksAsync(DateOnly fromDate, int weeks, CancellationToken cancellationToken = default)
    {
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu rapor yalnız öğrencinin kendi hesabında kullanılabilir.");
        if (weeks is < 1 or > MaxWeeks) throw new ArgumentOutOfRangeException(nameof(weeks));
        var toDate = fromDate.AddDays(weeks * 7 - 1);
        // One read for the whole span; each week is calculated exactly like a single-week report of the same rows.
        var rows = await CoachingStudyWorkReports.ReadAsync(db, student, fromDate, toDate, cancellationToken);
        return Enumerable.Range(0, weeks).Select(index =>
        {
            var from = fromDate.AddDays(index * 7); var to = from.AddDays(6);
            var report = CoachingStudyWorkReports.Calculate(from, to, rows);
            return new StudyWeekTotals(from, to, report.ScheduledTasks, report.CompletedTasks, report.ActualMinutes);
        }).ToArray();
    }

    internal static async Task<StudentStudyReport> ReadAsync(CoachingDbContext db, Guid student,
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        StudyReportCalculator.ValidatePeriod(fromDate, toDate);
        // Goals are a current snapshot, not historical progress in the selected task/exam period.
        var goals = await db.AcademicGoals.AsNoTracking().Where(x => x.StudentId == student)
            .OrderBy(x => x.IsCompleted).ThenBy(x => x.TargetDate).ThenBy(x => x.Id)
            .Select(x => new StudyGoalReport(x.Id, x.Title, x.SetByTeacherId.HasValue ? "TeacherSet" : "Unspecified",
                x.CurrentProgress, x.IsCompleted, x.TargetDate, x.TargetScore, x.TargetExamType, x.TargetSubject, x.TargetMaxScore))
            .Take(1001).ToListAsync(cancellationToken);
        if (goals.Count > 1000) throw new BusinessRuleException("StudyPlanning.GoalReportLimit",
            "Hedef sayısı rapor sınırını aşıyor. Hedefleri Hedefler ekranından inceleyebilirsiniz.");
        var rows = await CoachingStudyWorkReports.ReadAsync(db,student,fromDate,toDate,cancellationToken);
        var report = CoachingStudyWorkReports.Calculate(fromDate,toDate,rows);
        var topicIds = report.Topics.Where(x => x.TopicId.HasValue).Select(x => x.TopicId!.Value).ToArray();
        var names = await db.StudyCatalogTopics.AsNoTracking().Where(x => Enumerable.Contains(topicIds, x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var start = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = toDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var exams = await db.ExamResults.AsNoTracking()
            .Where(x => x.StudentId == student && x.Exam.ExamDate >= start && x.Exam.ExamDate <= end)
            .OrderBy(x => x.Exam.ExamDate).ThenBy(x => x.Id)
            .Select(x => new { ResultId = x.Id, x.ExamId, x.Exam.ExamDate, x.Exam.StudentOwnerId, x.Exam.ExamType, x.Exam.MaxScore, x.Score, x.LessonAnswersJson })
            .Take(1001).ToListAsync(cancellationToken);
        if (exams.Count > 1000) throw ReportLimit();
        var evidence = exams.Select(x => new GoalScoreEvidence(x.ResultId, x.ExamId,
            x.StudentOwnerId.HasValue ? "StudentReported" : "TeacherRecorded", x.ExamType,
            x.MaxScore, x.Score, x.ExamDate)).ToArray();
        var groups = exams.Where(x => x.MaxScore > 0).GroupBy(x => new { Source = x.StudentOwnerId.HasValue ? "StudentReported" : "TeacherRecorded", x.ExamType, x.MaxScore })
            .Select(g => new StudyExamGroup(g.Key.Source, g.Key.ExamType, g.Key.MaxScore, g.Count(),
                decimal.Round(g.Average(x => 100m * x.Score / x.MaxScore), 1)))
            .OrderBy(x => x.Source).ThenBy(x => x.ExamType).ThenBy(x => x.MaxScore).ToArray();
        var lessons = exams.Where(x => x.LessonAnswersJson is not null).SelectMany(x =>
            (System.Text.Json.JsonSerializer.Deserialize<LessonAnswerStatistics[]>(x.LessonAnswersJson!) ?? [])
                .Select(l => new { Source = x.StudentOwnerId.HasValue ? "StudentReported" : "TeacherRecorded", x.ExamType, Lesson = l }))
            .GroupBy(x => new { x.Source, x.ExamType, x.Lesson.LessonId, x.Lesson.TopicId })
            .Select(g => new StudyLessonResult(g.Key.Source, g.Key.ExamType, g.Key.LessonId, g.Key.TopicId,
                g.Last().Lesson.LessonName, g.Last().Lesson.TopicName, g.Sum(x => (long)x.Lesson.QuestionCount),
                g.Sum(x => (long)x.Lesson.Correct), g.Sum(x => (long)x.Lesson.Wrong), g.Sum(x => (long)x.Lesson.Empty)))
            .OrderBy(x => x.LessonId).ThenBy(x => x.TopicId).ThenBy(x => x.ExamType).ToArray();
        return report with { Topics = report.Topics.Select(x => x with {
            TopicName = x.TopicId.HasValue ? names.GetValueOrDefault(x.TopicId.Value) : null }).ToArray(),
            ExamGroups = groups, LessonResults = lessons, Goals = goals.Select(goal => goal with {
                ScoreAssessment = GoalScoreCalculator.Calculate(goal.TargetScore, goal.TargetMaxScore,
                    goal.TargetExamType, goal.TargetSubject, evidence) }).ToArray() };
    }
    private static BusinessRuleException ReportLimit() => new("StudyPlanning.ReportLimit",
        "Bu dönem çok fazla kayıt içeriyor. Tam rapor için daha kısa bir tarih aralığı seçin.");
}
