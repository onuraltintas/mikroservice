using Coaching.Application.Authorization;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingLearningSetupService(CoachingDbContext db, ICoachingAccessPolicy access,
    ICoachingAccountDirectory accounts, CoachingStudyPlanAccess? staffAccess = null) : ILearningSetupService
{
    public async Task<LearningSetupView> GetAsync(CancellationToken ct)
    {
        var profile = await RequireStudent(ct);
        return await View(await Context(profile.UserId).AsNoTracking().SingleOrDefaultAsync(ct), profile, ct);
    }

    public async Task<LearningSetupView> GetForStudentAsync(Guid studentId, CancellationToken ct)
    {
        if (staffAccess is null) throw Forbidden();
        await staffAccess.RequireAsync(studentId, ct);
        var profile = await RequireStudent(studentId, ct);
        return await View(await Context(studentId).AsNoTracking().SingleOrDefaultAsync(ct), profile, ct);
    }

    public async Task<LearningSetupView> SaveAsync(LearningSetupUpdate r, CancellationToken ct)
    {
        var profile = await RequireStudent(ct);
        ArgumentNullException.ThrowIfNull(r);
        if (r.Courses is null || r.Topics is null || r.Courses.Count > 100 || r.Topics.Count > 1000
            || r.Courses.Any(x => x is null) || r.Topics.Any(x => x is null)
            || r.Courses.Select(x => x.LessonId).Distinct().Count() != r.Courses.Count
            || r.Topics.Select(x => x.TopicId).Distinct().Count() != r.Topics.Count)
            throw new ArgumentException("Ders ve konu seçimlerini kontrol edin.");
        // KM8: new saves take exam purposes only; a School/General/Undecided row stays readable until the student re-chooses.
        if (!LearningPurposes.IsSelectable(r.Purpose)) throw new ArgumentException(LearningPurposes.RetiredPurposeNotice);
        var candidate = StudentLearningContext.Create(profile.UserId, r.AcademicYearStart, r.Purpose, r.TargetExamYear, r.PrimaryScoreType, r.ForeignLanguage);
        var context = await Context(profile.UserId).SingleOrDefaultAsync(ct);
        if (context is null ? r.ExpectedVersion.HasValue : r.ExpectedVersion != context.Version) throw Conflict();
        await ValidateSessionAsync(r, ct);
        var curriculum = await ResolveCurriculum(candidate, ct);
        var lessonQuery = Lessons(candidate).Where(x => x.CurriculumVersionId == (curriculum == null ? (Guid?)null : curriculum.Id) && x.CurriculumVersionId != null);
        var ids = r.Courses.Select(x => x.LessonId).ToArray();
        if (await lessonQuery.CountAsync(x => Enumerable.Contains(ids, x.Id), ct) != ids.Length)
            throw new ArgumentException("Eğitim bağlamına uygun aktif dersleri seçin.");
        var topicIds = r.Topics.Select(x => x.TopicId).ToArray();
        if (await Topics(lessonQuery,candidate).CountAsync(x => Enumerable.Contains(topicIds, x.Id) && Enumerable.Contains(ids, x.LessonId), ct) != topicIds.Length)
            throw new ArgumentException("Seçilen derslere ait aktif konuları belirtin.");
        // Validate every selection before mutating tracked state.
        var target = context ?? candidate;
        var courses = r.Courses.Select(x => StudentCoursePreference.Create(target.Id, x.LessonId, x.Priority, x.SelfReportedDifficulty)).ToArray();
        var topics = r.Topics.Select(x => StudentTopicState.Create(target.Id, x.TopicId, x.DeclaredStatus, profile.UserId)).ToArray();
        target.SetScope(r.AcademicYearStart, r.Purpose, r.TargetExamYear, r.PrimaryScoreType, r.ForeignLanguage);
        if (r.TargetExamSessionId != target.TargetExamSessionId) target.SetTargetExamSession(r.TargetExamSessionId);
        target.SetEducation(profile.EducationStatus, profile.GradeLevel, curriculum?.Id);
        target.ReplaceSelections(courses, topics, r.StartingStatusReviewed);
        if (context is null) db.StudentLearningContexts.Add(target);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) { throw Conflict(); }
        return await View(target, profile, ct);
    }

    public async Task<LearningCatalogView> GetCatalogAsync(Guid? lessonId, int pageNumber, CancellationToken ct)
    {
        if (pageNumber is < 1 or > 10000 || lessonId == Guid.Empty) throw new ArgumentException("Geçerli ders ve sayfa seçin.");
        var profile = await RequireStudent(ct);
        var context = await Context(profile.UserId).AsNoTracking().SingleOrDefaultAsync(ct);
        return await GetCatalog(profile, context, lessonId, pageNumber, ct);
    }

    public async Task<LearningCatalogView> GetCatalogForStudentAsync(Guid studentId, Guid? lessonId, int pageNumber, CancellationToken ct)
    {
        if (pageNumber is < 1 or > 10000 || lessonId == Guid.Empty) throw new ArgumentException("Geçerli ders ve sayfa seçin.");
        if (staffAccess is null) throw Forbidden();
        await staffAccess.RequireAsync(studentId, ct);
        var profile = await RequireStudent(studentId, ct);
        var context = await Context(studentId).AsNoTracking().SingleOrDefaultAsync(ct);
        return await GetCatalog(profile, context, lessonId, pageNumber, ct);
    }

    private async Task<LearningCatalogView> GetCatalog(CoachingStudentProfile profile, StudentLearningContext? context, Guid? lessonId, int pageNumber, CancellationToken ct)
    {
        if (context is null) return new(null, [], new([], 0, pageNumber, 50), 0, ["Önce çalışma amacınızı kaydedin."]);
        if (LearningPurposes.IsRetired(context.Purpose)) return new(null, [], new([], 0, pageNumber, 50), 0, [LearningPurposes.RetiredPurposeNotice]);
        var curriculum = await ResolveCurriculum(context, ct);
        var curriculumId = curriculum?.Id;
        var lessons = Lessons(context).Where(x => curriculumId != null && x.CurriculumVersionId == curriculumId);
        var eligibleTopics = Topics(lessons,context);
        var choices = await lessons.OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new LearningLessonView(x.Id, x.Name, x.GradeNumber, x.ExamCode,
            eligibleTopics.Count(t => t.LessonId == x.Id), eligibleTopics.Count(t => t.LessonId == x.Id && t.EstimatedMinutes == null))).ToArrayAsync(ct);
        var topicQuery = eligibleTopics.Where(x => lessonId != null && x.LessonId == lessonId);
        var count = await topicQuery.CountAsync(ct);
        var topicViews = await (from t in topicQuery
            join l in db.StudyCatalogLessons on t.LessonId equals l.Id
            join u in db.StudyCatalogUnits on t.UnitId equals u.Id
            orderby u.DisplayOrder, u.Id, t.DisplayOrder, t.Id
            select new StudyTopicView(t.Id, t.Name, l.Name, u.Name, l.GradeNumber, l.ExamCode, t.EstimatedMinutes, l.Id))
            .Skip((pageNumber - 1) * 50).Take(50).ToArrayAsync(ct);
        var missing = choices.Sum(x => x.MissingDurationCount);
        return new(curriculum, choices, new(topicViews, count, pageNumber, 50), missing,
            curriculum is null ? ["Bu eğitim yılı ve kapsam için yayımlanmış ders kataloğu bulunamadı. Mevcut planlarınızı kullanabilirsiniz."]
            : missing > 0 ? ["Bazı konuların tahmini çalışma süresi eksik. Plan önizlemesinde süre belirtmeniz gerekecek."] : []);
    }

    private IQueryable<StudentLearningContext> Context(Guid student) => db.StudentLearningContexts
        .Include(x => x.CoursePreferences).Include(x => x.TopicStates).Where(x => x.StudentUserId == student);

    // KM8: only exam purposes have lessons; the grade-based School/General branches are gone with the school lessons.
    private IQueryable<StudyCatalogLesson> Lessons(StudentLearningContext c)
    {
        var query = db.StudyCatalogLessons.AsNoTracking().Where(x => x.IsActive);
        var yksTopics=YksCatalogEligibility.Topics(db,c);
        return c.Purpose switch
        {
            LearningPurpose.Lgs => query.Where(x => x.ExamCode == "LGS"),
            LearningPurpose.Yks => query.Where(x => yksTopics.Any(t=>t.LessonId==x.Id&&t.IsActive)),
            LearningPurpose.Ags or LearningPurpose.Yds => ByExam(query, LearningPurposes.LessonExamCodes(c.Purpose, c.PrimaryScoreType)),
            // KM6: a YÖKDİL student sees the lesson of their own field (source id = field code) and any shared lesson.
            LearningPurpose.Yokdil => ByExam(query, LearningPurposes.LessonExamCodes(c.Purpose))
                .Where(x => c.PrimaryScoreType == null || x.SourceId == c.PrimaryScoreType || !x.SourceId.StartsWith("YOKDIL_")),
            _ => query.Where(x => false)
        };
    }

    private static IQueryable<StudyCatalogLesson> ByExam(IQueryable<StudyCatalogLesson> query, IReadOnlyList<string> codes)
    { var exams = codes.ToArray(); return query.Where(x => x.ExamCode != null && Enumerable.Contains(exams, x.ExamCode)); }

    /// <summary>KM1: the chosen session must be an upcoming session of the purpose's own, active exam in the target year.</summary>
    private async Task ValidateSessionAsync(LearningSetupUpdate r, CancellationToken ct)
    {
        if (r.TargetExamSessionId is not { } id) return;
        var code = LearningPurposes.CatalogExamCode(r.Purpose) ?? throw new ArgumentException("Sınav oturumu yalnız sınav hazırlığında seçilir.");
        var session = await (from s in db.Set<StudyCatalogExamSession>().AsNoTracking()
                             join e in db.Set<StudyCatalogExam>().AsNoTracking() on s.ExamId equals e.Id
                             where s.Id == id && e.Code == code && e.IsActive
                             select s).SingleOrDefaultAsync(ct)
            ?? throw new ArgumentException("Seçilen oturum bu sınava ait değil veya artık sunulmuyor.");
        if (session.Date <= CoachingTodayInTurkey() || session.Date.Year != r.TargetExamYear)
            throw new ArgumentException("Hedef oturum gelecekte ve hedef sınav yılında olmalıdır.");
    }

    private static DateOnly CoachingTodayInTurkey() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Coaching.Domain.CoachingTime.Turkey));

    private async Task<IReadOnlyList<LearningExamSessionView>> SessionsAsync(CancellationToken ct)
    {
        var today = CoachingTodayInTurkey();
        var codes = Enum.GetValues<LearningPurpose>().Select(LearningPurposes.CatalogExamCode).OfType<string>().ToArray();
        return await (from s in db.Set<StudyCatalogExamSession>().AsNoTracking()
                      join e in db.Set<StudyCatalogExam>().AsNoTracking() on s.ExamId equals e.Id
                      where e.IsActive && Enumerable.Contains(codes, e.Code) && s.Date > today
                      orderby e.Code, s.Date
                      select new LearningExamSessionView(s.Id, e.Code, e.Name, s.Date, s.Label, s.ApplicationStart, s.ApplicationEnd)).ToArrayAsync(ct);
    }

    private IQueryable<StudyCatalogTopic> Topics(IQueryable<StudyCatalogLesson> lessons,StudentLearningContext? context) =>
        from t in YksCatalogEligibility.Topics(db,context).AsNoTracking()
        join u in db.StudyCatalogUnits on new { t.UnitId, t.LessonId } equals new { UnitId = u.Id, u.LessonId }
        where t.IsActive && u.IsActive && lessons.Any(l => l.Id == t.LessonId)
            && !db.StudyCatalogTopics.Any(child => child.ParentId == t.Id)
            && (t.ParentId == null || db.StudyCatalogTopics.Any(parent => parent.Id == t.ParentId && parent.IsActive))
        select t;

    private Task<CurriculumVersion?> ResolveCurriculum(StudentLearningContext c, CancellationToken ct)
    {
        var lessons = Lessons(c);
        return db.CurriculumVersions.AsNoTracking().Where(x => x.IsPublished && x.ValidFromYear <= c.AcademicYearStart
            && x.ValidToYear >= c.AcademicYearStart && lessons.Any(l => l.CurriculumVersionId == x.Id))
            .OrderByDescending(x => x.ValidFromYear).ThenBy(x => x.Code).FirstOrDefaultAsync(ct);
    }

    private async Task<LearningSetupView> View(StudentLearningContext? c, CoachingStudentProfile p, CancellationToken ct)
    {
        var missing = new List<string>(); var warnings = new List<string>();
        if (!p.IsEducationComplete) missing.Add("Profile");
        var sessions = await SessionsAsync(ct);
        if (c is null) { missing.Add("Purpose"); return new(null, missing[0], false, missing, warnings) { ExamSessions = sessions }; }
        if (LearningPurposes.IsRetired(c.Purpose) || p.EducationStatus != c.EducationStatus || p.GradeLevel != c.GradeLevel) missing.Add("Purpose");
        var hours = await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == p.UserId, ct);
        if (hours is null || hours.Windows.Count == 0) missing.Add("Availability");
        var curriculum = await ResolveCurriculum(c, ct);
        if (curriculum is null || curriculum.Id != c.CurriculumVersionId) missing.Add("Catalog");
        var lessonQuery = Lessons(c).Where(x => x.CurriculumVersionId == c.CurriculumVersionId && x.CurriculumVersionId != null);
        var ids = c.CoursePreferences.Select(x => x.LessonId).ToArray();
        var eligibleTopics = Topics(lessonQuery,c);
        if (ids.Length == 0 || await lessonQuery.CountAsync(x => Enumerable.Contains(ids, x.Id), ct) != ids.Length
            || await lessonQuery.AnyAsync(x => Enumerable.Contains(ids, x.Id) && !eligibleTopics.Any(t => t.LessonId == x.Id), ct)) missing.Add("Courses");
        if (!c.StartingStatusReviewed) missing.Add("Starting");
        if (await Topics(lessonQuery,c).AnyAsync(x => Enumerable.Contains(ids, x.LessonId) && x.EstimatedMinutes == null, ct))
            warnings.Add("Seçilen derslerde çalışma süresi eksik konular var; önizlemede süre girin.");
        if (missing.Contains("Catalog")) warnings.Add("Uygun yayımlanmış katalog eksik veya değişmiş. Mevcut planlarınıza erişebilirsiniz.");
        return new(c, missing.FirstOrDefault() ?? "Preview", missing.Count == 0, missing, warnings) { ExamSessions = sessions };
    }

    private async Task<CoachingStudentProfile> RequireStudent(CancellationToken ct)
    {
        var student = access.CurrentUserId ?? Guid.Empty;
        if (!access.IsCurrentStudent(student)) throw Forbidden();
        return await RequireStudent(student, ct);
    }
    private async Task<CoachingStudentProfile> RequireStudent(Guid student, CancellationToken ct)
    {
        var account = (await accounts.GetAccountsAsync([student], ct)).SingleOrDefault(x => x.UserId == student);
        if (account?.IsEligible("Student") != true) throw Forbidden();
        var profile = await db.CoachingStudentProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == student, ct)
            ?? throw new KeyNotFoundException("Koçluk profilinizi hazırlayın.");
        if (!profile.IsActive) throw Forbidden();
        return profile;
    }
    private static BusinessRuleException Forbidden() => new("Authorization.Forbidden", "Aktif Koçluk öğrenci hesabı gereklidir.");
    private static BusinessRuleException Conflict() => new("StudyPlanning.Conflict", "Kurulum başka bir oturumda değişti. Güncel bilgileri yeniden yükleyin.");
}
