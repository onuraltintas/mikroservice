using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class PracticeExamTests
{
    [Fact]
    public void TeacherCanRegisterAnAlreadyCompletedPracticeExam()
    {
        var validator = new Coaching.Application.Commands.CreateExam.CreateExamCommandValidator();
        var request = new Coaching.Application.Commands.CreateExam.CreateExamCommand(Guid.NewGuid(), "Dünkü deneme", ExamType.LGS,
            DateTime.UtcNow.AddDays(-1), 500, null, null, IdempotencyKey: "completed-practice-20261020");
        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { ExamDate = DateTime.MinValue }).IsValid);
    }
    [Theory]
    [InlineData("LGS", 90, 3)]
    [InlineData("TYT", 120, 4)]
    [InlineData("AYT_SAY", 80, 4)]
    [InlineData("AYT_EA", 80, 4)]
    [InlineData("AYT_SOZ", 80, 4)]
    [InlineData("YDT_EN", 80, 4)]
    public void ProfilesUseOfficialQuestionCountsAndNetRule(string code, int questions, int divisor)
    {
        var profile = PracticeExamRules.Profiles.Single(x => x.Code == code);
        Assert.Equal(questions, profile.Sections.Sum(x => x.QuestionCount));
        Assert.Equal(divisor, profile.WrongAnswerDivisor);
        var input = Blank(profile);
        var summary = PracticeExamRules.Summarize(input);
        Assert.Equal(questions, summary.Empty);
        Assert.Equal(0, summary.Net);
        Assert.Equal(questions, PracticeExamRules.Summarize(input with {
            Sections = profile.Sections.Select(s => new PracticeSectionAnswers(s.Code, s.QuestionCount, 0, 0)).ToArray()
        }).Net);
    }

    [Fact]
    public void NegativeNetsArePreservedAndTotalIsRoundedOnlyAfterSumming()
    {
        var profile = PracticeExamRules.Profiles.Single(x => x.Code == "LGS");
        var input = Blank(profile) with { Sections = profile.Sections.Select(s => new PracticeSectionAnswers(s.Code, 0, 1, s.QuestionCount - 1)).ToArray() };
        var summary = PracticeExamRules.Summarize(input);
        Assert.Equal(-2m, summary.Net);
        Assert.All(summary.Sections, s => Assert.Equal(-0.33m, s.Net));
    }

    [Fact]
    public void RejectsUnknownMissingDuplicateAndInconsistentSections()
    {
        var input = Blank(PracticeExamRules.Profiles.Single(x => x.Code == "TYT"));
        Assert.Throws<ArgumentException>(() => PracticeExamRules.Summarize(input with { ExamCode = "OTHER" }));
        Assert.Throws<ArgumentException>(() => PracticeExamRules.Summarize(input with { Sections = input.Sections.Skip(1).ToArray() }));
        Assert.Throws<ArgumentException>(() => PracticeExamRules.Summarize(input with { Sections = [.. input.Sections.Skip(1), input.Sections[1]] }));
        Assert.Throws<ArgumentException>(() => PracticeExamRules.Summarize(input with { Sections = [input.Sections[0] with { Correct = 41 }, .. input.Sections.Skip(1)] }));
        Assert.Throws<ArgumentException>(() => PracticeExamRules.Summarize(input with { Sections = [input.Sections[0] with { Wrong = -1 }, .. input.Sections.Skip(1)] }));
    }

    [Fact]
    public void ResultRoundTripsAndRejectedEditsDoNotMutatePreviousDetails()
    {
        var result = ExamResult.Create(Guid.NewGuid(), Guid.NewGuid(), 350);
        var input = Blank(PracticeExamRules.Profiles.Single(x => x.Code == "LGS"));
        result.SetAnswerStatistics(0, 0, 90);
        result.SetPracticeExam(input, ExamType.LGS);
        var before = result.PracticeExamJson;
        Assert.Equal("LGS", result.GetPracticeExam()!.ExamCode);
        Assert.Throws<ArgumentException>(() => result.UpdateEditableDetails(400, 1, 0, 89, null, null, "Changed", 500));
        Assert.Equal(350, result.Score);
        Assert.Equal(before, result.PracticeExamJson);
        Assert.Throws<ArgumentException>(() => result.SetPracticeExam(input, ExamType.YKS));
        Assert.Equal(before, result.PracticeExamJson);
    }

    internal static PracticeExamInput Blank(PracticeExamProfile profile) => new(profile.Code,
        profile.Sections.Select(s => new PracticeSectionAnswers(s.Code, 0, 0, s.QuestionCount)).ToArray());
}
