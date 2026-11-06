using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Paket C (#3, user decisions 05.11.2026): the coaching student's mandatory first-login wizard. Each step is saved on its own
// into the records the product already uses; a changed answer re-opens only the steps that depend on it; long lists are
// filtered and paged on the server. #4: the score estimate compares a score-only target and uses the saved diploma grade.
public sealed class CoachingStudentOnboardingTests
{
    // ---------- Steps, resume and dependencies ----------

    [Fact]
    public async Task ANewStudentGoesThroughEveryStepAndResumesWhereTheyLeftOff()
    {
        await using var s = await Setup.CreateAsync();
        var start = await s.Service.GetAsync(default);
        (start.Status, start.NextStep, start.SavedSteps.Count).Should().Be(("NotStarted", 1, 0));

        var personal = await s.Personal(grade: 8);
        (personal.Status, personal.NextStep).Should().Be(("InProgress", 2));
        personal.Answers.Should().Match<OnboardingAnswers>(x => x.HomeCity == "Ankara" && x.HomeDistrict == "Çankaya" && x.HomeProvinceId == "06"
            && x.BirthDate == new DateOnly(2012, 5, 1) && x.GradeLevel == 8 && x.EducationStatus == CoachingEducationStatus.Student);

        var exam = await s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default);
        exam.NextStep.Should().Be(3);
        (exam.Answers.Purpose, exam.Answers.TargetExamSessionId, exam.Answers.TargetExamYear).Should().Be(((LearningPurpose?)LearningPurpose.Lgs, (Guid?)s.LgsSession, (int?)s.LgsYear));

        // A fresh service (another request) resumes at the target question.
        (await s.Service.GetAsync(default)).NextStep.Should().Be(3);
        var target = await s.Service.SaveTargetAsync(new("school", s.School, null, null), default);
        target.Answers.Target.Should().Match<OnboardingTarget>(x => x.Kind == "school" && x.Name == "Ankara Fen Lisesi" && x.MinimumScore == 480m);
        var goal = await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync();
        (goal.TargetSchoolId, goal.Category).Should().Be(((Guid?)s.School, GoalCategory.ExamPreparation));
        (await s.F.Db.Set<StudentPrimaryGoal>().AsNoTracking().SingleAsync()).GoalId.Should().Be(goal.Id);

        foreach (var step in new[] { 4, 5, 6 }) await s.Service.PassStepAsync(step, default);
        (await s.Service.GetAsync(default)).NextStep.Should().Be(7);
        var done = await s.Service.CompleteAsync(default);
        (done.State.Status, done.State.CompletedAt.HasValue, done.NeedsSubscription).Should().Be(("Completed", true, false));

        // After completion the wizard is closed; changes go through the regular pages.
        var closed = await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.SaveTargetAsync(new("none", null, null, null), default));
        closed.Code.Should().Be("Onboarding.Completed");
    }

    [Fact]
    public async Task CompletingNeedsEveryStepAndReportsWhetherASubscriptionIsNeeded()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 8);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CompleteAsync(default));
        await s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default);
        await s.Service.SaveTargetAsync(new("none", null, null, null), default);
        foreach (var step in new[] { 4, 5, 6 }) await s.Service.PassStepAsync(step, default);
        (await s.ServiceWith(NoAccess()).CompleteAsync(default)).NeedsSubscription.Should().BeTrue();
    }

    [Fact]
    public async Task ChangingTheExamReopensOnlyTheTargetAndClearsIt()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 8);
        await s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default);
        await s.Service.SaveTargetAsync(new("school", s.School, null, null), default);
        foreach (var step in new[] { 4, 5, 6 }) await s.Service.PassStepAsync(step, default);

        var changed = await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, null, false), default);
        changed.NextStep.Should().Be(3);
        changed.SavedSteps.Should().Equal(1, 2, 4, 5, 6);
        changed.Answers.Target.Should().BeNull();
        (await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync()).TargetSchoolId.Should().BeNull();

        // The same exam again keeps the target step.
        await s.Service.SaveTargetAsync(new("score", null, "SAY", 420), default);
        (await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, null, false), default)).NextStep.Should().Be(7);
    }

    [Fact]
    public async Task ChangingTheGradeReopensTheExamStep()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 11);
        await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "EA", null, null, s.LgsYear + 1, 82.5m, true), default);
        (await s.Personal(grade: 11)).NextStep.Should().Be(3, "the same grade keeps the exam step");
        (await s.Personal(grade: 12)).NextStep.Should().Be(2);
    }

    [Theory]
    [InlineData(12, 85.5, 85.5)]
    [InlineData(11, 70.0, 70.0)]
    [InlineData(10, 85.0, null)]   // OBP is asked for 11th, 12th grade and graduates only
    public async Task TheDiplomaGradeIsKeptForYksFromEleventhGrade(int grade, double given, double? kept)
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade);
        var view = await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, (decimal)given, true), default);
        view.Answers.DiplomaGrade.Should().Be((decimal?)kept);
        view.Answers.DiplomaGradeEstimated.Should().Be(kept.HasValue);
    }

    [Fact]
    public async Task PersonalAnswersAreChecked()
    {
        await using var s = await Setup.CreateAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "06", "99", CoachingEducationStatus.Student, 8, null, null), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "34", "0601", CoachingEducationStatus.Student, 8, null, null), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SavePersonalAsync(new(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), "06", "0601", CoachingEducationStatus.Student, 8, null, null), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SavePersonalAsync(new(null, "06", "0601", CoachingEducationStatus.Student, 8, null, null), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, null, s.LgsYear, null, false), default));
        (await s.Service.GetAsync(default)).SavedSteps.Should().BeEmpty();
        var graduate = await s.Service.SavePersonalAsync(new(new DateOnly(2000, 1, 1), "06", "0601", CoachingEducationStatus.Graduate, null, 2018, "Ankara Lisesi"), default);
        (graduate.Answers.EducationStatus, graduate.Answers.GraduationYear, graduate.Answers.SchoolName).Should().Be((CoachingEducationStatus.Graduate, (int?)2018, "Ankara Lisesi"));
    }

    // ---------- Targets ----------

    [Fact]
    public async Task ScoreTargetsUseTheScoreTypeOfTheExam()
    {
        Types(LearningPurpose.Lgs, null).Should().Equal("LGS");
        Types(LearningPurpose.Yks, "SAY").Should().Equal("TYT", "SAY");
        Types(LearningPurpose.Yks, "TYT").Should().Equal("TYT");
        Types(LearningPurpose.Yks, "DIL").Should().Equal("TYT", "DIL");
        Types(LearningPurpose.Ags, null).Should().Equal("P1");
        Types(LearningPurpose.Ags, "OABT_IMAT").Should().Equal("P2_IMAT");
        Types(LearningPurpose.Ags, "OABT_DIN").Should().Equal("P2_16", "P2_17");
        Types(LearningPurpose.Yds, null).Should().Equal("YDS_EN");
        Types(LearningPurpose.Yokdil, "YOKDIL_SAG").Should().Equal("YOKDIL_SAG");

        static IReadOnlyList<string> Types(LearningPurpose purpose, string? scoreType) =>
            CoachingStudentOnboardingService.ScoreTargetTypes(StudentLearningContext.Create(Guid.NewGuid(), 2026, purpose, 2027, scoreType,
                purpose is LearningPurpose.Yds or LearningPurpose.Yokdil || scoreType == "DIL" ? "English" : null));
    }

    [Fact]
    public async Task ScoreTargetsAreValidatedAndStoredWithTheirType()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 12);
        await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, null, false), default);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("score", null, "EA", 400), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("score", null, "SAY", 561), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("school", s.School, null, null), default));
        var view = await s.Service.SaveTargetAsync(new("score", null, "TYT", 380.5m), default);
        view.Answers.Target.Should().Match<OnboardingTarget>(x => x.Kind == "score" && x.ScoreType == "TYT" && x.TargetScore == 380.5m && x.Name == "TYT");
        var goal = await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync();
        (goal.TargetScore, goal.TargetMaxScore, goal.TargetExamType, goal.TargetScoreType, goal.Title).Should().Be(((decimal?)380.5m, (decimal?)560m, (ExamType?)ExamType.YKS, "TYT", "TYT hedefi"));

        // "Henüz karar vermedim" clears it again.
        (await s.Service.SaveTargetAsync(new("none", null, null, null), default)).Answers.Target.Should().BeNull();
        (await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync()).TargetScore.Should().BeNull();
    }

    [Fact]
    public async Task AProgrammeMustFitTheFieldAndOneWithoutATabanNeedsTheStudentsOwnScore()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 12);
        await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, null, false), default);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("program", s.Programs["Hukuk"], null, null), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("program", s.Programs["Fizik"], null, null), default));
        var withOwn = await s.Service.SaveTargetAsync(new("program", s.Programs["Fizik"], null, 410), default);
        withOwn.Answers.Target.Should().Match<OnboardingTarget>(x => x.Kind == "program" && x.MinimumScore == null && x.TargetScore == 410m);
        var view = await s.Service.SaveTargetAsync(new("program", s.Programs["Bilgisayar Mühendisliği"], null, null), default);
        view.Answers.Target.Should().Match<OnboardingTarget>(x => x.Kind == "program" && x.Name == "Ankara Üniversitesi · Bilgisayar Mühendisliği"
            && x.MinimumScore == 470m && x.TargetScore == null);
        (await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync()).TargetScore.Should().BeNull();
    }

    [Fact]
    public async Task ATeachersGoalIsNotChangedByTheWizard()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 8);
        await s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default);
        var goal = AcademicGoal.Create(s.Student, "Öğretmenin hedefi", GoalCategory.ExamPreparation, setByTeacherId: Guid.NewGuid());
        s.F.Db.Add(goal); await s.F.Db.SaveChangesAsync(); s.F.Db.Add(StudentPrimaryGoal.Create(s.Student, goal.Id)); await s.F.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.SaveTargetAsync(new("school", s.School, null, null), default));
        error.Code.Should().Be("Onboarding.TeacherGoal");
        (await s.Service.SaveTargetAsync(new("none", null, null, null), default)).NextStep.Should().Be(4);
    }

    // ---------- Filtered option lists ----------

    [Fact]
    public async Task SchoolListsAreNarrowedByCityAndTypeAndPaged()
    {
        await using var s = await Setup.CreateAsync();
        (await s.Service.SchoolCitiesAsync(default)).Select(x => (x.Value, x.Count)).Should().Equal(("Ankara", 26), ("İzmir", 1));
        (await s.Service.SchoolTypesAsync("Ankara", default)).Select(x => x.Value).Should().Equal("Anadolu Lisesi", "Fen Lisesi");
        var page = await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", null, 1, default);
        (page.TotalCount, page.Items.Count, page.PageSize).Should().Be((25, 20, 20));
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", null, 2, default)).Items.Should().HaveCount(5);
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", "lise 7", 1, default)).Items.Select(x => x.Name).Should().Equal("Anadolu Lise 7");
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", "LİSE 25", 1, default)).Items.Select(x => x.Name).Should().Equal("Anadolu Lise 25");
        (await s.Service.SchoolCitiesAsync(default)).Should().NotContain(x => x.Value == "Kapanan Lise");
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SchoolsAsync(null, "Anadolu Lisesi", null, 1, default));
    }

    [Fact]
    public async Task ProgrammeListsAreNarrowedByLevelScoreTypeCityAndUniversity()
    {
        await using var s = await Setup.CreateAsync();
        (await s.Service.ProgramCitiesAsync("Bachelor", "SAY", default)).Select(x => x.Value).Should().Equal("Ankara", "İstanbul");
        (await s.Service.ProgramCitiesAsync("Associate", null, default)).Select(x => x.Value).Should().Equal("Ankara");
        (await s.Service.UniversitiesAsync("Bachelor", "SAY", "Ankara", null, default)).Select(x => x.Value).Should().Equal("Ankara Üniversitesi");
        var programs = await s.Service.ProgramsAsync("Bachelor", "SAY", "Ankara", "Ankara Üniversitesi", null, 1, default);
        programs.Items.Select(x => x.Name).Should().Equal("Bilgisayar Mühendisliği", "Fizik");
        programs.Items[0].Should().Match<OnboardingProgramOption>(x => x.MinimumScore == 470m && x.Rank == 12000 && x.ScoreType == "SAY");
        (await s.Service.ProgramsAsync("Bachelor", "EA", "Ankara", "Ankara Üniversitesi", null, 1, default)).Items.Select(x => x.Name).Should().Equal("Hukuk");
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.ProgramCitiesAsync("Doctorate", null, default));
    }

    [Fact]
    public async Task OnlyAnActiveStudentCanUseTheWizard()
    {
        await using var s = await Setup.CreateAsync();
        s.F.Actor.UserId = s.F.AddAccount("Teacher"); s.F.Actor.Roles = ["Teacher"];
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.GetAsync(default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.SchoolCitiesAsync(default));
    }

    // ---------- #4: the score estimate behind the goal card ----------

    [Fact]
    public async Task AScoreOnlyTargetIsComparedWithTheEstimateOfItsScoreType()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 8);
        await s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default);
        await s.Service.SaveTargetAsync(new("score", null, "LGS", 450), default);
        var estimates = new CoachingScoreEstimateService(s.F.Db, s.F.Scope, TimeProvider.System);

        var before = await estimates.EstimateAsync(s.Student, s.Student, null, default);
        before.Target.Should().Match<ScoreTargetGap>(x => x.Kind == "score" && x.ScoreType == "LGS" && x.TargetScore == 450m && x.ScoreGap == null && x.Status == null);

        await s.ApplyLgsPack();
        s.FullLgsExam();
        var after = await estimates.EstimateAsync(s.Student, s.Student, null, default);
        after.Target!.ScoreGap.Should().BeLessThan(0, "a full-net exam is above 450");
        (after.Target.Status, after.Target.Reached, after.Goal!.ScoreType).Should().Be(("reached", true, "LGS"));
    }

    [Fact]
    public async Task AYdsTargetScoreIsComparedExactly()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: null);
        await s.Service.SaveExamAsync(new(LearningPurpose.Yds, null, null, null, s.LgsYear, null, false), default);
        await s.Service.SaveTargetAsync(new("score", null, "YDS_EN", 85), default);
        s.LanguageExam(60);
        var estimate = await new CoachingScoreEstimateService(s.F.Db, s.F.Scope, TimeProvider.System).EstimateAsync(s.Student, s.Student, null, default);
        estimate.Target.Should().Match<ScoreTargetGap>(x => x.Kind == "score" && x.ScoreType == "YDS_EN" && x.ScoreGap == 10m && x.NetGap == 8m && x.Status == "below");
        estimate.Goal!.ScoreType.Should().Be("YDS_EN");
    }

    [Fact]
    public async Task TheSavedDiplomaGradeIsUsedWhenTheCallerSendsNone()
    {
        await using var s = await Setup.CreateAsync();
        var profile = await s.F.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == s.Student);
        profile.SetDiplomaGrade(90, false); await s.F.Db.SaveChangesAsync();
        Assert.Throws<ArgumentException>(() => profile.SetDiplomaGrade(49.99m, false));
        Assert.Throws<ArgumentException>(() => profile.SetDiplomaGrade(80.123m, false));
        var service = new CoachingScoreEstimateService(s.F.Db, s.F.Scope, TimeProvider.System);
        // No YKS model in this test; the call must still accept the stored grade (it is validated like the query one).
        (await service.EstimateAsync(s.Student, s.Student, null, default)).Estimates.Should().BeEmpty();
    }

    // ---------- Setup ----------

    /// <summary>A subscription service whose only answer is "no active subscription".</summary>
    public class NoAccessProxy : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args) => method?.Name == "GetMyAccessAsync"
            ? Task.FromResult(new Coaching.Application.Subscriptions.CoachingSubscriptionAccessSummary(false, true, null, null, null, []))
            : throw new NotSupportedException(method?.Name);
    }
    private static Coaching.Application.Subscriptions.ICoachingSubscription NoAccess() =>
        System.Reflection.DispatchProxy.Create<Coaching.Application.Subscriptions.ICoachingSubscription, NoAccessProxy>();

    private sealed class Locations : ICoachingLocationDirectory
    {
        public Task<IReadOnlyList<LocationProvince>> GetProvincesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LocationProvince>>([new("06", "Ankara"), new("34", "İstanbul")]);
        public Task<IReadOnlyList<LocationDistrict>> GetDistrictsAsync(string provinceId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LocationDistrict>>(provinceId == "06" ? [new("0601", "06", "Çankaya"), new("0602", "06", "Keçiören")] : [new("3401", "34", "Kadıköy")]);
        public Task<bool> VerifyPairAsync(string provinceId, string districtId, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CoachingOwnedProfilePersistenceTests.Fixture F { get; } = new();
        public Guid Student, School, LgsSession;
        public int LgsYear;
        public Dictionary<string, Guid> Programs { get; } = [];
        private int order;

        public CoachingStudentOnboardingService Service => ServiceWith(null);
        public CoachingStudentOnboardingService ServiceWith(Coaching.Application.Subscriptions.ICoachingSubscription? subscriptions)
        {
            var access = new CoachingAccessPolicy(F.Actor);
            return new(F.Db, access, F.Accounts, new CoachingLearningSetupService(F.Db, access, F.Accounts), new Locations(), subscriptions);
        }

        public static async Task<Setup> CreateAsync()
        {
            var s = new Setup();
            s.Student = s.F.AddAccount("Student");
            s.F.Actor.UserId = s.Student; s.F.Actor.Roles = ["Student"];
            await s.F.Store.ProvisionAsync(s.Student, "Student", default);
            s.LgsYear = DateTime.UtcNow.Year + 1;
            var lgs = StudyCatalogExam.Create("LGS", "Liselere Geçiş Sistemi");
            lgs.ReplaceSessions([new(null, new(s.LgsYear, 6, 14), $"{s.LgsYear}", null, null)]);
            var yds = StudyCatalogExam.Create("YDS", "Yabancı Dil Bilgisi Seviye Tespit Sınavı");
            s.F.Db.AddRange(lgs, yds);
            var fen = TargetSchool.Create("meb", "fen-1", "Ankara Fen Lisesi", "Ankara", "Çankaya", 480, 2025);
            fen.SetProgramDetails("Fen Lisesi", null, null); fen.SetActive(true); s.F.Db.Add(fen); s.School = fen.Id;
            for (var i = 1; i <= 25; i++)
            {
                var school = TargetSchool.Create("meb", $"al-{i}", $"Anadolu Lise {i}", "Ankara", i % 2 == 0 ? "Çankaya" : "Keçiören", 300 + i, 2025);
                school.SetProgramDetails("Anadolu Lisesi", null, null); school.SetActive(true); s.F.Db.Add(school);
            }
            var izmir = TargetSchool.Create("meb", "iz-1", "İzmir Fen Lisesi", "İzmir", "Bornova", 470, 2025);
            izmir.SetProgramDetails("Fen Lisesi", null, null); izmir.SetActive(true); s.F.Db.Add(izmir);
            var passive = TargetSchool.Create("meb", "old", "Kapanan Lise", "Ankara", "Çankaya", 300, 2025);
            passive.SetProgramDetails("Anadolu Lisesi", null, null); s.F.Db.Add(passive);
            s.Program("1001", "Ankara Üniversitesi", "Bilgisayar Mühendisliği", "Bachelor", "SAY", "Ankara", 470, 12000);
            s.Program("1002", "Ankara Üniversitesi", "Fizik", "Bachelor", "SAY", "Ankara", null, null);
            s.Program("1003", "Ankara Üniversitesi", "Hukuk", "Bachelor", "EA", "Ankara", 450, 20000);
            s.Program("1004", "İstanbul Üniversitesi", "Matematik", "Bachelor", "SAY", "İstanbul", 420, 50000);
            s.Program("1005", "Ankara Üniversitesi", "Bilgisayar Programcılığı", "Associate", "TYT", "Ankara", 300, 400000);
            await s.F.Db.SaveChangesAsync();
            s.LgsSession = lgs.Sessions.Single().Id;
            s.F.Db.ChangeTracker.Clear();
            return s;
        }

        private void Program(string code, string university, string name, string level, string scoreType, string city, int? minimum, int? rank)
        {
            var program = TargetUniversityProgram.Create(code, university, name, "Fakülte", level, scoreType);
            program.UpdateDetails(new(code, university, name, scoreType, city, null, "Devlet", level, null, null, null, null, 50, 50, minimum, minimum, rank, null),
                2026, 2026, null, "https://yokatlas.yok.gov.tr");
            program.SetActive(true); F.Db.Add(program); Programs[name] = program.Id;
        }

        public Task<OnboardingView> Personal(int? grade) => Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "06", "0601",
            grade is null ? CoachingEducationStatus.Graduate : CoachingEducationStatus.Student, grade, null, null), default);

        public async Task ApplyLgsPack()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
            var csv = await File.ReadAllTextAsync(Path.Combine(root!.FullName, "content-packs", "coaching-score-models", "2026-lgs", "puan-modelleri-2026-lgs.csv"));
            var admin = F.AddAccount("SystemAdmin");
            var service = new CoachingScoreEstimateService(F.Db, F.Scope, TimeProvider.System);
            await service.ApplyAsync(admin, new(2026, csv, (await service.PreviewAsync(admin, new(2026, csv, null), default)).Fingerprint), default);
        }

        public void FullLgsExam() => Exam("LGS", PracticeExamRules.Profiles.Single(x => x.Code == "LGS").Sections.ToDictionary(x => x.Code, x => (x.QuestionCount, 0)));
        public void LanguageExam(int correct) => Exam("YDS_EN", new() { ["ENG"] = (correct, 10) });

        private void Exam(string code, Dictionary<string, (int Correct, int Wrong)> answers)
        {
            var profile = PracticeExamRules.Profiles.Single(x => x.Code == code);
            var sections = profile.Sections.Select(section =>
            {
                var (correct, wrong) = answers.GetValueOrDefault(section.Code);
                return new PracticeSectionAnswers(section.Code, correct, wrong, section.QuestionCount - correct - wrong);
            }).ToArray();
            var type = PracticeExamRules.ExamTypeFor(code);
            var exam = Coaching.Domain.Entities.Exam.CreateStudentReported(Student, $"{code} denemesi", type,
                DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-(10 + order++)), DateTimeKind.Utc), 500);
            var result = ExamResult.Create(exam.Id, Student, 0);
            result.SetAnswerStatistics(sections.Sum(x => x.Correct), sections.Sum(x => x.Wrong), sections.Sum(x => x.Empty));
            result.SetPracticeExam(new(code, sections), type);
            exam.AddResult(result);
            F.Db.Add(exam); F.Db.SaveChanges();
        }

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }
}
