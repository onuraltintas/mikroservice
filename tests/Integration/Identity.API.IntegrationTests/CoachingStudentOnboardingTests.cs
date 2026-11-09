using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Events.Coaching;
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
        var target = await s.Service.SaveTargetAsync(new("school", s.School, null), default);
        target.Answers.Target.Should().Match<OnboardingTarget>(x => x.Kind == "school" && x.Name == "Ankara Fen Lisesi" && x.MinimumScore == 480m);
        var goal = await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync();
        (goal.TargetSchoolId, goal.Category).Should().Be(((Guid?)s.School, GoalCategory.ExamPreparation));
        (await s.F.Db.Set<StudentPrimaryGoal>().AsNoTracking().SingleAsync()).GoalId.Should().Be(goal.Id);

        foreach (var step in new[] { 4, 5, 6 }) await s.Service.PassStepAsync(step, default);
        (await s.Service.GetAsync(default)).NextStep.Should().Be(7);
        var done = await s.Service.CompleteAsync(default);
        (done.State.Status, done.State.CompletedAt.HasValue, done.NeedsSubscription).Should().Be(("Completed", true, false));

        // After completion the wizard is closed; changes go through the regular pages.
        var closed = await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.PassStepAsync(4, default));
        closed.Code.Should().Be("Onboarding.Completed");
        // #4 "Hedefini belirle": the target question stays open after the wizard and keeps it completed.
        var later = await s.Service.SaveTargetAsync(new("none", null, null), default);
        (later.Status, later.Answers.Target).Should().Be(("Completed", (OnboardingTarget?)null));
    }

    [Fact]
    public async Task AResumedWizardIsToldWhichInvitationsTheStudentAccepted()
    {
        // E4 #18 (live test 06.11.2026): after a reload the invite step shows the accepted invitation, not "Tamam".
        await using var s = await Setup.CreateAsync();
        var teacher = s.F.AddAccount("Teacher"); var now = DateTime.UtcNow;
        var email = s.F.Accounts.Users[s.Student].Email;
        var accepted = CoachingInvitation.Create(teacher, email, "Student", null, teacher, false, null, now.AddMinutes(-5));
        accepted.Finish(CoachingInvitationStatus.Accepted, s.Student, now);
        var rejected = CoachingInvitation.Create(teacher, email, "Student", null, teacher, false, null, now.AddMinutes(-4));
        rejected.Finish(CoachingInvitationStatus.Rejected, s.Student, now);
        var pending = CoachingInvitation.Create(teacher, email, "Student", null, teacher, false, null, now);
        var someoneElse = CoachingInvitation.Create(teacher, "other@example.test", "Student", null, teacher, false, null, now.AddMinutes(-3));
        someoneElse.Finish(CoachingInvitationStatus.Accepted, Guid.NewGuid(), now);
        s.F.Db.AddRange(accepted, rejected, pending, someoneElse); await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();

        var view = await s.Service.GetAsync(default);
        view.AcceptedInvitations.Should().ContainSingle().Which.Should().Be(new OnboardingInvitation($"{teacher:N}@example.test", "StudentToTeacher", now));
    }

    [Fact]
    public async Task CompletingNeedsEveryStepAndReportsWhetherASubscriptionIsNeeded()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 8);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.CompleteAsync(default));
        await s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default);
        await s.Service.SaveTargetAsync(new("none", null, null), default);
        foreach (var step in new[] { 4, 5, 6 }) await s.Service.PassStepAsync(step, default);
        (await s.ServiceWith(NoAccess()).CompleteAsync(default)).NeedsSubscription.Should().BeTrue();
    }

    [Fact]
    public async Task ChangingTheExamReopensOnlyTheTargetAndClearsIt()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 8);
        await s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default);
        await s.Service.SaveTargetAsync(new("school", s.School, null), default);
        foreach (var step in new[] { 4, 5, 6 }) await s.Service.PassStepAsync(step, default);

        // Paket D: YKS fits from 9th grade on, so the grade changes first (that re-opens the exam step).
        (await s.Personal(grade: 9)).NextStep.Should().Be(2);
        var changed = await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, null, false), default);
        changed.NextStep.Should().Be(3);
        changed.SavedSteps.Should().Equal(1, 2, 4, 5, 6);
        changed.Answers.Target.Should().BeNull();
        (await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync()).TargetSchoolId.Should().BeNull();

        // The same exam again keeps the target step.
        await s.Service.SaveTargetAsync(new("score", null, [new("SAY", 420)]), default);
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
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "06", "99", CoachingEducationStatus.Student, 8, null, null), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "34", "0601", CoachingEducationStatus.Student, 8, null, null), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SavePersonalAsync(new(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), "06", "0601", CoachingEducationStatus.Student, 8, null, null), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SavePersonalAsync(new(null, "06", "0601", CoachingEducationStatus.Student, 8, null, null), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "06", "0601", CoachingEducationStatus.Student, 8, null, Guid.NewGuid()), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, null, s.LgsYear, null, false), default));
        (await s.Service.GetAsync(default)).SavedSteps.Should().BeEmpty();
        var student = await s.Service.SavePersonalAsync(new(new DateOnly(2009, 1, 1), "06", "0601", CoachingEducationStatus.Student, 11, null, s.School), default);
        student.Answers.SchoolName.Should().Be("Ankara Fen Lisesi");
        // The picked school also sets the school's city, district and type; a resuming student keeps the school.
        var profile = await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == s.Student);
        // G4 (#43): the catalog's capitals are stored as names.
        (profile.SchoolCity, profile.SchoolDistrict, profile.SchoolType).Should().Be(("Ankara", "Çankaya", "Fen Lisesi"));
        var resumed = await s.Service.SavePersonalAsync(new(new DateOnly(2009, 1, 1), "06", "0602", CoachingEducationStatus.Student, 11, null, null), default);
        (resumed.Answers.SchoolName, resumed.Answers.HomeDistrict).Should().Be(("Ankara Fen Lisesi", "Keçiören"));
        // E3 #14: a graduate has no school (it is cleared) and keeps the graduation year when resuming.
        var graduate = await s.Service.SavePersonalAsync(new(new DateOnly(2000, 1, 1), "06", "0601", CoachingEducationStatus.HighSchoolGraduate, null, 2018, s.School), default);
        (graduate.Answers.EducationStatus, graduate.Answers.GraduationYear, graduate.Answers.SchoolName).Should().Be((CoachingEducationStatus.HighSchoolGraduate, (int?)2018, (string?)null));
        var again = await s.Service.SavePersonalAsync(new(new DateOnly(2000, 1, 1), "06", "0602", CoachingEducationStatus.HighSchoolGraduate, null, null, null), default);
        (again.Answers.GraduationYear, again.Answers.SchoolName, again.Answers.HomeDistrict).Should().Be(((int?)2018, (string?)null, "Keçiören"));
    }

    // F2 (E4 leftover a, live test 06.11.2026): a refused answer names the wizard question to ask again (the API sends it as "field").
    [Fact]
    public async Task ARefusedAnswerNamesTheQuestionToAskAgain()
    {
        await using var s = await Setup.CreateAsync();
        async Task<string> Refused(Func<Task> save) => (await Assert.ThrowsAsync<OnboardingFieldException>(save)).Field;
        (await Refused(() => s.Service.SavePersonalAsync(new(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), "06", "0601", CoachingEducationStatus.Student, 8, null, null), default))).Should().Be("birthDate");
        (await Refused(() => s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "99", "0601", CoachingEducationStatus.Student, 8, null, null), default))).Should().Be("city");
        (await Refused(() => s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "06", "99", CoachingEducationStatus.Student, 8, null, null), default))).Should().Be("district");
        (await Refused(() => s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "06", "0601", CoachingEducationStatus.Student, 13, null, null), default))).Should().Be("education");
        (await Refused(() => s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "06", "0601", CoachingEducationStatus.Student, 11, null, Guid.NewGuid()), default))).Should().Be("ownSchool");
        await s.Personal(grade: 12);
        (await Refused(() => s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default))).Should().Be("purpose");
        (await Refused(() => s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, 120m, false), default))).Should().Be("diploma");
        await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, null, false), default);
        (await Refused(() => s.Service.SaveTargetAsync(new("score", null, [new("TYT", 600)]), default))).Should().Be("tytScore");
        (await Refused(() => s.Service.SaveTargetAsync(new("score", null, [new("SAY", 561)]), default))).Should().Be("fieldScore");
        (await Refused(() => s.Service.SaveTargetAsync(new("school", s.School, null), default))).Should().Be("targetMode");
        (await Refused(() => s.Service.SaveTargetAsync(new("program", Guid.NewGuid(), null), default))).Should().Be("program");
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
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("score", null, [new("EA", 400)]), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("score", null, [new("SAY", 561)]), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("school", s.School, null), default));
        var view = await s.Service.SaveTargetAsync(new("score", null, [new("TYT", 380.5m)]), default);
        view.Answers.Target.Should().Match<OnboardingTarget>(x => x.Kind == "score" && x.ScoreType == "TYT" && x.Scores.Single().Score == 380.5m && x.Name == "TYT");
        var goal = await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync();
        (goal.TargetScore, goal.TargetMaxScore, goal.TargetExamType, goal.ScoreTargets.Single().ScoreType, goal.Title).Should().Be(((decimal?)380.5m, (decimal?)560m, (ExamType?)ExamType.YKS, "TYT", "TYT hedefi"));

        // "Henüz karar vermedim" clears it again.
        (await s.Service.SaveTargetAsync(new("none", null, null), default)).Answers.Target.Should().BeNull();
        (await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync()).TargetScore.Should().BeNull();
    }

    [Fact]
    public async Task AProgrammeMustFitTheFieldAndOneWithoutATabanNeedsTheStudentsOwnScore()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 12);
        await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, null, false), default);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("program", s.Programs["Hukuk"], null), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("program", s.Programs["Fizik"], null), default));
        var withOwn = await s.Service.SaveTargetAsync(new("program", s.Programs["Fizik"], [new("SAY", 410)]), default);
        withOwn.Answers.Target.Should().Match<OnboardingTarget>(x => x.Kind == "program" && x.MinimumScore == null && x.Scores.Single().Score == 410m);
        var view = await s.Service.SaveTargetAsync(new("program", s.Programs["Bilgisayar Mühendisliği"], null), default);
        view.Answers.Target.Should().Match<OnboardingTarget>(x => x.Kind == "program" && x.Name == "Ankara Üniversitesi · Bilgisayar Mühendisliği"
            && x.MinimumScore == 470m && x.Scores.Count == 0);
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
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.SaveTargetAsync(new("school", s.School, null), default));
        error.Code.Should().Be("Onboarding.TeacherGoal");
        (await s.Service.GetAsync(default)).Answers.TargetLocked.Should().BeTrue();
        (await s.Service.SaveTargetAsync(new("none", null, null), default)).NextStep.Should().Be(4);
    }

    // ---------- Filtered option lists ----------

    [Fact]
    public async Task SchoolListsAreNarrowedByCityAndTypeAndPaged()
    {
        await using var s = await Setup.CreateAsync();
        (await s.Service.SchoolCitiesAsync(default)).Select(x => (x.Value, x.Label, x.Count)).Should().Equal(("ANKARA", "Ankara", 26), ("İZMİR", "İzmir", 1));
        (await s.Service.SchoolTypesAsync("Ankara", default)).Select(x => x.Value).Should().Equal("Anadolu Lisesi", "Fen Lisesi");
        var page = await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", null, 1, default);
        (page.TotalCount, page.Items.Count, page.PageSize).Should().Be((25, 20, 20));
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", null, 2, default)).Items.Should().HaveCount(5);
        // G4 (#43): every word must be in the name; the schools where each word starts a word come first.
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", "lise 7", 1, default)).Items.Select(x => x.Name).Should().Equal("Anadolu Lise 7", "Anadolu Lise 17");
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", "LİSE 25", 1, default)).Items.Select(x => x.Name).Should().Equal("Anadolu Lise 25");
        (await s.Service.SchoolCitiesAsync(default)).Should().NotContain(x => x.Value == "Kapanan Lise");
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SchoolsAsync(null, "Anadolu Lisesi", null, 1, default));
        (await s.Service.SchoolsAsync("Ankara", null, null, 1, default)).TotalCount.Should().Be(26, "without a type every school of the city");
    }

    [Fact]
    public async Task ProgrammeListsAreNarrowedByLevelScoreTypeCityAndUniversity()
    {
        await using var s = await Setup.CreateAsync();
        (await s.Service.ProgramCitiesAsync("Bachelor", "SAY", default)).Select(x => x.Label).Should().Equal("Ankara", "İstanbul");
        (await s.Service.ProgramCitiesAsync("Associate", null, default)).Select(x => x.Value).Should().Equal("ANKARA");
        // The catalog value and the place name both match.
        (await s.Service.UniversitiesAsync("Bachelor", "SAY", "İSTANBUL", null, default)).Select(x => x.Value).Should().Equal("İstanbul Üniversitesi");
        (await s.Service.UniversitiesAsync("Bachelor", "SAY", "istanbul", null, default)).Select(x => x.Value).Should().Equal("İstanbul Üniversitesi");
        (await s.Service.UniversitiesAsync("Bachelor", "SAY", "Ankara", null, default)).Select(x => x.Value).Should().Equal("Ankara Üniversitesi");
        var programs = await s.Service.ProgramsAsync("Bachelor", "SAY", "Ankara", "Ankara Üniversitesi", null, 1, default);
        programs.Items.Select(x => x.Name).Should().Equal("Bilgisayar Mühendisliği", "Fizik");
        programs.Items[0].Should().Match<OnboardingProgramOption>(x => x.MinimumScore == 470m && x.Rank == 12000 && x.ScoreType == "SAY");
        (await s.Service.ProgramsAsync("Bachelor", "EA", "Ankara", "Ankara Üniversitesi", null, 1, default)).Items.Select(x => x.Name).Should().Equal("Hukuk");
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.ProgramCitiesAsync("Doctorate", null, default));
    }

    [Fact]
    public async Task H6_AProgrammeNameFindsItsScoreTypesAcrossTheCountry()
    {
        // Paket H6 (09.11.2026): "Emin değil misin? Bölümüne göre bulalım": a student who knows the programme but not its score type
        // searches by name only (no level, city or university); one entry per name and score type, undergraduate programmes only.
        await using var s = await Setup.CreateAsync();
        s.Program("1101", "İstanbul Üniversitesi", "Bilgisayar Mühendisliği (İngilizce)", "Bachelor", "SAY", "İSTANBUL", 500, 5000);
        s.Program("1102", "Ankara Üniversitesi", "Bilgisayar ve Öğretim Teknolojileri Öğretmenliği", "Bachelor", "SAY", "ANKARA", 300, 300000);
        s.Program("1103", "İstanbul Üniversitesi", "İşletme", "Bachelor", "EA", "İSTANBUL", 400, 80000);
        s.Program("1104", "Ankara Üniversitesi", "İşletme", "Bachelor", "SAY", "ANKARA", 380, 150000);
        s.Program("1105", "Ankara Üniversitesi", "İngilizce Öğretmenliği", "Bachelor", "YDT", "ANKARA", 420, 9000);
        s.Program("1106", "Girne Üniversitesi", "Bilgisayar Mühendisliği (KKTC Uyruklu)", "Bachelor", "EA", "KIBRIS", 200, 900000);
        await s.F.Db.SaveChangesAsync();

        // The parenthesised variants are one programme; "Bilgisayar Programcılığı" (önlisans, TYT) is not offered.
        (await s.Service.ProgramFieldsAsync("bilgisayar", default)).Should().Equal(
            new OnboardingProgramField("Bilgisayar Mühendisliği", "SAY"), new OnboardingProgramField("Bilgisayar ve Öğretim Teknolojileri Öğretmenliği", "SAY"));
        // Turkish letters and case do not matter; every word must be in the name.
        (await s.Service.ProgramFieldsAsync("BILGISAYAR muh", default)).Should().Equal(new OnboardingProgramField("Bilgisayar Mühendisliği", "SAY"));
        // One name with two score types is listed once per score type.
        (await s.Service.ProgramFieldsAsync("isletme", default)).Should().Equal(new OnboardingProgramField("İşletme", "EA"), new OnboardingProgramField("İşletme", "SAY"));
        (await s.Service.ProgramFieldsAsync("ingilizce", default)).Should().Equal(new OnboardingProgramField("İngilizce Öğretmenliği", "YDT"));
        (await s.Service.ProgramFieldsAsync("hukuk", default)).Should().Equal(new OnboardingProgramField("Hukuk", "EA"));
        // Too short to search: nothing (not the whole catalog).
        (await s.Service.ProgramFieldsAsync(" b ", default)).Should().BeEmpty();
        (await s.Service.ProgramFieldsAsync(null, default)).Should().BeEmpty();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.ProgramFieldsAsync(new string('a', 101), default));

        // A student who has not started the wizard may search; another role may not.
        s.F.Actor.UserId = s.F.AddAccount("Teacher"); s.F.Actor.Roles = ["Teacher"];
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.ProgramFieldsAsync("hukuk", default));
    }

    [Fact]
    public void H6_TheProgrammeFieldSearchIsOpenToAStudentWithoutASubscriptionAndRateLimited()
    {
        // The wizard comes before the subscription: the route sits under api/coaching/students like the other option lists.
        Coaching.Application.Subscriptions.CoachingSubscriptionAccessRules
            .RequiresSubscription("/api/coaching/students/me/onboarding/options/programs/fields").Should().BeFalse();
        var method = typeof(Coaching.API.Controllers.CoachingStudentOnboardingController).GetMethod("ProgramFields")!;
        method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), false).Cast<Microsoft.AspNetCore.Mvc.HttpGetAttribute>()
            .Single().Template.Should().Be("options/programs/fields");
        method.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), false)
            .Cast<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>().Single().PolicyName.Should().Be("target-catalog-search");
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
        await s.Service.SaveTargetAsync(new("score", null, [new("LGS", 450)]), default);
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
        // G1 (07.11.2026): YDS is for university students and graduates.
        await s.Personal(grade: null, CoachingEducationStatus.UniversityGraduate);
        await s.Service.SaveExamAsync(new(LearningPurpose.Yds, null, null, null, s.LgsYear, null, false), default);
        await s.Service.SaveTargetAsync(new("score", null, [new("YDS_EN", 85)]), default);
        s.LanguageExam(60);
        var estimate = await new CoachingScoreEstimateService(s.F.Db, s.F.Scope, TimeProvider.System).EstimateAsync(s.Student, s.Student, null, default);
        estimate.Target.Should().Match<ScoreTargetGap>(x => x.Kind == "score" && x.ScoreType == "YDS_EN" && x.ScoreGap == 10m && x.NetGap == 8m && x.Status == "below");
        estimate.Goal!.ScoreType.Should().Be("YDS_EN");
        // G2 (#40): no YDS session in the catalog: the chosen year counts down to an estimated date (15 June when none is known).
        estimate.TargetExam.Should().Match<ScoreTargetExam>(x => x.Source == "Estimated" && x.Date == new DateOnly(s.LgsYear, 6, 15));
    }

    [Fact]
    // G2 (#40, user decision 07.11.2026): a year the catalog has no session for is a target of its own next to the sessions (a 7th
    // grader aims for LGS a year after the catalog's session). The year must stay within the school year's range.
    public async Task AYearWithoutACatalogSessionIsSavedAsTheTarget()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 7);
        var exam = await s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, null, s.LgsYear + 1, null, false), default);
        (exam.NextStep, exam.Answers.TargetExamSessionId, exam.Answers.TargetExamYear).Should().Be((3, (Guid?)null, (int?)(s.LgsYear + 1)));
        var context = await s.F.Db.StudentLearningContexts.AsNoTracking().SingleAsync();
        (context.TargetExamSessionId, context.TargetExamYear).Should().Be(((Guid?)null, (int?)(s.LgsYear + 1)));
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, null, context.AcademicYearStart + 6, null, false), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, null, null, null, false), default));
        // Choosing the catalog session again replaces the year-only target.
        (await s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default)).Answers.TargetExamSessionId.Should().Be(s.LgsSession);
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

    // ---------- Review fixes (Paket C) ----------

    [Fact]
    public async Task HomeCitiesFromTheLocationDirectoryMatchTheCatalogsCapitals()
    {
        await using var s = await Setup.CreateAsync();
        foreach (var (city, name) in new[] { ("ADIYAMAN", "Adıyaman Fen Lisesi"), ("AFYONKARAHİSAR", "Afyon Fen Lisesi"), ("İSTANBUL", "İstanbul Fen Lisesi") })
        {
            var school = TargetSchool.Create("meb", name, name, city, "MERKEZ", 450, 2025); school.SetProgramDetails("Fen Lisesi", null, null); school.SetActive(true);
            s.F.Db.Add(school);
        }
        await s.F.Db.SaveChangesAsync();
        // Step 1: the student's own school is listed by the home city's name from the Identity directory.
        foreach (var (identityName, expected) in new[] { ("Adiyaman", "Adıyaman Fen Lisesi"), ("Afyonkarahi\u0307sar", "Afyon Fen Lisesi"), ("İstanbul", "İstanbul Fen Lisesi"), ("Ankara", "Ankara Fen Lisesi") })
            (await s.Service.SchoolsAsync(identityName, null, "Fen", 1, default)).Items.Select(x => x.Name).Should().Contain(expected, identityName);
        CoachingStudentOnboardingService.Fold("AFYONKARAHİSAR").Should().Be(CoachingStudentOnboardingService.Fold("Afyonkarahi\u0307sar"));
        CoachingStudentOnboardingService.Fold("ADIYAMAN").Should().Be(CoachingStudentOnboardingService.Fold("Adiyaman"));
        // LGS target path: city (catalog value) → type → school.
        var cities = await s.Service.SchoolCitiesAsync(default);
        cities.Select(x => x.Label).Should().Contain(["Adıyaman", "Afyonkarahisar", "İstanbul"]);
        var afyon = cities.Single(x => x.Label == "Afyonkarahisar").Value;
        (await s.Service.SchoolTypesAsync(afyon, default)).Select(x => x.Value).Should().Equal("Fen Lisesi");
        (await s.Service.SchoolsAsync(afyon, "Fen Lisesi", null, 1, default)).Items.Select(x => x.Name).Should().Equal("Afyon Fen Lisesi");
    }

    [Fact]
    public async Task ASlowLocationDirectoryIsReportedAsUnavailable()
    {
        await using var s = await Setup.CreateAsync();
        s.Places.TimesOut = true;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => s.Personal(grade: 8));
        error.Code.Should().Be("Onboarding.LocationsUnavailable");
    }

    [Fact]
    public async Task AnInvalidDiplomaGradeChangesNothing()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 12);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, 120, false), default));
        (await s.F.Db.StudentLearningContexts.AsNoTracking().AnyAsync()).Should().BeFalse("every check runs before any write");
        (await s.Service.GetAsync(default)).NextStep.Should().Be(2);
    }

    [Fact]
    public async Task AYksStudentCanSetBothTheTytAndTheFieldTargetScore()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 12);
        await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, null, false), default);
        var view = await s.Service.SaveTargetAsync(new("score", null, [new("TYT", 380), new("SAY", 450)]), default);
        view.Answers.Target!.Scores.Select(x => (x.ScoreType, x.Score)).Should().Equal(("SAY", 450m), ("TYT", 380m));
        var goal = await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync();
        (goal.TargetScore, goal.TargetExamType).Should().Be(((decimal?)450m, (ExamType?)ExamType.YKS), "the field's score is the main one for older readers");
        var estimate = await new CoachingScoreEstimateService(s.F.Db, s.F.Scope, TimeProvider.System).EstimateAsync(s.Student, s.Student, null, default);
        estimate.ScoreTargets.Select(x => (x.ScoreType, x.TargetScore, x.Kind)).Should().Equal(("SAY", (decimal?)450m, "score"), ("TYT", (decimal?)380m, "score"));
        estimate.Target!.ScoreType.Should().Be("SAY");
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("score", null, [new("TYT", 380), new("TYT", 390)]), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("score", null, [new("TYT", 0)]), default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SaveTargetAsync(new("score", null, []), default));
    }

    [Fact]
    public async Task TargetChangesPublishTheGoalEventsAndDropNetTargetsThatNoLongerFit()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 12);
        await s.Service.SaveExamAsync(new(LearningPurpose.Yks, "SAY", null, null, s.LgsYear + 1, null, false), default);
        await s.Service.SaveTargetAsync(new("score", null, [new("SAY", 420)]), default);
        s.Published.OfType<GoalCreatedEvent>().Should().ContainSingle();
        var goal = await s.F.Db.AcademicGoals.SingleAsync();
        goal.SetNetTarget("TYT", 90); goal.SetNetTarget("AYT_EA", 60); await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();
        await s.Service.SaveTargetAsync(new("program", s.Programs["Bilgisayar Mühendisliği"], null), default);
        s.Published.OfType<GoalUpdatedEvent>().Should().ContainSingle();
        var updated = await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync();
        updated.NetTargets.Select(x => x.ExamCode).Should().Equal("TYT");
        updated.Title.Should().Be("Ankara Üniversitesi · Bilgisayar Mühendisliği");
    }

    // ---------- Older target scores (no score type stored) ----------

    [Theory]
    [InlineData(LearningPurpose.Lgs, null, ExamType.LGS, 450, null, true)]
    [InlineData(LearningPurpose.Lgs, null, ExamType.LGS, 450, 500, true)]
    [InlineData(LearningPurpose.Lgs, null, ExamType.LGS, 90, 100, false)]   // a score on another scale
    [InlineData(LearningPurpose.Lgs, null, ExamType.Mock, 450, null, false)] // a practice-exam goal
    [InlineData(LearningPurpose.Yks, "SAY", ExamType.YKS, 400, 560, false)] // "TYT 400" or SAY 400? not certain
    [InlineData(LearningPurpose.Yks, "TYT", ExamType.YKS, 400, null, true)]
    [InlineData(LearningPurpose.Ags, "OABT_DIN", ExamType.AGS, 70, 100, false)] // P2-16 or P2-17
    [InlineData(LearningPurpose.Ags, "OABT_IMAT", ExamType.AGS, 70, 100, true)]
    [InlineData(LearningPurpose.Yds, null, ExamType.YDS, 85, 100, true)]
    [InlineData(LearningPurpose.Yds, null, ExamType.YDS, 600, 999, false)]
    public async Task AnOlderTargetScoreCountsOnlyWhenItsScoreTypeIsCertain(LearningPurpose purpose, string? field, ExamType examType, int score, int? max, bool counts)
    {
        await using var s = await Setup.CreateAsync();
        s.F.Db.Add(StudentLearningContext.Create(s.Student, 2026, purpose, 2027, field, purpose is LearningPurpose.Yds ? "English" : null));
        var goal = AcademicGoal.Create(s.Student, "Eski hedef", GoalCategory.ExamPreparation);
        if (max is null) goal.SetTarget(targetScore: (decimal)score, targetExamType: examType);
        else goal.SetScoreTarget((decimal)score, (decimal)max, examType);
        s.F.Db.Add(goal); await s.F.Db.SaveChangesAsync(); s.F.Db.Add(StudentPrimaryGoal.Create(s.Student, goal.Id)); await s.F.Db.SaveChangesAsync();
        // The parent sees the same target as the student (and the teacher, through the same service).
        var parent = s.F.AddAccount("Parent"); s.F.Accounts.ParentReads.Add((parent, s.Student));
        var service = new CoachingScoreEstimateService(s.F.Db, s.F.Scope, TimeProvider.System);
        foreach (var viewer in new[] { s.Student, parent })
        {
            var estimate = await service.EstimateAsync(viewer, s.Student, null, default);
            (estimate.Target is not null).Should().Be(counts, viewer == parent ? "parent" : "student");
            if (counts) (estimate.Target!.Kind, estimate.Target.TargetScore).Should().Be(("score", (decimal?)(decimal)score));
        }
    }

    [Fact]
    public async Task TheProfilePageCorrectsTheHomeCityAndTheDiplomaGrade()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 12);
        var store = new CoachingProfileStore(s.F.Db, s.F.Accounts, null, null, s.Places);
        var profile = await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == s.Student);
        Coaching.Application.Profiles.UpdateCoachingStudentProfile Request(int version) => new(version, CoachingEducationStatus.Student, 12, null, null, null, null, null, null,
            new DateOnly(2008, 1, 1), null, null, true);
        var updated = await store.UpdateStudentAsync(s.Student, Request(profile.Version) with { HomeProvinceId = "34", HomeDistrictId = "3401", DiplomaGrade = 88.5m }, default);
        (updated.HomeCity, updated.HomeDistrict, updated.DiplomaGrade, updated.DiplomaGradeEstimated).Should().Be(("İstanbul", "Kadıköy", (decimal?)88.5m, false));
        updated = await store.UpdateStudentAsync(s.Student, Request(updated.Version), default);
        (updated.HomeCity, updated.DiplomaGrade).Should().Be(("İstanbul", (decimal?)88.5m), "unset fields keep their values");
        updated = await store.UpdateStudentAsync(s.Student, Request(updated.Version) with { ClearDiplomaGrade = true }, default);
        updated.DiplomaGrade.Should().BeNull();
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateStudentAsync(s.Student, Request(updated.Version) with { HomeProvinceId = "34", HomeDistrictId = "0601" }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateStudentAsync(s.Student, Request(updated.Version) with { DiplomaGrade = 30 }, default));
    }

    // ---------- E2 (live test 06.11.2026) ----------

    [Fact]
    public async Task RealTurkishPlaceNamesMatchTheCatalogsCapitals()
    {
        // #15: the Identity directory now writes the names in Turkish; the folding match of PR #113 keeps working for them.
        foreach (var (name, capitals) in new[] { ("Adıyaman", "ADIYAMAN"), ("Afyonkarahisar", "AFYONKARAHİSAR"), ("Iğdır", "IĞDIR"), ("İstanbul", "İSTANBUL"),
                     ("Şanlıurfa", "ŞANLIURFA"), ("Kadıköy", "KADIKÖY"), ("Keçiören", "KEÇİÖREN"), ("Çamlıdere", "ÇAMLIDERE") })
            CoachingStudentOnboardingService.Fold(name).Should().Be(CoachingStudentOnboardingService.Fold(capitals), name);
        await using var s = await Setup.CreateAsync();
        foreach (var (city, district, name) in new[] { ("ADIYAMAN", "MERKEZ", "Adıyaman Fen Lisesi"), ("IĞDIR", "MERKEZ", "Iğdır Fen Lisesi"),
                     ("ŞANLIURFA", "EYYÜBİYE", "Şanlıurfa Fen Lisesi"), ("ANKARA", "ÇAMLIDERE", "Çamlıdere Anadolu Lisesi") })
        {
            var school = TargetSchool.Create("meb", name, name, city, district, 400, 2025); school.SetProgramDetails("Fen Lisesi", null, null); school.SetActive(true);
            s.F.Db.Add(school);
        }
        await s.F.Db.SaveChangesAsync();
        foreach (var (city, expected) in new[] { ("Adıyaman", "Adıyaman Fen Lisesi"), ("Iğdır", "Iğdır Fen Lisesi"), ("Şanlıurfa", "Şanlıurfa Fen Lisesi") })
            (await s.Service.SchoolsAsync(city, null, null, 1, default)).Items.Select(x => x.Name).Should().Equal([expected], city);
        var ankara = await s.Service.SchoolsAsync("Ankara", null, null, 1, default, district: "Çamlıdere");
        ankara.Items[0].Should().Match<OnboardingSchoolOption>(x => x.Name == "Çamlıdere Anadolu Lisesi" && x.InHomeDistrict);
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", null, 1, default, district: "Keçiören")).Items[0].InHomeDistrict.Should().BeTrue();
        // The wizard stores the directory's (corrected) names.
        var view = await s.Personal(grade: 8);
        (view.Answers.HomeCity, view.Answers.HomeDistrict).Should().Be(("Ankara", "Çankaya"));
        await s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "02", "0201", CoachingEducationStatus.Student, 8, null, null), default);
        (await s.Service.GetAsync(default)).Answers.HomeCity.Should().Be("Adıyaman");
    }

    [Fact]
    public async Task TheOwnSchoolListShowsEachSchoolOnceAndTheTargetListKeepsEachProgramme()
    {
        // #16: the catalog has one row per type, programme and language; the student's own school is one school.
        await using var s = await Setup.CreateAsync();
        TargetSchool Add(string name, string type, string? language)
        {
            var school = TargetSchool.Create("meb", Guid.NewGuid().ToString(), name, "ANKARA", "ÇANKAYA", 400, 2025);
            school.SetProgramDetails(type, language, null); school.SetActive(true); s.F.Db.Add(school);
            return school;
        }
        var ayranci = new[] { Add("Ayrancı Anadolu Lisesi", "Anadolu Lisesi", "İngilizce"), Add("Ayrancı Anadolu Lisesi", "Anadolu Lisesi", "Almanca") };
        var balgat = new[] { Add("Balgat Mesleki ve Teknik Anadolu Lisesi", "Anadolu Teknik Programı", "İngilizce"),
            Add("Balgat Mesleki ve Teknik Anadolu Lisesi", "Anadolu Teknik Programı", "Almanca"),
            Add("Balgat Mesleki ve Teknik Anadolu Lisesi", "Anadolu Meslek Programı", "İngilizce"),
            Add("Balgat Mesleki ve Teknik Anadolu Lisesi", "Anadolu Meslek Programı", "Almanca") };
        await s.F.Db.SaveChangesAsync();

        (await s.Service.SchoolsAsync("Ankara", null, null, 1, default)).TotalCount.Should().Be(28, "26 schools and two schools with several rows");
        var one = (await s.Service.SchoolsAsync("Ankara", null, "Ayran", 1, default)).Items.Should().ContainSingle().Subject;
        one.Should().Match<OnboardingSchoolOption>(x => x.Name == "Ayrancı Anadolu Lisesi" && x.District == "Çankaya" && x.Type == "Anadolu Lisesi");
        one.Id.Should().Be(ayranci.Min(x => x.Id), "the representative is stable: the smallest id");
        var mixed = (await s.Service.SchoolsAsync("Ankara", null, "balgat", 1, default)).Items.Should().ContainSingle().Subject;
        mixed.Type.Should().BeNull("the rows have different types, so none is shown");
        mixed.Id.Should().Be(balgat.Min(x => x.Id));
        (await s.Service.SchoolsAsync("Ankara", null, "balgat", 1, default)).Items.Single().Id.Should().Be(mixed.Id);
        // The LGS target list (city → type → school) keeps one row per programme.
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Teknik Programı", "balgat", 1, default)).Items.Should().HaveCount(2);
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", "Ayran", 1, default)).Items.Should().HaveCount(2);

        // Saving keeps the name, city and district; the type only where every row agrees.
        await s.Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "06", "0601", CoachingEducationStatus.Student, 8, null, balgat[2].Id), default);
        var profile = await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == s.Student);
        (profile.SchoolName, profile.SchoolCity, profile.SchoolDistrict, profile.SchoolType).Should().Be(("Balgat Mesleki ve Teknik Anadolu Lisesi", "Ankara", "Çankaya", (string?)null));
        var store = new CoachingProfileStore(s.F.Db, s.F.Accounts, null, null, s.Places);
        var updated = await store.UpdateStudentAsync(s.Student, new(profile.Version, CoachingEducationStatus.Student, 8, null, null, null, null, null, null,
            new DateOnly(2012, 5, 1), null, null, true) { SchoolId = ayranci[1].Id }, default);
        (updated.SchoolName, updated.SchoolType).Should().Be(("Ayrancı Anadolu Lisesi", "Anadolu Lisesi"));
        updated = await store.UpdateStudentAsync(s.Student, new(updated.Version, CoachingEducationStatus.Student, 8, null, null, null, null, null, null,
            new DateOnly(2012, 5, 1), null, null, true) { SchoolId = balgat[0].Id }, default);
        (updated.SchoolName, updated.SchoolType).Should().Be(("Balgat Mesleki ve Teknik Anadolu Lisesi", (string?)null));
    }

    [Fact]
    public async Task TheSameSchoolNameInTwoDistrictsStaysTwoSchools()
    {
        await using var s = await Setup.CreateAsync();
        foreach (var (district, language) in new[] { ("ÇANKAYA", "İngilizce"), ("ÇANKAYA", "Almanca"), ("KEÇİÖREN", "İngilizce") })
        {
            var school = TargetSchool.Create("meb", Guid.NewGuid().ToString(), "Atatürk Anadolu Lisesi", "ANKARA", district, 400, 2025);
            school.SetProgramDetails("Anadolu Lisesi", language, null); school.SetActive(true); s.F.Db.Add(school);
        }
        await s.F.Db.SaveChangesAsync();
        var page = await s.Service.SchoolsAsync("Ankara", null, "atatürk", 1, default, district: "Keçiören");
        page.Items.Select(x => (x.Name, x.District, x.InHomeDistrict)).Should().Equal(
            ("Atatürk Anadolu Lisesi", "Keçiören", true), ("Atatürk Anadolu Lisesi", "Çankaya", false));
        page.TotalCount.Should().Be(2);
    }

    // ---------- Paket G (G4 #43, G5 #44) ----------

    [Fact]
    public async Task TheSchoolSearchMatchesEveryWordInAnyOrderAndShowsDistrictsAsNames()
    {
        await using var s = await Setup.CreateAsync();
        foreach (var (name, district, type) in new[] { ("Karşıyaka Cihat Kora Anadolu Lisesi", "KARŞIYAKA", "Anadolu Lisesi"),
                     ("Karşıyaka Fen Lisesi", "KARŞIYAKA", "Fen Lisesi"), ("Cihat Kora Anadolu Lisesi", "KARŞIYAKA", "Anadolu Lisesi"),
                     ("Bornova Anadolu Lisesi", "BORNOVA", "Anadolu Lisesi"), ("Prof.Dr. Işıl Ünal Kız Anadolu Lisesi", "ÇİĞLİ", "Anadolu Lisesi") })
        {
            var school = TargetSchool.Create("meb", Guid.NewGuid().ToString(), name, "İZMİR", district, 400, 2025);
            school.SetProgramDetails(type, null, null); school.SetActive(true); s.F.Db.Add(school);
        }
        await s.F.Db.SaveChangesAsync();
        async Task<string[]> Names(string search, string? type = null) =>
            (await s.Service.SchoolsAsync("İzmir", type, search, 1, default)).Items.Select(x => x.Name).ToArray();

        // Every word must match, in any order, Turkish case-insensitively; a word may match the district instead of the name.
        (await Names("karşıyaka anadolu")).Should().BeEquivalentTo("Karşıyaka Cihat Kora Anadolu Lisesi", "Cihat Kora Anadolu Lisesi");
        (await Names("ANADOLU  KARŞIYAKA")).Should().BeEquivalentTo("Karşıyaka Cihat Kora Anadolu Lisesi", "Cihat Kora Anadolu Lisesi");
        (await Names("kora karşıyaka cihat")).Should().HaveCount(2);
        (await Names("karşıyaka fen")).Should().Equal("Karşıyaka Fen Lisesi");
        (await Names("ışıl kız")).Should().Equal("Prof.Dr. Işıl Ünal Kız Anadolu Lisesi");
        (await Names("dr. işil")).Should().Equal("Prof.Dr. Işıl Ünal Kız Anadolu Lisesi");
        (await Names("karşıyaka bornova")).Should().BeEmpty("both words must match");
        // PR #128 review M1: without Turkish letters too.
        (await Names("karsiyaka anadolu")).Should().BeEquivalentTo("Karşıyaka Cihat Kora Anadolu Lisesi", "Cihat Kora Anadolu Lisesi");
        (await Names("isil kiz")).Should().Equal("Prof.Dr. Işıl Ünal Kız Anadolu Lisesi");
        (await s.Service.SchoolsAsync("İzmir", null, "cigli", 1, default)).Items.Single().District.Should().Be("Çiğli");
        TurkishSearch.EveryWordStarts("karsiyaka", "KARŞIYAKA").Should().BeTrue();
        // In the own-school list the schools where every word starts a word come first ("lise 7": Lise 7, then Lise 17).
        (await s.Service.SchoolsAsync("Ankara", null, "lise 7", 1, default)).Items.Select(x => x.Name).Should().Equal("Anadolu Lise 7", "Anadolu Lise 17");
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", "7 lise", 1, default)).Items.Select(x => x.Name).Should().Equal("Anadolu Lise 7", "Anadolu Lise 17");
        TurkishSearch.EveryWordStarts("dr işil", "Prof.Dr. Işıl Ünal Kız Anadolu Lisesi").Should().BeTrue();
        TurkishSearch.EveryWordStarts("lise 7", "Anadolu Lise 17").Should().BeFalse();
        (await Names("anadolu karşıyaka", "Anadolu Lisesi")).Should().HaveCount(2, "the LGS target list searches the same way");
        // Districts are shown as names, in both lists.
        (await s.Service.SchoolsAsync("İzmir", null, "karşıyaka fen", 1, default)).Items.Single().District.Should().Be("Karşıyaka");
        (await s.Service.SchoolsAsync("İzmir", "Fen Lisesi", "karşıyaka", 1, default)).Items.Single().District.Should().Be("Karşıyaka");
        (await s.Service.SchoolsAsync("İzmir", null, "çiğli", 1, default)).Items.Single().District.Should().Be("Çiğli");
        await Assert.ThrowsAnyAsync<ArgumentException>(() => s.Service.SchoolsAsync("İzmir", null, new string('a', 101), 1, default));
    }

    [Fact]
    public async Task PlaceNamesInCapitalsAreShownAsNames()
    {
        foreach (var (capitals, name) in new[] { ("KARŞIYAKA", "Karşıyaka"), ("ÇANKAYA (MERKEZ)", "Çankaya (Merkez)"), ("İZMİR", "İzmir"),
                     ("IĞDIR", "Iğdır"), ("19 MAYIS", "19 Mayıs"), ("Kadıköy", "Kadıköy") })
            CoachingStudentOnboardingService.PlaceName(capitals).Should().Be(name, capitals);
        CoachingStudentOnboardingService.PlaceName(null).Should().BeNull();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task AStudentWhoseSchoolIsNotInTheListTypesItsName()
    {
        await using var s = await Setup.CreateAsync();
        Task<OnboardingView> Save(Guid? school, string? name) => s.Service.SavePersonalAsync(new(new DateOnly(2009, 1, 1), "06", "0601",
            CoachingEducationStatus.Student, 11, null, school, name), default);
        // "Okulum listede yok": the name is trimmed and saved without a catalog city, district or type.
        await Save(s.School, null);
        var typed = await Save(null, "  Özel   Deneme Koleji ");
        typed.Answers.SchoolName.Should().Be("Özel Deneme Koleji");
        var profile = await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == s.Student);
        (profile.SchoolName, profile.SchoolCity, profile.SchoolDistrict, profile.SchoolType).Should().Be(("Özel Deneme Koleji", (string?)null, (string?)null, (string?)null));
        // Resuming without a new answer keeps it; a name over 150 characters is refused at the school question.
        (await Save(null, null)).Answers.SchoolName.Should().Be("Özel Deneme Koleji");
        (await Assert.ThrowsAsync<OnboardingFieldException>(() => Save(null, new string('a', 151)))).Field.Should().Be("ownSchool");
        (await Save(null, new string('a', 150))).Answers.SchoolName.Should().HaveLength(150);
        // Review L2: a name of only zero-width or format characters is refused; such characters are dropped from a name.
        (await Assert.ThrowsAsync<OnboardingFieldException>(() => Save(null, "\u200B\u200C \uFEFF"))).Field.Should().Be("ownSchool");
        (await Save(null, "Deneme\u200B Lisesi")).Answers.SchoolName.Should().Be("Deneme Lisesi");
        // A school picked from the list wins over a typed name.
        (await Save(s.School, "Yazılan")).Answers.SchoolName.Should().Be("Ankara Fen Lisesi");

        // Profilim keeps and edits a typed school the same way.
        var store = new CoachingProfileStore(s.F.Db, s.F.Accounts, null, null, s.Places);
        profile = await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == s.Student);
        var updated = await store.UpdateStudentAsync(s.Student, new(profile.Version, CoachingEducationStatus.Student, 11, " Köy   Lisesi ", null, null, null, null, null,
            new DateOnly(2009, 1, 1), null, null, true), default);
        (updated.SchoolName, updated.SchoolCity, updated.SchoolDistrict, updated.SchoolType).Should().Be(("Köy Lisesi", (string?)null, (string?)null, (string?)null));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.UpdateStudentAsync(s.Student, new(updated.Version, CoachingEducationStatus.Student, 11,
            new string('b', 151), null, null, null, null, null, new DateOnly(2009, 1, 1), null, null, true), default));
    }

    // PR #128 review L1: Profilim (and a teacher's edit) checks the birth date with the wizard's rule (Türkiye date, 6–100 years).
    [Fact]
    public async Task ProfilimChecksTheBirthDateLikeTheWizard()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 8);
        var store = new CoachingProfileStore(s.F.Db, s.F.Accounts, null, null, s.Places);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")));
        async Task<CoachingStudentProfile> Update(DateOnly? date)
        {
            var profile = await s.F.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == s.Student);
            return await store.UpdateStudentAsync(s.Student, new(profile.Version, CoachingEducationStatus.Student, 8, null, null, null, null, null, null,
                date, null, null, true), default);
        }
        foreach (var wrong in new[] { today.AddDays(1), today.AddYears(-3), today.AddYears(-101) })
            await Assert.ThrowsAnyAsync<ArgumentException>(() => Update(wrong));
        (await Update(today.AddYears(-14))).BirthDate.Should().Be(today.AddYears(-14));
        (await Update(null)).BirthDate.Should().BeNull();
        // A stored date is not refused again when it is sent back unchanged (only a new date is checked).
        var profile = await s.F.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == s.Student);
        s.F.Db.Entry(profile).Property(x => x.BirthDate).CurrentValue = today.AddYears(-4); await s.F.Db.SaveChangesAsync(); s.F.Db.ChangeTracker.Clear();
        (await Update(today.AddYears(-4))).BirthDate.Should().Be(today.AddYears(-4));
    }

    [Fact]
    public async Task TheBirthDateErrorSaysWhatIsWrong()
    {
        await using var s = await Setup.CreateAsync();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")));
        async Task<string> Refused(DateOnly? date)
        {
            var error = await Assert.ThrowsAsync<OnboardingFieldException>(() => s.Service.SavePersonalAsync(new(date, "06", "0601",
                CoachingEducationStatus.Student, 8, null, null), default));
            error.Field.Should().Be("birthDate");
            return error.Message;
        }
        (await Refused(today.AddDays(2))).Should().Be("Doğum tarihi bugünden sonra olamaz.");
        (await Refused(today.AddYears(-3))).Should().Be("Bu tarihe göre 6 yaşından küçüksün. Doğum tarihini kontrol et.");
        (await Refused(today.AddYears(-101))).Should().Be("Bu tarihe göre 100 yaşından büyüksün. Doğum tarihini kontrol et.");
        (await Refused(null)).Should().Be("Doğum tarihini seç.");
    }

    [Fact]
    public async Task NationalityQuotaProgrammesAreNotOffered()
    {
        // #17: "(KKTC Uyruklu)" places are only for TRNC citizens; the catalog keeps them, the student lists do not offer them.
        await using var s = await Setup.CreateAsync();
        s.Program("2001", "Ankara Üniversitesi", "Ergoterapi (KKTC Uyruklu)", "Bachelor", "SAY", "ANKARA", 211, 900000);
        s.Program("2002", "Ankara Üniversitesi", "Makine Mühendisliği (M.T.O.K.)", "Bachelor", "SAY", "ANKARA", 300, 300000);
        s.Program("2003", "Girne Üniversitesi", "Tıp (KKTC Uyruklu)", "Bachelor", "SAY", "KIBRIS", 400, 60000);
        await s.F.Db.SaveChangesAsync();

        (await s.Service.ProgramCitiesAsync("Bachelor", "SAY", default)).Select(x => x.Value).Should().Equal("ANKARA", "İSTANBUL");
        (await s.Service.UniversitiesAsync("Bachelor", "SAY", "Kıbrıs", null, default)).Should().BeEmpty();
        (await s.Service.ProgramsAsync("Bachelor", "SAY", "Ankara", "Ankara Üniversitesi", null, 1, default)).Items.Select(x => x.Name)
            .Should().Equal("Bilgisayar Mühendisliği", "Fizik", "Makine Mühendisliği (M.T.O.K.)");
        (await s.Service.ProgramsAsync("Bachelor", "SAY", "Ankara", "Ankara Üniversitesi", "ergo", 1, default)).TotalCount.Should().Be(0);
        // Hedefim's catalog search (students and teachers) does not offer them either.
        var search = await new TargetAdmissionSearchService(s.F.Db).SearchAsync(new("YKS", 2026, ProgramType: "Bachelor", PageSize: 50));
        search.Items.Select(x => x.Name).Should().NotContain(x => x.Contains("Uyruklu")).And.Contain("Makine Mühendisliği (M.T.O.K.)");
        search.TotalCount.Should().Be(5);
    }

    // ---------- Re-check LOWs (Paket C) ----------

    [Fact]
    public async Task TheStudentsDistrictComesFirstInTheSchoolListWithoutFilteringOthers()
    {
        await using var s = await Setup.CreateAsync();
        var page = await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", null, 1, default, district: "Keçiören");
        page.TotalCount.Should().Be(25, "a sort, not a filter");
        page.Items.Take(13).Should().OnlyContain(x => x.District == "Keçiören" && x.InHomeDistrict);
        page.Items.Skip(13).Should().OnlyContain(x => x.District == "Çankaya" && !x.InHomeDistrict);
        (await s.Service.SchoolsAsync("Ankara", null, null, 1, default, district: "Çankaya")).Items[0].Name.Should().Be("Anadolu Lise 10");
        (await s.Service.SchoolsAsync("Ankara", "Anadolu Lisesi", null, 1, default)).Items.Should().OnlyContain(x => !x.InHomeDistrict);
    }

    [Fact]
    public async Task ATargetFromTheWizardRetitlesTheStudentsOwnGoal()
    {
        await using var s = await Setup.CreateAsync();
        await s.Personal(grade: 8);
        await s.Service.SaveExamAsync(new(LearningPurpose.Lgs, null, null, s.LgsSession, null, null, false), default);
        var goal = AcademicGoal.Create(s.Student, "Benim eski hedefim", GoalCategory.ExamPreparation);
        s.F.Db.Add(goal); await s.F.Db.SaveChangesAsync(); s.F.Db.Add(StudentPrimaryGoal.Create(s.Student, goal.Id)); await s.F.Db.SaveChangesAsync();
        s.F.Db.ChangeTracker.Clear();
        await s.Service.SaveTargetAsync(new("school", s.School, null), default);
        (await s.F.Db.AcademicGoals.AsNoTracking().SingleAsync()).Title.Should().Be("Ankara Fen Lisesi");
    }

    [Fact]
    public void ChoosingASchoolOrProgrammeClearsTheWizardsTargetScores()
    {
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Hedef", GoalCategory.ExamPreparation);
        goal.SetScoreTargets([new("TYT", 380, 560), new("SAY", 450, 560)], ExamType.YKS);
        goal.SetCatalogTarget(Guid.NewGuid(), null);
        (goal.ScoreTargets.Count, goal.TargetScore, goal.TargetMaxScore).Should().Be((0, (decimal?)null, (decimal?)null));
        // An older score (no score targets) set on the goal page is not touched by a catalog target.
        var older = AcademicGoal.Create(Guid.NewGuid(), "Hedef", GoalCategory.ExamPreparation);
        older.SetScoreTarget(400, 500, ExamType.LGS);
        older.SetCatalogTarget(null, Guid.NewGuid());
        older.TargetScore.Should().Be(400);
        // The wizard's programme without a taban: the catalog target first, then its own score.
        var own = AcademicGoal.Create(Guid.NewGuid(), "Hedef", GoalCategory.ExamPreparation);
        own.SetCatalogTarget(Guid.NewGuid(), null); own.SetScoreTargets([new("SAY", 410, 560)], ExamType.YKS);
        own.ScoreTargets.Should().ContainSingle();
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

    private sealed class Recorder(List<object> published) : Coaching.Application.Interfaces.ICoachingEventPublisher
    {
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class { published.Add(message); return Task.CompletedTask; }
    }

    /// <summary>
    /// Province names as the corrected directory writes them ("Adıyaman"); "Afyonkarahi̇sar" (with a combining dot) stands for a
    /// name written before the E2 correction, which still matches the catalog.
    /// </summary>
    internal sealed class Locations : ICoachingLocationDirectory
    {
        public bool TimesOut { get; set; }
        public Task<IReadOnlyList<LocationProvince>> GetProvincesAsync(CancellationToken ct) => TimesOut
            ? throw new TaskCanceledException("timeout")
            : Task.FromResult<IReadOnlyList<LocationProvince>>([new("06", "Ankara"), new("34", "İstanbul"), new("02", "Adıyaman"), new("03", "Afyonkarahi\u0307sar")]);
        public Task<IReadOnlyList<LocationDistrict>> GetDistrictsAsync(string provinceId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LocationDistrict>>(provinceId switch
            {
                "06" => [new("0601", "06", "Çankaya"), new("0602", "06", "Keçiören")],
                "02" => [new("0201", "02", "Merkez")],
                "03" => [new("0301", "03", "Merkez")],
                _ => [new("3401", "34", "Kadıköy")]
            });
        public Task<bool> VerifyPairAsync(string provinceId, string districtId, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CoachingOwnedProfilePersistenceTests.Fixture F { get; } = new();
        public Guid Student, School, LgsSession;
        public int LgsYear;
        public Dictionary<string, Guid> Programs { get; } = [];
        public List<object> Published { get; } = [];
        public Locations Places { get; } = new();
        private Coaching.Application.Interfaces.ICoachingEventPublisher Events => new Recorder(Published);
        private int order;

        public CoachingStudentOnboardingService Service => ServiceWith(null);
        public CoachingStudentOnboardingService ServiceWith(Coaching.Application.Subscriptions.ICoachingSubscription? subscriptions)
        {
            var access = new CoachingAccessPolicy(F.Actor);
            return new(F.Db, access, F.Accounts, new CoachingLearningSetupService(F.Db, access, F.Accounts), Places, subscriptions, null, Events);
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
            var fen = TargetSchool.Create("meb", "fen-1", "Ankara Fen Lisesi", "ANKARA", "ÇANKAYA", 480, 2025);
            fen.SetProgramDetails("Fen Lisesi", null, null); fen.SetActive(true); s.F.Db.Add(fen); s.School = fen.Id;
            for (var i = 1; i <= 25; i++)
            {
                var school = TargetSchool.Create("meb", $"al-{i}", $"Anadolu Lise {i}", "ANKARA", i % 2 == 0 ? "ÇANKAYA" : "KEÇİÖREN", 300 + i, 2025);
                school.SetProgramDetails("Anadolu Lisesi", null, null); school.SetActive(true); s.F.Db.Add(school);
            }
            var izmir = TargetSchool.Create("meb", "iz-1", "İzmir Fen Lisesi", "İZMİR", "BORNOVA", 470, 2025);
            izmir.SetProgramDetails("Fen Lisesi", null, null); izmir.SetActive(true); s.F.Db.Add(izmir);
            var passive = TargetSchool.Create("meb", "old", "Kapanan Lise", "ANKARA", "ÇANKAYA", 300, 2025);
            passive.SetProgramDetails("Anadolu Lisesi", null, null); s.F.Db.Add(passive);
            s.Program("1001", "Ankara Üniversitesi", "Bilgisayar Mühendisliği", "Bachelor", "SAY", "ANKARA", 470, 12000);
            s.Program("1002", "Ankara Üniversitesi", "Fizik", "Bachelor", "SAY", "ANKARA", null, null);
            s.Program("1003", "Ankara Üniversitesi", "Hukuk", "Bachelor", "EA", "ANKARA", 450, 20000);
            s.Program("1004", "İstanbul Üniversitesi", "Matematik", "Bachelor", "SAY", "İSTANBUL", 420, 50000);
            s.Program("1005", "Ankara Üniversitesi", "Bilgisayar Programcılığı", "Associate", "TYT", "ANKARA", 300, 400000);
            await s.F.Db.SaveChangesAsync();
            s.LgsSession = lgs.Sessions.Single().Id;
            s.F.Db.ChangeTracker.Clear();
            return s;
        }

        public void Program(string code, string university, string name, string level, string scoreType, string city, int? minimum, int? rank)
        {
            var program = TargetUniversityProgram.Create(code, university, name, "Fakülte", level, scoreType);
            program.UpdateDetails(new(code, university, name, scoreType, city, null, "Devlet", level, null, null, null, null, 50, 50, minimum, minimum, rank, null),
                2026, 2026, null, "https://yokatlas.yok.gov.tr");
            program.SetActive(true); F.Db.Add(program); Programs[name] = program.Id;
        }

        public Task<OnboardingView> Personal(int? grade, CoachingEducationStatus? status = null) => Service.SavePersonalAsync(new(new DateOnly(2012, 5, 1), "06", "0601",
            status ?? (grade is null ? CoachingEducationStatus.HighSchoolGraduate : CoachingEducationStatus.Student), grade, null, null), default);

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
