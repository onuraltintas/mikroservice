using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class ExamResultManagementTests
{
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
