using System.Globalization;
using System.Text;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Microsoft.Extensions.Caching.Memory;
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
    OnboardingAnswers Answers, IReadOnlyList<LearningExamSessionView> ExamSessions)
{
    /// <summary>Paket D review (M1, L3): the exams that fit the saved grade (the server's rule); the wizard offers only these.</summary>
    public IReadOnlyList<LearningPurpose> AllowedPurposes { get; init; } = [];
    /// <summary>E4 #18: the invitations the student accepted (the invite step's answer when the wizard is resumed), oldest first.</summary>
    public IReadOnlyList<OnboardingInvitation> AcceptedInvitations { get; init; } = [];
}
/// <summary>Type: StudentToTeacher or StudentToInstitution. InviterEmail is null when the inviter's account is gone.</summary>
public sealed record OnboardingInvitation(string? InviterEmail, string Type, DateTime AcceptedAt);
public sealed record OnboardingAnswers(DateOnly? BirthDate, string? HomeProvinceId, string? HomeCity, string? HomeDistrictId, string? HomeDistrict,
    CoachingEducationStatus EducationStatus, int? GradeLevel, int? GraduationYear, string? SchoolName,
    LearningPurpose? Purpose, string? PrimaryScoreType, string? ForeignLanguage, Guid? TargetExamSessionId, int? TargetExamYear,
    decimal? DiplomaGrade, bool DiplomaGradeEstimated, OnboardingTarget? Target)
{
    /// <summary>Paket C review: the primary goal was set by a teacher; the wizard cannot change it.</summary>
    public bool TargetLocked { get; init; }
}
/// <summary>A target score in one score type (LGS, TYT, SAY, EA, SOZ, DIL, P1, P2_…, YDS_EN, YOKDIL_…).</summary>
public sealed record OnboardingScore(string ScoreType, decimal Score);
/// <summary>Kind: school, program or score. Scores: the student's own target scores (YKS: TYT and the field's; a programme without a taban: one).</summary>
public sealed record OnboardingTarget(string Kind, Guid? TargetId, string Name, string? Detail, decimal? MinimumScore, int? ScoreYear,
    string? ScoreType, IReadOnlyList<OnboardingScore> Scores, bool CanEdit);
/// <summary>SchoolId: the student's own school from the school catalog (null keeps the saved school).</summary>
public sealed record OnboardingPersonalStep(DateOnly? BirthDate, string? ProvinceId, string? DistrictId, CoachingEducationStatus EducationStatus,
    int? GradeLevel, int? GraduationYear, Guid? SchoolId);
public sealed record OnboardingExamStep(LearningPurpose Purpose, string? PrimaryScoreType, string? ForeignLanguage, Guid? TargetExamSessionId,
    int? TargetExamYear, decimal? DiplomaGrade, bool DiplomaGradeEstimated);
/// <summary>Kind: none ("Henüz karar vermedim"), school, program or score. Scores: score targets, or a programme's own score.</summary>
public sealed record OnboardingTargetStep(string Kind, Guid? TargetId, IReadOnlyList<OnboardingScore>? Scores);
public sealed record OnboardingCompletion(OnboardingView State, bool NeedsSubscription);
/// <summary>Value is the catalog's own spelling (sent back as the filter); Label is the same name for display.</summary>
public sealed record OnboardingOption(string Value, int Count)
{
    public string Label { get; init; } = Value;
}
public sealed record OnboardingSchoolOption(Guid Id, string Name, string? District, decimal? MinimumScore, int? ScoreYear)
{
    /// <summary>Paket C: the school is in the student's own district (those come first).</summary>
    public bool InHomeDistrict { get; init; }
    /// <summary>Paket D (#7): the catalog's school type (Anadolu Lisesi, Fen Lisesi, …), shown next to the school.</summary>
    public string? Type { get; init; }
}
public sealed record OnboardingProgramOption(Guid Id, string Name, string University, string? Faculty, string? ScoreType, string? ProgramType,
    decimal? MinimumScore, int? Rank, int? ScoreYear, string? EducationType, string? Scholarship, string? Language);


