using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap O7: rule-based plan engine (version 2). The rules are pure so each one is checked without a database.
public sealed class WeeklyPlanEngineTests
{
    private static readonly Guid Math = Guid.NewGuid(), Turkish = Guid.NewGuid();
    private static readonly DateOnly Start = new(2027, 5, 24); // Monday
    private static WeeklyDraftSlot Slot(Guid lesson, DayOfWeek day, int start = 1080, int minutes = 30) =>
        new(Guid.NewGuid(), lesson, lesson == Math ? "Matematik" : "Türkçe", "LGS", day, start, minutes);
    private static WeeklyOutcomeWork Outcome(Guid topic, string name, int minutes, double practiceShare = .4) =>
        new(topic, name, Guid.NewGuid(), "K." + name, "Kazanım " + name, Guid.NewGuid(), 1, minutes, PracticeShare: practiceShare);
    private static WeeklyLessonWork Lesson(Guid lesson, params WeeklyOutcomeWork[] outcomes) =>
        new(lesson, lesson == Math ? "Matematik" : "Türkçe", outcomes);

    [Fact]
    public void StudiedTopicsStayAsShortRefresh_NeedsReviewIsShortened_AndDifficultyStretchesLearning()
    {
        Guid learn = Guid.NewGuid(), review = Guid.NewGuid(), studied = Guid.NewGuid();
        var shaped = WeeklyPlanEngine.Shape(
            [Lesson(Math, Outcome(learn, "Öğren", 100), Outcome(review, "Tekrar", 100), Outcome(studied, "Çalıştım", 100))],
            new Dictionary<Guid, DeclaredTopicStatus> { [review] = DeclaredTopicStatus.NeedsReview, [studied] = DeclaredTopicStatus.Studied },
            new Dictionary<Guid, WeeklyPlanEngine.LessonPreference> { [Math] = new(1, 5) },
            new HashSet<Guid>());
        var lesson = Assert.Single(shaped);
        Assert.Equal(1, lesson.Priority);
        var byTopic = lesson.Outcomes.ToDictionary(x => x.TopicId);
        Assert.Equal(("Learn", 130), (byTopic[learn].Kind, byTopic[learn].Minutes));
        Assert.Equal(("Review", 78), (byTopic[review].Kind, byTopic[review].Minutes));
        Assert.Equal(("Refresh", 25), (byTopic[studied].Kind, byTopic[studied].Minutes));
        Assert.Contains("zor", byTopic[learn].Reason);
        Assert.Contains("Tekrar gerek", byTopic[review].Reason);
        Assert.Contains("Çalıştım", byTopic[studied].Reason);
        Assert.Equal(10, WeeklyPlanEngine.Shape([Lesson(Math, Outcome(studied, "Kısa", 20))],
            new Dictionary<Guid, DeclaredTopicStatus> { [studied] = DeclaredTopicStatus.Studied }, new Dictionary<Guid, WeeklyPlanEngine.LessonPreference>(), new HashSet<Guid>())
            .Single().Outcomes.Single().Minutes);
    }

    [Fact]
    public void ExamErrorTopicsMoveToTheFrontWithTheirOutcomesTogether()
    {
        Guid first = Guid.NewGuid(), second = Guid.NewGuid(), weak = Guid.NewGuid();
        var shaped = WeeklyPlanEngine.Shape(
            [Lesson(Math, Outcome(first, "A", 30), Outcome(second, "B", 30), Outcome(weak, "C1", 30), Outcome(weak, "C2", 30))],
            new Dictionary<Guid, DeclaredTopicStatus>(), new Dictionary<Guid, WeeklyPlanEngine.LessonPreference>(), new HashSet<Guid> { weak });
        Assert.Equal(new[] { weak, weak, first, second }, shaped.Single().Outcomes.Select(x => x.TopicId));
        Assert.All(shaped.Single().Outcomes.Where(x => x.TopicId == weak), x => Assert.Contains("deneme", x.Reason));
    }

