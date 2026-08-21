using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingComparativeReportRepositoryTests
{
    [Fact]
    public async Task InstitutionComparison_ShouldAggregateOnlyScopedTenantRows()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var otherInstitutionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();

        var assignment = Assignment.Create(
            Guid.NewGuid(),
            "Deneme analizi",
            DateTime.UtcNow.AddDays(2),
            AssignmentType.Individual,
            institutionId);
        assignment.SetScoring(100);
        assignment.AssignToStudent(studentId);
        assignment.SubmitAssignment(studentId, "Tamamlandı");
        assignment.GradeAssignment(studentId, 80);

        var otherAssignment = Assignment.Create(
            Guid.NewGuid(),
            "Diğer kurum ödevi",
            DateTime.UtcNow.AddDays(2),
            AssignmentType.Individual,
            otherInstitutionId);
        otherAssignment.AssignToStudent(otherStudentId);

        var exam = Exam.Create(
            Guid.NewGuid(),
            "Kurum denemesi",
            ExamType.Mock,
            DateTime.UtcNow,
            100,
            institutionId);
        var examResult = ExamResult.Create(exam.Id, studentId, 75);
        exam.AddResult(examResult);

        var otherExam = Exam.Create(
            Guid.NewGuid(),
            "Diğer kurum sınavı",
            ExamType.Mock,
            DateTime.UtcNow,
            100,
            otherInstitutionId);
        otherExam.AddResult(ExamResult.Create(otherExam.Id, otherStudentId, 100));

        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Kurum seansı",
            DateTime.UtcNow.AddDays(1),
            SessionType.OneOnOne,
            institutionId: institutionId);
        session.AddStudent(studentId);
        session.RecordAttendance(studentId, attended: true);

        var goal = AcademicGoal.Create(studentId, "Hedef", GoalCategory.ExamPreparation);
        goal.UpdateProgress(100);

        context.AddRange(assignment, otherAssignment, exam, otherExam, session, goal);
        await context.SaveChangesAsync();

        var result = await new CoachingComparativeReportRepository(context)
            .GetInstitutionComparisonAsync(
                institutionId,
                [studentId],
                null,
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow.AddDays(2),
                CancellationToken.None);

        result.StudentCount.Should().Be(1);
        result.AssignmentCount.Should().Be(1);
        result.AssignedAssignmentCount.Should().Be(1);
        result.SubmittedAssignmentCount.Should().Be(1);
        result.GradedAssignmentCount.Should().Be(1);
        result.AverageAssignmentPercentage.Should().Be(80m);
        result.ExamCount.Should().Be(1);
        result.ExamResultCount.Should().Be(1);
        result.AverageExamPercentage.Should().Be(75m);
        result.SessionCount.Should().Be(1);
        result.AttendanceRecordedCount.Should().Be(1);
        result.AttendedSessionCount.Should().Be(1);
        result.AttendancePercentage.Should().Be(100m);
        result.GoalCount.Should().Be(1);
        result.CompletedGoalCount.Should().Be(1);
        result.AverageGoalProgress.Should().Be(100);
    }

    private static CoachingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options);
    }
}
