using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Application.Interfaces;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Kernel.Primitives;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminStudentHistoryTests
{
    [Theory]
    [InlineData(CoachingStudentHistoryType.Assignments)]
    [InlineData(CoachingStudentHistoryType.Exams)]
    [InlineData(CoachingStudentHistoryType.Sessions)]
    [InlineData(CoachingStudentHistoryType.Goals)]
    public async Task StudentHistory_IsPagedAndIncludesRecordsFromEveryInstitution(
        CoachingStudentHistoryType type)
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var currentInstitutionId = Guid.NewGuid();
        var previousInstitutionId = Guid.NewGuid();

        var assignmentIds = new[] { currentInstitutionId, previousInstitutionId }
            .Select(institutionId =>
            {
                var assignment = Assignment.Create(
                    Guid.NewGuid(), institutionId.ToString(), DateTime.UtcNow.AddDays(1),
                    institutionId: institutionId);
                assignment.AssignToStudent(studentId);
                return assignment;
            }).ToArray();

        var exams = new[] { currentInstitutionId, previousInstitutionId }
            .Select((institutionId, index) =>
            {
                var exam = Exam.Create(
                    Guid.NewGuid(), institutionId.ToString(), ExamType.Mock,
                    DateTime.UtcNow.AddDays(-index), 100, institutionId);
                exam.AddResult(ExamResult.Create(exam.Id, studentId, 80));
                return exam;
            }).ToArray();

        var sessions = new[] { currentInstitutionId, previousInstitutionId }
            .Select((institutionId, index) =>
            {
                var session = CoachingSession.Create(
                    Guid.NewGuid(), institutionId.ToString(), DateTime.UtcNow.AddDays(index + 1),
                    SessionType.OneOnOne, institutionId: institutionId);
                session.AddStudent(studentId);
                return session;
            }).ToArray();

        var goals = new[]
        {
            AcademicGoal.Create(studentId, "Current goal", GoalCategory.ExamPreparation),
            AcademicGoal.Create(studentId, "Previous goal", GoalCategory.ExamPreparation)
        };
        context.AddRange(assignmentIds);
        context.AddRange(exams);
        context.AddRange(sessions);
        context.AddRange(goals);
        await context.SaveChangesAsync();

        var repository = new CoachingAdminRepository(context);
        var firstPage = await repository.GetStudentHistoryAsync(studentId, type, 1, 1, CancellationToken.None);
        var secondPage = await repository.GetStudentHistoryAsync(studentId, type, 2, 1, CancellationToken.None);

        firstPage.TotalCount.Should().Be(2);
        secondPage.TotalCount.Should().Be(2);
        firstPage.Items.Should().ContainSingle();
        secondPage.Items.Should().ContainSingle();
        firstPage.Items[0].Id.Should().NotBe(secondPage.Items[0].Id);
        firstPage.Items[0].Type.Should().Be(type.ToString());
        secondPage.Items[0].Type.Should().Be(type.ToString());
    }

    [Fact]
    public async Task AssignmentHistory_filters_before_paging_and_keeps_previous_institution_records()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var old = Assignment.Create(Guid.NewGuid(), "Math older", DateTime.UtcNow.AddDays(1), institutionId: Guid.NewGuid());
        var current = Assignment.Create(Guid.NewGuid(), "Math current", DateTime.UtcNow.AddDays(1), institutionId: Guid.NewGuid());
        var unrelated = Assignment.Create(Guid.NewGuid(), "Reading", DateTime.UtcNow.AddDays(1), institutionId: Guid.NewGuid());
        old.AssignToStudent(studentId);
        current.AssignToStudent(studentId);
        unrelated.AssignToStudent(studentId);
        SetCreatedAt(old.AssignedStudents.Single(), DateTime.UtcNow.AddDays(-10));
        SetCreatedAt(current.AssignedStudents.Single(), DateTime.UtcNow.AddDays(-1));
        SetCreatedAt(unrelated.AssignedStudents.Single(), DateTime.UtcNow.AddDays(-1));
        context.Assignments.AddRange(old, current, unrelated);
        await context.SaveChangesAsync();

        var page = await new CoachingAdminRepository(context).GetStudentHistoryAsync(
            studentId, CoachingStudentHistoryType.Assignments, 1, 1, CancellationToken.None,
            new CoachingStudentHistoryFilter(DateTime.UtcNow.AddDays(-20), DateTime.UtcNow.AddDays(-5), "Assigned", "Math"));

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Id.Should().Be(old.Id);
    }

    [Fact]
    public async Task AssignmentHistory_UsesWhenTheStudentWasAssignedRatherThanWhenTheAssignmentWasCreated()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var olderAssignment = Assignment.Create(
            Guid.NewGuid(), "Reading", DateTime.UtcNow.AddDays(2),
            institutionId: Guid.NewGuid());
        var newerAssignment = Assignment.Create(
            Guid.NewGuid(), "Math", DateTime.UtcNow.AddDays(3),
            institutionId: Guid.NewGuid());
        SetCreatedAt(olderAssignment, DateTime.UtcNow.AddDays(-20));
        SetCreatedAt(newerAssignment, DateTime.UtcNow.AddDays(-10));
        olderAssignment.AssignToStudent(studentId);
        newerAssignment.AssignToStudent(studentId);
        var olderAssignmentSentAt = DateTime.UtcNow.AddDays(-5);
        var newerAssignmentSentAt = DateTime.UtcNow.AddDays(-8);
        SetCreatedAt(olderAssignment.AssignedStudents.Single(), olderAssignmentSentAt);
        SetCreatedAt(newerAssignment.AssignedStudents.Single(), newerAssignmentSentAt);
        context.Assignments.AddRange(olderAssignment, newerAssignment);
        await context.SaveChangesAsync();

        var page = await new CoachingAdminRepository(context).GetStudentHistoryAsync(
            studentId, CoachingStudentHistoryType.Assignments, 1, 25, CancellationToken.None);

        page.Items.Should().HaveCount(2);
        page.Items.Select(item => item.Id).Should().ContainInOrder(olderAssignment.Id, newerAssignment.Id);
        page.Items[0].EventDate.Should().Be(olderAssignmentSentAt);
        page.Items[1].EventDate.Should().Be(newerAssignmentSentAt);
    }

    private static void SetCreatedAt(object entity, DateTime createdAt) =>
        entity.GetType().GetProperty(nameof(Entity<Guid>.CreatedAt))!.SetValue(entity, createdAt);

    private static CoachingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options);
    }
}
