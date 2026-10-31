using System.Text.Json;
using Coaching.Application.Interfaces;
using Coaching.Domain;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed record StudentExamOverviewItem(Guid ExamId, Guid ResultId, string Source, string Title, DateTime ExamDate, string ExamType,
    string? ExamCode, string? ExamName, decimal? Net, decimal Score, decimal MaxScore, IReadOnlyList<PracticeSectionSummary> Sections,
    int? Rank, int? Participants, decimal? TopPercent);
public sealed record StudentExamTrendPoint(Guid ExamId, DateTime ExamDate, decimal Net, string Source);
public sealed record StudentExamTrend(string ExamCode, string ExamName, decimal MaxNet, IReadOnlyList<StudentExamTrendPoint> Points);
public sealed record StudentExamSubject(string ExamCode, string SectionCode, string Name, int QuestionCount, decimal AverageNet,
    decimal SuccessRatio, int ExamsUsed, bool Weakest);
public sealed record StudentExamOverview(IReadOnlyList<StudentExamOverviewItem> Exams, IReadOnlyList<StudentExamTrend> Trends,
    IReadOnlyList<StudentExamSubject> Subjects);

/// <summary>
/// Student panel redesign T6 (31.10.2026): one list of a student's exams — entered by the student, recorded by a teacher, or
/// imported by the institution with the student's place — plus a net trend per exam scope and the average net per section of
/// the last three exams of each scope. Future-dated exams are left out, and a student's own copy of a deneme that a teacher or
/// the institution recorded for the same scope on the same Türkiye day is a duplicate (the K4 rule of the score estimate).
/// </summary>
public sealed class CoachingStudentExamOverviewService(CoachingDbContext db, ICoachingIdentityAuthorizationClient authorization, TimeProvider clock)
{
    public const string StudentSource = "Student", TeacherSource = "Teacher", InstitutionSource = "Institution";
    private const int MaxExams = 200;
    private const int ExamsPerSubject = 3;

    public async Task<StudentExamOverview> GetAsync(Guid viewer, Guid studentId, CancellationToken ct)
    {
        if (viewer == Guid.Empty || studentId == Guid.Empty || viewer != studentId && !(await authorization.AuthorizeStudentReadAsync(viewer, [studentId], ct)).Contains(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu öğrencinin denemelerini görme yetkiniz yok.");

        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await (from result in db.ExamResults.AsNoTracking()
                          join exam in db.Exams.AsNoTracking() on result.ExamId equals exam.Id
                          where result.StudentId == studentId && exam.ExamDate <= now
                          orderby exam.ExamDate descending
                          select new
                          {
                              ExamId = exam.Id, ResultId = result.Id, exam.Title, exam.ExamDate, exam.ExamType, exam.MaxScore,
                              exam.StudentOwnerId, exam.CreatedByAdminId, result.Score, result.PracticeExamJson
                          }).Take(MaxExams).ToListAsync(ct);

        var parsed = rows.Select(x =>
        {
            var source = x.StudentOwnerId is not null ? StudentSource : x.CreatedByAdminId is not null ? InstitutionSource : TeacherSource;
            var summary = x.PracticeExamJson is null ? null : PracticeExamRules.Summarize(JsonSerializer.Deserialize<PracticeExamInput>(x.PracticeExamJson)!);
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.ExamDate, DateTimeKind.Utc), CoachingTime.Turkey));
            return (Row: x, Source: source, Summary: summary, Day: day);
        }).ToArray();
        var recorded = parsed.Where(x => x.Source != StudentSource && x.Summary is not null).Select(x => (x.Summary!.ExamCode, x.Day)).ToHashSet();
        var kept = parsed.Where(x => x.Source != StudentSource || x.Summary is null || !recorded.Contains((x.Summary.ExamCode, x.Day))).ToArray();

        var places = await InstitutionPlacesAsync(studentId, kept.Where(x => x.Source == InstitutionSource).Select(x => x.Row.ExamId).ToArray(), ct);
        var items = kept.Select(x =>
        {
            places.TryGetValue(x.Row.ExamId, out var place);
            return new StudentExamOverviewItem(x.Row.ExamId, x.Row.ResultId, x.Source, x.Row.Title, x.Row.ExamDate, x.Row.ExamType.ToString(),
                x.Summary?.ExamCode, x.Summary?.Name, x.Summary?.Net, x.Row.Score, x.Row.MaxScore, x.Summary?.Sections ?? [],
                place?.Rank, place?.Participants, place?.TopPercent);
        }).ToArray();

        var practice = kept.Where(x => x.Summary is not null).ToArray();
        var trends = practice.GroupBy(x => x.Summary!.ExamCode).Select(group =>
        {
            var profile = PracticeExamRules.Profiles.Single(x => x.Code == group.Key);
            return new StudentExamTrend(group.Key, profile.Name, profile.Sections.Sum(x => x.QuestionCount),
                group.OrderBy(x => x.Row.ExamDate).Select(x => new StudentExamTrendPoint(x.Row.ExamId, x.Row.ExamDate, x.Summary!.Net, x.Source)).ToArray());
        }).OrderByDescending(x => x.Points.Count).ThenBy(x => x.ExamCode, StringComparer.Ordinal).ToArray();

        var subjects = new List<StudentExamSubject>();
        foreach (var group in practice.GroupBy(x => x.Summary!.ExamCode))
        {
            var recent = group.OrderByDescending(x => x.Row.ExamDate).Take(ExamsPerSubject).Select(x => x.Summary!).ToArray();
            var profile = PracticeExamRules.Profiles.Single(x => x.Code == group.Key);
            var rowsForScope = profile.Sections.Select(section =>
            {
                var nets = recent.Select(x => x.Sections.Single(y => y.SectionCode == section.Code).Net).ToArray();
                var average = Math.Round(nets.Average(), 2, MidpointRounding.AwayFromZero);
                var ratio = section.QuestionCount == 0 ? 0 : Math.Round(Math.Max(0, average) / section.QuestionCount, 3, MidpointRounding.AwayFromZero);
                return (section, average, ratio);
            }).ToArray();
            var weakest = rowsForScope.MinBy(x => x.ratio).section.Code;
            subjects.AddRange(rowsForScope.Select(x => new StudentExamSubject(group.Key, x.section.Code, x.section.Name, x.section.QuestionCount,
                x.average, x.ratio, recent.Length, x.section.Code == weakest)));
        }

        return new(items, trends, subjects);
    }

    private sealed record Place(int Rank, int Participants, decimal TopPercent);

    private async Task<Dictionary<Guid, Place>> InstitutionPlacesAsync(Guid studentId, IReadOnlyCollection<Guid> examIds, CancellationToken ct)
    {
        if (examIds.Count == 0) return [];
        var results = await db.ExamResults.AsNoTracking().Where(x => examIds.Contains(x.ExamId))
            .Select(x => new { x.ExamId, x.StudentId, x.PracticeExamJson }).ToListAsync(ct);
        var places = new Dictionary<Guid, Place>();
        foreach (var exam in results.GroupBy(x => x.ExamId))
        {
            var nets = exam.Select(x => (x.StudentId, Net: x.PracticeExamJson is null ? 0m
                : PracticeExamRules.Summarize(JsonSerializer.Deserialize<PracticeExamInput>(x.PracticeExamJson)!).Net)).ToArray();
            var ranked = InstitutionExamRanking.Rank(nets.Select(x => (x.StudentId, x.Net)));
            if (ranked.TryGetValue(studentId, out var place)) places[exam.Key] = new(place.Rank, nets.Length, place.TopPercent);
        }
        return places;
    }
}
