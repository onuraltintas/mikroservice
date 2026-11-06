using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Application.Subscriptions;
using Coaching.Domain;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Profiles;

/// <summary>Paket C (#3): where a student is in the first-login wizard, with the answers saved so far.</summary>
public sealed record OnboardingView(string Status, int NextStep, IReadOnlyList<int> SavedSteps, DateTime? CompletedAt, int AcademicYearStart,
    OnboardingAnswers Answers, IReadOnlyList<LearningExamSessionView> ExamSessions);
public sealed record OnboardingAnswers(DateOnly? BirthDate, string? HomeProvinceId, string? HomeCity, string? HomeDistrictId, string? HomeDistrict,
    CoachingEducationStatus EducationStatus, int? GradeLevel, int? GraduationYear, string? SchoolName,
    LearningPurpose? Purpose, string? PrimaryScoreType, string? ForeignLanguage, Guid? TargetExamSessionId, int? TargetExamYear,
    decimal? DiplomaGrade, bool DiplomaGradeEstimated, OnboardingTarget? Target);
/// <summary>Kind: school, program or score. TargetScore is the student's own target score (also for a programme without a taban).</summary>
public sealed record OnboardingTarget(string Kind, Guid? TargetId, string Name, string? Detail, decimal? MinimumScore, int? ScoreYear,
    string? ScoreType, decimal? TargetScore, bool CanEdit);
public sealed record OnboardingPersonalStep(DateOnly? BirthDate, string? ProvinceId, string? DistrictId, CoachingEducationStatus EducationStatus,
    int? GradeLevel, int? GraduationYear, string? SchoolName);
public sealed record OnboardingExamStep(LearningPurpose Purpose, string? PrimaryScoreType, string? ForeignLanguage, Guid? TargetExamSessionId,
    int? TargetExamYear, decimal? DiplomaGrade, bool DiplomaGradeEstimated);
/// <summary>Kind: none ("Henüz karar vermedim"), school, program or score.</summary>
public sealed record OnboardingTargetStep(string Kind, Guid? TargetId, string? ScoreType, decimal? TargetScore);
public sealed record OnboardingCompletion(OnboardingView State, bool NeedsSubscription);
public sealed record OnboardingOption(string Value, int Count);
public sealed record OnboardingSchoolOption(Guid Id, string Name, string? District, decimal? MinimumScore, int? ScoreYear);
public sealed record OnboardingProgramOption(Guid Id, string Name, string University, string? Faculty, string? ScoreType, string? ProgramType,
    decimal? MinimumScore, int? Rank, int? ScoreYear, string? EducationType, string? Scholarship, string? Language);