/// <summary>
/// Paket C (#3, user decisions 05.11.2026): the mandatory first-login wizard of a coaching student. Each step is saved on its
/// own into the records the rest of the product already uses: the coaching profile (birth date, home city and district,
/// education, school, diploma grade), the learning context through <see cref="ILearningSetupService"/> (exam, field, language,
/// target session) and the primary goal (target school, programme or target scores; the goal events are published as for any
/// goal change). The invite, parent link and notification steps use their own existing flows; the wizard only records that the
/// student passed them. A changed answer re-opens only the steps that depend on it (education → exam step; exam or field →
/// target step). The target step stays open after the wizard (#4 "Hedefini belirle"). Long lists are read here page by page
/// and filtered on the server; the routes sit under api/coaching/students, which the subscription check leaves open, because
/// the wizard comes before the subscription.
/// </summary>
public sealed class CoachingStudentOnboardingService(CoachingDbContext db, ICoachingAccessPolicy access, ICoachingAccountDirectory accounts,
    ILearningSetupService setup, ICoachingLocationDirectory locations, ICoachingSubscription? subscriptions = null, TimeProvider? clock = null,
    ICoachingEventPublisher? events = null, IMemoryCache? cache = null)
{
    public const int PageSize = 20;
    private const string WizardMarker = "İlk giriş sihirbazında belirlendi.";
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private TimeProvider Clock => clock ?? TimeProvider.System;

    public async Task<OnboardingView> GetAsync(CancellationToken ct) => await ViewAsync(await RequireProfileAsync(false, ct), ct);

    /// <summary>F2 (E4 a): a rule of the profile refused an answer; the error names the wizard question it belongs to.</summary>
    private static void Field(string field, Action action)
    {
        try { action(); }
        catch (ArgumentException e) when (e is not OnboardingFieldException) { throw new OnboardingFieldException(field, e.Message); }
    }

    public async Task<OnboardingView> SavePersonalAsync(OnboardingPersonalStep request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profile = await RequireProfileAsync(true, ct);
        RequireOpen(profile);
        var today = Today();
        if (request.BirthDate is not { } birthDate || birthDate > today.AddYears(-6) || birthDate < today.AddYears(-100))
            throw new OnboardingFieldException("birthDate", "Doğum tarihini kontrol et.");
        if (string.IsNullOrWhiteSpace(request.ProvinceId) || request.ProvinceId.Length > 20)
            throw new OnboardingFieldException("city", "Şehrini ve ilçeni listeden seç.");
        if (string.IsNullOrWhiteSpace(request.DistrictId) || request.DistrictId.Length > 20)
            throw new OnboardingFieldException("district", "Şehrini ve ilçeni listeden seç.");
        var (province, district) = await LocationAsync(request.ProvinceId, request.DistrictId, ct);
        TargetSchool? school = null;
        if (request.SchoolId is { } schoolId)
            school = await db.TargetSchools.AsNoTracking().SingleOrDefaultAsync(x => x.Id == schoolId && x.IsActive, ct)
                ?? throw new OnboardingFieldException("ownSchool", "Okulunu listeden seç.");
        var educationChanged = profile.EducationStatus != request.EducationStatus || profile.GradeLevel != request.GradeLevel;
        // A resuming student keeps what the wizard does not ask: the graduation year (a graduate's) and, unless a new one is picked,
        // the school (a student's; E3 #14: a non-student status has no school, so it is cleared).
        var graduationYear = request.GraduationYear ?? (CoachingStudentProfile.IsGraduate(request.EducationStatus) ? profile.GraduationYear : null);
        var schoolType = school is null ? profile.SchoolType : await OwnSchoolTypeAsync(db, school, ct);
        // F2 (E4 a): a refused answer names the wizard question to ask again.
        var educationField = request.EducationStatus == CoachingEducationStatus.Student ? "education" : "stage";
        Field(educationField, () => profile.UpdateEducation(request.EducationStatus, request.GradeLevel, school?.Name ?? profile.SchoolName,
            school is null ? profile.SchoolCity : school.City, school is null ? profile.SchoolDistrict : school.District, schoolType, profile.ClassBranch, graduationYear));
        Field("birthDate", () => profile.UpdatePersonal(birthDate, profile.Bio, profile.AvatarUrl, profile.ShareProgressWithTeachers));
        Field("city", () => profile.SetHome(province.Id, province.Name, district.Id, district.Name));
        // The exam step depends on the grade (the diploma question is for 11th, 12th grade and graduates).
        if (educationChanged && profile.HasOnboardingStep(2)) profile.ReopenOnboardingStep(2);
        profile.RecordOnboardingStep(1);
        await SaveAsync(ct);
        return await ViewAsync(profile, ct);
    }

    /// <summary>The province and district from the shared Identity directory; a slow or failing directory is a 503.</summary>
    public async Task<(LocationProvince Province, LocationDistrict District)> LocationAsync(string provinceId, string districtId, CancellationToken ct)
    {
        LocationProvince? province; LocationDistrict? district;
        try
        {
            province = (await locations.GetProvincesAsync(ct)).SingleOrDefault(x => x.Id == provinceId);
            district = province is null ? null
                : (await locations.GetDistrictsAsync(province.Id, ct)).SingleOrDefault(x => x.Id == districtId && x.ProvinceId == province.Id);
        }
        catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException && !ct.IsCancellationRequested)
        { throw new BusinessRuleException("Onboarding.LocationsUnavailable", "Şehir listesi şu an yüklenemedi. Biraz sonra yeniden dene."); }
        if (province is null || district is null)
            throw new OnboardingFieldException(province is null ? "city" : "district", "Şehrini ve ilçeni listeden seç.");
        return (province, district);
    }

    public async Task<OnboardingView> SaveExamAsync(OnboardingExamStep request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profile = await RequireProfileAsync(true, ct);
        RequireOpen(profile);
        if (!profile.HasOnboardingStep(1) || !profile.IsEducationComplete) throw new ArgumentException("Önce kişisel bilgilerini kaydet.");
        if (!LearningPurposes.IsSelectable(request.Purpose)) throw new OnboardingFieldException("purpose", "Hazırlandığın sınavı seç.");
        // Paket D re-check (N1): the wizard is never finished with an exam the grade does not fit, even one already saved
        // (the study settings let an unchanged exam through so unrelated saves keep working).
        if (!LearningPurposes.FitsEducation(request.Purpose, profile.EducationStatus, profile.GradeLevel))
            throw new OnboardingFieldException("purpose", LearningPurposes.PurposeNotForEducationNotice);
        var diplomaApplies = LearningPurposes.DiplomaApplies(request.Purpose, profile.EducationStatus, profile.GradeLevel);
        var diploma = diplomaApplies ? request.DiplomaGrade : null;
        // Every check before any write.
        if (diploma is < 50 or > 100 || diploma.HasValue && decimal.Round(diploma.Value, 2) != diploma.Value)
            throw new OnboardingFieldException("diploma", "Diploma notu 50 ile 100 arasında olmalı.");
        var scoreType = string.IsNullOrWhiteSpace(request.PrimaryScoreType) ? null : request.PrimaryScoreType.Trim().ToUpperInvariant();
        var language = request.Purpose is LearningPurpose.Yds or LearningPurpose.Yokdil || request.Purpose == LearningPurpose.Yks && scoreType == "DIL"
            ? (string.IsNullOrWhiteSpace(request.ForeignLanguage) ? "English" : request.ForeignLanguage.Trim()) : null;
        var context = await db.StudentLearningContexts.AsNoTracking().Include(x => x.CoursePreferences).Include(x => x.TopicStates)
            .SingleOrDefaultAsync(x => x.StudentUserId == profile.UserId, ct);
        var year = AcademicYearStart();
        var examYear = request.TargetExamYear;
        if (request.TargetExamSessionId is { } sessionId)
            examYear = await db.Set<StudyCatalogExamSession>().AsNoTracking().Where(x => x.Id == sessionId).Select(x => (int?)x.Date.Year).SingleOrDefaultAsync(ct)
                ?? throw new OnboardingFieldException("session", "Seçilen oturum artık sunulmuyor.");
        var sameScope = context is not null && context.Purpose == request.Purpose && context.PrimaryScoreType == scoreType && context.ForeignLanguage == language;
        var keepSelections = sameScope && context!.AcademicYearStart == year;
        await InTransactionAsync(async () =>
        {
            await setup.SaveAsync(new LearningSetupUpdate(context?.Version, year, request.Purpose, examYear, scoreType, language,
                keepSelections ? context!.CoursePreferences.Select(x => new LearningCourseInput(x.LessonId, x.Priority, x.SelfReportedDifficulty)).ToArray() : [],
                keepSelections ? context!.TopicStates.Select(x => new LearningTopicInput(x.TopicId, x.DeclaredStatus)).ToArray() : [],
                keepSelections && context!.StartingStatusReviewed, request.TargetExamSessionId) { SkipGoalAlignment = true }, ct);
            profile.SetDiplomaGrade(diploma, request.DiplomaGradeEstimated);
            // The target depends on the exam and the field: a change asks it again.
            if (context is not null && !sameScope)
            {
                await ClearTargetAsync(profile.UserId, ct);
                if (profile.HasOnboardingStep(3)) profile.ReopenOnboardingStep(3);
            }
            profile.RecordOnboardingStep(2);
            await SaveAsync(ct);
        }, ct);
        return await ViewAsync(profile, ct);
    }

    /// <summary>The target question; also after the wizard ("Hedefini belirle" on the home page leads here).</summary>
    public async Task<OnboardingView> SaveTargetAsync(OnboardingTargetStep request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profile = await RequireProfileAsync(true, ct);
        var context = await db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x => x.StudentUserId == profile.UserId, ct);
        if (!profile.HasOnboardingStep(2) || context is null || !LearningPurposes.IsSelectable(context.Purpose))
            throw new OnboardingFieldException("purpose", "Önce hazırlandığın sınavı seç.");
        var kind = request.Kind?.Trim().ToLowerInvariant();
        if (kind is not ("none" or "school" or "program" or "score")) throw new OnboardingFieldException("targetMode", "Bir hedef seç ya da \"Henüz karar vermedim\" de.");
        var primary = await db.Set<StudentPrimaryGoal>().SingleOrDefaultAsync(x => x.StudentId == profile.UserId, ct);
        var goal = primary is null ? null : await db.AcademicGoals.SingleOrDefaultAsync(x => x.Id == primary.GoalId && x.StudentId == profile.UserId, ct);
        // Karar 10: a teacher's goal is not changed here.
        if (goal?.SetByTeacherId is not null && kind != "none")
            throw new BusinessRuleException("Onboarding.TeacherGoal", "Hedefini öğretmenin belirledi. Değiştirmek istersen öğretmenine yaz.");
        var examType = ExamTypeOf(context.Purpose);
        var created = false;
        if (kind == "none")
        {
            if (goal is not null && goal.SetByTeacherId is null) { goal.SetCatalogTarget(null, null); goal.SetScoreTargets([], null); }
        }
        else
        {
            string title; Action<AcademicGoal> apply; IReadOnlyList<string> nets;
            if (kind == "school")
            {
                if (context.Purpose != LearningPurpose.Lgs) throw new OnboardingFieldException("targetMode", "Okul hedefi yalnız LGS'de seçilir.");
                var school = await db.TargetSchools.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TargetId && x.IsActive, ct)
                    ?? throw new OnboardingFieldException("targetSchool", "Okulu listeden seç.");
                title = school.Name; nets = ["LGS"];
                apply = target => { target.SetCatalogTarget(null, school.Id); target.SetScoreTargets([], null); };
            }
            else if (kind == "program")
            {
                if (context.Purpose != LearningPurpose.Yks) throw new OnboardingFieldException("targetMode", "Bölüm hedefi yalnız YKS'de seçilir.");
                var program = await db.TargetUniversityPrograms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TargetId && x.IsActive, ct)
                    ?? throw new OnboardingFieldException("program", "Bölümü listeden seç.");
                if (!YksScoreTypes.ProgramFitsProfile(context.PrimaryScoreType, program.ScoreType))
                    throw new OnboardingFieldException("program", "Bu bölümün puan türü seçtiğin alanla eşleşmiyor.");
                var programType = ScoreModelRules.Normalize(program.ScoreType) ?? throw new OnboardingFieldException("program", "Bu bölümün puan türü tanınmadı.");
                var max = ScoreModelRules.Get(programType).MaxScore;
                // A programme without a published taban needs the student's own target score.
                var own = request.Scores?.FirstOrDefault()?.Score;
                if (program.MinimumScore is null && (own is not { } given || given <= 0 || given > max || decimal.Round(given, 2) != given))
                    throw new OnboardingFieldException("ownScore", $"Bu bölümün taban puanı yok. Hedef puanını yaz: 0'dan büyük, en çok {max:0}.");
                title = $"{program.UniversityName} · {program.Name}";
                nets = new[] { "TYT", YksScoreTypes.AdvancedNetExam(program.ScoreType) }.Distinct().ToArray();
                apply = target =>
                {
                    target.SetCatalogTarget(program.Id, null);
                    target.SetScoreTargets(program.MinimumScore is null ? [new AcademicScoreTarget(programType, own!.Value, max)] : [], examType);
                };
            }
            else
            {
                var allowed = ScoreTargetTypes(context);
                var scores = (request.Scores ?? []).Select(x => (Type: Normalize(x.ScoreType), x.Score)).ToArray();
                if (scores.Length is 0 or > 2 || scores.Select(x => x.Type).Distinct().Count() != scores.Length || scores.Any(x => !allowed.Contains(x.Type)))
                    throw new OnboardingFieldException(context.Purpose == LearningPurpose.Yks ? "tytScore" : "targetScoreType", "Bu sınav için geçerli bir puan türü seç.");
                var targets = scores.Select(x =>
                {
                    var max = ScaleMax(x.Type);
                    if (x.Score <= 0 || x.Score > max || decimal.Round(x.Score, 2) != x.Score)
                        throw new OnboardingFieldException(context.Purpose != LearningPurpose.Yks ? "targetScore" : x.Type == "TYT" ? "tytScore" : "fieldScore",
                            $"Hedef puanını yaz: 0'dan büyük, en çok {max:0}.");
                    return new AcademicScoreTarget(x.Type, x.Score, max);
                }).OrderBy(x => x.ScoreType == "TYT").ToArray();
                title = $"{TypeName(targets[0].ScoreType)} hedefi";
                nets = LearningPurposes.NetExamCodes(context.Purpose, context.PrimaryScoreType);
                apply = target => { target.SetCatalogTarget(null, null); target.SetScoreTargets(targets, examType); };
            }
            if (goal is null)
            {
                goal = AcademicGoal.Create(profile.UserId, Clip(title), GoalCategory.ExamPreparation);
                goal.UpdateDetails(description: WizardMarker);
                db.AcademicGoals.Add(goal); created = true;
                if (primary is null) db.Add(StudentPrimaryGoal.Create(profile.UserId, goal.Id)); else primary.Select(goal.Id);
            }
            // The goal is named after its target, so the home page and Hedefim show the new one (a teacher's goal never reaches here).
            else goal.UpdateDetails(title: Clip(title));
            apply(goal);
            KeepNetTargets(goal, nets);
        }
        profile.RecordOnboardingStep(3);
        // The goal events go through the same outbox as any goal change (notifications, read models).
        if (goal is not null && goal.SetByTeacherId is null && events is not null)
        {
            if (created) await events.PublishAsync(new GoalCreatedEvent(goal.Id, goal.StudentId, goal.SetByTeacherId, goal.Title), ct);
            else await events.PublishAsync(new GoalUpdatedEvent(goal.Id, goal.StudentId, goal.SetByTeacherId, goal.Title), ct);
        }
        await SaveAsync(ct);
        return await ViewAsync(profile, ct);
    }

    /// <summary>Net targets of scopes the new target no longer uses are removed (the goal target page enforces the same rule).</summary>
    private static void KeepNetTargets(AcademicGoal goal, IReadOnlyList<string> allowed)
    {
        var current = goal.NetTargets;
        if (current.All(x => allowed.Contains(x.ExamCode))) return;
        goal.SetNetTarget(null, null);
        foreach (var keep in current.Where(x => allowed.Contains(x.ExamCode))) goal.SetNetTarget(keep.ExamCode, keep.TargetNet);
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
        await RequireListAccessAsync(ct);
        return await Options(db.TargetSchools.AsNoTracking().Where(x => x.IsActive).Select(x => x.City), ct, titleCase: true);
    }

    public async Task<IReadOnlyList<OnboardingOption>> SchoolTypesAsync(string? city, CancellationToken ct)
    {
        await RequireListAccessAsync(ct);
        var cities = await CitiesAsync(db.TargetSchools.AsNoTracking().Where(x => x.IsActive).Select(x => x.City), RequireText(city, "Şehri seç."), ct);
        return await Options(db.TargetSchools.AsNoTracking().Where(x => x.IsActive && Enumerable.Contains(cities, x.City)).Select(x => x.InstitutionType), ct);
    }

    /// <summary>
    /// City: a catalog value or a place name from the Identity directory ("Adıyaman"); both match the catalog's spelling. Without a
    /// type it is the student's own school: one entry per school (E2 #16); with a type, the LGS target list: one row per programme.
    /// </summary>
    public async Task<TargetSearchPage<OnboardingSchoolOption>> SchoolsAsync(string? city, string? type, string? search, int page, CancellationToken ct,
        string? district = null)
    {
        await RequireListAccessAsync(ct);
        var cities = await CitiesAsync(db.TargetSchools.AsNoTracking().Where(x => x.IsActive).Select(x => x.City), RequireText(city, "Şehri seç."), ct);
        // Without a type: every school of the city (the student's own high school in step 1).
        var kind = string.IsNullOrWhiteSpace(type) ? null : RequireText(type, "Okul türünü seç.");
        var query = db.TargetSchools.AsNoTracking().Where(x => x.IsActive && Enumerable.Contains(cities, x.City) && (kind == null || x.InstitutionType == kind));
        if (Search(search) is { } text) query = query.Where(x => x.Name.ToUpper().Replace("İ", "I").Contains(text) || (x.District ?? "").ToUpper().Replace("İ", "I").Contains(text));
        // Paket C: the student's own district comes first (a sort, not a filter); the catalog spells districts in capitals.
        string[] home = string.IsNullOrWhiteSpace(district) ? [] : await CitiesAsync(query.Select(x => x.District), RequireText(district, "İlçeyi seç."), ct);
        if (kind is null) return await OwnSchoolsAsync(query, home, page, ct);
        return await PageAsync(query.OrderBy(x => Enumerable.Contains(home, x.District) ? 0 : 1).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new OnboardingSchoolOption(x.Id, x.Name, x.District, x.MinimumScore, x.ScoreYear) { InHomeDistrict = Enumerable.Contains(home, x.District), Type = x.InstitutionType }), page, ct);
    }

    /// <summary>
    /// E2 (#16): the catalog has one row per school type, programme and language ("Balgat Mesleki ve Teknik Anadolu Lisesi" four
    /// times); the student's own school is one school. One entry per school (name, district, city; folded), represented by its
    /// smallest id (stable); the type is shown only when every row of the school has the same one. Scores are left out (the own
    /// school is not a target). A city has at most a few hundred rows, so they are grouped and paged here.
    /// </summary>
    private static async Task<TargetSearchPage<OnboardingSchoolOption>> OwnSchoolsAsync(IQueryable<TargetSchool> query, string[] home, int page, CancellationToken ct)
    {
        if (page is < 1 or > 10000) throw new ArgumentException("Geçerli bir sayfa seç.");
        var rows = await query.Select(x => new { x.Id, x.Name, x.City, x.District, x.InstitutionType }).ToListAsync(ct);
        var schools = rows.GroupBy(x => (Fold(x.Name), Fold(x.City ?? ""), Fold(x.District ?? "")))
            .Select(group =>
            {
                var first = group.MinBy(x => x.Id)!;
                var types = group.Select(x => x.InstitutionType).Distinct().ToArray();
                return new OnboardingSchoolOption(first.Id, first.Name, first.District, null, null)
                    { InHomeDistrict = home.Contains(first.District), Type = types.Length == 1 ? types[0] : null };
            })
            .OrderBy(x => x.InHomeDistrict ? 0 : 1).ThenBy(x => x.Name, StringComparer.Create(Turkish, false)).ThenBy(x => x.Id).ToList();
        return new(schools.Skip((page - 1) * PageSize).Take(PageSize).ToList(), schools.Count, page, PageSize);
    }

    /// <summary>E2 (#16): the type saved with the student's own school: the school's only type, or none when its rows differ.</summary>
    public static async Task<string?> OwnSchoolTypeAsync(CoachingDbContext db, TargetSchool school, CancellationToken ct)
    {
        var name = Fold(school.Name); var district = Fold(school.District ?? "");
        var types = (await db.TargetSchools.AsNoTracking().Where(x => x.IsActive && x.City == school.City)
                .Select(x => new { x.Name, x.District, x.InstitutionType }).ToListAsync(ct))
            .Where(x => Fold(x.Name) == name && Fold(x.District ?? "") == district).Select(x => x.InstitutionType).Distinct().ToArray();
        return types.Length == 1 ? types[0] : null;
    }

    public async Task<IReadOnlyList<OnboardingOption>> ProgramCitiesAsync(string? programType, string? scoreType, CancellationToken ct)
    {
        await RequireListAccessAsync(ct);
        return await Options((await Programs(programType, scoreType, ct)).Select(x => x.City), ct, titleCase: true);
    }

    public async Task<IReadOnlyList<OnboardingOption>> UniversitiesAsync(string? programType, string? scoreType, string? city, string? search, CancellationToken ct)
    {
        await RequireListAccessAsync(ct);
        var programs = await Programs(programType, scoreType, ct);
        var cities = await CitiesAsync(programs.Select(x => x.City), RequireText(city, "Şehri seç."), ct);
        var query = programs.Where(x => Enumerable.Contains(cities, x.City));
        if (Search(search) is { } text) query = query.Where(x => x.UniversityName.ToUpper().Replace("İ", "I").Contains(text));
        return await Options(query.Select(x => x.UniversityName), ct);
    }

    public async Task<TargetSearchPage<OnboardingProgramOption>> ProgramsAsync(string? programType, string? scoreType, string? city, string? university,
        string? search, int page, CancellationToken ct)
    {
        await RequireListAccessAsync(ct);
        var programs = await Programs(programType, scoreType, ct);
        var cities = await CitiesAsync(programs.Select(x => x.City), RequireText(city, "Şehri seç."), ct);
        var school = RequireText(university, "Üniversiteyi seç.");
        var query = programs.Where(x => Enumerable.Contains(cities, x.City) && x.UniversityName == school);
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
        // E2 (#17): nationality-restricted quotas ("(KKTC Uyruklu)") are not offered.
        var query = db.TargetUniversityPrograms.AsNoTracking().Where(Catalogs.NationalityQuota.OpenToEveryone)
            .Where(x => x.IsActive && x.ProgramType == programType && (x.CatalogYear == null || x.CatalogYear == year));
        return type is null ? query : query.Where(x => x.ScoreType == type);
    }

    /// <summary>
    /// Paket C review: the catalog writes cities in capitals ("ADIYAMAN", "İSTANBUL"); the Identity directory writes them as names
    /// ("Adıyaman" since E2; before it "Adiyaman", "Afyonkarahi̇sar" with a combining dot). All are folded (Turkish lower case,
    /// marks removed, ı → i) to match.
    /// </summary>
    private static async Task<string[]> CitiesAsync(IQueryable<string?> cities, string requested, CancellationToken ct)
    {
        var key = Fold(requested);
        return (await cities.Where(x => x != null).Distinct().ToListAsync(ct)).Where(x => Fold(x!) == key).Select(x => x!).ToArray();
    }

    public static string Fold(string value)
    {
        var lower = value.Trim().ToLower(Turkish).Normalize(NormalizationForm.FormD);
        var text = new StringBuilder(lower.Length);
        foreach (var c in lower) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) text.Append(c == 'ı' ? 'i' : c);
        return text.ToString();
    }

    private static string TitleCase(string value) => Turkish.TextInfo.ToTitleCase(value.ToLower(Turkish));

    private static async Task<IReadOnlyList<OnboardingOption>> Options(IQueryable<string?> values, CancellationToken ct, bool titleCase = false) =>
        (await values.Where(x => x != null && x != "").GroupBy(x => x!).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .OrderBy(x => x.Key, StringComparer.Create(Turkish, true))
            .Select(x => new OnboardingOption(x.Key, x.Count) { Label = titleCase ? TitleCase(x.Key) : x.Key }).ToArray();

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
        return value.Trim().ToUpper(Turkish).Replace('İ', 'I');
    }

    // ---------- Rules ----------

    /// <summary>The score types a score-only target may use for the student's exam (see #3 step 3).</summary>
    public static IReadOnlyList<string> ScoreTargetTypes(StudentLearningContext context) => context.Purpose switch
    {
        LearningPurpose.Lgs => ["LGS"],
        // YKS: TYT and the field's own score type (SAY, EA, SOZ, DIL); the student may set both.
        LearningPurpose.Yks => new[] { "TYT", ScoreModelRules.Normalize(context.PrimaryScoreType) ?? "TYT" }.Distinct().ToArray(),
        // AGS: P2 of the ÖABT field (Din Kültürü: P2-16 or P2-17); P1 for AGS-only candidates.
        LearningPurpose.Ags => context.PrimaryScoreType == "OABT_DIN" ? ["P2_16", "P2_17"] : [ScoreModelRules.AgsTarget(context.PrimaryScoreType)],
        LearningPurpose.Yds => ["YDS_EN"],
        LearningPurpose.Yokdil => context.PrimaryScoreType is { } field ? [field] : [],
        _ => []
    };

    private static string Normalize(string? type) =>
        type is null ? "" : LanguageScoreRules.IsLanguageExam(type.Trim().ToUpperInvariant()) ? type.Trim().ToUpperInvariant() : ScoreModelRules.Normalize(type) ?? "";
    private static decimal ScaleMax(string type) => LanguageScoreRules.IsLanguageExam(type) ? 100 : ScoreModelRules.Get(type).MaxScore;
    /// <summary>E4: a score type as the student reads it ("YKS Sayısal"); an unknown one as it is.</summary>
    internal static string ScoreTypeName(string type)
    {
        try { return TypeName(type); } catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException) { return type; }
    }
    private static string TypeName(string type) => LanguageScoreRules.IsLanguageExam(type)
        ? PracticeExamRules.Profiles.Single(x => x.Code == type).Name : ScoreModelRules.Get(type).Name;
    internal static ExamType ExamTypeOf(LearningPurpose purpose) => purpose switch
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
        goal.SetCatalogTarget(null, null); goal.SetScoreTargets([], null);
        // Net targets of the old exam no longer fit the new one.
        if (goal.NetTargets.Count > 0) goal.SetNetTarget(null, null);
        if (events is not null) await events.PublishAsync(new GoalUpdatedEvent(goal.Id, goal.StudentId, goal.SetByTeacherId, goal.Title), ct);
    }

    private int AcademicYearStart() { var today = Today(); return today.Month >= 9 ? today.Year : today.Year - 1; }
    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Clock.GetUtcNow().UtcDateTime, CoachingTime.Turkey));

    private static void RequireOpen(CoachingStudentProfile profile)
    {
        if (profile.OnboardingCompletedAt is not null)
            throw new BusinessRuleException("Onboarding.Completed", "Kurulumun zaten tamamlandı. Bilgilerini Profil ve ayarlar bölümünden değiştirebilirsin.");
    }

    private async Task InTransactionAsync(Func<Task> action, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) { await action(); return; }
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await action();
            await transaction.CommitAsync(ct);
        });
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (Exception e) when (e is DbUpdateConcurrencyException or ConcurrencyException
            || e is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505" } })
        { throw new BusinessRuleException("Onboarding.Conflict", "Bilgilerin başka bir oturumda değişti. Sayfayı yenileyip devam et."); }
    }

    /// <summary>The option lists are asked per keystroke: an active student is remembered for a minute (writes always check).</summary>
    private async Task RequireListAccessAsync(CancellationToken ct)
    {
        var key = "coaching-onboarding-student:" + (access.CurrentUserId ?? Guid.Empty).ToString("N");
        if (cache?.TryGetValue(key, out _) == true && access.CurrentUserId is { } id && access.IsCurrentStudent(id)) return;
        await RequireProfileAsync(false, ct);
        cache?.Set(key, true, TimeSpan.FromMinutes(1));
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
        var goalId = await db.Set<StudentPrimaryGoal>().AsNoTracking().Where(x => x.StudentId == p.UserId).Select(x => (Guid?)x.GoalId).SingleOrDefaultAsync(ct);
        var goal = goalId is null ? null : await db.AcademicGoals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == p.UserId, ct);
        var answers = new OnboardingAnswers(p.BirthDate, p.HomeProvinceId, p.HomeCity, p.HomeDistrictId, p.HomeDistrict, p.EducationStatus, p.GradeLevel,
            p.GraduationYear, p.SchoolName, exam?.Purpose, exam?.PrimaryScoreType, exam?.ForeignLanguage, exam?.TargetExamSessionId, exam?.TargetExamYear,
            p.DiplomaGrade, p.DiplomaGradeEstimated, goal is null ? null : await TargetAsync(goal, ct)) { TargetLocked = goal?.SetByTeacherId is not null };
        var saved = Enumerable.Range(1, CoachingStudentProfile.OnboardingStepCount).Where(p.HasOnboardingStep).ToArray();
        var status = p.OnboardingCompletedAt is not null ? "Completed" : p.OnboardingSteps == 0 ? "NotStarted" : "InProgress";
        return new(status, p.NextOnboardingStep, saved, p.OnboardingCompletedAt, AcademicYearStart(), answers, sessions)
            { AllowedPurposes = LearningPurposes.AllowedFor(p.EducationStatus, p.GradeLevel), AcceptedInvitations = await AcceptedInvitationsAsync(p.UserId, ct) };
    }

    private async Task<IReadOnlyList<OnboardingInvitation>> AcceptedInvitationsAsync(Guid student, CancellationToken ct)
    {
        var rows = await db.CoachingInvitations.AsNoTracking()
            .Where(x => x.AcceptedByUserId == student && x.Status == CoachingInvitationStatus.Accepted && x.Role == "Student")
            .OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id).Take(20)
            .Select(x => new { x.InviterUserId, x.IsInstitutionInvitation, x.UpdatedAt, x.CreatedAt }).ToArrayAsync(ct);
        if (rows.Length == 0) return [];
        var senders = (await accounts.GetAccountsAsync(rows.Select(x => x.InviterUserId).Distinct().ToArray(), ct)).ToDictionary(x => x.UserId);
        return rows.Select(x => new OnboardingInvitation(senders.GetValueOrDefault(x.InviterUserId)?.Email,
            x.IsInstitutionInvitation ? "StudentToInstitution" : "StudentToTeacher", x.UpdatedAt ?? x.CreatedAt)).ToArray();
    }

    private async Task<OnboardingTarget?> TargetAsync(AcademicGoal goal, CancellationToken ct)
    {
        var canEdit = goal.SetByTeacherId is null;
        var scores = goal.ScoreTargets.Select(x => new OnboardingScore(x.ScoreType, x.Score)).ToArray();
        if (goal.TargetSchoolId is { } schoolId)
            return await db.TargetSchools.AsNoTracking().Where(x => x.Id == schoolId).Select(x => new OnboardingTarget("school", x.Id, x.Name,
                x.City + " / " + x.District, x.MinimumScore, x.ScoreYear, "LGS", Array.Empty<OnboardingScore>(), canEdit)).SingleOrDefaultAsync(ct);
        if (goal.TargetUniversityProgramId is { } programId)
            return await db.TargetUniversityPrograms.AsNoTracking().Where(x => x.Id == programId).Select(x => new OnboardingTarget("program", x.Id,
                x.UniversityName + " · " + x.Name, x.FacultyName, x.MinimumScore, x.ScoreYear, x.ScoreType, scores, canEdit)).SingleOrDefaultAsync(ct);
        if (scores.Length > 0) return new("score", null, string.Join(" ve ", scores.Select(x => TypeName(x.ScoreType))), null, null, null, scores[0].ScoreType, scores, canEdit);
        // An older target score without a stored score type.
        return goal.TargetScore is { } legacy ? new("score", null, goal.Title, null, null, null, null, [new OnboardingScore("", legacy)], canEdit) : null;
    }
}

/// <summary>
/// F2 (E4 leftover a, live test 06.11.2026): a wizard answer the server refused, with the id of the wizard question that asks it
/// (for example <c>birthDate</c>, <c>district</c>, <c>education</c>, <c>diploma</c>, <c>targetScore</c>), so the wizard asks that one again.
/// </summary>
public sealed class OnboardingFieldException(string field, string message) : ArgumentException(message)
{
    public string Field { get; } = field;
}
