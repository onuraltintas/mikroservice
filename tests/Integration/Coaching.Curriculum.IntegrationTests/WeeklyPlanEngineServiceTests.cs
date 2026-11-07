using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap O7: the weekly outcome plan with engine version 2.
public sealed class WeeklyPlanEngineServiceTests
{
    private static readonly WeeklyOutcomePlanRequest Input = new(new(2027, 5, 24), new(2027, 6, 1), 0, false, WeeklyPlanEngine.CurrentVersion);

    [Fact]
    public async Task SavedPlanCarriesKindsReasonsQuestionTargetsAndSpacedReviewsWithoutCurriculumCredit()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var service = await WeeklyOutcomePlanTests.Seed(f);
        var preview = await service.PreviewAsync(Input, default);
        Assert.True(preview.CanCreate);
        var learn = Assert.Single(preview.Schedule.Tasks.Where(x => x.Kind == "Learn"));
        Assert.Equal(new DateOnly(2027, 5, 24), learn.Date);
        var spaced = preview.Schedule.Tasks.Where(x => x.Kind == "Spaced").ToArray();
        Assert.Equal(2, spaced.Length); // +1 and +7 days, both in the next Monday hour.
        Assert.All(spaced, x => Assert.Equal(new DateOnly(2027, 5, 31), x.Date));

        var created = await service.CreateAsync(new("Kurallı plan", Input, preview.Fingerprint), default);
        var tasks = await f.Db.StudyPlanTasks.Where(x => x.RevisionId == created.Id).OrderBy(x => x.PlannedDate).ThenBy(x => x.PlannedStartMinute).ToArrayAsync();
        Assert.Equal(new[] { "Learn", "Spaced", "Spaced" }, tasks.Select(x => x.PlanKind));
        Assert.All(tasks, x => Assert.False(string.IsNullOrWhiteSpace(x.PlanReason)));
        Assert.All(tasks, x => Assert.NotNull(x.PlannedQuestions));
        var ids = tasks.Select(x => x.Id).ToArray();
        var links = await f.Db.Set<StudyPlanTaskOutcome>().Where(x => Enumerable.Contains(ids, x.TaskId)).ToArrayAsync();
        Assert.Equal(tasks[0].Id, Assert.Single(links).TaskId);

