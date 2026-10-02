using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class ExamResultManagementTests
{
    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void AnswerStatistics_RejectNegativeCountsWithoutChangingTheResult(int correct, int wrong, int empty)
    {
        var result = ExamResult.Create(Guid.NewGuid(), Guid.NewGuid(), 85);
        result.SetAnswerStatistics(10, 2, 3);

        var act = () => result.SetAnswerStatistics(correct, wrong, empty);

        act.Should().Throw<ArgumentOutOfRangeException>();
        result.CorrectAnswers.Should().Be(10);
        result.WrongAnswers.Should().Be(2);
        result.EmptyAnswers.Should().Be(3);
    }

    [Fact]
    public void AnswerStatistics_AcceptZeroCounts()
    {
        var result = ExamResult.Create(Guid.NewGuid(), Guid.NewGuid(), 0);
        result.SetAnswerStatistics(0, 0, 0);
        result.CorrectAnswers.Should().Be(0);
        result.WrongAnswers.Should().Be(0);
        result.EmptyAnswers.Should().Be(0);
    }

    [Fact]
    public void Exam_CanRemoveOneResultWithoutRemovingTheExam()
    {
        var exam = Exam.Create(
            Guid.NewGuid(),
            "LGS denemesi",
            ExamType.Mock,
            DateTime.UtcNow,
            100);
        var result = ExamResult.Create(exam.Id, Guid.NewGuid(), 85);
        exam.AddResult(result);

        exam.RemoveResult(result.Id);

        exam.Results.Should().BeEmpty();
        exam.Id.Should().NotBeEmpty();
    }
}
