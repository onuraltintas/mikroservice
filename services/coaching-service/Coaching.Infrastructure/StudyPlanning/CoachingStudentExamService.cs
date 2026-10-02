using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingStudentExamService(CoachingDbContext db, ICoachingAccessPolicy access) : IStudentExamService
{
    public async Task<StudentExamView> CreateAsync(StudentExamInput request, CancellationToken cancellationToken = default)
    {
        var student = RequireStudent();
        var lessons = await ValidateAsync(request, cancellationToken);
        var exam = Exam.CreateStudentReported(student, request.Title, request.ExamType,
            request.ExamDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), request.MaxScore);
        var result = ExamResult.Create(exam.Id, student, request.Score);
        result.SetAnswerStatistics(request.CorrectAnswers, request.WrongAnswers, request.EmptyAnswers);
        result.SetLessonAnswers(lessons);
        exam.AddResult(result); db.Exams.Add(exam);
        await SaveAsync(cancellationToken);
        return View(exam);
    }

    public async Task<StudentExamView?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var student = RequireStudent();
        var exam = await db.Exams.AsNoTracking().Include(x => x.Results)
            .SingleOrDefaultAsync(x => x.Id == id && x.StudentOwnerId == student, cancellationToken);
        return exam is null ? null : View(exam);
    }

    public async Task<TargetSearchPage<StudentExamView>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var student = RequireStudent();
        if (pageNumber is < 1 or > 10000 || pageSize is < 1 or > 50) throw new ArgumentException("Invalid page.");
        var query = db.Exams.AsNoTracking().Where(x => x.StudentOwnerId == student);
        var total = await query.CountAsync(cancellationToken);
        var exams = await query.Include(x => x.Results).OrderByDescending(x => x.ExamDate).ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(exams.Select(View).ToArray(), total, pageNumber, pageSize);
    }

    public async Task<StudentExamView> ReplaceAsync(Guid id, ReplaceStudentExamRequest request, CancellationToken cancellationToken = default)
    {
        var student = RequireStudent();
        ArgumentNullException.ThrowIfNull(request);
        var exam = await OwnedAsync(id, student, request.ExpectedVersion, cancellationToken);
        var lessons = await ValidateAsync(request.Exam, cancellationToken);
        var input = request.Exam;
        // Validate a detached result first, so rejected lesson totals cannot partially mutate the tracked aggregate.
        var check = ExamResult.Create(exam.Id, student, input.Score);
        check.SetAnswerStatistics(input.CorrectAnswers, input.WrongAnswers, input.EmptyAnswers);
        check.SetLessonAnswers(lessons);
        var result = exam.Results.Single();
        result.UpdateEditableDetails(input.Score, input.CorrectAnswers, input.WrongAnswers, input.EmptyAnswers, null, null, null, input.MaxScore);
        result.SetLessonAnswers(lessons);
        exam.UpdateEditableDetails(input.Title, input.ExamType, null, null,
            input.ExamDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), null, input.MaxScore, null);
        await SaveAsync(cancellationToken);
        return View(exam);
    }

    public async Task DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var student = RequireStudent();
        var exam = await OwnedAsync(id, student, expectedVersion, cancellationToken);
        db.Exams.Remove(exam); await SaveAsync(cancellationToken);
    }

    private async Task<Exam> OwnedAsync(Guid id, Guid student, int version, CancellationToken ct)
    {
        if (version < 0) throw new ArgumentException("Invalid version.");
        var exam = await db.Exams.Include(x => x.Results).SingleOrDefaultAsync(x => x.Id == id && x.StudentOwnerId == student, ct)
            ?? throw new KeyNotFoundException("Sonuç bulunamadı.");
        if (exam.Version != version) throw Conflict();
        return exam;
    }

    private async Task<IReadOnlyList<LessonAnswerStatistics>> ValidateAsync(StudentExamInput request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200
            || !Enum.IsDefined(request.ExamType) || request.ExamDate == DateOnly.MinValue
            || request.ExamDate > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)
            || request.MaxScore is <= 0 or > 999.99m || request.Score < 0 || request.Score > request.MaxScore
            || decimal.Round(request.Score, 2) != request.Score || decimal.Round(request.MaxScore, 2) != request.MaxScore
            || request.CorrectAnswers < 0 || request.WrongAnswers < 0 || request.EmptyAnswers < 0
            || (long)request.CorrectAnswers + request.WrongAnswers + request.EmptyAnswers > 1000
            || request.Lessons is null || request.Lessons.Count > 50)
            throw new ArgumentException("Sınav bilgilerini ve soru sayılarını kontrol edin.");
        var check = ExamResult.Create(Guid.NewGuid(), Guid.NewGuid(), request.Score);
        check.SetAnswerStatistics(request.CorrectAnswers, request.WrongAnswers, request.EmptyAnswers);
        check.SetLessonAnswers(request.Lessons);
        var lessonIds = request.Lessons.Select(x => x.LessonId).Distinct().ToArray();
        var activeLessons = await db.StudyCatalogLessons.AsNoTracking().Where(x => x.IsActive && Enumerable.Contains(lessonIds, x.Id))
            .Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        if (activeLessons.Count != lessonIds.Length) throw new ArgumentException("Seçilen ders kullanılamıyor.");
        var topicIds = request.Lessons.Where(x => x.TopicId.HasValue).Select(x => x.TopicId!.Value).ToArray();
        var topics = await (from t in db.StudyCatalogTopics.AsNoTracking()
            join u in db.StudyCatalogUnits on t.UnitId equals u.Id
            where Enumerable.Contains(topicIds, t.Id) && t.IsActive && u.IsActive && t.LessonId == u.LessonId
                && !db.StudyCatalogTopics.Any(child => child.ParentId == t.Id)
                && (t.ParentId == null || db.StudyCatalogTopics.Any(p => p.Id == t.ParentId
                    && p.IsActive && p.LessonId == t.LessonId && p.UnitId == t.UnitId))
            select new { t.Id, t.LessonId, t.Name }).ToListAsync(ct);
        if (request.Lessons.Any(x => x.TopicId.HasValue && !topics.Any(t => t.Id == x.TopicId && t.LessonId == x.LessonId)))
            throw new ArgumentException("Seçilen konu derse ait değil veya kullanılamıyor.");
        return request.Lessons.Select(x => x with { LessonName = activeLessons.Single(l => l.Id == x.LessonId).Name,
            TopicName = x.TopicId.HasValue ? topics.Single(t => t.Id == x.TopicId).Name : null }).ToArray();
    }

    private Guid RequireStudent() => access.CurrentUserId is { } student && access.IsCurrentStudent(student)
        ? student : throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
    }
    private static BusinessRuleException Conflict() => new("StudyPlanning.Conflict", "Sonuç değişti. Güncel kaydı yükleyip yeniden deneyin.");
    private static StudentExamView View(Exam exam)
    {
        var result = exam.Results.Single();
        return new(exam.Id, exam.Version, exam.Title, exam.ExamType, DateOnly.FromDateTime(exam.ExamDate),
            result.Score, exam.MaxScore, result.CorrectAnswers ?? 0, result.WrongAnswers ?? 0, result.EmptyAnswers ?? 0,
            "StudentReported", result.GetLessonAnswers());
    }
}