        var context = await f.Db.StudentLearningContexts.SingleAsync(); var topic = await f.Db.StudyCatalogTopics.SingleAsync();
        context.ReplaceSelections(context.CoursePreferences, [StudentTopicState.Create(context.Id, topic.Id, DeclaredTopicStatus.Studied, f.User.UserId!.Value)], true);
        f.Db.RemoveRange(links); f.Db.RemoveRange(f.Db.StudyPlanTasks.Where(x => x.RevisionId == created.Id)); await f.Db.SaveChangesAsync();
        f.Db.RemoveRange(f.Db.StudyPlanRevisions.Where(x => x.Id == created.Id)); await f.Db.SaveChangesAsync();
        var refresh = await service.PreviewAsync(Input, default);
        var light = Assert.Single(refresh.Schedule.Tasks.Where(x => x.Kind == "Refresh"));
        // A quarter of the 60-minute estimate; earlier completed work does not cancel the refresh.
        Assert.Equal(15, light.Minutes);
        Assert.Contains("Çalıştım", light.Reason);
        var legacy = await service.PreviewAsync(Input with { EngineVersion = 1 }, default);
        Assert.Empty(legacy.Schedule.Tasks);
    }

    [Fact]
    public async Task PlanEndsBeforeTheExamDate_AndTheStudentMayChooseHerOwnDate()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var service = await WeeklyOutcomePlanTests.Seed(f);
        var exam = await f.Db.Set<StudyCatalogExam>().SingleAsync(x => x.Code == "LGS");
        exam.ReplaceSessions([new(null, new DateOnly(2027, 6, 13), "2027", null, null)]); await f.Db.SaveChangesAsync();
        var options = await service.OptionsAsync(default);
        Assert.Equal((new DateOnly(2027, 6, 12), (DateOnly?)new DateOnly(2027, 6, 13), "Catalog"), (options.EndDate, options.ExamDate, options.EndDateSource));

        await service.SetExamDateAsync(new(new DateOnly(2027, 6, 20)), default);
        options = await service.OptionsAsync(default);
        Assert.Equal((new DateOnly(2027, 6, 19), (DateOnly?)new DateOnly(2027, 6, 20), "Student"), (options.EndDate, options.ExamDate, options.EndDateSource));
        Assert.Equal(new DateOnly(2027, 6, 13), options.CatalogExamDate);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetExamDateAsync(new(options.StartDate.AddDays(-1)), default));
        await service.SetExamDateAsync(new(null), default);
        Assert.Equal("Catalog", (await service.OptionsAsync(default)).EndDateSource);

        // G2 (#40): the chosen year (2027) with no session known at all: an estimated 15 June of that year, not 1 June.
        exam.ReplaceSessions([]); await f.Db.SaveChangesAsync();
        options = await service.OptionsAsync(default);
        Assert.Equal((new DateOnly(2027, 6, 14), (DateOnly?)new DateOnly(2027, 6, 15), "Estimated"), (options.EndDate, options.ExamDate, options.EndDateSource));
        // Without an exam year (no study settings) the plan still ends on the default 1 June.
        f.Db.RemoveRange(f.Db.StudentLearningContexts); await f.Db.SaveChangesAsync();
        options = await service.OptionsAsync(default);
        Assert.Equal((WeeklyOutcomeScheduler.DefaultEnd(options.StartDate), "Default"), (options.EndDate, options.EndDateSource));
    }

    [Fact]
    // G2 (#40, user decision 07.11.2026): an 11th grader targets YKS/LGS 2028 while the catalog only has the 2027 session. The plan
    // and its "Resmî tarihe dön" use the 2027 session's month and day carried to 2028, shown as an estimate; once the catalog has a
    // 2028 session it is used.
    public async Task AYearOnlyTargetIsNotCountedDownToAnEarlierYearsSession()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var service = await WeeklyOutcomePlanTests.Seed(f);
        var exam = await f.Db.Set<StudyCatalogExam>().SingleAsync(x => x.Code == "LGS");
        exam.ReplaceSessions([new(null, new DateOnly(2027, 6, 13), "2027", null, null)]);
        var context = await f.Db.StudentLearningContexts.SingleAsync();
        context.SetScope(2026, LearningPurpose.Lgs, 2028, null, null); await f.Db.SaveChangesAsync();

        var options = await service.OptionsAsync(default);
        Assert.Equal((new DateOnly(2028, 6, 12), (DateOnly?)new DateOnly(2028, 6, 13), "Estimated", (DateOnly?)new DateOnly(2028, 6, 13), true),
            (options.EndDate, options.ExamDate, options.EndDateSource, options.CatalogExamDate, options.CatalogExamDateEstimated));
        // The exam list still names the catalog's next session (for reference only).
        Assert.Equal(new DateOnly(2027, 6, 13), Assert.Single(options.Exams).Date);

        exam.ReplaceSessions([new(exam.Sessions.Single().Id, new DateOnly(2027, 6, 13), "2027", null, null), new(null, new DateOnly(2028, 6, 11), "2028", null, null)]);
        await f.Db.SaveChangesAsync();
        options = await service.OptionsAsync(default);
        Assert.Equal(((DateOnly?)new DateOnly(2028, 6, 11), "Catalog", false), (options.ExamDate, options.EndDateSource, options.CatalogExamDateEstimated));
    }

    [Fact]
    // KM1 (01.11.2026): a student who picked one of several sessions plans up to that session; an own date still wins.
    public async Task PlanEndsBeforeTheChosenSession()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var service = await WeeklyOutcomePlanTests.Seed(f);
        var lgs = await f.Db.Set<StudyCatalogExam>().SingleAsync(x => x.Code == "LGS");
        lgs.ReplaceSessions([new(null, new DateOnly(2027, 6, 13), "2027", null, null)]);
        var yds = StudyCatalogExam.Create("YDS", "YDS");
        yds.ReplaceSessions([new(null, new DateOnly(2027, 3, 28), "2027/1", null, null), new(null, new DateOnly(2027, 11, 21), "2027/2", null, null)]);
        f.Db.Add(yds); await f.Db.SaveChangesAsync();
        var context = await f.Db.StudentLearningContexts.SingleAsync();
        context.SetTargetExamSession(yds.Sessions.Single(x => x.Label == "2027/2").Id); await f.Db.SaveChangesAsync();
        var options = await service.OptionsAsync(default);
        Assert.Equal((new DateOnly(2027, 11, 20), (DateOnly?)new DateOnly(2027, 11, 21), "Session", (DateOnly?)new DateOnly(2027, 6, 13)),
            (options.EndDate, options.ExamDate, options.EndDateSource, options.CatalogExamDate));
        await service.SetExamDateAsync(new(new DateOnly(2027, 6, 20)), default);
        options = await service.OptionsAsync(default);
        Assert.Equal(("Student", (Guid?)null), (options.EndDateSource, (await f.Db.StudentLearningContexts.AsNoTracking().SingleAsync()).TargetExamSessionId));
        // The admin moves the chosen session into the past: the plan falls back to the catalog date.
        context = await f.Db.StudentLearningContexts.SingleAsync(); context.SetTargetExamDate(null, new DateOnly(2026, 11, 1));
        context.SetTargetExamSession(yds.Sessions.Single(x => x.Label == "2027/1").Id);
        yds.ReplaceSessions([new(yds.Sessions.Single(x => x.Label == "2027/1").Id, new DateOnly(2026, 3, 28), "2026/1", null, null)]);
        await f.Db.SaveChangesAsync();
        Assert.Equal("Catalog", (await service.OptionsAsync(default)).EndDateSource);
    }

    [Fact]
    public async Task WhenTimeIsShortHighYieldTopicsGoFirst_AndTheShorterPlanMayBeSavedOnRequest()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var service = await WeeklyOutcomePlanTests.Seed(f);
        var old = await f.Db.StudyCatalogTopics.SingleAsync(); var existing = await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var important = StudyCatalogTopic.Create("test", "high-yield", old.LessonId, old.UnitId, "Sık çıkan konu", null, 5); important.SetActive(true); important.SetYieldWeight(5);
        var outcome = StudyCatalogOutcome.Create(existing.EditionId, existing.OfficialTopicId, "M.TEST.HY", "Sık çıkan kazanım", "", 73, 5);
        var estimate = OutcomeDurationEstimate.Create(outcome.Id, 1, 10, 10, 10, "ReviewedStudy", "Medium", "Test süresi", f.User.UserId); estimate.Publish();
        var mapping = StudyCatalogTopicOutcome.Create(important.Id, outcome.Id, "Test eşleştirmesi"); mapping.Review(true, "Test eşleştirmesi", f.User.UserId!.Value);
        f.Db.AddRange(important, outcome, estimate, mapping); await f.Db.SaveChangesAsync();
        var oneHour = Input with { EndDate = Input.StartDate };

        var strict = await service.PreviewAsync(oneHour, default);
        Assert.False(strict.CanCreate); Assert.Equal(old.Id, strict.Schedule.Tasks[0].Outcome.TopicId);

        var prioritized = await service.PreviewAsync(oneHour with { PrioritizeWhenShort = true }, default);
        Assert.True(prioritized.CanCreate);
        Assert.Equal(important.Id, Assert.Single(prioritized.Schedule.Tasks).Outcome.TopicId);
        Assert.Contains("sık çıkan", prioritized.Schedule.Tasks[0].Reason);
        var left = Assert.Single(prioritized.LeftOutTopics!);
        Assert.Equal((old.Id, 30), (left.TopicId, left.Minutes));
        var created = await service.CreateAsync(new("Öncelikli plan", prioritized.Request, prioritized.Fingerprint), default);
        Assert.Single(await f.Db.StudyPlanTasks.Where(x => x.RevisionId == created.Id).ToArrayAsync());
    }

    [Fact]
    public void YieldWeightIsBetweenOneAndFive()
    {
        var topic = StudyCatalogTopic.Create("test", "yield", Guid.NewGuid(), Guid.NewGuid(), "Konu", null, 1);
        Assert.Equal(3, topic.YieldWeight);
        topic.SetYieldWeight(5); Assert.Equal(5, topic.YieldWeight);
        Assert.Throws<ArgumentOutOfRangeException>(() => topic.SetYieldWeight(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => topic.SetYieldWeight(6));
    }
}