    // Paket H4a: the topics the student wants first ("Önemli konulara odaklan") lead their lesson, before the exam-error topics.
    [Fact]
    public void FocusTopicsLeadTheirLessonBeforeExamErrors_AlsoWhenTimeIsShort()
    {
        Guid first = Guid.NewGuid(), weak = Guid.NewGuid(), focus = Guid.NewGuid(), turkish = Guid.NewGuid();
        WeeklyLessonWork[] lessons = [Lesson(Math, Outcome(first, "A", 30), Outcome(weak, "B", 30), Outcome(focus, "C1", 30), Outcome(focus, "C2", 30)),
            Lesson(Turkish, Outcome(turkish, "T", 30))];
        var none = new Dictionary<Guid, DeclaredTopicStatus>(); var preferences = new Dictionary<Guid, WeeklyPlanEngine.LessonPreference>();
        var shaped = WeeklyPlanEngine.Shape(lessons, none, preferences, new HashSet<Guid> { weak }, new HashSet<Guid> { focus });
        Assert.Equal(new[] { focus, focus, weak, first }, shaped[0].Outcomes.Select(x => x.TopicId));
        Assert.All(shaped[0].Outcomes.Take(2), x => Assert.Equal("Öne almak istediğin konu", x.Reason));
        Assert.DoesNotContain(shaped[0].Outcomes.Skip(2), x => x.Reason!.Contains("Öne almak"));
        Assert.Equal("Müfredat sırasındaki yeni konu", shaped[1].Outcomes.Single().Reason);
        // No focus: exactly as before.
        var plain = WeeklyPlanEngine.Shape(lessons, none, preferences, new HashSet<Guid> { weak });
        var empty = WeeklyPlanEngine.Shape(lessons, none, preferences, new HashSet<Guid> { weak }, new HashSet<Guid>());
        Assert.Equal(plain.SelectMany(x => x.Outcomes), empty.SelectMany(x => x.Outcomes));
        Assert.Equal(new[] { weak, first, focus, focus }, plain[0].Outcomes.Select(x => x.TopicId));
        // A focus topic that is also an exam-error topic keeps both reasons.
        var both = WeeklyPlanEngine.Shape(lessons, none, preferences, new HashSet<Guid> { focus }, new HashSet<Guid> { focus });
        Assert.StartsWith("Öne almak istediğin konu · Son denemelerde", both[0].Outcomes[0].Reason);
        // A teacher's choice says so (review of PR #132, S2).
        var teacher = WeeklyPlanEngine.Shape(lessons, none, preferences, new HashSet<Guid>(), new HashSet<Guid> { focus }, WeeklyPlanEngine.TeacherFocusReason);
        Assert.Equal("Öğretmeninin öne aldığı konu", teacher[0].Outcomes[0].Reason);
        // When time is short, high-yield topics follow the focus topics.
        var shortened = WeeklyPlanEngine.PrioritizeByYield(shaped, [Math], new Dictionary<Guid, int> { [first] = 5 }, new HashSet<Guid> { weak }, new HashSet<Guid> { focus });
        Assert.Equal(new[] { focus, focus, weak, first }, shortened[0].Outcomes.Select(x => x.TopicId));
    }

    [Fact]
    public void HighYieldTopicsLeadWhenTimeIsShort()
    {
        Guid low = Guid.NewGuid(), high = Guid.NewGuid(), mid = Guid.NewGuid();
        var lessons = new[] { Lesson(Math, Outcome(low, "Az", 30), Outcome(mid, "Orta", 30), Outcome(high, "Çok", 30)) };
        var ordered = WeeklyPlanEngine.PrioritizeByYield(lessons, [Math], new Dictionary<Guid, int> { [low] = 1, [high] = 5 }, new HashSet<Guid>());
        Assert.Equal(new[] { high, mid, low }, ordered.Single().Outcomes.Select(x => x.TopicId));
        Assert.Contains("sık çıkan", ordered.Single().Outcomes[0].Reason);
        var untouched = WeeklyPlanEngine.PrioritizeByYield(lessons, [], new Dictionary<Guid, int> { [high] = 5 }, new HashSet<Guid>());
        Assert.Equal(new[] { low, mid, high }, untouched.Single().Outcomes.Select(x => x.TopicId));
    }

    [Fact]
    public void QuestionTargetsFollowThePracticeShareOfEachKind()
    {
        Assert.Equal(16, WeeklyPlanEngine.Questions("Learn", 60, .4));
        Assert.Equal(20, WeeklyPlanEngine.Questions("Review", 60, .2));
        Assert.Equal(7, WeeklyPlanEngine.Questions("Spaced", 15, .1));
        Assert.Null(WeeklyPlanEngine.Questions("Learn", 10, 0));
    }

