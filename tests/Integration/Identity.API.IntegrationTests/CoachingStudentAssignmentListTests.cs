using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetStudentAssignments;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap O1: the student's assignment list filters on the server and opens with the work still waiting.
public sealed class CoachingStudentAssignmentListTests
{
    private static readonly Guid StudentId = Guid.NewGuid();

    [Fact]
    public async Task OpenFilterListsWaitingAndLateWorkSoonestFirstAcrossPages()
    {
        await using var context = CreateContext();
        var (late, soon, later, _, _, _) = await SeedAsync(context);
        var handler = Handler(context);

        var first = await handler.Handle(new GetStudentAssignmentsQuery(StudentId, 1, 2, "open"), CancellationToken.None);
        var second = await handler.Handle(new GetStudentAssignmentsQuery(StudentId, 2, 2, "open"), CancellationToken.None);

        first.TotalCount.Should().Be(3, "only active work the student has not handed in counts");
        first.Items.Select(x => x.Id).Should().Equal(late.Id, soon.Id);
        second.Items.Select(x => x.Id).Should().Equal(later.Id);
    }

    [Theory]
    [InlineData("pending", new[] { "Yakında" , "Sonra" })]
    [InlineData("overdue", new[] { "Gecikmiş" })]
    [InlineData("submitted", new[] { "Teslim edilen" })]
    [InlineData("cancelled", new[] { "İptal edilen" })]
    public async Task EachFilterIsAppliedBeforeCountingAndPaging(string status, string[] titles)
    {
        await using var context = CreateContext();
        await SeedAsync(context);

        var page = await Handler(context).Handle(new GetStudentAssignmentsQuery(StudentId, 1, 25, status), CancellationToken.None);

        page.TotalCount.Should().Be(titles.Length);
        page.Items.Select(x => x.Title).Should().BeEquivalentTo(titles);
    }

    [Fact]
    public async Task NoFilterStillListsEveryAssignment()
    {
        await using var context = CreateContext();
        await SeedAsync(context);

        var page = await Handler(context).Handle(new GetStudentAssignmentsQuery(StudentId), CancellationToken.None);

        page.TotalCount.Should().Be(6);
    }

    [Fact]
    public async Task StartedButUnsubmittedWorkPastItsDueDateIsLate()
    {
        await using var context = CreateContext();
        var started = Assignment.Create(Guid.NewGuid(), "Başlanmış", DateTime.UtcNow.AddDays(-1));
        started.AssignToStudent(StudentId);
        started.AssignedStudents.Single().MarkAsInProgress();
        context.Assignments.Add(started); await context.SaveChangesAsync();

        var page = await Handler(context).Handle(new GetStudentAssignmentsQuery(StudentId, 1, 25, "overdue"), CancellationToken.None);

        page.Items.Should().ContainSingle().Which.IsOverdue.Should().BeTrue();
    }

    [Theory]
    [InlineData("open", true)]
    [InlineData("OVERDUE", true)]
    [InlineData(null, true)]
    [InlineData("graded", false)]
    [InlineData("'; drop", false)]
    public void UnknownFiltersAreRejected(string? status, bool valid) =>
        new GetStudentAssignmentsQueryValidator().Validate(new GetStudentAssignmentsQuery(StudentId, 1, 25, status)).IsValid.Should().Be(valid);

    [Fact]
    public void TheEndpointAcceptsTheFilterFromTheQueryString() =>
        typeof(AssignmentsController).GetMethod(nameof(AssignmentsController.GetStudentAssignments))!.GetParameters()
            .Should().Contain(x => x.Name == "status" && x.GetCustomAttributes(typeof(FromQueryAttribute), false).Length == 1);

    private static async Task<(Assignment Late, Assignment Soon, Assignment Later, Assignment Submitted, Assignment Cancelled, Assignment Completed)> SeedAsync(CoachingDbContext context)
    {
        var teacher = Guid.NewGuid();
        Assignment Make(string title, int dueInDays)
        {
            var assignment = Assignment.Create(teacher, title, DateTime.UtcNow.AddDays(dueInDays));
            assignment.AssignToStudent(StudentId);
            return assignment;
        }
        var later = Make("Sonra", 9);
        var soon = Make("Yakında", 2);
        var late = Make("Gecikmiş", -3);
        var submitted = Make("Teslim edilen", 1); submitted.SubmitAssignment(StudentId);
        var cancelled = Make("İptal edilen", 4); cancelled.Cancel();
        var completed = Make("Kapanmış", -10); completed.Complete();
        var someoneElse = Assignment.Create(teacher, "Başka öğrenci", DateTime.UtcNow.AddDays(1)); someoneElse.AssignToStudent(Guid.NewGuid());
        context.Assignments.AddRange(later, soon, late, submitted, cancelled, completed, someoneElse);
        await context.SaveChangesAsync();
        return (late, soon, later, submitted, cancelled, completed);
    }

    private static CoachingDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static GetStudentAssignmentsQueryHandler Handler(CoachingDbContext context) =>
        new(new AssignmentRepository(context), new StudentPolicy(), new AllowStudentReadClient());

    private sealed class StudentPolicy : ICoachingAccessPolicy
    {
        public Guid? CurrentUserId => StudentId;
        public bool IsSystemAdministrator => false;
        public bool IsInstitutionAdministrator => false;
        public bool IsCurrentTeacher(Guid teacherId) => false;
        public bool IsCurrentStudent(Guid requestedStudentId) => requestedStudentId == StudentId;
        public Guid RequireCurrentTeacher() => throw new NotSupportedException();
        public void RequireTeacher(Guid teacherId) => throw new NotSupportedException();
        public void RequireStudent(Guid requestedStudentId) { if (requestedStudentId != StudentId) throw new NotSupportedException(); }
        public void RequireTeacherOrStudent(Guid teacherId, Guid requestedStudentId) => throw new NotSupportedException();
        public void RequireTeacherOrAssignedStudent(Guid teacherId, IEnumerable<Guid> studentIds) => throw new NotSupportedException();
    }

    private sealed class AllowStudentReadClient : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) =>
            Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId,
            bool isSystemAdministrator, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds,
            CancellationToken cancellationToken) => Task.FromResult(studentIds);
    }
}
