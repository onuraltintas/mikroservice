using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetStudentAssignments;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingTeacherAssignmentFilterTests
{
    [Fact]
    public async Task TeacherAssignmentFilter_ShouldApplyBeforeCountAndPaging()
    {
        var teacherId = Guid.NewGuid();
        await using var context = new CoachingDbContext(
            new DbContextOptionsBuilder<CoachingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var activeOne = Assignment.Create(teacherId, "Aktif ödev 1", DateTime.UtcNow.AddDays(1));
        var activeTwo = Assignment.Create(teacherId, "Aktif ödev 2", DateTime.UtcNow.AddDays(2));
        var completed = Assignment.Create(teacherId, "Tamamlanan ödev", DateTime.UtcNow.AddDays(-1));
        completed.Complete();
        context.Assignments.AddRange(
            activeOne,
            activeTwo,
            completed,
            Assignment.Create(Guid.NewGuid(), "Başka öğretmenin ödevi", DateTime.UtcNow.AddDays(1)));
        await context.SaveChangesAsync();

        var page = await new AssignmentRepository(context).GetByTeacherIdAsync(
            teacherId,
            pageNumber: 2,
            pageSize: 1,
            cancellationToken: CancellationToken.None,
            status: AssignmentStatus.Active);

        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle().Which.Status.Should().Be(AssignmentStatus.Active);
        page.Items.Single().TeacherId.Should().Be(teacherId);
    }

    [Fact]
    public void CancelledAssignment_ShouldRejectNewStudentWork()
    {
        var assignment = Assignment.Create(Guid.NewGuid(), "İptal edilmiş ödev", DateTime.UtcNow.AddDays(1));
        assignment.AssignToStudent(Guid.NewGuid());
        assignment.Cancel();

        var submit = () => assignment.SubmitAssignment(assignment.AssignedStudents.Single().StudentId);
        var prepareAttachment = () => assignment.EnsureAcceptingStudentWork();

        submit.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Assignment.NotAcceptingStudentWork");
        prepareAttachment.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Assignment.NotAcceptingStudentWork");
    }

    [Fact]
    public async Task StudentAssignmentHistory_ShouldExposeCancelledStateWithoutMarkingItOverdue()
    {
        var studentId = Guid.NewGuid();
        await using var context = new CoachingDbContext(
            new DbContextOptionsBuilder<CoachingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var assignment = Assignment.Create(Guid.NewGuid(), "İptal edilen ödev", DateTime.UtcNow.AddDays(-1));
        assignment.AssignToStudent(studentId);
        assignment.Cancel();
        context.Assignments.Add(assignment);
        await context.SaveChangesAsync();

        var handler = new GetStudentAssignmentsQueryHandler(
            new AssignmentRepository(context),
            new StudentPolicy(studentId),
            new AllowStudentReadClient());
        var result = await handler.Handle(new GetStudentAssignmentsQuery(studentId), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Should().Match<StudentAssignmentDto>(item =>
            item.AssignmentStatus == "Cancelled"
            && !item.IsOverdue);
    }

    private sealed class StudentPolicy(Guid studentId) : ICoachingAccessPolicy
    {
        public Guid? CurrentUserId => studentId;
        public bool IsSystemAdministrator => false;
        public bool IsInstitutionAdministrator => false;
        public bool IsCurrentTeacher(Guid teacherId) => false;
        public bool IsCurrentStudent(Guid requestedStudentId) => requestedStudentId == studentId;
        public Guid RequireCurrentTeacher() => throw new NotSupportedException();
        public void RequireTeacher(Guid teacherId) => throw new NotSupportedException();
        public void RequireStudent(Guid requestedStudentId)
        {
            if (requestedStudentId != studentId) throw new NotSupportedException();
        }
        public void RequireTeacherOrStudent(Guid teacherId, Guid requestedStudentId) => throw new NotSupportedException();
        public void RequireTeacherOrAssignedStudent(Guid teacherId, IEnumerable<Guid> studentIds) => throw new NotSupportedException();
    }

    private sealed class AllowStudentReadClient : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) =>
            Task.FromResult<CoachingAdminAccessScope?>(null);

        public Task<Guid?> AuthorizeTeacherTargetsAsync(
            Guid teacherId,
            IReadOnlyCollection<Guid> studentIds,
            Guid? requestedInstitutionId,
            bool isSystemAdministrator,
            CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);

        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
            Guid viewerUserId,
            IReadOnlyCollection<Guid> studentIds,
            CancellationToken cancellationToken) => Task.FromResult(studentIds);
    }
}
