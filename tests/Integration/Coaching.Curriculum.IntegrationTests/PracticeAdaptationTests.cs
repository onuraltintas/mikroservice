using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class PracticeAdaptationTests
{
    [Fact]
    public void TopicCountsCanBePartialButCannotExceedSectionOrDuplicateTopics()
    {
        var topic = Guid.NewGuid();
        var input = new PracticeExamInput("YDT_EN", [new("ENG", 40, 20, 20)]);
        var analysis = new PracticeTopicAnalysis([new("ENG", topic, 2, 3, 1, "Knowledge")]);
        PracticeExamRules.ValidateTopics(input, analysis);
        Assert.Throws<ArgumentException>(() => PracticeExamRules.ValidateTopics(input, analysis with { Topics = [new("ENG", topic, 41, 0, 0, null)] }));
        Assert.Throws<ArgumentException>(() => PracticeExamRules.ValidateTopics(input, analysis with { Topics = [analysis.Topics![0], analysis.Topics[0]] }));
        Assert.Throws<ArgumentException>(() => PracticeExamRules.ValidateTopics(input, analysis with { Topics = [new("MAT", topic, 0, 1, 0, "Knowledge")] }));
        Assert.Throws<ArgumentException>(() => PracticeExamRules.ValidateTopics(input, analysis with { Topics = [new("ENG", topic, 0, 1, 0, "invalid")] }));
    }

    [Fact]
    public void OneOrTwoExamsOnlyObserveAndThreeUseRepeatedTopicEvidence()
    {
        var topic = Guid.NewGuid();
        var exams = Enumerable.Range(1, 4).Select(i => Evidence(i, topic)).ToArray();
        Assert.Empty(PracticeAdaptationCalculator.Calculate(exams.Take(2)).Single().Topics);
        var advice = PracticeAdaptationCalculator.Calculate(exams).Single();
        Assert.Equal(3, advice.ExamIds.Count);
        Assert.DoesNotContain(exams[0].ExamId, advice.ExamIds);
        var weak = Assert.Single(advice.Topics);
        Assert.Equal(topic, weak.TopicId); Assert.Equal(3, weak.Appearances); Assert.Equal(9, weak.Wrong);
        Assert.Equal(60, weak.SuggestedMinutes);
    }

    [Fact]
    public void DuplicatedEvidenceAcrossSourcesIsNotTwoExamsAndSparseTopicsDoNotProduceAdvice()
    {
        var topic = Guid.NewGuid(); var first = Evidence(1, topic);
        var duplicate = first with { ExamId = Guid.NewGuid(), Source = "TeacherRecorded" };
        var result = PracticeAdaptationCalculator.Calculate([first, duplicate, Evidence(2, topic)]);
        Assert.All(result, x => Assert.True(x.ExamIds.Count < 3));
        var sparse = Enumerable.Range(1, 3).Select(i => Evidence(i, topic) with {
            Analysis = new([new("ENG", topic, 0, 1, 0, null)]) });
        Assert.Empty(PracticeAdaptationCalculator.Calculate(sparse).Single().Topics);
    }

    [Fact]
    public void RecentRecoveryAndDifferentScopesAreNotMistakenForPersistentWeakness()
    {
        var topic = Guid.NewGuid(); var rows = Enumerable.Range(1, 3).Select(i => Evidence(i, topic)).ToArray();
        rows[2] = rows[2] with { Analysis = new([new("ENG", topic, 5, 0, 0, null)]) };
        Assert.Empty(PracticeAdaptationCalculator.Calculate(rows).Single().Topics);
        var other = Evidence(4, topic) with { Input = PracticeExamTests.Blank(PracticeExamRules.Profiles.Single(x => x.Code == "LGS")) };
        Assert.Equal(2, PracticeAdaptationCalculator.Calculate([rows[0], rows[1], other]).Count);
    }

    private static PracticeEvidence Evidence(int day, Guid topic) => new(Guid.NewGuid(), "Deneme " + day,
        new(2026, 10, day), "StudentReported", new("YDT_EN", [new("ENG", 40, 20, 20)]), Analysis: new([new("ENG", topic, 1, 3, 1, "Knowledge")], "test-" + day));
}
