using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

// Review of PR #130 (Paket H2): the annual preview's work was extracted into a shared method for the weekly proposal. Its fingerprint
// must not change, or saved previews would be rejected after a release. The values below were computed with the same test on main
// (836bd449, before Paket H2) and must stay equal; every id, date and clock in the scenario is fixed so the fingerprint is stable.
// The scenario has completed work, a declared topic, a lesson preference and a pending pinned task (placement credits).
public sealed class WeeklyOutcomePlanFingerprintTests
{
    private const string EngineOneOnMain = "e7c37d8b58cd37375e95dee65f01cb8cfdcbbba75f7c7f35b63aade86a615642";
    private const string EngineTwoOnMain = "239755fb05f2a8dc8e26f169dc6fca257af21cabfa0eb0c855be3b29e13f166b";

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private static Guid Id(int n) => new($"0000000a-0000-0000-0000-{n:D12}");
    private static T Fixed<T>(T entity, int n) where T : class
    {
        entity.GetType().GetProperty("Id")!.SetValue(entity, Id(n));
        return entity;
    }

    [Fact]
    public async Task ThePreviewFingerprintIsTheSameAsBeforeTheExtraction()
    {
        await using var f = new CurriculumManagementTests.Fixture();
        var student = Id(1); f.User.Roles = ["Student"]; f.User.UserId = student; var reviewer = Id(2);
        var lesson = Fixed(StudyCatalogLesson.Create("approved-catalog", "math", "Matematik", 8, "LGS"), 10); lesson.SetActive(true);
        var unit = Fixed(StudyCatalogUnit.Create("test", "unit", lesson.Id, "Sayılar", 1), 11); unit.SetActive(true);
        var topic = Fixed(StudyCatalogTopic.Create("approved-catalog", "topic:math", lesson.Id, unit.Id, "Çarpanlar", null, 1, 60), 12); topic.SetActive(true);
        var second = Fixed(StudyCatalogTopic.Create("approved-catalog", "topic:math-2", lesson.Id, unit.Id, "Üslü sayılar", null, 2, 60), 13); second.SetActive(true);
        var edition = Fixed(OfficialCurriculumEdition.Create("test-2026", "LGS", 8, "Matematik", 2026, 2026, "https://meb.gov.tr/program", new string('a', 64), "https://meb.gov.tr/scope"), 14);
        var official = Fixed(OfficialCurriculumTopic.Create(edition.Id, "M.8.1.1", "Çarpanlar", 1, 10), 15);
        var outcome = Fixed(StudyCatalogOutcome.Create(edition.Id, official.Id, "M.8.1.1.1", "İlk kazanım metni", "", 73, 1), 16);
        var other = Fixed(StudyCatalogOutcome.Create(edition.Id, official.Id, "M.8.1.1.2", "İkinci kazanım metni", "", 73, 2), 17);
        var estimate = Fixed(OutcomeDurationEstimate.Create(outcome.Id, 1, 20, 30, 10, "ReviewedStudy", "Medium", "İlk süre dayanağı", reviewer), 18); estimate.Publish();
        var otherEstimate = Fixed(OutcomeDurationEstimate.Create(other.Id, 1, 40, 30, 20, "ReviewedStudy", "Medium", "İkinci süre dayanağı", reviewer), 19); otherEstimate.Publish();
        var mapping = StudyCatalogTopicOutcome.Create(topic.Id, outcome.Id, "Onaylı eşleştirme"); mapping.Review(true, "Onaylı eşleştirme", reviewer);
        var otherMapping = StudyCatalogTopicOutcome.Create(second.Id, other.Id, "Onaylı eşleştirme"); otherMapping.Review(true, "Onaylı eşleştirme", reviewer);
        var plan = Fixed(StudyPlanRevision.Create(student, Id(20), 1, "Eski plan"), 21); plan.Activate();
        var task = Fixed(StudyPlanTask.Create(plan, new(2026, 10, 19), "Çarpanlar", 30, topic.Id), 22); task.Complete(25, 8, new(2026, 10, 19));
        var link = StudyPlanTaskOutcome.Create(task.Id, outcome.Id, estimate.Id, 30, "StudentOverride");
        // Final review of PR 130: a pending pinned task carried into the new plan, so the placement credits are fingerprinted too.
        var pinned = Fixed(StudyPlanTask.Create(plan, new(2027, 5, 24), "Sabit kazanım", 15, second.Id, true), 27); pinned.SetWeeklyPlacement(lesson.Id, "Matematik", 1080);
        var pinnedLink = StudyPlanTaskOutcome.Create(pinned.Id, other.Id, otherEstimate.Id, 15, "Catalog");
        var draft = Fixed(WeeklyStudyDraft.Create(student), 23);
        draft.Replace("Europe/Istanbul", [new(Id(24), lesson.Id, lesson.Name, "LGS", DayOfWeek.Monday, 1080, 60), new(Id(25), lesson.Id, lesson.Name, "LGS", DayOfWeek.Thursday, 1110, 30)]);
        var context = Fixed(StudentLearningContext.Create(student, 2026, LearningPurpose.Lgs, 2027, null, null), 26);
        f.Db.AddRange(lesson, unit, topic, second, edition, official, outcome, other, estimate, otherEstimate, mapping, otherMapping, plan, task, link, pinned, pinnedLink, draft, context,
            Coaching.Domain.Profiles.CoachingStudentProfile.Create(student));
        await f.Db.SaveChangesAsync();
        context = await f.Db.StudentLearningContexts.Include(x => x.CoursePreferences).SingleAsync();
        context.ReplaceSelections([StudentCoursePreference.Create(context.Id, lesson.Id, 2, 4)],
            [StudentTopicState.Create(context.Id, second.Id, DeclaredTopicStatus.NeedsReview, student)], true);
        await f.Db.SaveChangesAsync();
        var version = (await f.Db.Set<WeeklyStudyDraft>().AsNoTracking().SingleAsync()).Version;
        var service = new CoachingWeeklyOutcomePlanService(f.Db, new CoachingAccessPolicy(f.User), new WeeklyStudyDraftTests.Catalog(f.Db),
            new FixedClock(new(2027, 5, 24, 6, 0, 0, TimeSpan.Zero)));

        var one = await service.PreviewAsync(new(new(2027, 5, 24), new(2027, 6, 1), version, true), default);
        var two = await service.PreviewAsync(new(new(2027, 5, 24), new(2027, 6, 1), version, true, WeeklyPlanEngine.CurrentVersion), default);
        Assert.Equal(1, two.ProtectedTaskCount);
        Assert.Equal((EngineOneOnMain, EngineTwoOnMain), (one.Fingerprint, two.Fingerprint));
    }
}
