using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>
/// User decision (01.11.2026, "sınırı yükselt"): one topic or outcome may need up to 4320 minutes (72 h) instead of 1440.
/// A day still has 24 hours: every task, day total and task-outcome allocation stays within 1440, so a large topic is
/// scheduled as several tasks on several days.
/// </summary>
public sealed class TopicMinutesCapTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);
    private const string Previous = "20261110090000_CatalogAdministration", Current = "20261111090000_TopicMinutesCap4320";

    [Fact]
    public void LimitsAreNamed()
    {
        Assert.Equal(4320, StudyLimits.MaxTopicMinutes);
        Assert.Equal(1440, StudyLimits.MaxDayMinutes);
    }

    [Fact]
    public void OutcomeEstimateAndTopicShareAcceptUpTo4320Minutes()
    {
        var outcome = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var estimate = OutcomeDurationEstimate.Create(outcome, 2, 2600, 1300, 420, "ReviewedStudy", "Low", "KM3 süre", reviewer);
        Assert.Equal(4320, estimate.TotalMinutes);
        Assert.Equal(4320, OutcomeDurationEstimate.Create(outcome, 2, 4320, 0, 0, "ReviewedStudy", "Low", "Tek bileşen", reviewer).TotalMinutes);
        var error = Assert.Throws<ArgumentException>(() => OutcomeDurationEstimate.Create(outcome, 2, 2600, 1300, 421, "ReviewedStudy", "Low", "KM3 süre", reviewer));
        Assert.Contains("4320", error.Message);
        Assert.Equal(4320, StudyCatalogTopicOutcomeShare.Create(Guid.NewGuid(), estimate, 2600, 1300, 420, "Kapsam").TotalMinutes);
        Assert.Throws<ArgumentException>(() => StudyCatalogTopicOutcomeShare.Create(Guid.NewGuid(), estimate, 2600, 1300, 421, "Kapsam"));
    }

    [Fact]
    public void TopicMinutesAreLimitedTo4320()
    {
        Guid lesson = Guid.NewGuid(), unit = Guid.NewGuid();
        var topic = StudyCatalogTopic.Create("test", "analiz", lesson, unit, "Analiz", null, 1, 4320);
        Assert.Equal(4320, topic.EstimatedMinutes);
        Assert.ThrowsAny<ArgumentException>(() => StudyCatalogTopic.Create("test", "analiz-2", lesson, unit, "Analiz", null, 1, 4321));
        Assert.ThrowsAny<ArgumentException>(() => topic.Edit("Analiz", 1, 4321));
        Assert.Equal(4320, topic.EstimatedMinutes);
    }

    [Fact]
    public void TasksDaysAndTaskOutcomeAllocationsStayWithinOneDay()
    {
        var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan");
        Assert.Equal(1440, StudyPlanTask.Create(plan, Monday, "Tam gün", 1440).PlannedMinutes);
        Assert.ThrowsAny<ArgumentException>(() => StudyPlanTask.Create(plan, Monday, "Bir günden uzun", 1441));
        Assert.Throws<ArgumentException>(() => StudyPlanTaskOutcome.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1441, "Catalog"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StudyAvailabilityWindow(DayOfWeek.Monday, 0, 1441));
    }

    [Fact]
    public void AutomaticPreviewAcceptsATopicOrOutcomeUpTo4320Minutes()
    {
        var topic = Guid.NewGuid();
        CoachingAutomaticStudyPlanPreviewService.Validate(new(Monday, 14, 0, [new(topic, 4320)]));
        CoachingAutomaticStudyPlanPreviewService.Validate(new(Monday, 14, 0, [new(topic, null, [new(Guid.NewGuid(), Guid.NewGuid(), 4320)])]));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(Monday, 14, 0, [new(topic, 4321)])));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(Monday, 14, 0, [new(topic, null, [new(Guid.NewGuid(), Guid.NewGuid(), 4321)])])));
    }

    private static readonly StudyAvailabilityWindow[] Hours =
        [new(DayOfWeek.Monday, 600, 1440), new(DayOfWeek.Tuesday, 0, 1440), new(DayOfWeek.Wednesday, 1080, 1200)];

    [Fact]
    public void DraftSchedulerSplitsA3000MinuteTopicIntoDailyTasks()
    {
        var topic = Guid.NewGuid();
        var schedule = StudyPlanDraftScheduler.Generate(Monday, 14, Hours, [new(topic, 3000)], []);
        Assert.Equal(new[] { (Monday, 840), (Monday.AddDays(1), 1440), (Monday.AddDays(2), 120), (Monday.AddDays(7), 600) },
            schedule.Tasks.Select(x => (x.PlannedDate, x.PlannedMinutes)));
        Assert.All(schedule.Tasks, x => Assert.True(x.PlannedMinutes <= StudyLimits.MaxDayMinutes));
        Assert.All(schedule.Tasks.GroupBy(x => x.PlannedDate), day => Assert.True(day.Sum(x => x.PlannedMinutes)
            <= Hours.Where(w => w.Day == day.Key.DayOfWeek).Sum(w => w.EndMinute - w.StartMinute)));
        Assert.Equal(3000, schedule.ScheduledMinutes); Assert.Empty(schedule.UnscheduledTopics);
        // A short horizon keeps what does not fit as unscheduled instead of losing it.
        var short_ = StudyPlanDraftScheduler.Generate(Monday, 7, Hours, [new(topic, 3000)], []);
        Assert.Equal(2400, short_.ScheduledMinutes); Assert.Equal(600, Assert.Single(short_.UnscheduledTopics).RemainingMinutes);
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 14, Hours, [new(topic, 4321)], []));
        // Reservations are work on one day: still at most 1440.
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 14, Hours, [new(topic, 60)], [new(Monday.AddDays(1), 1441)]));
    }

    [Fact]
    public void OutcomeAllocationKeepsEachTaskLinkWithinItsTask()
    {
        var topic = Guid.NewGuid(); var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan");
        var schedule = StudyPlanDraftScheduler.Generate(Monday, 14, Hours, [new(topic, 3000)], []);
        var tasks = schedule.Tasks.Select(x => StudyPlanTask.Create(plan, x.PlannedDate, "Analiz", x.PlannedMinutes, x.TopicId)).ToArray();
        var outcome = new ResolvedStudyOutcome(topic, Guid.NewGuid(), Guid.NewGuid(), 3000, "Catalog", "K.1", "Analiz");
        var links = StudyOutcomeAllocation.Allocate(tasks, [outcome]);
        Assert.Equal(tasks.Select(x => x.PlannedMinutes), links.Select(x => x.AllocatedMinutes));
        Assert.Equal(3000, links.Sum(x => x.AllocatedMinutes));
    }

    [Fact]
    public void PlanEngineKeepsTheWholeTopicInsteadOfCuttingItToOneDay()
    {
        Guid lesson = Guid.NewGuid(), topic = Guid.NewGuid(), big = Guid.NewGuid();
        WeeklyOutcomeWork Work(Guid t, int minutes) => new(t, "Analiz", Guid.NewGuid(), "K", "Analiz", Guid.NewGuid(), 1, minutes);
        var shaped = WeeklyPlanEngine.Shape([new(lesson, "Matematik", [Work(topic, 3000), Work(big, 4000)])],
            new Dictionary<Guid, DeclaredTopicStatus>(), new Dictionary<Guid, WeeklyPlanEngine.LessonPreference> { [lesson] = new(1, 5) }, new HashSet<Guid>());
        var minutes = shaped.Single().Outcomes.ToDictionary(x => x.TopicId, x => x.Minutes);
        Assert.Equal(3900, minutes[topic]);                       // 3000 × 1,3 (hard lesson), not cut to 1440
        Assert.Equal(StudyLimits.MaxTopicMinutes, minutes[big]);  // 4000 × 1,3 = 5200 → topic limit
    }

    [Fact]
    public void WeeklySchedulerSplitsA3000MinuteOutcomeIntoTasksOfAtMostOneDay()
    {
        var lesson = Guid.NewGuid();
        // Full-day hours on Monday and Tuesday: 24 one-hour slots each.
        var slots = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday }.SelectMany(day => Enumerable.Range(0, 24)
            .Select(h => new WeeklyDraftSlot(Guid.NewGuid(), lesson, "Matematik", "OABT_IMAT", day, h * 60, 60))).ToArray();
        var work = new WeeklyLessonWork(lesson, "Matematik", [new(Guid.NewGuid(), "Analiz", Guid.NewGuid(), "K", "Analiz", Guid.NewGuid(), 2, 3000)]);
        foreach (var rules in new WeeklyPlanRules?[] { null, WeeklyPlanEngine.Rules })
        {
            var schedule = WeeklyOutcomeScheduler.Build(Monday, Monday.AddDays(13), slots, [work], false, [], rules);
            var learning = schedule.Tasks.Where(x => x.Kind != "Spaced").ToArray();
            Assert.Equal(3000, learning.Sum(x => x.Minutes));
            Assert.All(schedule.Tasks, x => Assert.True(x.Minutes <= StudyLimits.MaxDayMinutes && x.StartMinute + x.Minutes <= 1440));
            Assert.All(schedule.Tasks.GroupBy(x => x.Date), day => Assert.True(day.Sum(x => x.Minutes) <= StudyLimits.MaxDayMinutes));
            Assert.Equal(new[] { Monday, Monday.AddDays(1), Monday.AddDays(7) }, learning.Select(x => x.Date).Distinct());
            Assert.Equal(0, Assert.Single(schedule.Lessons).RemainingMinutes);
            if (rules is not null) // version 2 merges back-to-back hours: one task per day, never longer than the day
                Assert.Equal(new[] { 1440, 1440, 120 }, learning.Select(x => x.Minutes));
        }
    }

    // ------------------------------------------------------------------ PostgreSQL

    private static async Task<int> Count(Microsoft.EntityFrameworkCore.DbContext db, string sql)
        => await db.Database.SqlQueryRaw<int>(sql).SingleAsync();

    private static Task<string> Constraint(Microsoft.EntityFrameworkCore.DbContext db, string name)
        => db.Database.SqlQueryRaw<string>($"SELECT pg_get_constraintdef(oid) AS \"Value\" FROM pg_constraint WHERE conname = '{name}'").SingleAsync();

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task DatabaseAcceptsTopicAndOutcomeTotalsUpTo4320ButKeepsTasksWithinOneDay()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data; await f.Seed();
        var outcome = await f.Db.Set<StudyCatalogOutcome>().SingleAsync(); var topic = await f.Db.StudyCatalogTopics.SingleAsync();
        var estimate = await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();
        Assert.Contains("4320", await Constraint(f.Db, "ck_outcome_minutes"));
        Assert.Contains("4320", await Constraint(f.Db, "ck_topic_outcome_share_minutes"));
        Assert.Contains("1440", await Constraint(f.Db, "ck_study_task_minutes"));
        Assert.Contains("1440", await Constraint(f.Db, "ck_task_outcome_minutes"));
        var big = Guid.NewGuid();
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.outcome_duration_estimates ("Id","OutcomeId","Number","LearningMinutes","PracticeMinutes","CheckMinutes","Method","Confidence","Evidence","ReviewedBy","ReviewedAt","IsPublished")
            VALUES ({big},{outcome.Id},2,2600,1300,420,'ReviewedStudy','Low','4320',{Guid.NewGuid()},now(),true);
            INSERT INTO coaching.study_catalog_topic_outcome_shares ("TopicId","OutcomeId","EstimateId","LearningMinutes","PracticeMinutes","CheckMinutes","ScopeDescription")
            VALUES ({topic.Id},{outcome.Id},{big},2600,1300,420,'Kapsam');
            """);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.outcome_duration_estimates ("Id","OutcomeId","Number","LearningMinutes","PracticeMinutes","CheckMinutes","Method","Confidence","Evidence","ReviewedBy","ReviewedAt","IsPublished")
            VALUES ({Guid.NewGuid()},{outcome.Id},3,2600,1300,421,'ReviewedStudy','Low','4321',{Guid.NewGuid()},now(),false)
            """));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE coaching.study_plan_tasks SET \"PlannedMinutes\" = 1441"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE coaching.study_plan_task_outcomes SET \"AllocatedMinutes\" = 1441"));
        Assert.Equal(1, await Count(f.Db, $"SELECT count(*)::int AS \"Value\" FROM coaching.outcome_duration_estimates WHERE \"Id\" = '{big}'"));
        _ = estimate;
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationDownRestores1440OnlyWhenNoRowNeedsMore_AndOtherwiseChangesNothing()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data; await f.Seed();
        var outcome = await f.Db.Set<StudyCatalogOutcome>().SingleAsync(); var topic = await f.Db.StudyCatalogTopics.SingleAsync();
        var migrator = f.Db.GetService<IMigrator>(); var big = Guid.NewGuid();
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.outcome_duration_estimates ("Id","OutcomeId","Number","LearningMinutes","PracticeMinutes","CheckMinutes","Method","Confidence","Evidence","ReviewedBy","ReviewedAt","IsPublished")
            VALUES ({big},{outcome.Id},2,2000,1000,300,'ReviewedStudy','Low','3300',{Guid.NewGuid()},now(),true);
            INSERT INTO coaching.study_catalog_topic_outcome_shares ("TopicId","OutcomeId","EstimateId","LearningMinutes","PracticeMinutes","CheckMinutes","ScopeDescription")
            VALUES ({topic.Id},{outcome.Id},{big},2000,1000,300,'Kapsam');
            """);
        // Refused: an estimate and a share need more than 1440 minutes. Nothing changes.
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync(Previous));
        Assert.Contains("1440", error.MessageText);
        Assert.Contains("4320", await Constraint(f.Db, "ck_outcome_minutes"));
        Assert.Contains("4320", await Constraint(f.Db, "ck_topic_outcome_share_minutes"));
        Assert.Equal(Current, (await f.Db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(1, await Count(f.Db, $"SELECT count(*)::int AS \"Value\" FROM coaching.study_catalog_topic_outcome_shares WHERE \"EstimateId\" = '{big}'"));

        // Only the share is large: still refused.
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE coaching.study_catalog_topic_outcome_shares SET "LearningMinutes" = 2000, "PracticeMinutes" = 0, "CheckMinutes" = 0 WHERE "EstimateId" = {big};
            UPDATE coaching.outcome_duration_estimates SET "LearningMinutes" = 2000, "PracticeMinutes" = 0, "CheckMinutes" = 0 WHERE "Id" = {big};
            """);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync(Previous));

        // A topic planned with more than 1440 minutes (the KM3 minutes pack) also keeps the old release out.
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE coaching.study_catalog_topic_outcome_shares SET "LearningMinutes" = 600 WHERE "EstimateId" = {big};
            UPDATE coaching.outcome_duration_estimates SET "LearningMinutes" = 600 WHERE "Id" = {big};
            UPDATE coaching.study_catalog_topics SET "EstimatedMinutes" = 3890 WHERE "Id" = {topic.Id};
            """);
        Assert.Contains("1 topic", (await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync(Previous))).MessageText);
        Assert.Contains("4320", await Constraint(f.Db, "ck_outcome_minutes"));
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE coaching.study_catalog_topics SET \"EstimatedMinutes\" = 60 WHERE \"Id\" = {topic.Id}");

        // Without such rows Down restores the 1440 limit, and Up raises it again.
        await migrator.MigrateAsync(Previous);
        Assert.Contains("1440", await Constraint(f.Db, "ck_outcome_minutes"));
        Assert.Contains("1440", await Constraint(f.Db, "ck_topic_outcome_share_minutes"));
        Assert.DoesNotContain("4320", await Constraint(f.Db, "ck_outcome_minutes"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE coaching.outcome_duration_estimates SET \"LearningMinutes\" = 1441 WHERE \"Id\" = {big}"));
        await f.Db.Database.MigrateAsync();
        Assert.Contains("4320", await Constraint(f.Db, "ck_outcome_minutes"));
        Assert.Contains("4320", await Constraint(f.Db, "ck_topic_outcome_share_minutes"));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AutomaticDraftPlacesA3000MinuteOutcomeOnSeveralDays()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data; await f.Seed();
        var topic = await f.Db.StudyCatalogTopics.SingleAsync(); var outcome = await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        // What the KM3 minutes pack publishes: a newer reviewed version; the older one stays published for the plans that use it.
        var version = OutcomeDurationEstimate.Create(outcome.Id, 2, 1800, 900, 300, "ReviewedStudy", "Low", "KM3: 3000 dk", f.User.UserId); version.Publish();
        f.Db.Add(version);
        var student = Guid.NewGuid(); f.User.UserId = student; f.User.Roles = ["Student"];
        var hours = StudyAvailability.Create(student, "Europe/Istanbul"); hours.ReplacePreferences("Europe/Istanbul", Hours); f.Db.Add(hours);
        await f.Db.SaveChangesAsync();
        var access = new CoachingAccessPolicy(f.User); var previews = new CoachingAutomaticStudyPlanPreviewService(f.Db, access);
        var input = new AutomaticStudyPreviewRequest(Monday, 14, hours.Version, [new(topic.Id, null, [new(outcome.Id, version.Id)])]);
        var preview = await previews.PreviewAsync(input);
        Assert.Equal(new[] { 840, 1440, 120, 600 }, preview.Schedule.Tasks.Select(x => x.PlannedMinutes));
        var service = new CoachingManualStudyPlanService(f.Db, access, previews, new CurriculumManagementTests.NoopPublisher());
        var draft = await service.CreateAutomaticDraftAsync(new("Analiz", input, preview.ActiveRevisionId, preview.ActiveRevisionVersion, preview.EvidenceFingerprint));
        Assert.Equal(4, draft.Tasks.Count); Assert.Equal(3000, draft.Tasks.Sum(x => x.PlannedMinutes));
        Assert.All(draft.Tasks, x => Assert.True(x.PlannedMinutes <= StudyLimits.MaxDayMinutes));
        Assert.All(draft.Tasks.GroupBy(x => x.PlannedDate), day => Assert.True(day.Sum(x => x.PlannedMinutes) <= StudyLimits.MaxDayMinutes));
        var links = await f.Db.Set<StudyPlanTaskOutcome>().Where(x => x.EstimateId == version.Id).ToArrayAsync();
        Assert.Equal(3000, links.Sum(x => x.AllocatedMinutes)); Assert.All(links, x => Assert.True(x.AllocatedMinutes <= StudyLimits.MaxDayMinutes));
        // The earlier version stays published (the seeded completed task points at it).
        Assert.True((await f.Db.Set<OutcomeDurationEstimate>().SingleAsync(x => x.Number == 1)).IsPublished);
    }
}
