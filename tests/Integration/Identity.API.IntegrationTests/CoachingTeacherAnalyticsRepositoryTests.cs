using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingTeacherAnalyticsRepositoryTests
{
    [Fact]
    public async Task TeacherAnalytics_ShouldMeasureOnlySelectedInstitutionResults()
    {
        await using var context = new CoachingDbContext(
            new DbContextOptionsBuilder<CoachingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var institutionId = Guid.NewGuid();
        var otherInstitutionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var assignment = Assignment.Create(teacherId, "Kurum ödevi", now.AddDays(1),
            AssignmentType.Individual, institutionId);
        assignment.AssignToStudent(studentId);
        assignment.SubmitAssignment(studentId, "Tamamlandı");

        var otherAssignment = Assignment.Create(teacherId, "Diğer kurum ödevi", now.AddDays(1),
            AssignmentType.Individual, otherInstitutionId);
        otherAssignment.AssignToStudent(studentId);

        var exam = Exam.Create(teacherId, "Kurum sınavı", ExamType.Mock, now, 100, institutionId);
        exam.AddResult(ExamResult.Create(exam.Id, studentId, 80));
        var otherExam = Exam.Create(teacherId, "Diğer sınav", ExamType.Mock, now, 100, otherInstitutionId);
        otherExam.AddResult(ExamResult.Create(otherExam.Id, studentId, 20));

        var session = CoachingSession.Create(teacherId, "Kurum görüşmesi", now,
            SessionType.OneOnOne, institutionId: institutionId);
        session.AddStudent(studentId);
        session.RecordAttendance(studentId, attended: true);

        context.AddRange(assignment, otherAssignment, exam, otherExam, session);
        await context.SaveChangesAsync();

        var result = await new CoachingAdminRepository(context)
            .GetTeacherAnalyticsAsync(teacherId, institutionId, now.AddDays(-1), now.AddDays(1));

        result.AssignedAssignmentCount.Should().Be(1);
        result.SubmittedAssignmentCount.Should().Be(1);
        result.AverageExamPercentage.Should().Be(80);
        result.AttendanceRecordedCount.Should().Be(1);
        result.AttendedSessionCount.Should().Be(1);
    }
}
