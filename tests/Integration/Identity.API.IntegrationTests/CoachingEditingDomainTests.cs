using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingEditingDomainTests
{
    [Fact]
    public void AssignmentEdit_ShouldReplaceEditableFieldsAndReassignUnsubmittedStudents()
    {
        var originalStudent = Guid.NewGuid();
        var retainedStudent = Guid.NewGuid();
        var newStudent = Guid.NewGuid();
        var assignment = Assignment.Create(
            Guid.NewGuid(),
            "Eski ödev",
            DateTime.UtcNow.AddDays(2),
            institutionId: Guid.NewGuid());
        assignment.AssignToStudents([originalStudent, retainedStudent]);

        assignment.UpdateEditableDetails(
            "Yeni ödev",
            null,
            "Matematik",
            DateTime.UtcNow.AddDays(5),
            45);
        assignment.SetTargetGradeLevel(8);
        assignment.SetScoring(100, 50);
        assignment.ReassignStudents([retainedStudent, newStudent]);

        assignment.Title.Should().Be("Yeni ödev");
        assignment.Description.Should().BeNull();
        assignment.Subject.Should().Be("Matematik");
        assignment.EstimatedDurationMinutes.Should().Be(45);
        assignment.TargetGradeLevel.Should().Be(8);
        assignment.AssignedStudents.Select(item => item.StudentId)
            .Should().BeEquivalentTo([retainedStudent, newStudent]);
    }

    [Fact]
    public void AssignmentReassignment_ShouldProtectSubmittedWork()
    {
        var studentId = Guid.NewGuid();
        var assignment = Assignment.Create(
            Guid.NewGuid(),
            "Ödev",
            DateTime.UtcNow.AddDays(2));
        assignment.AssignToStudent(studentId);
        assignment.SubmitAssignment(studentId);

        var action = () => assignment.ReassignStudents([Guid.NewGuid()]);

        action.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Assignment.ReassignmentNotAllowed");
        assignment.AssignedStudents.Should().ContainSingle()
            .Which.StudentId.Should().Be(studentId);
    }

    [Fact]
    public void AssignmentReassignment_ShouldRequireAtLeastOneStudent()
    {
        var assignment = Assignment.Create(
            Guid.NewGuid(),
            "Ödev",
            DateTime.UtcNow.AddDays(2));
        assignment.AssignToStudent(Guid.NewGuid());

        var action = () => assignment.ReassignStudents([]);

        action.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Assignment.ReassignmentRequiresStudent");
    }

    [Fact]
    public void AssignmentEdit_ShouldRejectMaxScoreBelowExistingGrade()
    {
        var studentId = Guid.NewGuid();
        var assignment = Assignment.Create(
            Guid.NewGuid(),
            "Ödev",
            DateTime.UtcNow.AddDays(2));
        assignment.AssignToStudent(studentId);
        assignment.SetScoring(100);
        assignment.GradeAssignment(studentId, 80);

        var action = () => assignment.SetScoring(70);

        action.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Assignment.MaxScoreBelowGrade");
    }

    [Fact]
    public void SessionEdit_ShouldRescheduleAndAllowClearingOptionalFields()
    {
        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Eski seans",
            DateTime.UtcNow.AddDays(1),
            SessionType.OneOnOne);
        session.SetMeetingLink("https://meet.example/old");
        session.AddTeacherNotes("Eski not");

        session.UpdateEditableDetails(
            "Yeni seans",
            null,
            DateTime.UtcNow.AddDays(3),
            90,
            null,
            null);

        session.Title.Should().Be("Yeni seans");
        session.Description.Should().BeNull();
        session.DurationMinutes.Should().Be(90);
        session.MeetingLink.Should().BeNull();
        session.TeacherNotes.Should().BeNull();
    }

    [Fact]
    public void ExamEdit_ShouldRejectMaxScoreBelowExistingResult()
    {
        var exam = Exam.Create(
            Guid.NewGuid(),
            "Deneme",
            ExamType.Mock,
            DateTime.UtcNow.AddDays(1),
            100);
        var result = ExamResult.Create(exam.Id, Guid.NewGuid(), 80);
        exam.AddResult(result);

        var action = () => exam.UpdateEditableDetails(
            "Güncel deneme",
            ExamType.Weekly,
            "Matematik",
            null,
            DateTime.UtcNow.AddDays(2),
            60,
            70,
            null);

        action.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Exam.MaxScoreBelowResult");
        exam.MaxScore.Should().Be(100);
    }

    [Fact]
    public void ExamResultEdit_ShouldUpdateScoreStatisticsAndClearOptionalValues()
    {
        var exam = Exam.Create(
            Guid.NewGuid(),
            "Deneme",
            ExamType.Mock,
            DateTime.UtcNow.AddDays(1),
            100);
        var result = ExamResult.Create(exam.Id, Guid.NewGuid(), 80);
        result.SetAnswerStatistics(40, 5, 5);
        result.SetSubjectScores(new Dictionary<string, decimal> { ["Matematik"] = 80 });
        result.AddTeacherNotes("Eski not");
        exam.AddResult(result);

        exam.UpdateResult(
            result.Id,
            90,
            45,
            3,
            2,
            null,
            null,
            null);

        result.Score.Should().Be(90);
        result.CorrectAnswers.Should().Be(45);
        result.WrongAnswers.Should().Be(3);
        result.EmptyAnswers.Should().Be(2);
        result.SubjectScoresJson.Should().BeNull();
        result.TeacherNotes.Should().BeNull();
    }

    [Fact]
    public void GoalEdit_ShouldReplaceDetailsAndClearNullableTargets()
    {
        var goal = AcademicGoal.Create(
            Guid.NewGuid(),
            "Eski hedef",
            GoalCategory.ExamPreparation,
            Guid.NewGuid());
        goal.UpdateDetails("Güncel hedef", "Açıklama", GoalCategory.SubjectMastery);
        goal.SetTarget(DateTime.UtcNow.AddDays(30), 80, ExamType.YKS, "Matematik");

        goal.UpdateEditableDetails(
            "Yeni hedef",
            null,
            GoalCategory.StudyHabits,
            null,
            null,
            null,
            null);

        goal.Title.Should().Be("Yeni hedef");
        goal.Description.Should().BeNull();
        goal.Category.Should().Be(GoalCategory.StudyHabits);
        goal.TargetDate.Should().BeNull();
        goal.TargetScore.Should().BeNull();
        goal.TargetExamType.Should().BeNull();
        goal.TargetSubject.Should().BeNull();
    }
}