    [Fact]
    public void SpacedReviewsFollowAFinishedTopicInItsOwnLessonHours_WithoutCountingAsCurriculum()
    {
        var topic = Guid.NewGuid();
        var slots = new[] { Slot(Math, DayOfWeek.Monday), Slot(Math, DayOfWeek.Tuesday), Slot(Math, DayOfWeek.Monday, 1110) };
        var result = WeeklyOutcomeScheduler.Build(Start, Start.AddDays(40), slots, [Lesson(Math, Outcome(topic, "Konu", 30))], false, [], WeeklyPlanEngine.Rules);
        var learn = Assert.Single(result.Tasks.Where(x => x.Kind == "Learn"));
        Assert.Equal((Start, 1080, 30), (learn.Date, learn.StartMinute, learn.Minutes));
        var spaced = result.Tasks.Where(x => x.Kind == "Spaced").ToArray();
        // Paket I2: one review, +7 days (next Monday).
        Assert.Equal(new[] { Start.AddDays(7) }, spaced.Select(x => x.Date));
        Assert.Equal(10, WeeklyPlanEngine.SpacedMinutes); Assert.Equal(new[] { 7 }, WeeklyPlanEngine.SpacedIntervalDays);
        Assert.All(spaced, x => Assert.Equal(WeeklyPlanEngine.SpacedMinutes, x.Minutes));
        Assert.All(spaced, x => Assert.Contains("Aralıklı tekrar", x.Reason));
        Assert.All(result.Tasks, x => Assert.NotNull(x.PlannedQuestions));
        var summary = Assert.Single(result.Lessons);
        Assert.Equal(30, summary.RequiredMinutes); Assert.Equal(30, summary.ScheduledMinutes); Assert.Equal(Start, summary.FinishDate);
        var shortRange = WeeklyOutcomeScheduler.Build(Start, Start.AddDays(3), slots, [Lesson(Math, Outcome(topic, "Konu", 30))], false, [], WeeklyPlanEngine.Rules);
        Assert.Empty(shortRange.Tasks.Where(x => x.Kind == "Spaced"));
    }

    // Paket I2 (user decision 13.11.2026): the plan asks for 75 % of a catalog duration; the suite itself runs with 1.
    [Theory]
    [InlineData(60, 45)]
    [InlineData(23, 17)]
    [InlineData(90, 68)]
    [InlineData(4, 5)]
    [InlineData(0, 0)]
    public void ThePlanAsksForAShareOfTheCatalogMinutes(int catalog, int planned)
    {
        Assert.Equal(.75m, WeeklyPlanEngine.DefaultContentFactor);
        Assert.Equal(planned, WeeklyPlanEngine.PlanMinutes(catalog, WeeklyPlanEngine.DefaultContentFactor));
        Assert.Equal(60, WeeklyPlanEngine.PlanMinutes(60, 1m));
    }

    [Fact]
    public void ContiguousHoursOfOneOutcomeOnADayBecomeOneTask()
    {
        var slots = new[] { Slot(Math, DayOfWeek.Monday, 1080), Slot(Math, DayOfWeek.Monday, 1110) };
        var work = Lesson(Math, Outcome(Guid.NewGuid(), "Uzun", 60));
        var merged = WeeklyOutcomeScheduler.Build(Start, Start, slots, [work], false, [], WeeklyPlanEngine.Rules);
        var task = Assert.Single(merged.Tasks);
        Assert.Equal((1080, 60), (task.StartMinute, task.Minutes));
        var legacy = WeeklyOutcomeScheduler.Build(Start, Start, slots, [work], false, []);
        Assert.Equal(2, legacy.Tasks.Count);
    }

    [Fact]
    public void FreedHoursGoToTheHigherPriorityLessonFirst()
    {
        var slots = new[] { Slot(Math, DayOfWeek.Monday), Slot(Turkish, DayOfWeek.Monday, 1110), Slot(Turkish, DayOfWeek.Tuesday) };
        var math = Lesson(Math, Outcome(Guid.NewGuid(), "M", 120)) with { Priority = 4 };
        var turkish = Lesson(Turkish, Outcome(Guid.NewGuid(), "T", 30)) with { Priority = 1 };
        var extra = new WeeklyLessonWork(Guid.NewGuid(), "Fen", [Outcome(Guid.NewGuid(), "F", 60)], Priority: 1);
        var result = WeeklyOutcomeScheduler.Build(Start, Start.AddDays(1), [..slots, new(Guid.NewGuid(), extra.LessonId, "Fen", "LGS", DayOfWeek.Sunday, 600, 30)],
            [math, turkish, extra], true, [], WeeklyPlanEngine.Rules with { SpacedIntervalDays = [] });
        // Turkish finishes on Monday, so its Tuesday hour is free: Fen (priority 1) takes it before Math (priority 4), although Math has more minutes left.
        var tuesday = Assert.Single(result.Tasks.Where(x => x.Date == Start.AddDays(1)));
        Assert.Equal(extra.LessonId, tuesday.LessonId);
    }
}
