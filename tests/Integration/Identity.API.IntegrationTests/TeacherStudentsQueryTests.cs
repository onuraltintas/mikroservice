using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetAllUsers;
using Identity.Application.Queries.GetTeacherStudents;
using Identity.Domain.Entities;
using EduPlatform.Shared.Contracts.Reporting;
using System.Security.Claims;

namespace Identity.API.IntegrationTests;

public sealed class TeacherStudentsQueryTests
{
    [Fact]
    public async Task Handle_ShouldReturnPagedStudentsForAuthenticatedTeacher()
    {
        var teacherId = Guid.NewGuid();
        var expected = new TeacherStudentDto(
            Guid.NewGuid(),
            "Ada",
            "Yılmaz",
            "Ada Yılmaz",
            8,
            Guid.NewGuid(),
            "Örnek Kurum",
            null,
            "Matematik",
            DateTime.UtcNow.AddDays(-5));
        var handler = new GetTeacherStudentsQueryHandler(
            new StubTeacherRepository(new PagedList<TeacherStudentDto>([expected], 1, 1, 25)),
            new StubCurrentUserService(teacherId, ["Teacher"]));

        var result = await handler.Handle(
            new GetTeacherStudentsQuery(2, 25, "Ada"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle().Which.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ShouldRejectAnonymousCall()
    {
        var handler = new GetTeacherStudentsQueryHandler(
            new StubTeacherRepository(new PagedList<TeacherStudentDto>([], 0, 1, 25)),
            new StubCurrentUserService(null, []));

        var result = await handler.Handle(new GetTeacherStudentsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Error.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldRejectNonTeacherRole()
    {
        var handler = new GetTeacherStudentsQueryHandler(
            new StubTeacherRepository(new PagedList<TeacherStudentDto>([], 0, 1, 25)),
            new StubCurrentUserService(Guid.NewGuid(), ["Student"]));

        var result = await handler.Handle(new GetTeacherStudentsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Error.Forbidden");
    }

    private sealed class StubTeacherRepository(PagedList<TeacherStudentDto> result) : ITeacherRepository
    {
        public Task<PagedList<TeacherStudentDto>> GetStudentsByTeacherUserIdAsync(
            Guid teacherUserId,
            int pageNumber,
            int pageSize,
            string? searchTerm,
            CancellationToken cancellationToken) => Task.FromResult(result);

        public Task AddAsync(TeacherProfile teacher, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TeacherProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TeacherProfile?> GetByUserIdAsync(Guid userId, Guid? institutionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TeacherProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SpeedReadingTeacherStudentScopeResponse?> GetSpeedReadingTeacherStudentScopeAsync(
            Guid viewerUserId,
            Guid? targetTeacherUserId,
            CancellationToken cancellationToken) => Task.FromResult<SpeedReadingTeacherStudentScopeResponse?>(null);
        public Task AddStudentAssignmentAsync(TeacherStudentAssignment assignment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TeacherStudentAssignment?> GetAssignmentAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubCurrentUserService(Guid? userId, IEnumerable<string> roles) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => roles;
        public bool IsAuthenticated => userId.HasValue;
        public ClaimsPrincipal? User => null;
    }
}
