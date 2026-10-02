using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAutomaticStudySchedulerTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);
    private static readonly StudyAvailabilityWindow[] Windows = [new(DayOfWeek.Monday, 480, 540), new(DayOfWeek.Tuesday, 600, 660)];

    [Fact]
    public void Scheduler_SplitsTopicsWithoutExceedingDailyCapacityAndExplainsShortfall()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var result = StudyPlanDraftScheduler.Generate(Monday, 2, Windows,
            [new(first, 90), new(second, 60)], []);
        Assert.Equal(120, result.AvailableMinutes);
        Assert.Equal(120, result.ScheduledMinutes);
        Assert.Equal(30, result.UnscheduledMinutes);
        Assert.Equal(0, result.UnusedMinutes);
        Assert.Equal(new[] { 60, 30, 30 }, result.Tasks.Select(x => x.PlannedMinutes));
        Assert.Equal(new[] { first, first, second }, result.Tasks.Select(x => x.TopicId));
        Assert.Equal(second, Assert.Single(result.UnscheduledTopics).TopicId);
        Assert.Equal(30, result.UnscheduledTopics[0].RemainingMinutes);
        Assert.All(result.Tasks.GroupBy(x => x.PlannedDate), day => Assert.True(day.Sum(x => x.PlannedMinutes) <= 60));
    }

    [Fact]
    public void Scheduler_ReservesProtectedTaskMinutesAndDoesNotMutateInputs()
    {
        var topic = new AutomaticStudyTopic(Guid.NewGuid(), 90);
        var reserved = new StudyCapacityReservation(Monday, 45);
        var result = StudyPlanDraftScheduler.Generate(Monday, 2, Windows, [topic], [reserved]);
        Assert.Equal(75, result.AvailableMinutes);
        Assert.Equal(new[] { 15, 60 }, result.Tasks.Select(x => x.PlannedMinutes));
        Assert.Equal(15, result.UnscheduledMinutes);
        Assert.Equal(90, topic.RequiredMinutes);
        Assert.Equal(45, reserved.Minutes);
    }

    [Fact]
    public void Scheduler_HandlesNoAvailabilityAndDoesNotInventTopicDurations()
    {
        var topic = new AutomaticStudyTopic(Guid.NewGuid(), 45);
        var result = StudyPlanDraftScheduler.Generate(Monday, 7, [], [topic], []);
        Assert.Empty(result.Tasks);
        Assert.Equal(45, result.UnscheduledMinutes);
        Assert.Equal(0, result.AvailableMinutes);
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 1, Windows, [topic with { RequiredMinutes = 0 }], []));
    }

    [Fact]
    public void Scheduler_UsesLocalCalendarDaysAndIsDeterministic()
    {
        var topic = new AutomaticStudyTopic(Guid.NewGuid(), 20);
        var start = Monday.AddDays(-1);
        var first = StudyPlanDraftScheduler.Generate(start, 3, Windows, [topic], []);
        var second = StudyPlanDraftScheduler.Generate(start, 3, Windows, [topic], []);
        Assert.Equal(Monday, Assert.Single(first.Tasks).PlannedDate);
        Assert.Equal(first.Tasks, second.Tasks);
        Assert.Equal(100, first.UnusedMinutes);
    }

    [Fact]
    public void Scheduler_AcceptsAdjacentWindowsButRejectsOverlapsAndOverbookedReservations()
    {
        var topic = new AutomaticStudyTopic(Guid.NewGuid(), 90);
        var adjacent = new StudyAvailabilityWindow[] { new(DayOfWeek.Monday, 480, 540), new(DayOfWeek.Monday, 540, 600) };
        Assert.Equal(90, StudyPlanDraftScheduler.Generate(Monday, 1, adjacent, [topic], []).ScheduledMinutes);
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 1,
            [new(DayOfWeek.Monday, 480, 540), new(DayOfWeek.Monday, 500, 600)], [topic], []));
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 1, Windows, [topic], [new(Monday, 61)]));
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 1, Windows, [topic], [new(Monday.AddDays(1), 10)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public void Scheduler_BoundsPlanningHorizon(int days) => Assert.Throws<ArgumentException>(() =>
        StudyPlanDraftScheduler.Generate(Monday, days, Windows, [], []));

    [Fact]
    public void Scheduler_RejectsInvalidAndExcessiveInputs()
    {
        var topic = new AutomaticStudyTopic(Guid.NewGuid(), 60);
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(default, 1, Windows, [topic], []));
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(DateOnly.MaxValue, 2, Windows, [topic], []));
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 1, Windows, [topic, topic], []));
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 1, Windows, [topic with { TopicId = Guid.Empty }], []));
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 1, Windows, [topic with { RequiredMinutes = 1441 }], []));
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 1, Windows, Enumerable.Repeat(topic, 501).ToArray(), []));
        Assert.Throws<ArgumentException>(() => StudyPlanDraftScheduler.Generate(Monday, 1, Windows, [topic], [new(Monday, 0)]));
    }
}
