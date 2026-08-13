using System.Security.Claims;
using Coaching.Application.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAccessPolicyTests
{
    [Fact]
    public void Teacher_ShouldOnlyAccessOwnTeacherResources()
    {
        var teacherId = Guid.NewGuid();
        var policy = CreatePolicy(teacherId, "Teacher");

        policy.Invoking(value => value.RequireTeacher(teacherId)).Should().NotThrow();
        policy.Invoking(value => value.RequireTeacher(Guid.NewGuid()))
            .Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Authorization.Forbidden");
    }

    [Fact]
    public void Student_ShouldOnlyAccessOwnStudentResources()
    {
        var studentId = Guid.NewGuid();
        var policy = CreatePolicy(studentId, "Student");

        policy.Invoking(value => value.RequireStudent(studentId)).Should().NotThrow();
        policy.Invoking(value => value.RequireStudent(Guid.NewGuid()))
            .Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Authorization.Forbidden");
    }

    [Fact]
    public void AssignedStudent_ShouldReadAssignmentButNotOtherAssignment()
    {
        var studentId = Guid.NewGuid();
        var policy = CreatePolicy(studentId, "Student");

        policy.Invoking(value => value.RequireTeacherOrAssignedStudent(
                Guid.NewGuid(), new[] { studentId }))
            .Should().NotThrow();

        policy.Invoking(value => value.RequireTeacherOrAssignedStudent(
                Guid.NewGuid(), new[] { Guid.NewGuid() }))
            .Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void SystemAdmin_ShouldAccessAnyCoachingResource()
    {
        var policy = CreatePolicy(Guid.NewGuid(), "SystemAdmin");

        policy.Invoking(value => value.RequireTeacher(Guid.NewGuid())).Should().NotThrow();
        policy.Invoking(value => value.RequireStudent(Guid.NewGuid())).Should().NotThrow();
    }

    private static ICoachingAccessPolicy CreatePolicy(Guid userId, params string[] roles) =>
        new CoachingAccessPolicy(new StubCurrentUserService(userId, roles));

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        private readonly Guid _userId;
        private readonly string[] _roles;

        public StubCurrentUserService(Guid userId, string[] roles)
        {
            _userId = userId;
            _roles = roles;
        }

        public Guid? UserId => _userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => _roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
