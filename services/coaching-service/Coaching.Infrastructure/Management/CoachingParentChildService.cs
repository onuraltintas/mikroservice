using System.Globalization;
using Coaching.Application.Authorization;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed record ParentChildOverdueItem(string Title, DateOnly Date, string Kind);
public sealed record ParentChildNextSession(DateTime StartsAt, string Title, string? TeacherName);
public sealed record ParentChildExamCountdown(string Name, DateOnly Date, int DaysLeft);
public sealed record ParentChildAttentionItem(string Kind, string Text, string? Link = null);
public sealed record ParentChildWeek(DateOnly WeekStart, int Planned, int Completed, IReadOnlyList<ParentChildOverdueItem> Overdue,
    ParentChildNextSession? NextSession, ParentChildExamCountdown? ExamCountdown, IReadOnlyList<ParentChildAttentionItem> Attention);
public sealed record ParentChildAdherenceWeek(DateOnly WeekStart, int Planned, int Completed, long? Minutes);
public sealed record ParentChildTeacherNote(string TeacherName, string Note, DateTime ReviewedAt);
public sealed record ParentChildDecision(string Title, string Note, DateOnly DueOn, string Status, bool IsOverdue, DateTime? CompletedAt);
public sealed record ParentChildReviewNote(string Note, DateTime ReviewedAt, string TeacherName);
public sealed record ParentChildSingleWeeklyReview(DateOnly WeekStart, IReadOnlyList<ParentChildReviewNote> Reviews, IReadOnlyList<ParentChildDecision> Decisions);
public sealed record ParentChildWeeklyReview(DateOnly WeekStart, int Planned, int Completed, long? Minutes, bool ReflectionWritten,
    IReadOnlyList<ParentChildTeacherNote> TeacherNotes, IReadOnlyList<ParentChildDecision> Decisions);

/// <summary>A parent's authorized read of one child; only <see cref="CoachingParentChildService.RequireAsync"/> creates it.</summary>
public sealed class ParentChildScope
{
    internal ParentChildScope(Guid studentId, string firstName) { StudentId = studentId; FirstName = firstName; }
    public Guid StudentId { get; }
    public string FirstName { get; }
}

