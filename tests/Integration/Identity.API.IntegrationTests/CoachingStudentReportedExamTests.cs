using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudentReportedExamTests
{
    [Fact]
    public void StudentReportedExam_HasNoTeacherOrInstitutionAndCannotUseTeacherMutation()
    {
        var student = Guid.NewGuid();
        var exam = Exam.CreateStudentReported(student, "My mock exam", ExamType.Mock, DateTime.UtcNow, 100);
        exam.StudentOwnerId.Should().Be(student);
        exam.CreatedByTeacherId.Should().BeNull();
        exam.InstitutionId.Should().BeNull();
        var act = () => exam.RequireTeacherCreator();
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("Authorization.Forbidden");
    }

    [Fact]
    public void StudentReportedExam_OnlyAcceptsItsOwnersResult()
    {
        var student = Guid.NewGuid();
        var exam = Exam.CreateStudentReported(student, "My mock exam", ExamType.Mock, DateTime.UtcNow, 100);
        var foreign = () => exam.AddResult(ExamResult.Create(exam.Id, Guid.NewGuid(), 50));
        foreign.Should().Throw<BusinessRuleException>();
        var wrongExam = () => exam.AddResult(ExamResult.Create(Guid.NewGuid(), student, 50));
        wrongExam.Should().Throw<ArgumentException>();
        exam.Results.Should().BeEmpty();
        exam.AddResult(ExamResult.Create(exam.Id, student, 50));
        exam.Results.Should().ContainSingle();
    }

    [Fact]
    public void TeacherExam_RetainsItsCreatorAndHasNoStudentOwner()
    {
        var teacher = Guid.NewGuid();
        var exam = Exam.Create(teacher, "Teacher exam", ExamType.Mock, DateTime.UtcNow, 100);
        exam.StudentOwnerId.Should().BeNull();
        exam.RequireTeacherCreator().Should().Be(teacher);
    }

    [Fact]
    public void StudentReportedExam_RejectsEmptyOwnerAndInvalidDefinition()
    {
        var emptyOwner = () => Exam.CreateStudentReported(Guid.Empty, "Title", ExamType.Mock, DateTime.UtcNow, 100);
        emptyOwner.Should().Throw<ArgumentException>();
        var blankTitle = () => Exam.CreateStudentReported(Guid.NewGuid(), " ", ExamType.Mock, DateTime.UtcNow, 100);
        blankTitle.Should().Throw<ArgumentException>();
        var invalidType = () => Exam.CreateStudentReported(Guid.NewGuid(), "Title", (ExamType)99, DateTime.UtcNow, 100);
        invalidType.Should().Throw<ArgumentException>();
    }
}
