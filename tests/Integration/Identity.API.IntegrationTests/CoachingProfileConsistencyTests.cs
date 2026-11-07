using Coaching.Application.Authorization;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.Repositories;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
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
        // G1 (07.11.2026): a university graduate's YKS setup from before the rule; AGS is chosen now.
        await s.OlderYksSetup(CoachingEducationStatus.UniversityGraduate);
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
        await s.OlderYksSetup(CoachingEducationStatus.UniversityGraduate);
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

    // ---------- E4 #26 (live test 06.11.2026): the student is told before saving what a change removes ----------

    [Fact]
    public async Task TheStudySettingsTellBeforeSavingWhichPartsOfTheOwnTargetAndTheDiplomaGradeAChangeRemoves()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(12, LearningPurpose.Yks, "SAY", diploma: 85);
        await s.Onboarding.SaveTargetAsync(new("program", s.Programs["Bilgisayar Mühendisliği"], null), default);
        await s.SetNets(("TYT", 90), ("AYT_SAY", 60.5m));

        var same = await s.Settings.PreviewAsync(new(LearningPurpose.Yks, "SAY", null), default);
        (same.RemovedTargets.Count, same.TeacherGoalMismatch, same.DiplomaGradeRemoved).Should().Be((0, false, (decimal?)null));

        var field = await s.Settings.PreviewAsync(new(LearningPurpose.Yks, "EA", null), default);
        field.RemovedTargets.Should().Equal("Ankara Üniversitesi · Bilgisayar Mühendisliği", "AYT · Sayısal net hedefin (60,5)");
        field.DiplomaGradeRemoved.Should().BeNull("the diploma grade stays with YKS");

        var exam = await s.Settings.PreviewAsync(new(LearningPurpose.Yds, null, "English"), default);
        exam.RemovedTargets.Should().BeEquivalentTo(["Ankara Üniversitesi · Bilgisayar Mühendisliği", "TYT net hedefin (90)", "AYT · Sayısal net hedefin (60,5)"]);
        exam.DiplomaGradeRemoved.Should().Be(85m);

        // Asking saves nothing; saving then removes exactly what was announced.
        (await s.PrimaryGoal()).TargetUniversityProgramId.Should().Be(s.Programs["Bilgisayar Mühendisliği"]);
        (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).DiplomaGrade.Should().Be(85m);
        await s.SaveSetup(LearningPurpose.Yks, "EA");
        var goal = await s.PrimaryGoal();
        (goal.TargetUniversityProgramId, goal.NetTargets.Single().ExamCode).Should().Be(((Guid?)null, "TYT"));
    }

    [Fact]
    public async Task TheStudySettingsNameScoreTargetsThatNoLongerFitAndKeepATeachersGoal()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(12, LearningPurpose.Yks, "SAY");
        await s.Onboarding.SaveTargetAsync(new("score", null, [new("TYT", 400), new("SAY", 420.25m)]), default);
        (await s.Settings.PreviewAsync(new(LearningPurpose.Yks, "EA", null), default)).RemovedTargets.Should().Equal("YKS Sayısal hedef puanın (420,25)");

        await using var t = await Setup.CreateAsync();
        await t.OlderYksSetup(CoachingEducationStatus.UniversityGraduate);
        var goal = AcademicGoal.Create(t.Student, "Öğretmenin hedefi", GoalCategory.ExamPreparation, setByTeacherId: Guid.NewGuid());
        goal.SetCatalogTarget(t.Programs["Bilgisayar Mühendisliği"], null);
        t.F.Db.Add(goal); t.F.Db.Add(StudentPrimaryGoal.Create(t.Student, goal.Id)); await t.F.Db.SaveChangesAsync(); t.F.Db.ChangeTracker.Clear();
        var teacher = await t.Settings.PreviewAsync(new(LearningPurpose.Ags, null, null), default);
        (teacher.RemovedTargets.Count, teacher.TeacherGoalMismatch).Should().Be((0, true), "a teacher's goal is kept; the student is told it no longer matches");
        (await t.Settings.PreviewAsync(new(LearningPurpose.Yks, "SAY", null), default)).TeacherGoalMismatch.Should().BeFalse();
    }

    [Fact]
    public async Task ProfilimTellsBeforeSavingWhatAGradeChangeRemovesOrLeavesNotFitting()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(12, LearningPurpose.Yks, "SAY", diploma: 85);
        await s.Onboarding.SaveTargetAsync(new("program", s.Programs["Bilgisayar Mühendisliği"], null), default);

        (await s.Store.PreviewEducationAsync(s.Student, CoachingEducationStatus.Student, 11, default))
            .Should().Be(new StudentEducationImpact(false, null, null, null));
        (await s.Store.PreviewEducationAsync(s.Student, CoachingEducationStatus.Student, 10, default))
            .Should().Be(new StudentEducationImpact(false, null, 85m, null), "the diploma grade is asked from 11th grade on");
        var eighth = await s.Store.PreviewEducationAsync(s.Student, CoachingEducationStatus.Student, 8, default);
        (eighth.ExamNoLongerFits, eighth.Exam, eighth.DiplomaGradeRemoved, eighth.Target)
            .Should().Be((true, "YKS", (decimal?)85m, "Ankara Üniversitesi · Bilgisayar Mühendisliği"));
        // G1 (07.11.2026): a university graduate no longer takes YKS (and so has no diploma grade question).
        var graduate = await s.Store.PreviewEducationAsync(s.Student, CoachingEducationStatus.UniversityGraduate, null, default);
        (graduate.ExamNoLongerFits, graduate.Exam, graduate.DiplomaGradeRemoved).Should().Be((true, "YKS", (decimal?)85m));
        (await s.Store.PreviewEducationAsync(s.Student, CoachingEducationStatus.HighSchoolGraduate, null, default))
            .Should().Be(new StudentEducationImpact(false, null, null, null));

        var stored = await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync();
        (stored.GradeLevel, stored.DiplomaGrade).Should().Be(((int?)12, (decimal?)85m), "asking saves nothing");
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
        var setup = await s.Settings.GetAsync(default);
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
        var setup = await s.Settings.GetAsync(default);
        setup.MissingFields.Should().Contain("Purpose");
        setup.PurposeNotForEducation.Should().BeTrue();
        setup.IsReady.Should().BeFalse();
        // Choosing an exam that fits the new grade clears the reason.
        await s.SaveSetup(LearningPurpose.Yks, "SAY");
        (await s.Settings.GetAsync(default)).PurposeNotForEducation.Should().BeFalse();
    }

    [Theory]
    [InlineData(LearningPurpose.Lgs, null, 11)]
    [InlineData(LearningPurpose.Lgs, null, null)]
    [InlineData(LearningPurpose.Yks, "SAY", 8)]
    [InlineData(LearningPurpose.Ags, null, 7)]
    [InlineData(LearningPurpose.Ags, null, 12)]   // G1 (07.11.2026): AGS/ÖABT only for university students and graduates
    public async Task TheStudySettingsAcceptOnlyAnExamThatFitsTheGrade(LearningPurpose purpose, string? field, int? grade)
    {
        LearningPurposes.FitsEducation(purpose, grade is null ? CoachingEducationStatus.HighSchoolGraduate : CoachingEducationStatus.Student, grade).Should().BeFalse();
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
        // G1: a university student's YKS setup from before the rule keeps its diploma grade until another exam is chosen.
        await s.OlderYksSetup(CoachingEducationStatus.UniversityStudent, diploma: 85);
        // Profilim sends no diploma grade where the rule does not ask it; another save leaves the stored one as it is.
        (await s.UpdateProfile(x => x with { Bio = "Merhaba", DiplomaGrade = null })).DiplomaGrade.Should().Be(85m, "existing data is not changed by the new rule");
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

    // ---------- Review fixes (PR #114) ----------

    [Fact]
    public async Task AnUpgradedStudentWithLgsInTwelfthGradeKeepsThePanelAndIsToldToChooseAgain()
    {
        await using var s = await Setup.CreateAsync();
        // An existing student from before the wizard: grade 12, an old LGS setup, the wizard marked done by the upgrade.
        var profile = await s.F.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == s.Student);
        profile.UpdateEducation(CoachingEducationStatus.Student, 12, null, null, null, null, null, null);
        foreach (var step in Enumerable.Range(1, CoachingStudentProfile.OnboardingStepCount - 1)) profile.RecordOnboardingStep(step);
        profile.CompleteOnboarding(DateTime.UtcNow);
        var context = StudentLearningContext.Create(s.Student, s.Year - 1, LearningPurpose.Lgs, s.Year, null, null);
        context.SetEducation(CoachingEducationStatus.Student, 12, null);
        s.F.Db.Add(context); await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();

        (await s.Onboarding.GetAsync(default)).Status.Should().Be("Completed", "no wizard redirect");
        var setup = await s.Settings.GetAsync(default);
        (setup.PurposeNotForEducation, setup.IsReady).Should().Be((true, false));
        setup.AllowedPurposes.Should().NotContain(LearningPurpose.Lgs);
        var preview = new CoachingAutomaticStudyPlanPreviewService(s.F.Db, new CoachingAccessPolicy(s.F.Actor), s.Settings);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => preview.PreviewAsync(
            new AutomaticStudyPreviewRequest(new(2026, 10, 19), 7, 0, [new(Guid.NewGuid(), 30)], context.Version)));
        error.Message.Should().Contain("Seçili sınavın eğitim durumuna ya da sınıfına uymuyor");
    }

    [Fact]
    public async Task ANonGraduateWithAnAgsSetupIsToldToChooseAgain()
    {
        await using var s = await Setup.CreateAsync();
        // An existing 11th grader with AGS saved before the rule (user decision 06.11.2026: AGS only for graduates).
        var profile = await s.F.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == s.Student);
        profile.UpdateEducation(CoachingEducationStatus.Student, 11, null, null, null, null, null, null);
        foreach (var step in Enumerable.Range(1, CoachingStudentProfile.OnboardingStepCount - 1)) profile.RecordOnboardingStep(step);
        profile.CompleteOnboarding(DateTime.UtcNow);
        var context = StudentLearningContext.Create(s.Student, s.Year - 1, LearningPurpose.Ags, s.Year, null, null);
        context.SetEducation(CoachingEducationStatus.Student, 11, null);
        s.F.Db.Add(context); await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();

        (await s.Onboarding.GetAsync(default)).Status.Should().Be("Completed", "no wizard redirect");
        var setup = await s.Settings.GetAsync(default);
        (setup.PurposeNotForEducation, setup.IsReady).Should().Be((true, false));
        setup.MissingFields.Should().Contain("Purpose");
        setup.AllowedPurposes.Should().Equal(LearningPurpose.Yks);
    }

    [Fact]
    public async Task TheWizardRefusesAnExamTheGradeDoesNotFitEvenWhenItIsAlreadySaved()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(8);
        await s.Onboarding.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default);
        // The grade changes to 11th: the exam step re-opens; resending the saved LGS (an old bundle, a direct call) is refused.
        (await s.Personal(11)).NextStep.Should().Be(2);
        var error = await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            s.Onboarding.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default));
        error.Message.Should().Be(LearningPurposes.PurposeNotForEducationNotice);
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            s.Onboarding.SaveExamAsync(new(LearningPurpose.Ags, null, null, null, s.Year, null, false), default));
        var view = await s.Onboarding.GetAsync(default);
        (view.NextStep, view.Answers.Purpose).Should().Be((2, (LearningPurpose?)LearningPurpose.Lgs), "nothing was written");
    }

    [Fact]
    public async Task OtherSettingsCanStillBeSavedWhileTheSavedExamDoesNotFit()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(8, LearningPurpose.Lgs, null);
        await s.UpdateProfile(x => x with { GradeLevel = 12 });
        // A topic or session change resends the saved exam: it is not refused for an unrelated change.
        await s.SaveSetup(LearningPurpose.Lgs, null);
        (await s.Settings.GetAsync(default)).PurposeNotForEducation.Should().BeTrue();
        // Choosing an exam that does not fit is still refused.
        await s.SaveSetup(LearningPurpose.Yks, "SAY");
        await Assert.ThrowsAsync<ArgumentException>(() => s.SaveSetup(LearningPurpose.Lgs, null));
    }

    [Fact]
    public async Task ARetiredPurposeKeepsTheDiplomaGradeLikeNoExam()
    {
        await using var s = await Setup.CreateAsync();
        var profile = await s.F.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == s.Student);
        profile.UpdateEducation(CoachingEducationStatus.Student, 12, null, null, null, null, null, null);
        profile.SetDiplomaGrade(85, false);
        var context = StudentLearningContext.Create(s.Student, s.Year - 1, LearningPurpose.School, null, null, null);
        context.SetEducation(CoachingEducationStatus.Student, 12, null);
        s.F.Db.Add(context); await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();
        (await s.UpdateProfile(x => x with { Bio = "Merhaba" })).DiplomaGrade.Should().Be(85m);
        (await s.Store.ExamAsksDiplomaAsync(s.Student, default)).Should().BeTrue();
    }

    [Fact]
    public async Task ProfilimKnowsWhetherTheExamAsksTheDiplomaGrade()
    {
        await using var s = await Setup.CreateAsync();
        (await s.Store.ExamAsksDiplomaAsync(s.Student, default)).Should().BeTrue("no exam yet");
        await s.Wizard(null, LearningPurpose.Yds, null, status: CoachingEducationStatus.UniversityStudent);
        (await s.Store.ExamAsksDiplomaAsync(s.Student, default)).Should().BeFalse();
        await s.UpdateProfile(x => x with { EducationStatus = CoachingEducationStatus.HighSchoolGraduate });
        await s.SaveSetup(LearningPurpose.Yks, "EA");
        (await s.Store.ExamAsksDiplomaAsync(s.Student, default)).Should().BeTrue();
    }

    [Fact]
    public async Task TheWizardIsGivenTheExamsThatFitTheGradeByTheServer()
    {
        await using var s = await Setup.CreateAsync();
        (await s.Personal(8)).AllowedPurposes.Should().Equal(LearningPurpose.Lgs);
        // G1 (user decisions 07.11.2026).
        (await s.Personal(null)).AllowedPurposes.Should().Equal(LearningPurpose.Yks);
        (await s.Personal(null, CoachingEducationStatus.UniversityStudent)).AllowedPurposes.Should().Equal(LearningPurpose.Ags, LearningPurpose.Yds, LearningPurpose.Yokdil);
        (await s.Personal(null, CoachingEducationStatus.UniversityGraduate)).AllowedPurposes.Should().Equal(LearningPurpose.Ags, LearningPurpose.Yds, LearningPurpose.Yokdil);
        LearningPurposes.AllowedFor(CoachingEducationStatus.Student, 10).Should().Equal(LearningPurpose.Yks);
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

    // ---------- E3 #14: "Şu an durumun ne?" ----------

    [Fact]
    public async Task AUniversityGraduateChoosesAgsInTheWizard()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(null, LearningPurpose.Ags, null, status: CoachingEducationStatus.UniversityGraduate);
        var context = await s.F.Db.StudentLearningContexts.AsNoTracking().SingleAsync();
        (context.Purpose, context.EducationStatus, context.GradeLevel).Should().Be((LearningPurpose.Ags, CoachingEducationStatus.UniversityGraduate, (int?)null));
        (await s.Onboarding.GetAsync(default)).Answers.EducationStatus.Should().Be(CoachingEducationStatus.UniversityGraduate);
    }

    [Theory]
    [InlineData(CoachingEducationStatus.HighSchoolGraduate, LearningPurpose.Ags)]
    [InlineData(CoachingEducationStatus.HighSchoolGraduate, LearningPurpose.Yds)]
    [InlineData(CoachingEducationStatus.HighSchoolGraduate, LearningPurpose.Yokdil)]
    [InlineData(CoachingEducationStatus.UniversityStudent, LearningPurpose.Yks)]
    [InlineData(CoachingEducationStatus.UniversityGraduate, LearningPurpose.Yks)]
    // G1 (07.11.2026): AGS, YDS and YÖKDİL for university students and graduates; YKS for lise students and graduates.
    public async Task TheStudySettingsRefuseAnExamOfAnotherEducation(CoachingEducationStatus status, LearningPurpose purpose)
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(null, status);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => s.SaveSetup(purpose, purpose switch
        { LearningPurpose.Yks => "SAY", LearningPurpose.Yokdil => "YOKDIL_FEN", _ => null }));
        error.Message.Should().Contain("uymuyor");
    }

    [Fact]
    // G2 review (M1): a year whose exam has already happened cannot be chosen (a saved one is resent as it is).
    public async Task AYearWhoseExamHasPassedIsRefused()
    {
        await using var s = await Setup.CreateAsync();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Coaching.Domain.CoachingTime.Turkey));
        var tyt = StudyCatalogExam.Create("TYT", "YKS");
        tyt.ReplaceSessions([new(null, new DateOnly(today.Year, 1, 1), $"{today.Year}")]);
        s.F.Db.Add(tyt); await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();
        await s.Personal(12);
        var academic = today.Month >= 9 ? today.Year : today.Year - 1;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => s.Settings.SaveAsync(new(null, academic, LearningPurpose.Yks, today.Year, "SAY", null, [], [], false), default));
        error.Message.Should().Be(CoachingLearningSetupService.PastExamYearNotice);
        var saved = await s.Settings.SaveAsync(new(null, academic, LearningPurpose.Yks, today.Year + 1, "SAY", null, [], [], false), default);
        saved.ExamOpenYears.Should().ContainEquivalentOf(new LearningExamOpenYear("TYT", today.Year + 1));
        (await s.Onboarding.GetAsync(default)).ExamOpenYears.Should().ContainEquivalentOf(new LearningExamOpenYear("TYT", today.Year + 1));
    }

    [Fact]
    public async Task AUniversityStudentChoosesAgsInTheWizard()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(null, LearningPurpose.Ags, null, status: CoachingEducationStatus.UniversityStudent);
        (await s.F.Db.StudentLearningContexts.AsNoTracking().SingleAsync()).Purpose.Should().Be(LearningPurpose.Ags);
    }

    [Fact]
    public async Task ALiseGraduateKeepsTheYksDiplomaGrade()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(null, LearningPurpose.Yks, "SAY", diploma: 82.5m, status: CoachingEducationStatus.HighSchoolGraduate);
        (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).DiplomaGrade.Should().Be(82.5m);
        (await s.UpdateProfile(x => x with { Bio = "Merhaba" })).DiplomaGrade.Should().Be(82.5m);
        // G1: the grade goes with a move to university (YKS no longer fits; the study settings ask for a new exam).
        (await s.UpdateProfile(x => x with { EducationStatus = CoachingEducationStatus.UniversityStudent })).DiplomaGrade.Should().BeNull();
    }

    [Theory]
    [InlineData(CoachingEducationStatus.UniversityStudent)]
    [InlineData(CoachingEducationStatus.UniversityGraduate)]
    // G1 (07.11.2026): nothing is migrated. A university student's or graduate's saved YKS stays, with its diploma grade and session,
    // and the student is told to choose again (the wizard is not reopened).
    public async Task AnOlderYksSetupOfAUniversityStudentStaysAndIsToldToChooseAgain(CoachingEducationStatus status)
    {
        await using var s = await Setup.CreateAsync();
        await s.OlderYksSetup(status, diploma: 82.5m);
        var setup = await s.Settings.GetAsync(default);
        (setup.Context!.Purpose, setup.PurposeNotForEducation, setup.IsReady).Should().Be((LearningPurpose.Yks, true, false));
        setup.MissingFields.Should().Contain("Purpose");
        setup.AllowedPurposes.Should().Equal(LearningPurpose.Ags, LearningPurpose.Yds, LearningPurpose.Yokdil);
        (await s.Onboarding.GetAsync(default)).Status.Should().Be("Completed");
        (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).DiplomaGrade.Should().Be(82.5m);
        var preview = new CoachingAutomaticStudyPlanPreviewService(s.F.Db, new CoachingAccessPolicy(s.F.Actor), s.Settings);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => preview.PreviewAsync(
            new AutomaticStudyPreviewRequest(new(2026, 10, 19), 7, 0, [new(Guid.NewGuid(), 30)], setup.Context.Version)));
        error.Message.Should().Be(LearningPurposes.SavedPurposeNotForEducationNotice);
        // Re-sending the saved YKS (a topic or session change) is not refused; choosing it anew is.
        await s.SaveSetup(LearningPurpose.Yks, "SAY");
        await s.SaveSetup(LearningPurpose.Ags, null);
        await Assert.ThrowsAsync<ArgumentException>(() => s.SaveSetup(LearningPurpose.Yks, "SAY"));
    }

    [Fact]
    public async Task SwitchingProfilimToANonStudentStatusClearsTheGradeSchoolAndBranch()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(11, LearningPurpose.Yks, "SAY");
        await s.UpdateProfile(x => x with { SchoolId = s.School, ClassBranch = "11-A" });

        var updated = await s.UpdateProfile(x => x with { EducationStatus = CoachingEducationStatus.UniversityStudent, GradeLevel = null });

        (updated.EducationStatus, updated.GradeLevel, updated.SchoolName, updated.SchoolType, updated.ClassBranch)
            .Should().Be((CoachingEducationStatus.UniversityStudent, (int?)null, (string?)null, (string?)null, (string?)null));
        var context = await s.F.Db.StudentLearningContexts.AsNoTracking().SingleAsync();
        (context.EducationStatus, context.GradeLevel).Should().Be((CoachingEducationStatus.UniversityStudent, (int?)null));
        (await s.Settings.GetAsync(default)).PurposeNotForEducation.Should().BeTrue("G1: YKS no longer fits a university student");
    }

    [Fact]
    public async Task AMiddleSchoolStudentWhoBecomesAUniversityGraduateIsAskedForANewExam()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(8, LearningPurpose.Lgs, null);
        await s.UpdateProfile(x => x with { EducationStatus = CoachingEducationStatus.UniversityGraduate, GradeLevel = null });
        var setup = await s.Settings.GetAsync(default);
        setup.PurposeNotForEducation.Should().BeTrue();
        setup.AllowedPurposes.Should().Equal(LearningPurpose.Ags, LearningPurpose.Yds, LearningPurpose.Yokdil);
    }

    [Fact]
    public async Task TheInstitutionCanSetEveryEducationStatus()
    {
        await using var s = await Setup.CreateAsync();
        await s.Wizard(12, LearningPurpose.Yks, "SAY", diploma: 90);
        await s.UpdateProfile(x => x with { SchoolId = s.School });
        var admin = s.F.AddAccount("InstitutionAdmin"); var institution = Guid.NewGuid();
        s.F.Accounts.Institutions.Add(institution);
        s.F.Accounts.Admins[admin] = new(false, institution);
        s.F.Db.Add(CoachingInstitutionMembership.Create(institution, s.Student, CoachingMemberRole.Student, admin)); await s.F.Db.SaveChangesAsync();

        await s.F.Store.UpdateInstitutionStudentAsync(admin, institution, s.Student, CoachingEducationStatus.HighSchoolGraduate, null, null, default);
        s.F.Db.ChangeTracker.Clear();
        var profile = await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync();
        (profile.EducationStatus, profile.GradeLevel, profile.SchoolName, profile.DiplomaGrade)
            .Should().Be((CoachingEducationStatus.HighSchoolGraduate, (int?)null, (string?)null, (decimal?)90m));
        (await s.F.Db.StudentLearningContexts.AsNoTracking().SingleAsync()).EducationStatus.Should().Be(CoachingEducationStatus.HighSchoolGraduate);
        // A student status still needs its grade; a non-student status takes none.
        await Assert.ThrowsAsync<ArgumentException>(() => s.F.Store.UpdateInstitutionStudentAsync(admin, institution, s.Student, CoachingEducationStatus.Student, null, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.F.Store.UpdateInstitutionStudentAsync(admin, institution, s.Student, CoachingEducationStatus.UniversityStudent, 10, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.F.Store.UpdateInstitutionStudentAsync(admin, institution, s.Student, CoachingEducationStatus.Student, 4, null, default));
        await s.F.Store.UpdateInstitutionStudentAsync(admin, institution, s.Student, CoachingEducationStatus.Student, 9, null, default);
        s.F.Db.ChangeTracker.Clear();
        (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).GradeLevel.Should().Be(9);
    }

    [Fact]
    public async Task StaffListsSeeTheEducationStatus()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(null, CoachingEducationStatus.UniversityStudent);
        var admin = s.F.AddAccount("InstitutionAdmin"); var institution = Guid.NewGuid();
        s.F.Accounts.Institutions.Add(institution);
        s.F.Accounts.Admins[admin] = new(false, institution);
        s.F.Db.Add(CoachingInstitutionMembership.Create(institution, s.Student, CoachingMemberRole.Student, admin)); await s.F.Db.SaveChangesAsync();
        s.F.Actor.UserId = admin; s.F.Actor.Roles = ["InstitutionAdmin"];
        var directory = new OwnedCoachingDirectory(s.F.Db, s.F.Accounts, s.F.Actor);
        var page = await directory.GetMemberStudentPageAsync(admin, institution, null, 1, 25, default);
        var item = page.Students!.Should().ContainSingle().Subject;
        (item.EducationStatus, item.GradeLevel).Should().Be((CoachingEducationStatus.UniversityStudent, (int?)null));
    }

    // ---------- E3 review (M1, L4) ----------

    private static void SetStored(CoachingStudentProfile profile, string property, object? value) =>
        typeof(CoachingStudentProfile).GetProperty(property)!.SetValue(profile, value);

    [Fact]
    public async Task ATeacherEditsTheDetailsOfAStudentWithAnOlderGradeBelowFive()
    {
        await using var s = await Setup.CreateAsync();
        var tracked = await s.F.Db.CoachingStudentProfiles.SingleAsync();
        SetStored(tracked, nameof(CoachingStudentProfile.EducationStatus), CoachingEducationStatus.Student);
        SetStored(tracked, nameof(CoachingStudentProfile.GradeLevel), 3);
        await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();
        var teacher = s.F.AddAccount("Teacher");
        var version = (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).Version;

        var saved = await s.F.Store.UpdateTeacherStudentDetailsAsync(s.Student, teacher,
            new(version, "Atatürk İlkokulu", "ANKARA", "ÇANKAYA", null, "3-A", new DateOnly(2018, 3, 1), "Okumayı seviyor."), default);

        (saved.EducationStatus, saved.GradeLevel, saved.SchoolName, saved.ClassBranch, saved.Bio)
            .Should().Be((CoachingEducationStatus.Student, (int?)3, "Atatürk İlkokulu", "3-A", "Okumayı seviyor."));
    }

    [Fact]
    public async Task ATeachersDetailsSaveLeavesTheSchoolOfANonStudentAlone()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(null);
        var tracked = await s.F.Db.CoachingStudentProfiles.SingleAsync();
        SetStored(tracked, nameof(CoachingStudentProfile.SchoolName), "Eski Lise");   // stored before E3
        await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();
        var teacher = s.F.AddAccount("Teacher");
        var version = (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).Version;

        var saved = await s.F.Store.UpdateTeacherStudentDetailsAsync(s.Student, teacher,
            new(version, "Eski Lise", null, null, null, null, new DateOnly(2006, 1, 1), "Mezun, YKS'ye tekrar giriyor."), default);

        (saved.SchoolName, saved.Bio).Should().Be(("Eski Lise", "Mezun, YKS'ye tekrar giriyor."), "the teacher did not change the school");
    }

    [Fact]
    public async Task MovingFromLiseToOrtaokulDropsTheHighSchool()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(11, school: s.School);
        (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).SchoolName.Should().Be("Ankara Fen Lisesi");
        await s.Personal(8);
        (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).SchoolName.Should().BeNull("the catalog school is a lise");
        // On Profilim too, the stored lise is not kept for an ortaokul grade; a newly typed school is.
        await s.Personal(11, school: s.School);
        (await s.UpdateProfile(x => x with { GradeLevel = 7 })).SchoolName.Should().BeNull();
        (await s.UpdateProfile(x => x with { SchoolName = "Kızılay Ortaokulu" })).SchoolName.Should().Be("Kızılay Ortaokulu");
    }

    [Fact]
    public async Task TheInstitutionKeepsTheGraduationYearBetweenTheGraduateStatuses()
    {
        await using var s = await Setup.CreateAsync();
        await s.Onboarding.SavePersonalAsync(new(new DateOnly(2000, 5, 1), "06", "0601", CoachingEducationStatus.HighSchoolGraduate, null, 2018, null), default);
        var admin = s.F.AddAccount("InstitutionAdmin"); var institution = Guid.NewGuid();
        s.F.Accounts.Institutions.Add(institution);
        s.F.Accounts.Admins[admin] = new(false, institution);
        s.F.Db.Add(CoachingInstitutionMembership.Create(institution, s.Student, CoachingMemberRole.Student, admin)); await s.F.Db.SaveChangesAsync();

        await s.F.Store.UpdateInstitutionStudentAsync(admin, institution, s.Student, CoachingEducationStatus.UniversityGraduate, null, null, default);
        s.F.Db.ChangeTracker.Clear();
        (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).GraduationYear.Should().Be(2018);
        await s.F.Store.UpdateInstitutionStudentAsync(admin, institution, s.Student, CoachingEducationStatus.UniversityStudent, null, null, default);
        s.F.Db.ChangeTracker.Clear();
        (await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).GraduationYear.Should().BeNull();
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

        public CoachingLearningSetupService Settings => new(F.Db, Access, F.Accounts, null, Events);
        public CoachingStudentOnboardingService Onboarding => new(F.Db, Access, F.Accounts, Settings, Places, null, null, Events);
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

        /// <summary>E3 #14: no grade is a lise graduate unless another status is given.</summary>
        public Task<OnboardingView> Personal(int? grade, CoachingEducationStatus? status = null, Guid? school = null) => Onboarding.SavePersonalAsync(new(new DateOnly(2008, 5, 1), "06", "0601",
            status ?? (grade is null ? CoachingEducationStatus.HighSchoolGraduate : CoachingEducationStatus.Student), grade, null, school), default);

        /// <summary>The wizard's first two steps, then the wizard is finished.</summary>
        public async Task Wizard(int? grade, LearningPurpose purpose, string? field, decimal? diploma = null, CoachingEducationStatus? status = null)
        {
            await Personal(grade, status);
            await Onboarding.SaveExamAsync(new(purpose, field, null, purpose == LearningPurpose.Lgs ? LgsSession : null,
                purpose == LearningPurpose.Lgs ? null : Year, diploma, false), default);
            await Onboarding.SaveTargetAsync(new("none", null, null), default);
            foreach (var step in new[] { 4, 5, 6 }) await Onboarding.PassStepAsync(step, default);
            await Onboarding.CompleteAsync(default);
            F.Db.ChangeTracker.Clear();
        }

        /// <summary>
        /// G1 (07.11.2026): a YKS setup saved before the rule by a student now at university: the wizard as a lise graduate, then the
        /// stored education is changed directly (as the rows of such students are; nothing was migrated).
        /// </summary>
        public async Task OlderYksSetup(CoachingEducationStatus status, decimal? diploma = null)
        {
            await Wizard(null, LearningPurpose.Yks, "SAY", diploma);
            var profile = await F.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == Student);
            typeof(CoachingStudentProfile).GetProperty(nameof(CoachingStudentProfile.EducationStatus))!.SetValue(profile, status);
            var context = await F.Db.StudentLearningContexts.SingleAsync(x => x.StudentUserId == Student);
            context.SetEducation(status, null, context.CurriculumVersionId);
            await F.Db.SaveChangesAsync(); F.Db.ChangeTracker.Clear();
        }

        /// <summary>Çalışma tercihlerim: the exam and the field change, the rest of the setup is sent as it is.</summary>
        public async Task SaveSetup(LearningPurpose purpose, string? field)
        {
            var context = await F.Db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x => x.StudentUserId == Student);
            var year = context?.AcademicYearStart ?? (DateTime.UtcNow.Month >= 9 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1);
            var language = purpose is LearningPurpose.Yds or LearningPurpose.Yokdil || field == "DIL" ? "English" : null;
            await Settings.SaveAsync(new(context?.Version, year, purpose, Year, field, language, [], [], false), default);
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
