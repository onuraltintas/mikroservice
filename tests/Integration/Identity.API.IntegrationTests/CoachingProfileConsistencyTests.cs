using Coaching.Application.Authorization;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Events.Coaching;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Paket D (06.11.2026): after the first-login wizard the student edits the same data on Profilim (school, grade, diploma grade),
// Çalışma tercihlerim (exam, field) and Hedefim (target). The pages follow the wizard's rules, so the records never silently diverge.
public sealed class CoachingProfileConsistencyTests
{
    // ---------- #9: changing the exam in Çalışma tercihlerim and the target ----------

    [Fact]
    public async Task ChangingTheExamInStudySettingsClearsTheStudentsOwnTargetThatNoLongerFits()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(null, LearningPurpose.Yks, "SAY");
        await s.Onboarding.SaveTargetAsync(new("program", s.Programs["Bilgisayar Mühendisliği"], null), default);
        await s.SetNets(("TYT", 90), ("AYT_SAY", 60));
        s.Published.Clear();

        await s.SaveSetup(LearningPurpose.Ags, null);

        var goal = await s.PrimaryGoal();
        (goal.TargetUniversityProgramId, goal.TargetSchoolId, goal.TargetScore).Should().Be(((Guid?)null, (Guid?)null, (decimal?)null));
        goal.ScoreTargets.Should().BeEmpty();
        goal.NetTargets.Should().BeEmpty("TYT and AYT nets do not fit AGS");
        s.Published.OfType<GoalUpdatedEvent>().Should().ContainSingle(x => x.GoalId == goal.Id && x.StudentId == s.Student);
        // The home card and the wizard's target question then ask "Hedefini belirle".
        (await s.Onboarding.GetAsync(default)).Answers.Target.Should().BeNull();
        (await s.Estimate()).Target.Should().BeNull();
    }

    [Fact]
    public async Task ChangingTheFieldKeepsOnlyTheScoreAndNetTargetsThatStillFit()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(12, LearningPurpose.Yks, "SAY");
        await s.Onboarding.SaveTargetAsync(new("score", null, [new("TYT", 400), new("SAY", 420)]), default);
        await s.SetNets(("TYT", 90), ("AYT_SAY", 60));
        s.Published.Clear();

        await s.SaveSetup(LearningPurpose.Yks, "EA");

        var goal = await s.PrimaryGoal();
        goal.ScoreTargets.Select(x => (x.ScoreType, x.Score)).Should().Equal(("TYT", 400m));
        (goal.TargetScore, goal.TargetExamType).Should().Be(((decimal?)400m, (ExamType?)ExamType.YKS));
        goal.NetTargets.Select(x => x.ExamCode).Should().Equal("TYT");
        s.Published.OfType<GoalUpdatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task AnOlderSchoolTargetOnAYksStudentIsClearedWhenTheExamSettingsChange()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(12, LearningPurpose.Yks, "SAY");
        // An older target saved before the exam rules (a school goal on a YKS student).
        var goal = AcademicGoal.Create(s.Student, "Fen lisesi", GoalCategory.ExamPreparation);
        goal.SetCatalogTarget(null, s.School); goal.SetNetTarget("LGS", 70);
        s.F.Db.Add(goal); s.F.Db.Add(StudentPrimaryGoal.Create(s.Student, goal.Id)); await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();

        await s.SaveSetup(LearningPurpose.Yks, "EA");

        var saved = await s.PrimaryGoal();
        (saved.TargetSchoolId, saved.NetTargets.Count).Should().Be(((Guid?)null, 0));
    }

    [Fact]
    public async Task SavingTheSameExamSettingsLeavesTheTargetAlone()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(12, LearningPurpose.Yks, "SAY");
        await s.Onboarding.SaveTargetAsync(new("program", s.Programs["Bilgisayar Mühendisliği"], null), default);
        await s.SetNets(("AYT_SAY", 60));
        s.Published.Clear();

        await s.SaveSetup(LearningPurpose.Yks, "SAY");

        var goal = await s.PrimaryGoal();
        (goal.TargetUniversityProgramId, goal.NetTargets.Single().ExamCode).Should().Be(((Guid?)s.Programs["Bilgisayar Mühendisliği"], "AYT_SAY"));
        s.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ATeachersGoalIsKeptAndMarkedAsNotMatchingTheNewExam()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(null, LearningPurpose.Yks, "SAY");
        var goal = AcademicGoal.Create(s.Student, "Öğretmenin hedefi", GoalCategory.ExamPreparation, setByTeacherId: Guid.NewGuid());
        goal.SetCatalogTarget(s.Programs["Bilgisayar Mühendisliği"], null); goal.SetNetTarget("AYT_SAY", 60);
        s.F.Db.Add(goal); s.F.Db.Add(StudentPrimaryGoal.Create(s.Student, goal.Id)); await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();
        (await s.Targets.GetAsync(goal.Id))!.ExamMismatch.Should().BeFalse();
        s.Published.Clear();

        await s.SaveSetup(LearningPurpose.Ags, null);

        var kept = await s.PrimaryGoal();
        (kept.TargetUniversityProgramId, kept.NetTargets.Single().ExamCode).Should().Be(((Guid?)s.Programs["Bilgisayar Mühendisliği"], "AYT_SAY"));
        s.Published.Should().BeEmpty("a teacher's goal is not changed");
        (await s.Targets.GetAsync(goal.Id))!.ExamMismatch.Should().BeTrue();
    }

    // ---------- #8: the grade on Profilim and the study settings ----------

    [Fact]
    public async Task AGradeChangeOnProfilimUpdatesTheStudySettingsWhenTheExamStillFits()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(11, LearningPurpose.Yks, "SAY", diploma: 80);
        var updated = await s.UpdateProfile(x => x with { GradeLevel = 12 });

        updated.DiplomaGrade.Should().Be(80m, "YKS keeps the diploma grade in 12th grade");
        var context = await s.F.Db.StudentLearningContexts.AsNoTracking().SingleAsync();
        (context.EducationStatus, context.GradeLevel).Should().Be((CoachingEducationStatus.Student, (int?)12));
        var setup = await s.Setup.GetAsync(default);
        setup.MissingFields.Should().NotContain("Purpose");
        setup.PurposeNotForEducation.Should().BeFalse();
    }

    [Fact]
    public async Task AGradeTheExamDoesNotFitIsSavedAndTheStudySettingsSayWhy()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(8, LearningPurpose.Lgs, null);
        var updated = await s.UpdateProfile(x => x with { GradeLevel = 9 });

        updated.GradeLevel.Should().Be(9);
        (await s.F.Db.StudentLearningContexts.AsNoTracking().SingleAsync()).GradeLevel.Should().Be(9);
        var setup = await s.Setup.GetAsync(default);
        setup.MissingFields.Should().Contain("Purpose");
        setup.PurposeNotForEducation.Should().BeTrue();
        setup.IsReady.Should().BeFalse();
        // Choosing an exam that fits the new grade clears the reason.
        await s.SaveSetup(LearningPurpose.Yks, "SAY");
        (await s.Setup.GetAsync(default)).PurposeNotForEducation.Should().BeFalse();
    }

    [Theory]
    [InlineData(LearningPurpose.Lgs, null, 11)]
    [InlineData(LearningPurpose.Lgs, null, null)]
    [InlineData(LearningPurpose.Yks, "SAY", 8)]
    [InlineData(LearningPurpose.Ags, null, 7)]
    public async Task TheStudySettingsAcceptOnlyAnExamThatFitsTheGrade(LearningPurpose purpose, string? field, int? grade)
    {
        LearningPurposes.FitsEducation(purpose, grade is null ? CoachingEducationStatus.Graduate : CoachingEducationStatus.Student, grade).Should().BeFalse();
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => s.SaveSetup(purpose, field));
        error.Message.Should().Contain("sınıfına uymuyor");
    }

    [Fact]
    public async Task AGradeWithoutTheDiplomaQuestionClearsTheDiplomaGradeAsTheWizardDoes()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(12, LearningPurpose.Yks, "SAY", diploma: 85);
        var updated = await s.UpdateProfile(x => x with { GradeLevel = 10 });
        updated.DiplomaGrade.Should().BeNull("the diploma grade is asked for 11th, 12th grade and graduates only");
        // A diploma grade sent for such a grade is not kept either.
        (await s.UpdateProfile(x => x with { GradeLevel = 10, DiplomaGrade = 90 })).DiplomaGrade.Should().BeNull();
    }

    [Fact]
    public async Task ChangingTheExamAwayFromYksInStudySettingsClearsTheDiplomaGrade()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(null, LearningPurpose.Yks, "SAY", diploma: 85);
        await s.SaveSetup(LearningPurpose.Yds, null);
        (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).DiplomaGrade.Should().BeNull();
    }

    [Fact]
    public async Task AnInstitutionGradeChangeAlsoUpdatesTheStudySettings()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(11, LearningPurpose.Yks, "SAY");
        var admin = s.F.AddAccount("InstitutionAdmin"); var institution = Guid.NewGuid();
        s.F.Accounts.Institutions.Add(institution);
        s.F.Accounts.Admins[admin] = new(false, institution);
        s.F.Db.Add(CoachingInstitutionMembership.Create(institution, s.Student, CoachingMemberRole.Student, admin)); await s.F.Db.SaveChangesAsync();
        await s.F.Store.UpdateInstitutionStudentAsync(admin, institution, s.Student, 12, null, default);
        (await s.F.Db.StudentLearningContexts.AsNoTracking().SingleAsync()).GradeLevel.Should().Be(12);
    }

    // ---------- #7: the school on Profilim comes from the catalog ----------

    [Fact]
    public async Task ProfilimSavesACatalogSchoolWithItsCityDistrictAndType()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(10, LearningPurpose.Yks, "SAY");
        // The list shows the catalog's school type next to each school.
        (await s.Onboarding.SchoolsAsync("Ankara", null, null, 1, default, "Çankaya")).Items.Single().Should()
            .Match<OnboardingSchoolOption>(x => x.Type == "Fen Lisesi" && x.InHomeDistrict);
        var updated = await s.UpdateProfile(x => x with { SchoolId = s.School, SchoolName = "yazılan ad", SchoolCity = "x", SchoolDistrict = "y", SchoolType = "z" });
        (updated.SchoolName, updated.SchoolCity, updated.SchoolDistrict, updated.SchoolType).Should().Be(("Ankara Fen Lisesi", "ANKARA", "ÇANKAYA", "Fen Lisesi"));
        await Assert.ThrowsAsync<ArgumentException>(() => s.UpdateProfile(x => x with { SchoolId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<ArgumentException>(() => s.UpdateProfile(x => x with { SchoolId = s.ClosedSchool }));
    }

    [Fact]
    public async Task AnOlderTypedSchoolStaysUntilTheStudentPicksOneFromTheList()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(10, LearningPurpose.Yks, "SAY");
        var typed = await s.UpdateProfile(x => x with { SchoolName = "Eski Lise", SchoolCity = "Ankara", SchoolDistrict = "Keçiören", SchoolType = "Lise" });
        (typed.SchoolName, typed.SchoolType).Should().Be(("Eski Lise", "Lise"));
        var picked = await s.UpdateProfile(x => x with { SchoolId = s.School });
        picked.SchoolName.Should().Be("Ankara Fen Lisesi");
    }

    // ---------- Setup ----------

    private sealed class Setup : IAsyncDisposable
    {
        public CoachingOwnedProfilePersistenceTests.Fixture F { get; } = new();
        public Guid Student, School, ClosedSchool, LgsSession;
        public int Year;
        public Dictionary<string, Guid> Programs { get; } = [];
        public List<object> Published { get; } = [];
        public CoachingStudentOnboardingTests.Locations Places { get; } = new();
        private Recorder Events => new(Published);
        private CoachingAccessPolicy Access => new(F.Actor);

        public CoachingLearningSetupService Setup => new(F.Db, Access, F.Accounts, null, Events);
        public CoachingStudentOnboardingService Onboarding => new(F.Db, Access, F.Accounts, Setup, Places, null, null, Events);
        public CoachingProfileStore Store => new(F.Db, F.Accounts, Events, null, Places);
        public CoachingGoalTargetService Targets => new(F.Db, Access, Events);

        public static async Task<Setup> CreateAsync()
        {
            var s = new Setup();
            s.Student = s.F.AddAccount("Student");
            s.F.Actor.UserId = s.Student; s.F.Actor.Roles = ["Student"];
            await s.F.Store.ProvisionAsync(s.Student, "Student", default);
            s.Year = DateTime.UtcNow.Year + 1;
            var lgs = StudyCatalogExam.Create("LGS", "Liselere Geçiş Sistemi");
            lgs.ReplaceSessions([new(null, new(s.Year, 6, 14), $"{s.Year}", null, null)]);
            s.F.Db.Add(lgs);
            var fen = TargetSchool.Create("meb", "fen-1", "Ankara Fen Lisesi", "ANKARA", "ÇANKAYA", 480, 2025);
            fen.SetProgramDetails("Fen Lisesi", null, null); fen.SetActive(true); s.F.Db.Add(fen); s.School = fen.Id;
            var closed = TargetSchool.Create("meb", "old", "Kapanan Lise", "ANKARA", "ÇANKAYA", 300, 2025);
            closed.SetProgramDetails("Anadolu Lisesi", null, null); s.F.Db.Add(closed); s.ClosedSchool = closed.Id;
            s.Program("1001", "Bilgisayar Mühendisliği", "SAY", 470);
            s.Program("1003", "Hukuk", "EA", 450);
            await s.F.Db.SaveChangesAsync();
            s.LgsSession = lgs.Sessions.Single().Id;
            s.F.Db.ChangeTracker.Clear();
            return s;
        }

        private void Program(string code, string name, string scoreType, int minimum)
        {
            var program = TargetUniversityProgram.Create(code, "Ankara Üniversitesi", name, "Fakülte", "Bachelor", scoreType);
            program.UpdateDetails(new(code, "Ankara Üniversitesi", name, scoreType, "ANKARA", null, "Devlet", "Bachelor", null, null, null, null, 50, 50, minimum, minimum, 1000, null),
                2026, 2026, null, "https://yokatlas.yok.gov.tr");
            program.SetActive(true); F.Db.Add(program); Programs[name] = program.Id;
        }

        public Task<OnboardingView> Personal(int? grade) => Onboarding.SavePersonalAsync(new(new DateOnly(2008, 5, 1), "06", "0601",
            grade is null ? CoachingEducationStatus.Graduate : CoachingEducationStatus.Student, grade, null, null), default);

        /// <summary>The wizard's first two steps, then the wizard is finished.</summary>
        public async Task Wizard(int? grade, LearningPurpose purpose, string? field, decimal? diploma = null)
        {
            await Personal(grade);
            await Onboarding.SaveExamAsync(new(purpose, field, null, purpose == LearningPurpose.Lgs ? LgsSession : null,
                purpose == LearningPurpose.Lgs ? null : Year, diploma, false), default);
            await Onboarding.SaveTargetAsync(new("none", null, null), default);
            foreach (var step in new[] { 4, 5, 6 }) await Onboarding.PassStepAsync(step, default);
            await Onboarding.CompleteAsync(default);
            F.Db.ChangeTracker.Clear();
        }

        /// <summary>Çalışma tercihlerim: the exam and the field change, the rest of the setup is sent as it is.</summary>
        public async Task SaveSetup(LearningPurpose purpose, string? field)
        {
            var context = await F.Db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x => x.StudentUserId == Student);
            var year = context?.AcademicYearStart ?? (DateTime.UtcNow.Month >= 9 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1);
            var language = purpose is LearningPurpose.Yds or LearningPurpose.Yokdil || field == "DIL" ? "English" : null;
            await Setup.SaveAsync(new(context?.Version, year, purpose, Year, field, language, [], [], false), default);
            F.Db.ChangeTracker.Clear();
        }

        public async Task SetNets(params (string Exam, decimal Net)[] nets)
        {
            var goal = await F.Db.AcademicGoals.SingleAsync(x => x.Id == F.Db.Set<StudentPrimaryGoal>().Single(p => p.StudentId == Student).GoalId);
            foreach (var (exam, net) in nets) goal.SetNetTarget(exam, net);
            await F.Db.SaveChangesAsync(); F.Db.ChangeTracker.Clear();
        }

        public async Task<AcademicGoal> PrimaryGoal()
        {
            var id = await F.Db.Set<StudentPrimaryGoal>().AsNoTracking().Where(x => x.StudentId == Student).Select(x => x.GoalId).SingleAsync();
            return await F.Db.AcademicGoals.AsNoTracking().SingleAsync(x => x.Id == id);
        }

        public async Task<CoachingStudentProfile> UpdateProfile(Func<UpdateCoachingStudentProfile, UpdateCoachingStudentProfile> change)
        {
            var p = await F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == Student);
            var request = new UpdateCoachingStudentProfile(p.Version, p.EducationStatus, p.GradeLevel, p.SchoolName, p.SchoolCity, p.SchoolDistrict,
                p.SchoolType, p.ClassBranch, p.GraduationYear, p.BirthDate, p.Bio, p.AvatarUrl, p.ShareProgressWithTeachers)
                { DiplomaGrade = p.DiplomaGrade, DiplomaGradeEstimated = p.DiplomaGradeEstimated };
            var saved = await Store.UpdateStudentAsync(Student, change(request), default);
            F.Db.ChangeTracker.Clear();
            return saved;
        }

        public Task<ScoreEstimate> Estimate() =>
            new CoachingScoreEstimateService(F.Db, F.Scope, TimeProvider.System).EstimateAsync(Student, Student, null, default);

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }

    private sealed class Recorder(List<object> published) : Coaching.Application.Interfaces.ICoachingEventPublisher
    {
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class { published.Add(message); return Task.CompletedTask; }
    }
}
