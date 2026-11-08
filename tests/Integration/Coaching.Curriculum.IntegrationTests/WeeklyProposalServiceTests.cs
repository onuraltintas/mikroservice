using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

// Paket H2: the weekly proposal endpoint: saved availability, weekly draft, chosen lessons and their weekly need (the annual preview's numbers).
public sealed class WeeklyProposalServiceTests
{
    // Monday 24 May 2027, 09:00 in Istanbul; the fixture's LGS has no session, so the plan ends on 1 June (9 days).
    private static readonly DateTimeOffset Now = new(2027, 5, 24, 6, 0, 0, TimeSpan.Zero);
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class Identity(Guid student) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid id, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacher, IReadOnlyCollection<Guid> ids, Guid? institution, bool admin, CancellationToken ct)
            => ids.All(x => x == student) ? Task.FromResult(institution) : throw new BusinessRuleException("Authorization.Forbidden", "Denied");
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewer, IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult(ids);
    }

    private sealed record World(Guid Student, StudyCatalogLesson Math, StudyCatalogLesson Turkish, StudyCatalogTopic TurkishTopic);

    /// <summary>Matematik keeps 30 minutes of work after the seeded completed task; Türkçe has 180. Both are chosen (Derslerim).</summary>
    private static async Task<World> Seed(CurriculumManagementTests.Fixture f, bool availability = true)
    {
        await WeeklyOutcomePlanTests.Seed(f);
        var student = f.User.UserId!.Value; var math = await f.Db.StudyCatalogLessons.SingleAsync();
        var turkish = StudyCatalogLesson.Create("test", "h2-turkish", "Türkçe", 8, "LGS"); turkish.SetActive(true);
        var unit = StudyCatalogUnit.Create("test", "h2-turkish-unit", turkish.Id, "Türkçe", 1); unit.SetActive(true);
        var topic = StudyCatalogTopic.Create("test", "h2-turkish-topic", turkish.Id, unit.Id, "Paragraf", null, 1); topic.SetActive(true);
        var edition = OfficialCurriculumEdition.Create("test-h2-2026", "LGS", 8, "Türkçe", 2026, 2026, "https://meb.gov.tr/test", new string('c', 64), "https://meb.gov.tr/test");
        var official = OfficialCurriculumTopic.Create(edition.Id, "T.H2", "Paragraf", 1, 1);
        var outcome = StudyCatalogOutcome.Create(edition.Id, official.Id, "T.H2.1", "Paragraf kazanımı", "", 1, 1);
        var estimate = OutcomeDurationEstimate.Create(outcome.Id, 1, 60, 60, 60, "ReviewedStudy", "Medium", "H2 test süresi", f.User.UserId); estimate.Publish();
        var mapping = StudyCatalogTopicOutcome.Create(topic.Id, outcome.Id, "H2 eşleştirmesi"); mapping.Review(true, "H2 eşleştirmesi", student);
        f.Db.AddRange(turkish, unit, topic, edition, official, outcome, estimate, mapping);
        var context = await f.Db.StudentLearningContexts.Include(x => x.CoursePreferences).SingleAsync();
        context.ReplaceSelections([StudentCoursePreference.Create(context.Id, math.Id, 3, null), StudentCoursePreference.Create(context.Id, turkish.Id, 3, null)], [], false);
        if (availability)
        {
            var hours = StudyAvailability.Create(student);
            hours.ReplaceWindows(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                .Select(day => new StudyAvailabilityWindow(day, 1020, 1200)));
            f.Db.Add(hours);
        }
        await f.Db.SaveChangesAsync();
        return new(student, math, turkish, topic);
    }
    private static CoachingWeeklyProposalService Proposals(CurriculumManagementTests.Fixture f, CoachingStudyPlanAccess? staff = null) =>
        new(f.Db, new CoachingAccessPolicy(f.User), new WeeklyStudyDraftTests.Catalog(f.Db), new FixedClock(Now), staff);
    private static CoachingWeeklyOutcomePlanService Plans(CurriculumManagementTests.Fixture f) =>
        new(f.Db, new CoachingAccessPolicy(f.User), new WeeklyStudyDraftTests.Catalog(f.Db), new FixedClock(Now));

    [Fact]
    public async Task ReplaceProposesTheNeededHoursForEveryChosenLesson_AndTheAppliedWeekFitsTheAnnualPreview()
    {
        await using var f = new CurriculumManagementTests.Fixture(); await ProposeApplyAndPreview(f);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheAppliedProposalFitsTheAnnualPreviewOnPostgres()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); await ProposeApplyAndPreview(pg.Data);
    }

    private static async Task ProposeApplyAndPreview(CurriculumManagementTests.Fixture f)
    {
        var world = await Seed(f);
        var proposal = await Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace), default);

        Assert.Equal((new DateOnly(2027, 6, 1), 2, WeeklyDayLimit.SchoolMinutes, true), (proposal.EndDate, proposal.WeeksUntilEnd, proposal.MaxDayMinutes, proposal.MiddleSchool));
        // Weekly need = ceil((work + 15 × reviews before the end) × 7 ÷ 9 days): Matematik (30 + 2 × 15) → 47, Türkçe (180 + 2 × 15) → 164.
        var math = proposal.Lessons.Single(x => x.LessonId == world.Math.Id); var turkish = proposal.Lessons.Single(x => x.LessonId == world.Turkish.Id);
        Assert.Equal((0, 60, 60, false), (math.ExistingMinutes, math.ProposedMinutes, math.NeededMinutes, math.NoTime));
        Assert.Equal((0, 180, 180), (turkish.ExistingMinutes, turkish.ProposedMinutes, turkish.NeededMinutes));
        Assert.Equal(0, proposal.ShortageMinutes);
        Assert.Equal(new[] { "Matematik", "Türkçe" }, proposal.Lessons.Select(x => x.LessonName));
        Assert.All(proposal.Slots, x => Assert.Equal(60, x.DurationMinutes));
        // Middle school: a break after every session, so 17:00 and 18:30 each weekday; the needed sessions are spread over the days.
        Assert.Equal(10 * 60, proposal.FreeMinutes);
        Assert.Equal(4, proposal.Slots.Select(x => x.Day).Distinct().Count());
        Assert.All(proposal.Slots.GroupBy(x => x.Day), day => Assert.Single(day));

        // Applying it: the weekly draft is replaced with the proposal and the annual preview (engine 2, freed hours reused) fits.
        var drafts = WeeklyStudyDraftTests.Service(f, new(f.Db));
        var draft = await f.Db.Set<WeeklyStudyDraft>().AsNoTracking().SingleAsync();
        var saved = await drafts.SaveAsync(new(draft.Version, null, proposal.Slots.Select(x => new WeeklySlotInput(x.Id, x.LessonId, x.Day, x.StartMinute, x.DurationMinutes)).ToArray()), default);
        var preview = await Plans(f).PreviewAsync(new(new(2027, 5, 24), proposal.EndDate, saved.Version, true, WeeklyPlanEngine.CurrentVersion), default);
        Assert.True(preview.CanCreate);
        Assert.Equal(2, preview.Schedule.Lessons.Count);
        Assert.All(preview.Schedule.Lessons, x => Assert.Equal(0, x.RemainingMinutes));
        Assert.Empty(preview.UnscheduledLessons!);
    }

    [Fact]
    public async Task FillKeepsTheSavedCardsAndProposesOnlyInFreeTime_NothingIsSaved()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f);
        var before = await f.Db.Set<WeeklyStudyDraft>().AsNoTracking().SingleAsync();
        var proposal = await Proposals(f).ProposeAsync(new(), default);

        Assert.Equal(WeeklyProposalMode.Fill, proposal.Mode);
        // The saved 30-minute Matematik card (Monday 18:00) stays; Matematik still needs one hour, so it gets one more session.
        var math = proposal.Lessons.Single(x => x.LessonId == world.Math.Id);
        Assert.Equal((30, 60), (math.ExistingMinutes, math.ProposedMinutes));
        Assert.DoesNotContain(proposal.Slots, x => x.Day == DayOfWeek.Monday && x.StartMinute < 1110 && x.StartMinute + 60 > 1080);
        // Monday: 17:00 would touch the saved card without a break; only 19:00 fits.
        Assert.All(proposal.Slots.Where(x => x.Day == DayOfWeek.Monday), x => Assert.Equal(1140, x.StartMinute));
        var after = await f.Db.Set<WeeklyStudyDraft>().AsNoTracking().SingleAsync();
        Assert.Equal((before.Version, before.Slots.Count), (after.Version, after.Slots.Count));
    }

    [Fact]
    public async Task WithoutAvailabilityTheStudentIsAskedForHerHours_AnEmptyDraftIsFine()
    {
        await using var f = new CurriculumManagementTests.Fixture(); await Seed(f, availability: false);
        var missing = await Assert.ThrowsAsync<KeyNotFoundException>(() => Proposals(f).ProposeAsync(new(), default));
        Assert.Equal("Önce müsait saatlerini kaydet.", missing.Message);
        var api = new Coaching.API.Controllers.WeeklyStudyDraftController(WeeklyStudyDraftTests.Service(f, new(f.Db)));
        var notFound = Assert.IsType<NotFoundObjectResult>(await api.Proposal(new(), Proposals(f), default));
        Assert.Equal("Önce müsait saatlerini kaydet.", notFound.Value!.GetType().GetProperty("message")!.GetValue(notFound.Value));

        var hours = StudyAvailability.Create(f.User.UserId!.Value); hours.ReplaceWindows([new(DayOfWeek.Saturday, 600, 780)]); f.Db.Add(hours);
        f.Db.Remove(await f.Db.Set<WeeklyStudyDraft>().SingleAsync()); await f.Db.SaveChangesAsync();
        var proposal = await Proposals(f).ProposeAsync(new(), default);
        // Middle school: 10:00 and, after a 30-minute break, 11:30; both chosen lessons get an hour.
        Assert.Equal(new[] { 600, 690 }, proposal.Slots.Select(x => x.StartMinute));
        Assert.All(proposal.Lessons, x => Assert.Equal(0, x.ExistingMinutes));
        Assert.IsType<OkObjectResult>(await api.Proposal(new(WeeklyProposalMode.Replace), Proposals(f), default));
        await Assert.ThrowsAsync<ArgumentException>(() => Proposals(f).ProposeAsync(new(ExamFocus: "lgs"), default));
    }

    [Fact]
    public async Task TheTeacherSeesTheSameProposal()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f);
        var own = await Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace), default);
        f.User.Roles = ["Teacher"]; f.User.UserId = Guid.NewGuid();
        var staff = new CoachingStudyPlanAccess(new CoachingAccessPolicy(f.User), new Identity(world.Student), f.Scope);
        var teacher = await Proposals(f, staff).ProposeForStudentAsync(world.Student, new(WeeklyProposalMode.Replace), default);
        Assert.Equal(own.Lessons, teacher.Lessons);
        Assert.Equal(own.Slots.Select(x => (x.LessonId, x.Day, x.StartMinute)), teacher.Slots.Select(x => (x.LessonId, x.Day, x.StartMinute)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Proposals(f, staff).ProposeForStudentAsync(Guid.NewGuid(), new(), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Proposals(f).ProposeForStudentAsync(world.Student, new(), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Proposals(f, staff).ProposeAsync(new(), default));
    }

    [Fact]
    public async Task AYksStudentChoosesTheExamFocus_AGraduateGetsTenHoursADayAndABreakEveryTwoSessions()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f);
        // A lise graduate preparing for YKS (SAY): Matematik becomes a TYT lesson, Türkçe an AYT lesson she says she has studied
        // (a short refresh: 45 minutes, so its weekly need, 35, is below Matematik's 47).
        foreach (var (lesson, exam, test) in new[] { (world.Math, "TYT", "TYT.MAT"), (world.Turkish, "AYT", "AYT.EDB") })
        {
            f.Db.Entry(lesson).Property(x => x.ExamCode).CurrentValue = exam;
            var topic = await f.Db.StudyCatalogTopics.SingleAsync(x => x.LessonId == lesson.Id && !f.Db.StudyCatalogTopics.Any(c => c.ParentId == x.Id));
            f.Db.Add(StudyCatalogYksScope.Create(topic.Id, exam, "SAY", test, null, "H2 testi"));
            var edition = await f.Db.Set<OfficialCurriculumEdition>().SingleAsync(x => x.SubjectName == lesson.Name);
            f.Db.Add(OfficialCurriculumLessonBinding.Create(edition.Id, lesson.Id));
        }
        var context = await f.Db.StudentLearningContexts.Include(x => x.CoursePreferences).SingleAsync(); context.SetScope(2026, LearningPurpose.Yks, 2027, "SAY", null);
        context.ReplaceSelections(context.CoursePreferences, [StudentTopicState.Create(context.Id, world.TurkishTopic.Id, DeclaredTopicStatus.Studied, world.Student)], true);
        var profile = await f.Db.CoachingStudentProfiles.SingleAsync(); profile.UpdateEducation(CoachingEducationStatus.HighSchoolGraduate, null, null, null, null, null, null, null);
        var hours = await f.Db.StudyAvailability.SingleAsync(); hours.ReplaceWindows([new(DayOfWeek.Monday, 1020, 1200)]);
        await f.Db.SaveChangesAsync();
        int Minutes(WeeklyProposalView view, StudyCatalogLesson lesson) => view.Lessons.Single(x => x.LessonId == lesson.Id).ProposedMinutes;

        var balanced = await Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace, "balanced"), default);
        Assert.Equal((WeeklyDayLimit.GraduateMinutes, false, "balanced"), (balanced.MaxDayMinutes, balanced.MiddleSchool, balanced.ExamFocus));
        // 17:00–20:00 with a break after two sessions: 17:00 and 18:00 (19:00 would be a third hour in a row).
        Assert.Equal(new[] { 1020, 1080 }, balanced.Slots.Select(x => x.StartMinute));
        Assert.Equal((60, 60), (Minutes(balanced, world.Math), Minutes(balanced, world.Turkish)));

        // One free hour: the larger weighted need gets it; "ayt" puts the AYT lesson first (K6: ×1.3 and ×0.75).
        hours.ReplaceWindows([new(DayOfWeek.Monday, 1020, 1080)]); await f.Db.SaveChangesAsync();
        async Task<Guid> OnlyLesson(string focus) => Assert.Single((await Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace, focus), default)).Slots).LessonId;
        Assert.Equal(world.Math.Id, await OnlyLesson("balanced"));
        Assert.Equal(world.Math.Id, await OnlyLesson("tyt"));
        Assert.Equal(world.Turkish.Id, await OnlyLesson("ayt"));
        var shortage = await Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace, "ayt"), default);
        Assert.True(shortage.ShortageMinutes > 0);
        Assert.True(shortage.Lessons.Single(x => x.LessonId == world.Math.Id).NoTime);

        // A student who does not prepare for YKS has no exam focus.
        context.SetScope(2026, LearningPurpose.Lgs, 2027, null, null); await f.Db.SaveChangesAsync();
        Assert.Null((await Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace, "tyt"), default)).ExamFocus);
    }
}