/// <summary>
/// Paket C (#3, user decisions 05.11.2026): the mandatory first-login wizard of a coaching student. Each step is saved on its
/// own into the records the rest of the product already uses: the coaching profile (birth date, home city and district,
/// education, diploma grade), the learning context through <see cref="ILearningSetupService"/> (exam, field, language, target
/// session) and the primary goal (target school, programme or score). The invite, parent link and notification steps use their
/// own existing flows; the wizard only records that the student passed them. A changed answer re-opens only the steps that
/// depend on it (education → exam step; exam or field → target step). Long lists are read here page by page and filtered on the
/// server; the routes sit under api/coaching/students, which the subscription check leaves open, because the wizard comes before
/// the subscription.
/// </summary>
public sealed class CoachingStudentOnboardingService(CoachingDbContext db, ICoachingAccessPolicy access, ICoachingAccountDirectory accounts,
    ILearningSetupService setup, ICoachingLocationDirectory locations, ICoachingSubscription? subscriptions = null, TimeProvider? clock = null)
{
    public const int PageSize = 20;
    private const string WizardMarker = "İlk giriş sihirbazında belirlendi.";
    private TimeProvider Clock => clock ?? TimeProvider.System;

    public async Task<OnboardingView> GetAsync(CancellationToken ct) => await ViewAsync(await RequireProfileAsync(false, ct), ct);

    public async Task<OnboardingView> SavePersonalAsync(OnboardingPersonalStep request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profile = await RequireProfileAsync(true, ct);
        RequireOpen(profile);
        var today = Today();
        if (request.BirthDate is not { } birthDate || birthDate > today.AddYears(-6) || birthDate < today.AddYears(-100))
            throw new ArgumentException("Doğum tarihini kontrol et.");
        if (string.IsNullOrWhiteSpace(request.ProvinceId) || string.IsNullOrWhiteSpace(request.DistrictId)
            || request.ProvinceId.Length > 20 || request.DistrictId.Length > 20)
            throw new ArgumentException("Şehrini ve ilçeni listeden seç.");
        LocationProvince? province; LocationDistrict? district;
        try
        {
            province = (await locations.GetProvincesAsync(ct)).SingleOrDefault(x => x.Id == request.ProvinceId);
            district = province is null ? null
                : (await locations.GetDistrictsAsync(province.Id, ct)).SingleOrDefault(x => x.Id == request.DistrictId && x.ProvinceId == province.Id);
        }
        catch (HttpRequestException)
        { throw new BusinessRuleException("Onboarding.LocationsUnavailable", "Şehir listesi şu an yüklenemedi. Biraz sonra yeniden dene."); }
        if (province is null || district is null) throw new ArgumentException("Şehrini ve ilçeni listeden seç.");
        var educationChanged = profile.EducationStatus != request.EducationStatus || profile.GradeLevel != request.GradeLevel;
        profile.UpdateEducation(request.EducationStatus, request.GradeLevel, request.SchoolName, profile.SchoolCity, profile.SchoolDistrict,
            profile.SchoolType, profile.ClassBranch, request.GraduationYear);
        profile.UpdatePersonal(birthDate, profile.Bio, profile.AvatarUrl, profile.ShareProgressWithTeachers);
        profile.SetHome(province.Id, province.Name, district.Id, district.Name);
        // The exam step depends on the grade (the diploma question is for 11th, 12th grade and graduates).
        if (educationChanged && profile.HasOnboardingStep(2)) profile.ReopenOnboardingStep(2);
        profile.RecordOnboardingStep(1);
        await SaveAsync(ct);
        return await ViewAsync(profile, ct);
    }

    public async Task<OnboardingView> SaveExamAsync(OnboardingExamStep request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profile = await RequireProfileAsync(true, ct);
        RequireOpen(profile);
        if (!profile.HasOnboardingStep(1) || !profile.IsEducationComplete) throw new ArgumentException("Önce kişisel bilgilerini kaydet.");
        if (!LearningPurposes.IsSelectable(request.Purpose)) throw new ArgumentException("Hazırlandığın sınavı seç.");
        var scoreType = string.IsNullOrWhiteSpace(request.PrimaryScoreType) ? null : request.PrimaryScoreType.Trim().ToUpperInvariant();
        var language = request.Purpose is LearningPurpose.Yds or LearningPurpose.Yokdil || request.Purpose == LearningPurpose.Yks && scoreType == "DIL"
            ? (string.IsNullOrWhiteSpace(request.ForeignLanguage) ? "English" : request.ForeignLanguage.Trim()) : null;
        var context = await db.StudentLearningContexts.AsNoTracking().Include(x => x.CoursePreferences).Include(x => x.TopicStates)
            .SingleOrDefaultAsync(x => x.StudentUserId == profile.UserId, ct);
        var year = AcademicYearStart();
        var examYear = request.TargetExamYear;
        if (request.TargetExamSessionId is { } sessionId)
            examYear = await db.Set<StudyCatalogExamSession>().AsNoTracking().Where(x => x.Id == sessionId).Select(x => (int?)x.Date.Year).SingleOrDefaultAsync(ct)
                ?? throw new ArgumentException("Seçilen oturum artık sunulmuyor.");
        var sameScope = context is not null && context.Purpose == request.Purpose && context.PrimaryScoreType == scoreType
            && context.ForeignLanguage == language;
        var keepSelections = sameScope && context!.AcademicYearStart == year;
        await setup.SaveAsync(new LearningSetupUpdate(context?.Version, year, request.Purpose, examYear, scoreType, language,
            keepSelections ? context!.CoursePreferences.Select(x => new LearningCourseInput(x.LessonId, x.Priority, x.SelfReportedDifficulty)).ToArray() : [],
            keepSelections ? context!.TopicStates.Select(x => new LearningTopicInput(x.TopicId, x.DeclaredStatus)).ToArray() : [],
            keepSelections && context!.StartingStatusReviewed, request.TargetExamSessionId), ct);
        var diplomaApplies = request.Purpose == LearningPurpose.Yks && (profile.EducationStatus == CoachingEducationStatus.Graduate
            || profile.GradeLevel is 11 or 12);
        profile.SetDiplomaGrade(diplomaApplies ? request.DiplomaGrade : null, request.DiplomaGradeEstimated);
        // The target depends on the exam and the field: a change asks it again.
        if (context is not null && !sameScope)
        {
            await ClearTargetAsync(profile.UserId, ct);
            if (profile.HasOnboardingStep(3)) profile.ReopenOnboardingStep(3);
        }
        profile.RecordOnboardingStep(2);
        await SaveAsync(ct);
        return await ViewAsync(profile, ct);
    }

    public async Task<OnboardingView> SaveTargetAsync(OnboardingTargetStep request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profile = await RequireProfileAsync(true, ct);
        RequireOpen(profile);
        var context = await db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x => x.StudentUserId == profile.UserId, ct);
        if (!profile.HasOnboardingStep(2) || context is null || !LearningPurposes.IsSelectable(context.Purpose))
            throw new ArgumentException("Önce hazırlandığın sınavı seç.");
        var kind = request.Kind?.Trim().ToLowerInvariant();
        if (kind is not ("none" or "school" or "program" or "score")) throw new ArgumentException("Bir hedef seç ya da \"Henüz karar vermedim\" de.");
        var primary = await db.Set<StudentPrimaryGoal>().SingleOrDefaultAsync(x => x.StudentId == profile.UserId, ct);
        var goal = primary is null ? null : await db.AcademicGoals.SingleOrDefaultAsync(x => x.Id == primary.GoalId && x.StudentId == profile.UserId, ct);
        if (goal?.SetByTeacherId is not null && kind != "none")
            throw new BusinessRuleException("Onboarding.TeacherGoal", "Hedefini öğretmenin belirledi. Değiştirmek istersen öğretmenine yaz.");
        if (kind == "none")
        {
            if (goal is not null && goal.SetByTeacherId is null) { goal.SetCatalogTarget(null, null); goal.SetScoreTarget(null, null, null); }
        }
        else
        {
            var examType = ExamTypeOf(context.Purpose);
            string title;
            Action<AcademicGoal> apply;
            if (kind == "school")
            {
                if (context.Purpose != LearningPurpose.Lgs) throw new ArgumentException("Okul hedefi yalnız LGS'de seçilir.");
                var school = await db.TargetSchools.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TargetId && x.IsActive, ct)
                    ?? throw new ArgumentException("Okulu listeden seç.");
                title = school.Name;
                apply = target => { target.SetCatalogTarget(null, school.Id); target.SetScoreTarget(null, null, null); };
            }
            else if (kind == "program")
            {
                if (context.Purpose != LearningPurpose.Yks) throw new ArgumentException("Bölüm hedefi yalnız YKS'de seçilir.");
                var program = await db.TargetUniversityPrograms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TargetId && x.IsActive, ct)
                    ?? throw new ArgumentException("Bölümü listeden seç.");
                if (!YksScoreTypes.ProgramFitsProfile(context.PrimaryScoreType, program.ScoreType))
                    throw new ArgumentException("Bu bölümün puan türü seçtiğin alanla eşleşmiyor.");
                var programType = ScoreModelRules.Normalize(program.ScoreType) ?? throw new ArgumentException("Bu bölümün puan türü tanınmadı.");
                // A programme without a published taban needs the student's own target score.
                if (program.MinimumScore is null && request.TargetScore is not (> 0 and <= 560))
                    throw new ArgumentException("Bu bölümün taban puanı yok. Hedef puanını 1 ile 560 arasında gir.");
                var own = program.MinimumScore is null ? decimal.Round(request.TargetScore!.Value, 2) : (decimal?)null;
                title = $"{program.UniversityName} · {program.Name}";
                apply = target =>
                {
                    target.SetCatalogTarget(program.Id, null);
                    if (own is { } score) target.SetScoreTarget(score, 560, examType, programType); else target.SetScoreTarget(null, null, null);
                };
            }
            else
            {
                var type = request.ScoreType?.Trim().ToUpperInvariant().Replace('Ö', 'O').Replace('İ', 'I').Replace('-', '_') ?? "";
                if (!ScoreTargetTypes(context).Contains(type)) throw new ArgumentException("Bu sınav için geçerli bir puan türü seç.");
                var max = ScaleMax(type);
                if (request.TargetScore is not { } score || score <= 0 || score > max || decimal.Round(score, 2) != score)
                    throw new ArgumentException($"Hedef puanını 0 ile {max:0} arasında gir.");
                title = $"{TypeName(type)} hedefi";
                apply = target => { target.SetCatalogTarget(null, null); target.SetScoreTarget(score, max, examType, type); };
            }
            if (goal is null)
            {
                goal = AcademicGoal.Create(profile.UserId, Clip(title), GoalCategory.ExamPreparation);
                goal.UpdateDetails(description: WizardMarker);
                db.AcademicGoals.Add(goal);
                if (primary is null) db.Add(StudentPrimaryGoal.Create(profile.UserId, goal.Id)); else primary.Select(goal.Id);
            }
            else if (goal.Description == WizardMarker) goal.UpdateDetails(title: Clip(title));
            apply(goal);
        }
        profile.RecordOnboardingStep(3);
        await SaveAsync(ct);
        return await ViewAsync(profile, ct);
    }

    /// <summary>Steps 4–6 (invite, parent link, notifications) run their own existing flows; this records that the student passed them.</summary>
    public async Task<OnboardingView> PassStepAsync(int step, CancellationToken ct)
    {
        if (step is < 4 or > 6) throw new ArgumentException("Bu adım ayrı kaydedilir.");
        var profile = await RequireProfileAsync(true, ct);
        RequireOpen(profile);
        profile.RecordOnboardingStep(step);
        await SaveAsync(ct);
        return await ViewAsync(profile, ct);
    }

    public async Task<OnboardingCompletion> CompleteAsync(CancellationToken ct)
    {
        var profile = await RequireProfileAsync(true, ct);
        if (profile.OnboardingCompletedAt is null)
        {
            try { profile.CompleteOnboarding(Clock.GetUtcNow().UtcDateTime); }
            catch (InvalidOperationException) { throw new ArgumentException("Önce bütün adımları tamamla."); }
            await SaveAsync(ct);
        }
        var needsSubscription = subscriptions is not null && !(await subscriptions.GetMyAccessAsync(profile.UserId, ct)).HasAccess;
        return new(await ViewAsync(profile, ct), needsSubscription);
    }

    // ---------- Filtered lists (never the whole catalog) ----------

    public async Task<IReadOnlyList<OnboardingOption>> SchoolCitiesAsync(CancellationToken ct)
    {
        await RequireProfileAsync(false, ct);
        return await Options(db.TargetSchools.AsNoTracking().Where(x => x.IsActive).Select(x => x.City), ct);
    }

    public async Task<IReadOnlyList<OnboardingOption>> SchoolTypesAsync(string? city, CancellationToken ct)
    {
        await RequireProfileAsync(false, ct);
        var name = RequireText(city, "Şehri seç.");
        return await Options(db.TargetSchools.AsNoTracking().Where(x => x.IsActive && x.City == name).Select(x => x.InstitutionType), ct);
    }

    public async Task<TargetSearchPage<OnboardingSchoolOption>> SchoolsAsync(string? city, string? type, string? search, int page, CancellationToken ct)
    {
        await RequireProfileAsync(false, ct);
        var name = RequireText(city, "Şehri seç."); var kind = RequireText(type, "Okul türünü seç.");
        var query = db.TargetSchools.AsNoTracking().Where(x => x.IsActive && x.City == name && x.InstitutionType == kind);
        if (Search(search) is { } text) query = query.Where(x => x.Name.ToUpper().Replace("İ", "I").Contains(text) || (x.District ?? "").ToUpper().Replace("İ", "I").Contains(text));
        return await PageAsync(query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new OnboardingSchoolOption(x.Id, x.Name, x.District, x.MinimumScore, x.ScoreYear)), page, ct);
    }

    public async Task<IReadOnlyList<OnboardingOption>> ProgramCitiesAsync(string? programType, string? scoreType, CancellationToken ct)
    {
        await RequireProfileAsync(false, ct);
        return await Options((await Programs(programType, scoreType, ct)).Select(x => x.City), ct);
    }

    public async Task<IReadOnlyList<OnboardingOption>> UniversitiesAsync(string? programType, string? scoreType, string? city, string? search, CancellationToken ct)
    {
        await RequireProfileAsync(false, ct);
        var name = RequireText(city, "Şehri seç.");
        var query = (await Programs(programType, scoreType, ct)).Where(x => x.City == name);
        if (Search(search) is { } text) query = query.Where(x => x.UniversityName.ToUpper().Replace("İ", "I").Contains(text));
        return await Options(query.Select(x => x.UniversityName), ct);
    }

    public async Task<TargetSearchPage<OnboardingProgramOption>> ProgramsAsync(string? programType, string? scoreType, string? city, string? university,
        string? search, int page, CancellationToken ct)
    {
        await RequireProfileAsync(false, ct);
        var name = RequireText(city, "Şehri seç."); var school = RequireText(university, "Üniversiteyi seç.");
        var query = (await Programs(programType, scoreType, ct)).Where(x => x.City == name && x.UniversityName == school);
        if (Search(search) is { } text) query = query.Where(x => x.Name.ToUpper().Replace("İ", "I").Contains(text) || (x.FacultyName ?? "").ToUpper().Replace("İ", "I").Contains(text));
        return await PageAsync(query.OrderBy(x => x.Name).ThenBy(x => x.FacultyName).ThenBy(x => x.Id)
            .Select(x => new OnboardingProgramOption(x.Id, x.Name, x.UniversityName, x.FacultyName, x.ScoreType, x.ProgramType, x.MinimumScore, x.Rank,
                x.ScoreYear, x.EducationType, x.Scholarship, x.Language)), page, ct);
    }

    private async Task<IQueryable<TargetUniversityProgram>> Programs(string? programType, string? scoreType, CancellationToken ct)
    {
        if (programType is not ("Bachelor" or "Associate")) throw new ArgumentException("Lisans ya da önlisans seç.");
        var type = scoreType?.Trim().ToUpperInvariant() switch
        {
            null or "" => null, "DIL" or "DİL" or "YDT" => "YDT", "SÖZ" or "SOZ" => "SOZ", "SAY" => "SAY", "EA" => "EA", "TYT" => "TYT",
            _ => throw new ArgumentException("Puan türünü seç.")
        };
        // The catalog of the newest year in use (older imports stay readable for existing targets).
        var year = await db.TargetUniversityPrograms.AsNoTracking().Where(x => x.IsActive).MaxAsync(x => x.CatalogYear, ct);
        var query = db.TargetUniversityPrograms.AsNoTracking().Where(x => x.IsActive && x.ProgramType == programType && (x.CatalogYear == null || x.CatalogYear == year));
        return type is null ? query : query.Where(x => x.ScoreType == type);
    }

    private static async Task<IReadOnlyList<OnboardingOption>> Options(IQueryable<string?> values, CancellationToken ct) =>
        (await values.Where(x => x != null && x != "").GroupBy(x => x!).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .OrderBy(x => x.Key, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), false))
            .Select(x => new OnboardingOption(x.Key, x.Count)).ToArray();

    private static async Task<TargetSearchPage<T>> PageAsync<T>(IQueryable<T> query, int page, CancellationToken ct)
    {
        if (page is < 1 or > 10000) throw new ArgumentException("Geçerli bir sayfa seç.");
        var count = await query.CountAsync(ct);
        return new(await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct), count, page, PageSize);
    }

    private static string RequireText(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 300 ? throw new ArgumentException(message) : value.Trim();

    private static string? Search(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Length > 100) throw new ArgumentException("Arama en fazla 100 karakter olabilir.");
        // Turkish dotted and dotless i fold to I on both sides, whatever the database collation upper-cases them to.
        return value.Trim().ToUpper(new System.Globalization.CultureInfo("tr-TR")).Replace('İ', 'I');
    }

    // ---------- Rules ----------

    /// <summary>The score types a score-only target may use for the student's exam (see #3 step 3).</summary>
    public static IReadOnlyList<string> ScoreTargetTypes(StudentLearningContext context) => context.Purpose switch
    {
        LearningPurpose.Lgs => ["LGS"],
        // YKS: TYT, and the field's own score type (SAY, EA, SOZ, DIL).
        LearningPurpose.Yks => new[] { "TYT", ScoreModelRules.Normalize(context.PrimaryScoreType) ?? "TYT" }.Distinct().ToArray(),
        // AGS: P2 of the ÖABT field (Din Kültürü: P2-16 or P2-17); P1 for AGS-only candidates.
        LearningPurpose.Ags => context.PrimaryScoreType == "OABT_DIN" ? ["P2_16", "P2_17"] : [ScoreModelRules.AgsTarget(context.PrimaryScoreType)],
        LearningPurpose.Yds => ["YDS_EN"],
        LearningPurpose.Yokdil => context.PrimaryScoreType is { } field ? [field] : [],
        _ => []
    };

    private static decimal ScaleMax(string type) => LanguageScoreRules.IsLanguageExam(type) ? 100 : ScoreModelRules.Get(type).MaxScore;
    private static string TypeName(string type) => LanguageScoreRules.IsLanguageExam(type)
        ? PracticeExamRules.Profiles.Single(x => x.Code == type).Name : ScoreModelRules.Get(type).Name;
    private static ExamType ExamTypeOf(LearningPurpose purpose) => purpose switch
    {
        LearningPurpose.Lgs => ExamType.LGS, LearningPurpose.Yks => ExamType.YKS, LearningPurpose.Ags => ExamType.AGS,
        LearningPurpose.Yds => ExamType.YDS, LearningPurpose.Yokdil => ExamType.YOKDIL,
        _ => throw new ArgumentException("Hazırlandığın sınavı seç.")
    };
    private static string Clip(string title) => title.Length <= 200 ? title : title[..200];

    private async Task ClearTargetAsync(Guid student, CancellationToken ct)
    {
        var goalId = await db.Set<StudentPrimaryGoal>().AsNoTracking().Where(x => x.StudentId == student).Select(x => (Guid?)x.GoalId).SingleOrDefaultAsync(ct);
        var goal = goalId is null ? null : await db.AcademicGoals.SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == student && x.SetByTeacherId == null, ct);
        if (goal is null) return;
        goal.SetCatalogTarget(null, null); goal.SetScoreTarget(null, null, null);
        // Net targets of the old exam no longer fit the new one.
        if (goal.NetTargets.Count > 0) goal.SetNetTarget(null, null);
    }

    private int AcademicYearStart() { var today = Today(); return today.Month >= 9 ? today.Year : today.Year - 1; }
    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Clock.GetUtcNow().UtcDateTime, CoachingTime.Turkey));

    private static void RequireOpen(CoachingStudentProfile profile)
    {
        if (profile.OnboardingCompletedAt is not null)
            throw new BusinessRuleException("Onboarding.Completed", "Kurulumun zaten tamamlandı. Bilgilerini Profil ve ayarlar bölümünden değiştirebilirsin.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (Exception e) when (e is DbUpdateConcurrencyException or ConcurrencyException
            || e is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505" } })
        { throw new BusinessRuleException("Onboarding.Conflict", "Bilgilerin başka bir oturumda değişti. Sayfayı yenileyip devam et."); }
    }

    private async Task<CoachingStudentProfile> RequireProfileAsync(bool track, CancellationToken ct)
    {
        var student = access.CurrentUserId ?? Guid.Empty;
        if (student == Guid.Empty || !access.IsCurrentStudent(student)) throw Forbidden();
        var account = (await accounts.GetAccountsAsync([student], ct)).SingleOrDefault(x => x.UserId == student);
        if (account?.IsEligible("Student") != true) throw Forbidden();
        var query = track ? db.CoachingStudentProfiles : db.CoachingStudentProfiles.AsNoTracking();
        var profile = await query.SingleOrDefaultAsync(x => x.UserId == student, ct) ?? throw new KeyNotFoundException("Koçluk profilin bulunamadı.");
        if (!profile.IsActive) throw Forbidden();
        return profile;
    }

    private static BusinessRuleException Forbidden() => new("Authorization.Forbidden", "Aktif Koçluk öğrenci hesabı gereklidir.");

    private async Task<OnboardingView> ViewAsync(CoachingStudentProfile p, CancellationToken ct)
    {
        var context = await db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x => x.StudentUserId == p.UserId, ct);
        var exam = context is not null && LearningPurposes.IsSelectable(context.Purpose) ? context : null;
        var sessions = (await setup.GetAsync(ct)).ExamSessions;
        var answers = new OnboardingAnswers(p.BirthDate, p.HomeProvinceId, p.HomeCity, p.HomeDistrictId, p.HomeDistrict, p.EducationStatus, p.GradeLevel,
            p.GraduationYear, p.SchoolName, exam?.Purpose, exam?.PrimaryScoreType, exam?.ForeignLanguage, exam?.TargetExamSessionId, exam?.TargetExamYear,
            p.DiplomaGrade, p.DiplomaGradeEstimated, await TargetAsync(p.UserId, ct));
        var saved = Enumerable.Range(1, CoachingStudentProfile.OnboardingStepCount).Where(p.HasOnboardingStep).ToArray();
        var status = p.OnboardingCompletedAt is not null ? "Completed" : p.OnboardingSteps == 0 ? "NotStarted" : "InProgress";
        return new(status, p.NextOnboardingStep, saved, p.OnboardingCompletedAt, AcademicYearStart(), answers, sessions);
    }

    private async Task<OnboardingTarget?> TargetAsync(Guid student, CancellationToken ct)
    {
        var goalId = await db.Set<StudentPrimaryGoal>().AsNoTracking().Where(x => x.StudentId == student).Select(x => (Guid?)x.GoalId).SingleOrDefaultAsync(ct);
        var goal = goalId is null ? null : await db.AcademicGoals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == student, ct);
        if (goal is null) return null;
        var canEdit = goal.SetByTeacherId is null;
        if (goal.TargetSchoolId is { } schoolId)
            return await db.TargetSchools.AsNoTracking().Where(x => x.Id == schoolId).Select(x => new OnboardingTarget("school", x.Id, x.Name,
                x.City + " / " + x.District, x.MinimumScore, x.ScoreYear, "LGS", null, canEdit)).SingleOrDefaultAsync(ct);
        if (goal.TargetUniversityProgramId is { } programId)
            return await db.TargetUniversityPrograms.AsNoTracking().Where(x => x.Id == programId).Select(x => new OnboardingTarget("program", x.Id,
                x.UniversityName + " · " + x.Name, x.FacultyName, x.MinimumScore, x.ScoreYear, x.ScoreType, goal.TargetScore, canEdit)).SingleOrDefaultAsync(ct);
        if (goal.TargetScore is { } score)
        {
            var type = goal.TargetScoreType;
            var name = type is not null && (LanguageScoreRules.IsLanguageExam(type) || ScoreModelRules.Normalize(type) is not null) ? TypeName(ScoreModelRules.Normalize(type) ?? type) : goal.Title;
            return new("score", null, name, null, null, null, type, score, canEdit);
        }
        return null;
    }
}
