using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Commands.UpdateUser;
using Identity.Application.Commands.UpdateUserProfile;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class ProductScopedProfileUpdateTests
{
    [Fact]
    public async Task SpeedReadingProfileUpdate_ShouldNotModifyCoachingTeacherProfile()
    {
        var user = User.Create(Guid.NewGuid(), "teacher@example.test");
        user.AddRole(CreateUserRole(user.Id, "Teacher", PlatformProduct.SpeedReading));
        var teacher = TeacherProfile.Create(user.Id, "Old", "Name");
        var users = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        ((UserRepositoryProxy)(object)users).User = user;
        var teachers = DispatchProxy.Create<ITeacherRepository, TeacherRepositoryProxy>();
        ((TeacherRepositoryProxy)(object)teachers).Profile = teacher;
        var students = DispatchProxy.Create<IStudentRepository, StudentRepositoryProxy>();
        var unitOfWork = DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>();
        var handler = new UpdateUserProfileCommandHandler(
            users,
            teachers,
            students,
            unitOfWork,
            new TestCurrentUserService(user.Id));

        var result = await handler.Handle(
            new UpdateUserProfileCommand(
                "New",
                "Name",
                null,
                "Speed-reading bio",
                null,
                "Speed-reading title",
                ["Reading"],
                null,
                null,
                null,
                Product: PlatformProduct.SpeedReading),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", user.FirstName);
        Assert.Equal("Old", teacher.FirstName);
        Assert.Null(teacher.Title);
        Assert.Null(teacher.Bio);
        Assert.Equal(0, ((TeacherRepositoryProxy)(object)teachers).ReadCount);
    }

    [Fact]
    public async Task CoachingProfileUpdate_ShouldUpdateCoachingTeacherProfile()
    {
        var user = User.Create(Guid.NewGuid(), "teacher@example.test");
        user.AddRole(CreateUserRole(user.Id, "Teacher", PlatformProduct.Coaching));
        var teacher = TeacherProfile.Create(user.Id, "Old", "Name");
        var users = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        ((UserRepositoryProxy)(object)users).User = user;
        var teachers = DispatchProxy.Create<ITeacherRepository, TeacherRepositoryProxy>();
        ((TeacherRepositoryProxy)(object)teachers).Profile = teacher;
        var students = DispatchProxy.Create<IStudentRepository, StudentRepositoryProxy>();
        var unitOfWork = DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>();
        var handler = new UpdateUserProfileCommandHandler(
            users,
            teachers,
            students,
            unitOfWork,
            new TestCurrentUserService(user.Id));

        var result = await handler.Handle(
            new UpdateUserProfileCommand(
                "New",
                "Name",
                null,
                "Coaching bio",
                null,
                "Coach",
                ["Math"],
                null,
                null,
                null,
                Product: PlatformProduct.Coaching),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", teacher.FirstName);
        Assert.Equal("Coach", teacher.Title);
        Assert.Equal("Coaching bio", teacher.Bio);
        Assert.Equal(1, ((TeacherRepositoryProxy)(object)teachers).ReadCount);
    }

    [Fact]
    public async Task SpeedReadingAdminProfileUpdate_ShouldNotModifyCoachingTeacherProfile()
    {
        var user = User.Create(Guid.NewGuid(), "teacher@example.test");
        user.AddRole(CreateUserRole(user.Id, "Teacher", PlatformProduct.SpeedReading));
        var teacher = TeacherProfile.Create(user.Id, "Old", "Name");
        var users = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        ((UserRepositoryProxy)(object)users).User = user;
        var teachers = DispatchProxy.Create<ITeacherRepository, TeacherRepositoryProxy>();
        ((TeacherRepositoryProxy)(object)teachers).Profile = teacher;
        var students = DispatchProxy.Create<IStudentRepository, StudentRepositoryProxy>();
        var institutions = DispatchProxy.Create<IInstitutionRepository, InstitutionRepositoryProxy>();
        var unitOfWork = DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>();
        var handler = new UpdateUserCommandHandler(
            users,
            unitOfWork,
            students,
            teachers,
            institutions);

        var result = await handler.Handle(
            new UpdateUserCommand(
                user.Id,
                "New",
                "Name",
                null,
                Bio: "Speed-reading bio",
                TeacherTitle: "Speed-reading title",
                UpdateRoleProfile: true,
                Product: PlatformProduct.SpeedReading),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", user.FirstName);
        Assert.Equal("Old", teacher.FirstName);
        Assert.Null(teacher.Title);
        Assert.Null(teacher.Bio);
        Assert.Equal(0, ((TeacherRepositoryProxy)(object)teachers).ReadCount);
    }

    [Fact]
    public async Task CoachingAdminProfileUpdate_ShouldUpdateCoachingTeacherProfile()
    {
        var user = User.Create(Guid.NewGuid(), "teacher@example.test");
        user.AddRole(CreateUserRole(user.Id, "Teacher", PlatformProduct.Coaching));
        var teacher = TeacherProfile.Create(user.Id, "Old", "Name");
        var users = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        ((UserRepositoryProxy)(object)users).User = user;
        var teachers = DispatchProxy.Create<ITeacherRepository, TeacherRepositoryProxy>();
        ((TeacherRepositoryProxy)(object)teachers).Profile = teacher;
        var students = DispatchProxy.Create<IStudentRepository, StudentRepositoryProxy>();
        var institutions = DispatchProxy.Create<IInstitutionRepository, InstitutionRepositoryProxy>();
        var unitOfWork = DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>();
        var handler = new UpdateUserCommandHandler(
            users,
            unitOfWork,
            students,
            teachers,
            institutions);

        var result = await handler.Handle(
            new UpdateUserCommand(
                user.Id,
                "New",
                "Name",
                null,
                Bio: "Coaching bio",
                TeacherTitle: "Coach",
                UpdateRoleProfile: true,
                Product: PlatformProduct.Coaching),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", teacher.FirstName);
        Assert.Equal("Coach", teacher.Title);
        Assert.Equal("Coaching bio", teacher.Bio);
        Assert.Equal(1, ((TeacherRepositoryProxy)(object)teachers).ReadCount);
    }

    private static Identity.Domain.Entities.UserRole CreateUserRole(
        Guid userId,
        string name,
        PlatformProduct product)
    {
        var role = Role.Create(name, name);
        var userRole = new Identity.Domain.Entities.UserRole(userId, role.Id, product);
        typeof(Identity.Domain.Entities.UserRole)
            .GetProperty(nameof(Identity.Domain.Entities.UserRole.Role))!
            .SetValue(userRole, role);
        return userRole;
    }

    public class UserRepositoryProxy : DispatchProxy
    {
        public User User { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IUserRepository.GetByIdAsync)
                ? Task.FromResult<User?>(User)
                : throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}");
    }

    public class TeacherRepositoryProxy : DispatchProxy
    {
        public TeacherProfile Profile { get; set; } = null!;
        public int ReadCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ITeacherRepository.GetByUserIdAsync))
            {
                ReadCount++;
                return Task.FromResult<TeacherProfile?>(Profile);
            }

            throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}");
        }
    }

    public class StudentRepositoryProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}");
    }

    public class UnitOfWorkProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IUnitOfWork.SaveChangesAsync)
                ? Task.FromResult(1)
                : targetMethod?.Name == nameof(IUnitOfWork.ClearTracking)
                    ? null
                    : throw new NotSupportedException($"Unexpected unit of work call: {targetMethod?.Name}");
    }

    public class InstitutionRepositoryProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException($"Unexpected institution repository call: {targetMethod?.Name}");
    }

    private sealed class TestCurrentUserService(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => "teacher@example.test";
        public string? FullName => "Test Teacher";
        public IEnumerable<string> Roles => ["Teacher"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => new(new ClaimsIdentity("test"));
    }
}
