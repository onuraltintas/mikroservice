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
    private static CoachingWeeklyProposalService Proposals(CurriculumManagementTests.Fixture f, CoachingStudyPlanAccess? staff = null, DateTimeOffset? now = null) =>
        new(f.Db, new CoachingAccessPolicy(f.User), new WeeklyStudyDraftTests.Catalog(f.Db), new FixedClock(now ?? Now), staff);
    private static CoachingWeeklyOutcomePlanService Plans(CurriculumManagementTests.Fixture f, DateTimeOffset? now = null) =>
        new(f.Db, new CoachingAccessPolicy(f.User), new WeeklyStudyDraftTests.Catalog(f.Db), new FixedClock(now ?? Now));
    private static object? Body(IActionResult result, string name) { var value = ((ObjectResult)result).Value!; return value.GetType().GetProperty(name)!.GetValue(value); }
    private static Coaching.API.Controllers.CoachingStaffStudyController StaffApi(CurriculumManagementTests.Fixture f, CoachingStudyPlanAccess staff) =>
        new(null!, null!, staff, f.Db, null!, null!, null!, null!);
    private static async Task<CoachingStudyPlanAccess> BecomeTeacher(CurriculumManagementTests.Fixture f, World world)
    {
        f.User.Roles = ["Teacher"]; f.User.UserId = Guid.NewGuid(); await Task.CompletedTask;
        return new(new CoachingAccessPolicy(f.User), new Identity(world.Student), f.Scope);
    }
    private static async Task Apply(CurriculumManagementTests.Fixture f, WeeklyProposalView proposal)
    {
        var drafts = WeeklyStudyDraftTests.Service(f, new(f.Db));
        var draft = await f.Db.Set<WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync();
        var kept = proposal.Mode == WeeklyProposalMode.Fill ? draft?.Slots ?? [] : [];
        await drafts.SaveAsync(new(draft?.Version, null, kept.Concat(proposal.Slots).Select(x => new WeeklySlotInput(x.Id, x.LessonId, x.Day, x.StartMinute, x.DurationMinutes)).ToArray()), default);
    }

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
        var world = await Seed(f); var before = await f.Db.Set<WeeklyStudyDraft>().AsNoTracking().SingleAsync();
        var proposal = await Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace), default);

        Assert.Equal((new DateOnly(2027, 6, 1), 2, WeeklyDayLimit.SchoolMinutes, true), (proposal.EndDate, proposal.WeeksUntilEnd, proposal.MaxDayMinutes, proposal.MiddleSchool));
        // Weekly need = (work + 15 × reviews before the end) ÷ the times the rarest study weekday comes (Wednesday to Friday come once
        // in these 9 days): Matematik 30 + 2 × 15 = 60, Türkçe 180 + 2 × 15 = 210.
        var math = proposal.Lessons.Single(x => x.LessonId == world.Math.Id); var turkish = proposal.Lessons.Single(x => x.LessonId == world.Turkish.Id);
        Assert.Equal((0, 60, 60, false), (math.ExistingMinutes, math.ProposedMinutes, math.NeededMinutes, math.NoTime));
        Assert.Equal((0, 240, 210), (turkish.ExistingMinutes, turkish.ProposedMinutes, turkish.NeededMinutes));
        Assert.Equal((270, 270), (proposal.NeededMinutes, proposal.CoveredMinutes));
        Assert.Equal(before.Version, proposal.DraftVersion);
        Assert.Equal(0, proposal.ShortageMinutes);
        Assert.Equal(new[] { "Matematik", "Türkçe" }, proposal.Lessons.Select(x => x.LessonName));
        Assert.All(proposal.Slots, x => Assert.Equal(60, x.DurationMinutes));
        // Middle school: a break after every session, so 17:00 and 18:30 each weekday; the needed sessions are spread over the days.
        Assert.Equal(10 * 60, proposal.FreeMinutes);
        Assert.Equal(5, proposal.Slots.Select(x => x.Day).Distinct().Count());
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
        var missing = await Assert.ThrowsAsync<StudyAvailabilityMissingException>(() => Proposals(f).ProposeAsync(new(), default));
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
        await Assert.ThrowsAsync<StudyPlanValidationException>(() => Proposals(f).ProposeAsync(new(ExamFocus: "lgs"), default));
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
        // (a short refresh: 45 minutes, so its weekly need is below Matematik's 60).
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

    // Review of PR #130 (5, 13): a teacher may answer the quick questions for a student without study times; they are never saved.
    [Fact]
    public async Task ATeacherProposesFromHerAnswersForAStudentWithoutStudyTimes_NothingIsSaved()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f, availability: false);
        var studentApi = new Coaching.API.Controllers.WeeklyStudyDraftController(WeeklyStudyDraftTests.Service(f, new(f.Db)));
        // The student's own request has no answers: her saved study times are used, and a 404 has the same shape as the teacher's.
        var mine = await studentApi.Proposal(new(), Proposals(f), default);
        Assert.Equal((404, (object?)false, (object?)"Önce müsait saatlerini kaydet."), (((ObjectResult)mine).StatusCode, Body(mine, "success"), Body(mine, "message")));
        var sent = await studentApi.Proposal(new(Windows: [new(DayOfWeek.Monday, 1020, 1140)]), Proposals(f), default);
        Assert.IsType<BadRequestObjectResult>(sent); Assert.Equal(false, Body(sent, "success"));
        await Assert.ThrowsAsync<StudyPlanValidationException>(() => Proposals(f).ProposeAsync(new(Windows: [new(DayOfWeek.Monday, 1020, 1140)]), default));

        var staff = await BecomeTeacher(f, world); var api = StaffApi(f, staff);
        var none = await api.WeeklyProposal(world.Student, new(), Proposals(f, staff), default);
        Assert.Equal((404, (object?)false, (object?)"Öğrenci henüz çalışabileceği saatleri kaydetmemiş."), (((ObjectResult)none).StatusCode, Body(none, "success"), Body(none, "message")));
        foreach (var wrong in new StudyWindowInput[][] { [new(DayOfWeek.Monday, 1020, 1140), new(DayOfWeek.Monday, 1080, 1200)], [new(DayOfWeek.Monday, 1020, 1500)],
                     Enumerable.Range(0, 51).Select(i => new StudyWindowInput(DayOfWeek.Monday, i * 20, i * 20 + 10)).ToArray() })
        {
            var bad = await api.WeeklyProposal(world.Student, new(Windows: wrong), Proposals(f, staff), default);
            Assert.IsType<BadRequestObjectResult>(bad);
            Assert.Equal("Çalışma saatlerini kontrol edin: en çok 50 aralık olabilir; aralıklar gün içinde kalmalı ve birbiriyle çakışmamalı.", Body(bad, "message"));
        }
        var answered = Assert.IsType<OkObjectResult>(await api.WeeklyProposal(world.Student, new(Windows: [new(DayOfWeek.Monday, 1020, 1140), new(DayOfWeek.Tuesday, 1020, 1140)]), Proposals(f, staff), default));
        Assert.NotEmpty(((WeeklyProposalView)Body(answered, "data")!).Slots);
        Assert.Empty(await f.Db.StudyAvailability.ToArrayAsync());

        // With saved study times the teacher's answers are not used.
        var hours = StudyAvailability.Create(world.Student); hours.ReplaceWindows([new(DayOfWeek.Friday, 1020, 1140)]); f.Db.Add(hours); await f.Db.SaveChangesAsync();
        var both = await api.WeeklyProposal(world.Student, new(Windows: [new(DayOfWeek.Monday, 1020, 1140)]), Proposals(f, staff), default);
        Assert.Equal("Öğrencinin kayıtlı çalışma saatleri var; öneri bu saatlerden hazırlanır.", Body(Assert.IsType<BadRequestObjectResult>(both), "message"));
        Assert.All(((WeeklyProposalView)Body(await api.WeeklyProposal(world.Student, new(), Proposals(f, staff), default), "data")!).Slots, x => Assert.Equal(DayOfWeek.Friday, x.Day));
    }

    // Review of PR #130 (7): the plan ends where the annual preview will end it after the proposal is applied: Replace looks at the
    // proposed courses only (not the old cards), Fill also at the kept cards, and a course left without time does not count.
    [Theory]
    [InlineData(WeeklyProposalMode.Replace, 2027, 6, 12)]
    [InlineData(WeeklyProposalMode.Fill, 2027, 5, 29)]
    public async Task ThePlanEndIsTheOneTheAppliedWeekGets(WeeklyProposalMode mode, int year, int month, int day)
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f);
        var chemistry = StudyCatalogLesson.Create("test", "h2-chemistry", "Kimya", null, "TYT"); chemistry.SetActive(true); f.Db.Add(chemistry);
        (await f.Db.Set<StudyCatalogExam>().SingleAsync(x => x.Code == "TYT")).ReplaceSessions([new(null, new DateOnly(2027, 5, 30), "2027", null, null)]);
        (await f.Db.Set<StudyCatalogExam>().SingleAsync(x => x.Code == "LGS")).ReplaceSessions([new(null, new DateOnly(2027, 6, 13), "2027", null, null)]);
        var context = await f.Db.StudentLearningContexts.Include(x => x.CoursePreferences).SingleAsync();
        f.Db.Entry(context).Property(x => x.TargetExamYear).CurrentValue = null;
        context.ReplaceSelections([.. context.CoursePreferences, StudentCoursePreference.Create(context.Id, chemistry.Id, 3, null)], [], false);
        // One free hour a week (Türkçe takes it); the saved draft has a Kimya (TYT) card on Tuesday.
        (await f.Db.StudyAvailability.SingleAsync()).ReplaceWindows([new(DayOfWeek.Monday, 1020, 1080)]);
        var draft = await f.Db.Set<WeeklyStudyDraft>().SingleAsync();
        draft.Replace(draft.TimeZoneId, [new(Guid.NewGuid(), chemistry.Id, "Kimya", "TYT", DayOfWeek.Tuesday, 1080, 30)]);
        await f.Db.SaveChangesAsync();

        var proposal = await Proposals(f).ProposeAsync(new(mode), default);
        Assert.Equal(world.Turkish.Id, Assert.Single(proposal.Slots).LessonId);
        Assert.True(proposal.Lessons.Single(x => x.LessonId == world.Math.Id).NoTime);
        Assert.Equal(new DateOnly(year, month, day), proposal.EndDate);
        await Apply(f, proposal);
        Assert.Equal(proposal.EndDate, (await Plans(f).OptionsAsync(default)).EndDate);
    }

    // Review of PR #130 (14): a week without shortage fits the annual preview, also when some weekdays come fewer times before the end.
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public async Task AWeekWithoutShortageFitsTheAnnualPreview(int daysLater)
    {
        await using var f = new CurriculumManagementTests.Fixture(); await Seed(f);
        (await f.Db.StudyAvailability.SingleAsync()).ReplaceWindows(WeeklyProposalEngine.Week.Select(day => new StudyAvailabilityWindow(day, 480, 1320)));
        await f.Db.SaveChangesAsync();
        var now = Now.AddDays(daysLater); var today = DateOnly.FromDateTime(now.UtcDateTime);
        var proposal = await Proposals(f, now: now).ProposeAsync(new(WeeklyProposalMode.Replace), default);
        Assert.Equal(0, proposal.ShortageMinutes);
        Assert.Equal(proposal.NeededMinutes, proposal.CoveredMinutes);
        await Apply(f, proposal);
        var version = (await f.Db.Set<WeeklyStudyDraft>().AsNoTracking().SingleAsync()).Version;
        var preview = await Plans(f, now).PreviewAsync(new(today, proposal.EndDate, version, true, WeeklyPlanEngine.CurrentVersion), default);
        Assert.True(preview.CanCreate);
        Assert.All(preview.Schedule.Lessons, x => Assert.Equal(0, x.RemainingMinutes));
    }

    // Review of PR #130 (3): a day before the exam the need is the remaining work, not seven times it.
    [Fact]
    public async Task OneDayBeforeTheEndTheNeedIsTheRemainingWork()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f);
        var proposal = await Proposals(f, now: Now.AddDays(8)).ProposeAsync(new(WeeklyProposalMode.Replace), default);
        Assert.Equal(1, proposal.WeeksUntilEnd);
        Assert.Equal((30, 180), (proposal.Lessons.Single(x => x.LessonId == world.Math.Id).NeededMinutes, proposal.Lessons.Single(x => x.LessonId == world.Turkish.Id).NeededMinutes));
    }

    // Final review of PR 130 (10): only missing study times get the teacher's "no study times" message.
    [Fact]
    public async Task OnlyMissingStudyTimesGetTheTeachersStudyTimesMessage()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f, availability: false);
        var staff = await BecomeTeacher(f, world); var api = StaffApi(f, staff);
        var missing = await api.WeeklyProposal(world.Student, new(), Proposals(f, staff), default);
        Assert.Equal("Öğrenci henüz çalışabileceği saatleri kaydetmemiş.", Body(missing, "message"));
        var other = await api.WeeklyProposal(world.Student, new(), new Throwing(new KeyNotFoundException("Başka bir kayıt yok.")), default);
        Assert.IsType<NotFoundResult>(other);
    }
    private sealed class Throwing(Exception error) : IWeeklyProposalService
    {
        public Task<WeeklyProposalView> ProposeAsync(WeeklyProposalRequest request, CancellationToken ct) => throw error;
        public Task<WeeklyProposalView> ProposeForStudentAsync(Guid studentId, WeeklyProposalRequest request, CancellationToken ct) => throw error;
        public Task<WeeklyTempoOptionsView> TempoOptionsAsync(WeeklyTempoOptionsRequest request, CancellationToken ct) => throw error;
        public Task<WeeklyTempoOptionsView> TempoOptionsForStudentAsync(Guid studentId, WeeklyTempoOptionsRequest request, CancellationToken ct) => throw error;
    }

    // ---- Paket H4a: the single-flow wizard (finishBy, tempo options, day budgets) ----

    private static (int Math, int Turkish) Need(WeeklyProposalView view, World world) =>
        (view.Lessons.Single(x => x.LessonId == world.Math.Id).NeededMinutes, view.Lessons.Single(x => x.LessonId == world.Turkish.Id).NeededMinutes);
    private static Coaching.API.Controllers.WeeklyStudyDraftController StudentApi(CurriculumManagementTests.Fixture f) => new(WeeklyStudyDraftTests.Service(f, new(f.Db)));

    /// <summary>
    /// Review of PR #132 (B1, S4): every tempo option is the proposal the wizard will apply with that finishBy and mode: the same need,
    /// fits exactly when that proposal has no shortage, and the capacity is what it counts (free sessions + the saved minutes it counts).
    /// </summary>
    private static async Task AssertTheProposalAgrees(WeeklyTempoOptionsView view, Func<WeeklyProposalRequest, Task<WeeklyProposalView>> propose,
        WeeklyProposalMode mode = WeeklyProposalMode.Fill)
    {
        foreach (var option in view.Options)
        {
            var proposal = await propose(new(mode, FinishBy: option.FinishBy));
            Assert.Equal((proposal.NeededMinutes, proposal.ShortageMinutes == 0, proposal.FreeMinutes + proposal.Lessons.Sum(x => x.ExistingMinutes)),
                (option.NeededWeeklyMinutes, option.Fits, option.CapacityWeeklyMinutes));
            Assert.Equal(WeeklyProposalEngine.PeriodEnd(option.FinishBy, proposal.EndDate), proposal.FinishBy);
        }
    }
    private static async Task FarExam(CurriculumManagementTests.Fixture f)
    {
        (await f.Db.Set<StudyCatalogExam>().SingleAsync(x => x.Code == "LGS")).ReplaceSessions([new(null, new DateOnly(2027, 8, 2), "2027", null, null)]);
        var context = await f.Db.StudentLearningContexts.SingleAsync(); f.Db.Entry(context).Property(x => x.TargetExamYear).CurrentValue = null;
        await f.Db.SaveChangesAsync();
    }

    // finishBy: the weekly need is spread over today → finishBy (the times each study weekday still comes and the reviews inside it);
    // the plan end stays where it is and the date used is echoed.
    [Fact]
    public async Task FinishByComputesTheNeedUntilThatDay_AndIsEchoed()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f);
        (await f.Db.StudyAvailability.SingleAsync()).ReplaceWindows([new(DayOfWeek.Monday, 1020, 1200), new(DayOfWeek.Tuesday, 1020, 1200)]);
        await f.Db.SaveChangesAsync();
        Task<WeeklyProposalView> Propose(DateOnly? finishBy) => Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace, FinishBy: finishBy), default);

        // Until the plan end (Tuesday 1 June) Monday and Tuesday come twice: (30 + 2 × 15) ÷ 2 → 30, (180 + 2 × 15) ÷ 2 → 120.
        var plain = await Propose(null);
        Assert.Null(plain.FinishBy);
        Assert.Equal((30, 120), Need(plain, world));
        var end = await Propose(new DateOnly(2027, 6, 1));
        Assert.Equal((new DateOnly(2027, 6, 1), (30, 120)), (end.FinishBy, Need(end, world)));
        // Until Monday 31 May Tuesday comes only once, so each Tuesday hour must carry twice as much: 60 and 210.
        var monday = await Propose(new DateOnly(2027, 5, 31));
        Assert.Equal((new DateOnly(2027, 5, 31), (60, 210)), (monday.FinishBy, Need(monday, world)));
        Assert.True(monday.NeededMinutes > plain.NeededMinutes);
        // Until Sunday 30 May (today + 6) the 7-day review no longer falls inside: 30 + 15 → 60, 180 + 15 → 210.
        var sunday = await Propose(new DateOnly(2027, 5, 30));
        Assert.Equal((new DateOnly(2027, 5, 30), (60, 210)), (sunday.FinishBy, Need(sunday, world)));
        Assert.Equal((new DateOnly(2027, 6, 1), 2), (sunday.EndDate, sunday.WeeksUntilEnd));
        var ok = Assert.IsType<OkObjectResult>(await StudentApi(f).Proposal(new(WeeklyProposalMode.Replace, FinishBy: new DateOnly(2027, 5, 30)), Proposals(f), default));
        Assert.Equal(new DateOnly(2027, 5, 30), ((WeeklyProposalView)Body(ok, "data")!).FinishBy);
    }

    [Fact]
    public async Task FinishByIsAtLeastSixDaysAwayAndNotAfterThePlanEnd()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f);
        const string Soon = "Bitiş tarihi bugünden en az 6 gün sonra olmalı.";
        foreach (var (day, message) in new[] { (new DateOnly(2027, 5, 29), Soon), (new DateOnly(2027, 5, 24), Soon),
                     (new DateOnly(2027, 6, 2), "Bitiş tarihi en geç 01.06.2027 olabilir; planın o gün bitiyor.") })
        {
            Assert.Equal(message, (await Assert.ThrowsAsync<StudyPlanValidationException>(() => Proposals(f).ProposeAsync(new(FinishBy: day), default))).Message);
            var bad = Assert.IsType<BadRequestObjectResult>(await StudentApi(f).Proposal(new(FinishBy: day), Proposals(f), default));
            Assert.Equal((false, (object?)message), (Body(bad, "success"), Body(bad, "message")));
        }
        // Near the end, when today + 6 is after the plan end, the plan end itself is still accepted (it changes nothing).
        Assert.Equal(new DateOnly(2027, 6, 1), (await Proposals(f, now: Now.AddDays(5)).ProposeAsync(new(FinishBy: new DateOnly(2027, 6, 1)), default)).FinishBy);
        await Assert.ThrowsAsync<StudyPlanValidationException>(() => Proposals(f, now: Now.AddDays(5)).ProposeAsync(new(FinishBy: new DateOnly(2027, 5, 31)), default));
        // After the plan end and before today + 6 at once: the message names the latest date (review of PR #132).
        Assert.Equal("Bitiş tarihi en geç 01.06.2027 olabilir; planın o gün bitiyor.",
            (await Assert.ThrowsAsync<StudyPlanValidationException>(() => Proposals(f, now: Now.AddDays(5)).ProposeAsync(new(FinishBy: new DateOnly(2027, 6, 2)), default))).Message);

        // The teacher reads about the student in the third person.
        var staff = await BecomeTeacher(f, world);
        var late = await StaffApi(f, staff).WeeklyProposal(world.Student, new(FinishBy: new DateOnly(2027, 6, 2)), Proposals(f, staff), default);
        Assert.Equal("Bitiş tarihi en geç 01.06.2027 olabilir; öğrencinin planı o gün bitiyor.", Body(Assert.IsType<BadRequestObjectResult>(late), "message"));
        var soon = await StaffApi(f, staff).WeeklyProposal(world.Student, new(FinishBy: new DateOnly(2027, 5, 25)), Proposals(f, staff), default);
        Assert.Equal(Soon, Body(Assert.IsType<BadRequestObjectResult>(soon), "message"));
    }

    // Tempo options: Rahat ends at the plan end, Dengeli after 85 % and Yoğun after 70 % of the days; each with the weekly need for its
    // period and the weekly capacity of the free hours.
    [Fact]
    public async Task TempoOptionsGiveThreeEndDatesWithTheirWeeklyNeedAndCapacity()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f); await FarExam(f);
        var draftBefore = await f.Db.Set<WeeklyStudyDraft>().AsNoTracking().SingleAsync();

        var view = await Proposals(f).TempoOptionsAsync(new(), default);
        // 69 days to Sunday 1 August: Dengeli 24 May + 59 = 22 July, Yoğun 24 May + 48 = 11 July.
        Assert.Equal(new DateOnly(2027, 8, 1), view.PlanEnd);
        Assert.Equal(new[] { (StudyTempo.Relaxed, new DateOnly(2027, 8, 1)), (StudyTempo.Balanced, new DateOnly(2027, 7, 22)), (StudyTempo.Intense, new DateOnly(2027, 7, 11)) },
            view.Options.Select(x => (x.Tempo, x.FinishBy)));
        // Rahat: every weekday comes 10 times: (30 + 3 × 15) ÷ 10 → 30 and (180 + 45) ÷ 10 → 30. Dengeli: Friday comes 8 times → 30 + 30.
        // Yoğun: 7 times → 30 + 60.
        Assert.Equal(new[] { 60, 60, 90 }, view.Options.Select(x => x.NeededWeeklyMinutes));
        // Middle school, 17:00–20:00 on weekdays: two sessions a day, but Monday's saved 18:00 card leaves only 19:00: 9 × 60 + 30 saved.
        Assert.All(view.Options, x => Assert.Equal((570, true), (x.CapacityWeeklyMinutes, x.Fits)));
        // Each option is the proposal for its finishBy; nothing is saved.
        await AssertTheProposalAgrees(view, r => Proposals(f).ProposeAsync(r, default));
        var draftAfter = await f.Db.Set<WeeklyStudyDraft>().AsNoTracking().SingleAsync();
        Assert.Equal((draftBefore.Version, draftBefore.Slots.Count), (draftAfter.Version, draftAfter.Slots.Count));

        // One free hour a week (Monday) and no saved cards. 60 needed minutes and 60 free ones, but in hour sessions one of the two
        // lessons gets no time: no option fits (review of PR #132, B1).
        (await f.Db.StudyAvailability.SingleAsync()).ReplaceWindows([new(DayOfWeek.Monday, 1020, 1080)]);
        f.Db.Remove(await f.Db.Set<WeeklyStudyDraft>().SingleAsync()); await f.Db.SaveChangesAsync();
        var tight = await Proposals(f).TempoOptionsAsync(new(), default);
        Assert.Equal(new[] { (60, 60, false), (60, 60, false), (90, 60, false) }, tight.Options.Select(x => (x.NeededWeeklyMinutes, x.CapacityWeeklyMinutes, x.Fits)));
        await AssertTheProposalAgrees(tight, r => Proposals(f).ProposeAsync(r, default));

        // The API sends the names and dates the wizard reads.
        var ok = Assert.IsType<OkObjectResult>(await StudentApi(f).TempoOptions(new(), Proposals(f), default));
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web); Coaching.API.CoachingApiJson.Configure(json);
        var text = System.Text.Json.JsonSerializer.Serialize(Body(ok, "data"), json);
        Assert.StartsWith("{\"planEnd\":\"2027-08-01\",\"options\":[{\"tempo\":\"Relaxed\",\"finishBy\":\"2027-08-01\",\"neededWeeklyMinutes\":60,\"capacityWeeklyMinutes\":60,\"fits\":false}", text);
    }

    [Theory]
    [InlineData(1, new[] { StudyTempo.Relaxed, StudyTempo.Balanced }, new[] { 1, 31 })]
    [InlineData(5, new[] { StudyTempo.Relaxed }, new[] { 1 })]
    [InlineData(8, new[] { StudyTempo.Relaxed }, new[] { 1 })]
    public async Task OptionsWithTheSameEndAreMergedNearTheExam(int daysLater, StudyTempo[] tempos, int[] days)
    {
        // Tuesday 25 May: 7 days to 1 June; Dengeli 25 May + 6 = 31 May, Yoğun 25 May + 5 is raised to today + 6 = 31 May and merged
        // into Dengeli. Later every end is the plan end.
        await using var f = new CurriculumManagementTests.Fixture(); await Seed(f);
        var view = await Proposals(f, now: Now.AddDays(daysLater)).TempoOptionsAsync(new(), default);
        Assert.Equal(tempos, view.Options.Select(x => x.Tempo));
        Assert.Equal(days, view.Options.Select(x => x.FinishBy.Day));
        Assert.All(view.Options, x => Assert.True(x.FinishBy <= view.PlanEnd));
        await AssertTheProposalAgrees(view, r => Proposals(f, now: Now.AddDays(daysLater)).ProposeAsync(r, default));
    }

    // Review of PR #132 (B1): saved cards count as the proposal counts them: a card outside the study times still counts for its lesson,
    // a card of a lesson no longer chosen does not, and minutes beyond a lesson's need do not help another lesson.
    [Fact]
    public async Task SavedCardsCountForTheTempoOptionsAsTheProposalCountsThem()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f); await FarExam(f);
        (await f.Db.StudyAvailability.SingleAsync()).ReplaceWindows([.. new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday }
            .Select(day => new StudyAvailabilityWindow(day, 1020, 1080))]);
        var draft = await f.Db.Set<WeeklyStudyDraft>().SingleAsync();
        draft.Replace(draft.TimeZoneId, [new(Guid.NewGuid(), world.Math.Id, "Matematik", "LGS", DayOfWeek.Monday, 1260, 60),   // outside the study times
            new(Guid.NewGuid(), world.Math.Id, "Matematik", "LGS", DayOfWeek.Tuesday, 1020, 60),
            new(Guid.NewGuid(), world.Math.Id, "Matematik", "LGS", DayOfWeek.Wednesday, 1020, 60),                            // beyond Matematik's 30
            new(Guid.NewGuid(), Guid.NewGuid(), "Fizik", "LGS", DayOfWeek.Thursday, 1020, 60)]);                                 // no longer chosen
        await f.Db.SaveChangesAsync();
        var view = await Proposals(f).TempoOptionsAsync(new(), default);
        // No free hour is left; the 180 saved Matematik minutes count (Fizik's do not), yet Türkçe gets no time, so nothing fits.
        Assert.All(view.Options, x => Assert.Equal((180, false), (x.CapacityWeeklyMinutes, x.Fits)));
        Assert.All(view.Options, x => Assert.True(x.NeededWeeklyMinutes <= x.CapacityWeeklyMinutes));
        await AssertTheProposalAgrees(view, r => Proposals(f).ProposeAsync(r, default));
        // Replace mode starts from an empty week: three free hours, both lessons get time.
        var replace = await Proposals(f).TempoOptionsAsync(new(Mode: WeeklyProposalMode.Replace), default);
        Assert.All(replace.Options, x => Assert.Equal((180, true), (x.CapacityWeeklyMinutes, x.Fits)));
        await AssertTheProposalAgrees(replace, r => Proposals(f).ProposeAsync(r, default), WeeklyProposalMode.Replace);
    }

    // Review of PR #132 (S4): the options use the proposal's own plan end for the mode asked, also when settling changes it.
    [Fact]
    public async Task TempoOptionsSettleThePlanEndLikeTheProposalInEachMode()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f);
        var chemistry = StudyCatalogLesson.Create("test", "h4-chemistry", "Kimya", null, "TYT"); chemistry.SetActive(true); f.Db.Add(chemistry);
        (await f.Db.Set<StudyCatalogExam>().SingleAsync(x => x.Code == "TYT")).ReplaceSessions([new(null, new DateOnly(2027, 5, 30), "2027", null, null)]);
        (await f.Db.Set<StudyCatalogExam>().SingleAsync(x => x.Code == "LGS")).ReplaceSessions([new(null, new DateOnly(2027, 6, 13), "2027", null, null)]);
        var context = await f.Db.StudentLearningContexts.Include(x => x.CoursePreferences).SingleAsync();
        f.Db.Entry(context).Property(x => x.TargetExamYear).CurrentValue = null;
        context.ReplaceSelections([.. context.CoursePreferences, StudentCoursePreference.Create(context.Id, chemistry.Id, 3, null)], [], false);
        (await f.Db.StudyAvailability.SingleAsync()).ReplaceWindows([new(DayOfWeek.Monday, 1020, 1080)]);
        var draft = await f.Db.Set<WeeklyStudyDraft>().SingleAsync();
        draft.Replace(draft.TimeZoneId, [new(Guid.NewGuid(), chemistry.Id, "Kimya", "TYT", DayOfWeek.Tuesday, 1080, 30)]);
        await f.Db.SaveChangesAsync();

        // Replace: the proposed week has LGS lessons only, so the plan ends on 12 June (the chosen lessons alone would end it on 29 May).
        var replace = await Proposals(f).TempoOptionsAsync(new(Mode: WeeklyProposalMode.Replace), default);
        Assert.Equal(new DateOnly(2027, 6, 12), replace.PlanEnd);
        Assert.Equal(new[] { new DateOnly(2027, 6, 12), new DateOnly(2027, 6, 9), new DateOnly(2027, 6, 6) }, replace.Options.Select(x => x.FinishBy));
        Assert.Equal(replace.PlanEnd, (await Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace), default)).EndDate);
        await AssertTheProposalAgrees(replace, r => Proposals(f).ProposeAsync(r, default), WeeklyProposalMode.Replace);
        // Fill keeps the TYT card: the plan ends on 29 May, so only Rahat is left.
        var fill = await Proposals(f).TempoOptionsAsync(new(), default);
        Assert.Equal((new DateOnly(2027, 5, 29), StudyTempo.Relaxed), (fill.PlanEnd, Assert.Single(fill.Options).Tempo));
        await AssertTheProposalAgrees(fill, r => Proposals(f).ProposeAsync(r, default));
        // A finishBy up to the Replace plan end is valid there; Fill refuses it with its own latest date.
        Assert.Equal(new DateOnly(2027, 6, 9), (await Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace, FinishBy: new DateOnly(2027, 6, 9)), default)).FinishBy);
        Assert.Equal("Bitiş tarihi en geç 29.05.2027 olabilir; planın o gün bitiyor.",
            (await Assert.ThrowsAsync<StudyPlanValidationException>(() => Proposals(f).ProposeAsync(new(FinishBy: new DateOnly(2027, 6, 9)), default))).Message);
        await Assert.ThrowsAsync<StudyPlanValidationException>(() => Proposals(f).TempoOptionsAsync(new(Mode: (WeeklyProposalMode)5), default));
    }

    // Day budgets: with a routine profile whose free hours are still the ones it wrote, each day holds at most its budget (and the
    // daily limit); a day without a budget gets nothing. Otherwise nothing changes.
    [Fact]
    public async Task AMatchingRoutineCapsEachDayAtItsBudget_ANonMatchingOneChangesNothing()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f);
        var hours = await f.Db.StudyAvailability.SingleAsync();
        // Weekdays 60 minutes, Wednesday off (a busy day), no weekend.
        var answers = StudyRoutineTests.Morning() with { WeekdayMinutes = 60, BusyDays = [DayOfWeek.Wednesday], BusyDaysOff = true,
            Weekend = StudyWeekendDays.None, WeekendStart = null, WeekendEnd = null, WeekendMinutes = null };
        var json = new System.Text.Json.JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
        var routine = StudyRoutineProfile.Create(world.Student); routine.Replace(System.Text.Json.JsonSerializer.Serialize(answers, json), hours.Version);
        f.Db.Add(routine); await f.Db.SaveChangesAsync();

        var capped = await Proposals(f).ProposeAsync(new(WeeklyProposalMode.Replace), default);
        Assert.DoesNotContain(capped.Slots, x => x.Day == DayOfWeek.Wednesday);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday }, capped.Slots.Select(x => x.Day));
        Assert.Equal(240, capped.FreeMinutes);
        Assert.True(capped.ShortageMinutes > 0);
        // Fill: Monday's saved 30 minutes leave no room for an hour within its 60.
        var fill = await Proposals(f).ProposeAsync(new(), default);
        Assert.DoesNotContain(fill.Slots, x => x.Day is DayOfWeek.Monday or DayOfWeek.Wednesday);
        // The tempo options count the same: Tuesday, Thursday and Friday an hour each + Monday's saved 30 minutes.
        var budgeted = await Proposals(f).TempoOptionsAsync(new(), default);
        Assert.All(budgeted.Options, x => Assert.Equal(210, x.CapacityWeeklyMinutes));
        await AssertTheProposalAgrees(budgeted, r => Proposals(f).ProposeAsync(r, default));
        // The teacher sees the same proposal.
        var staff = await BecomeTeacher(f, world);
        Assert.Equal(capped.Slots.Select(x => (x.LessonId, x.Day, x.StartMinute)),
            (await Proposals(f, staff).ProposeForStudentAsync(world.Student, new(WeeklyProposalMode.Replace), default)).Slots.Select(x => (x.LessonId, x.Day, x.StartMinute)));

        // The free hours were edited by hand after the routine: the budgets no longer apply.
        f.Db.Entry(routine).Property(x => x.AvailabilityVersion).CurrentValue = hours.Version + 1; await f.Db.SaveChangesAsync();
        var free = await Proposals(f, staff).ProposeForStudentAsync(world.Student, new(WeeklyProposalMode.Replace), default);
        Assert.Equal(10 * 60, free.FreeMinutes);
        Assert.Contains(free.Slots, x => x.Day == DayOfWeek.Wednesday);
    }

    [Fact]
    public async Task TheTeacherGetsTempoOptions_WithHerAnswersOnlyForAStudentWithoutStudyTimes()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var world = await Seed(f, availability: false);
        var mine = await StudentApi(f).TempoOptions(new(), Proposals(f), default);
        Assert.Equal((404, (object?)false, (object?)"Önce müsait saatlerini kaydet."), (((ObjectResult)mine).StatusCode, Body(mine, "success"), Body(mine, "message")));
        var sent = await StudentApi(f).TempoOptions(new(Windows: [new(DayOfWeek.Monday, 1020, 1140)]), Proposals(f), default);
        Assert.Equal("Öneri kayıtlı çalışma saatlerinden hazırlanır; saatlerini Çalışma tercihlerimde değiştirebilirsin.", Body(Assert.IsType<BadRequestObjectResult>(sent), "message"));
        Assert.IsType<BadRequestObjectResult>(await StudentApi(f).TempoOptions(new("lgs"), Proposals(f), default));

        var staff = await BecomeTeacher(f, world); var api = StaffApi(f, staff);
        var none = await api.WeeklyTempoOptions(world.Student, new(), Proposals(f, staff), default);
        Assert.Equal((404, (object?)"Öğrenci henüz çalışabileceği saatleri kaydetmemiş."), (((ObjectResult)none).StatusCode, Body(none, "message")));
        var wrong = await api.WeeklyTempoOptions(world.Student, new(Windows: [new(DayOfWeek.Monday, 1020, 1500)]), Proposals(f, staff), default);
        Assert.Equal("Çalışma saatlerini kontrol edin: en çok 50 aralık olabilir; aralıklar gün içinde kalmalı ve birbiriyle çakışmamalı.", Body(Assert.IsType<BadRequestObjectResult>(wrong), "message"));
        // Monday and Tuesday 17:00–19:00 (middle school): Tuesday 17:00 is free; Monday's saved 18:00 card leaves no hour but counts 30.
        var answered = Assert.IsType<OkObjectResult>(await api.WeeklyTempoOptions(world.Student,
            new(Windows: [new(DayOfWeek.Monday, 1020, 1140), new(DayOfWeek.Tuesday, 1020, 1140)]), Proposals(f, staff), default));
        var view = (WeeklyTempoOptionsView)Body(answered, "data")!;
        Assert.Equal(3, view.Options.Count);
        Assert.All(view.Options, x => Assert.Equal(90, x.CapacityWeeklyMinutes));
        await AssertTheProposalAgrees(view, r => Proposals(f, staff).ProposeForStudentAsync(world.Student, r with { Windows = [new(DayOfWeek.Monday, 1020, 1140), new(DayOfWeek.Tuesday, 1020, 1140)] }, default));
        Assert.Empty(await f.Db.StudyAvailability.ToArrayAsync());
        await Assert.ThrowsAsync<BusinessRuleException>(() => Proposals(f, staff).TempoOptionsForStudentAsync(Guid.NewGuid(), new(), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Proposals(f, staff).TempoOptionsAsync(new(), default));

        // With saved study times the teacher's answers are not used.
        var hours = StudyAvailability.Create(world.Student); hours.ReplaceWindows([new(DayOfWeek.Friday, 1020, 1140)]); f.Db.Add(hours); await f.Db.SaveChangesAsync();
        var both = await api.WeeklyTempoOptions(world.Student, new(Windows: [new(DayOfWeek.Monday, 1020, 1140)]), Proposals(f, staff), default);
        Assert.Equal("Öğrencinin kayıtlı çalışma saatleri var; öneri bu saatlerden hazırlanır.", Body(Assert.IsType<BadRequestObjectResult>(both), "message"));
        Assert.All(((WeeklyTempoOptionsView)Body(Assert.IsType<OkObjectResult>(await api.WeeklyTempoOptions(world.Student, new(), Proposals(f, staff), default)), "data")!).Options,
            x => Assert.Equal(90, x.CapacityWeeklyMinutes));   // Friday's hour + Monday's saved card, which still counts outside the study times
    }
}