/// <summary>
/// V3 (parent panel): the parent's view of the child's Coaching week, weekly adherence and the teacher's weekly notes.
/// Read-only; "today" and the week are the child's own (CoachingStudentZone), as on the child's Bugün screen. Texts speak
/// to the parent ("sen") about the child by first name.
/// </summary>
public sealed class CoachingParentChildService(CoachingDbContext db, ICoachingAccessPolicy access, OwnedCoachingDirectory directory,
    ICoachingAccountDirectory accounts, CoachingStudentTodayService today, TimeProvider clock)
{
    public const int MaxWeeks = CoachingStudentStudyReportService.MaxWeeks;
    private const int QuietDays = 3;
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Only a Parent, and only while Identity's authorize-read (product Coaching) allows this child.</summary>
    public async Task<ParentChildScope> RequireAsync(Guid studentId, CancellationToken ct)
    {
        if (!access.IsParent || access.CurrentUserId is not { } parent)
            throw new BusinessRuleException("Authorization.Forbidden", "Bu sayfa yalnız bağlı veliler içindir.");
        var child = await directory.RequireParentReadAsync(parent, studentId, ct);
        return new ParentChildScope(studentId, string.IsNullOrWhiteSpace(child.FirstName) ? "Çocuğun" : child.FirstName.Trim());
    }

    public async Task<ParentChildWeek> GetWeekAsync(ParentChildScope child, IReadOnlyList<ParentChildOverdueItem> overdue, CancellationToken ct)
    {
        var student = child.StudentId; var name = child.FirstName;
        var facts = await today.ReadAsync(student, ct);
        var now = clock.GetUtcNow().UtcDateTime;

        ParentChildNextSession? next = null;
        if (facts.NextSession is { } session)
        {
            var teacherId = await db.CoachingSessions.AsNoTracking().Where(x => x.Id == session.Id).Select(x => x.TeacherId).SingleAsync(ct);
            var teacher = (await accounts.GetAccountsAsync([teacherId], ct)).SingleOrDefault(x => x.UserId == teacherId);
            next = new(session.ScheduledDate, session.Title, teacher is null ? null : $"{teacher.FirstName} {teacher.LastName}".Trim());
        }
        var exam = facts.Exam is { } e
            ? new ParentChildExamCountdown(e.Source == "Student" ? $"{Possessive(name)} sınavı" : e.Name, e.Date, e.DaysLeft) : null;

        var attention = new List<ParentChildAttentionItem>();
        // Every item opens the child's page in the parent panel (Koçluk tab).
        var page = $"/coaching-portal/parent/children/{student}";
        var lateWork = await db.AssignmentStudents.AsNoTracking().CountAsync(x => x.StudentId == student
            && x.Assignment.Status == AssignmentStatus.Active && x.SubmittedAt == null
            && (x.Status == StudentAssignmentStatus.Assigned || x.Status == StudentAssignmentStatus.InProgress)
            && x.Assignment.DueDate < now, ct);
        if (lateWork > 0) attention.Add(new("overdueAssignments", $"{Possessive(name)} teslim tarihi geçmiş {lateWork} ödevi var.", page));
        var weekAgo = now.AddDays(-7);
        var missed = await db.SessionAttendances.AsNoTracking().CountAsync(x => x.StudentId == student
            && x.AttendanceStatus == AttendanceStatus.Absent && x.Session.Status != SessionStatus.Cancelled
            && x.Session.ScheduledDate >= weekAgo && x.Session.ScheduledDate <= now, ct);
        if (missed > 0) attention.Add(new("missedSessions", $"{name} son 7 günde {missed} seansa katılmadı.", page));
        var day = facts.Today;
        var overdueFees = await db.CoachingFeeAgreements.AsNoTracking().Where(x => x.StudentId == student && !x.IsCancelled)
            .SumAsync(x => Math.Max(0m, (x.Installments.Where(i => i.DueOn < day).Sum(i => (decimal?)i.Amount) ?? 0m) - x.PaidAmount), ct);
        if (overdueFees > 0) attention.Add(new("overdueFees", $"Vadesi geçmiş {overdueFees.ToString("N2", Turkish)} TL ödeme var.", page));
        if (facts.HasActivePlan)
        {
            if (facts.LastActiveDay is not { } last) attention.Add(new("noStudy", $"{name} henüz hiç çalışma kaydetmedi.", page));
            else if (day.DayNumber - last.DayNumber - 1 is var quiet && quiet >= QuietDays)
                attention.Add(new("noStudy", $"{name} {quiet} gündür çalışma kaydetmedi.", page));
        }
        if (facts.WeeklyReview.Needed) attention.Add(new("weeklyReviewWaiting", $"{name} geçen haftanın değerlendirmesini henüz yazmadı.", page));
        return new(facts.Week.WeekStart, facts.Week.Planned, facts.Week.Completed, overdue, next, exam, attention);
    }

    /// <summary>T7 week totals (planned, completed, studied minutes) of the last <paramref name="weeks"/> weeks, oldest first.</summary>
    public async Task<IReadOnlyList<ParentChildAdherenceWeek>> GetAdherenceAsync(ParentChildScope child, int weeks, CancellationToken ct)
    {
        Validate(weeks);
        var current = await CurrentWeekAsync(child.StudentId, ct);
        var totals = await CoachingStudentStudyReportService.WeeksAsync(db, child.StudentId, current.AddDays(-7 * (weeks - 1)), weeks, ct);
        return totals.Select(x => new ParentChildAdherenceWeek(x.FromDate, x.ScheduledTasks, x.CompletedTasks, x.ActualMinutes)).ToArray();
    }

    /// <summary>
    /// The teacher's notes on the child's weekly reflections and the weekly decisions of the last <paramref name="weeks"/>
    /// weeks, newest first. The child's own reflection text stays between the child and the teacher.
    /// </summary>
    public async Task<IReadOnlyList<ParentChildWeeklyReview>> GetWeeklyReviewsAsync(ParentChildScope child, int weeks, CancellationToken ct)
    {
        Validate(weeks);
        return await ReadReviewsAsync(child.StudentId, null, weeks, ct);
    }

    /// <summary>
    /// One week of <see cref="GetWeeklyReviewsAsync"/>: the given Monday (one of the last 12 weeks), else the newest of the
    /// last 4 weeks with a teacher note or decision, else last week.
    /// </summary>
    public async Task<ParentChildSingleWeeklyReview> GetWeeklyReviewAsync(ParentChildScope child, DateOnly? weekStart, CancellationToken ct)
    {
        var current = await CurrentWeekAsync(child.StudentId, ct);
        ParentChildWeeklyReview week;
        if (weekStart is { } requested)
        {
            if (requested.DayOfWeek != DayOfWeek.Monday || requested > current || requested < current.AddDays(-7 * (MaxWeeks - 1)))
                throw new ArgumentException("Son 12 haftadan birinin pazartesi tarihini seçin.");
            week = (await ReadReviewsAsync(child.StudentId, requested, 1, ct))[0];
        }
        else
        {
            var recent = await ReadReviewsAsync(child.StudentId, null, 4, ct);
            week = recent.FirstOrDefault(x => x.TeacherNotes.Count > 0 || x.Decisions.Count > 0) ?? recent[1];
        }
        return new(week.WeekStart, week.TeacherNotes.Select(x => new ParentChildReviewNote(x.Note, x.ReviewedAt, x.TeacherName)).ToArray(), week.Decisions);
    }

    private async Task<IReadOnlyList<ParentChildWeeklyReview>> ReadReviewsAsync(Guid student, DateOnly? newest, int weeks, CancellationToken ct)
    {
        var zoneId = await CoachingStudentZone.IdAsync(db, student, ct);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), CoachingStudentZone.Find(zoneId)).DateTime);
        var current = newest ?? Monday(day); var first = current.AddDays(-7 * (weeks - 1));
        var totals = (await CoachingStudentStudyReportService.WeeksAsync(db, student, first, weeks, ct)).ToDictionary(x => x.FromDate);
        var reflections = await db.Set<WeeklyReflection>().AsNoTracking()
            .Where(x => x.StudentId == student && x.WeekStart >= first && x.WeekStart <= current)
            .Select(x => new { x.Id, x.WeekStart }).ToListAsync(ct);
        var reflectionIds = reflections.Select(x => x.Id).ToArray();
        var reviews = await db.Set<WeeklyReflectionReview>().AsNoTracking().Where(x => Enumerable.Contains(reflectionIds, x.ReflectionId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new { x.ReflectionId, x.TeacherId, x.Note, x.CreatedAt }).ToListAsync(ct);
        var decisions = await db.Set<WeeklyDecision>().AsNoTracking()
            .Where(x => x.StudentId == student && x.ReviewWeekStart >= first && x.ReviewWeekStart <= current)
            .OrderBy(x => x.DueOn).ThenBy(x => x.Id).ToListAsync(ct);
        var teacherIds = reviews.Select(x => x.TeacherId).Distinct().ToArray();
        var teachers = teacherIds.Length == 0 ? new Dictionary<Guid, string>()
            : (await accounts.GetAccountsAsync(teacherIds, ct)).ToDictionary(x => x.UserId, x => $"{x.FirstName} {x.LastName}".Trim());
        var weekOf = reflections.ToDictionary(x => x.Id, x => x.WeekStart);
        return Enumerable.Range(0, weeks).Select(index =>
        {
            var week = current.AddDays(-7 * index);
            var total = totals.GetValueOrDefault(week);
            return new ParentChildWeeklyReview(week, total?.ScheduledTasks ?? 0, total?.CompletedTasks ?? 0, total?.ActualMinutes,
                reflections.Any(x => x.WeekStart == week),
                reviews.Where(x => weekOf[x.ReflectionId] == week)
                    .Select(x => new ParentChildTeacherNote(teachers.GetValueOrDefault(x.TeacherId) ?? "Öğretmen", x.Note, x.CreatedAt)).ToArray(),
                decisions.Where(x => x.ReviewWeekStart == week)
                    .Select(x => new ParentChildDecision(x.Title, x.Note, x.DueOn, x.Status, x.IsOpen && x.DueOn < day, x.CompletedAt)).ToArray());
        }).ToArray();
    }

    private async Task<DateOnly> CurrentWeekAsync(Guid student, CancellationToken ct)
    {
        var zone = CoachingStudentZone.Find(await CoachingStudentZone.IdAsync(db, student, ct));
        return Monday(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime));
    }

    private static DateOnly Monday(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private static void Validate(int weeks)
    {
        if (weeks is < 1 or > MaxWeeks) throw new ArgumentException($"Hafta sayısı 1 ile {MaxWeeks} arasında olmalı.");
    }

    /// <summary>"Elif'in", "Ada'nın", "Umut'un", "Göksu'nun": the Turkish genitive of a first name.</summary>
    internal static string Possessive(string name)
    {
        var lower = name.ToLower(Turkish);
        var vowel = lower.LastOrDefault(c => "aıoueiöü".Contains(c));
        var suffix = vowel switch { 'a' or 'ı' => "ın", 'o' or 'u' => "un", 'ö' or 'ü' => "ün", _ => "in" };
        return "aıoueiöü".Contains(lower.LastOrDefault()) ? $"{name}'n{suffix}" : $"{name}'{suffix}";
    }
}
