using System.Reflection;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Commands.RegisterStudent;
using Identity.Application.Commands.RegisterTeacher;
using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;

namespace Identity.API.IntegrationTests;

public sealed class ProductRegistrationProfileOwnershipTests
{
    [Fact]
    public async Task SpeedReadingStudentRegistration_DoesNotCreateCoachingStudentProfile()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "student@example.test");
        var users = CreateUserRepository(user);
        var students = DispatchProxy.Create<IStudentRepository, StudentRepositoryProxy>();
        var handler = new RegisterStudentCommandHandler(
            CreateIdentityService(userId),
            users,
            students,
            DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>(),
            DispatchProxy.Create<IPublishEndpoint, PublishEndpointProxy>(),
            DispatchProxy.Create<IConfigurationService, ConfigurationServiceProxy>());

        var result = await handler.Handle(
            new RegisterStudentCommand(
                "student@example.test", "StrongPassword1!", "Test", "Student", null, PlatformProduct.SpeedReading),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, ((StudentRepositoryProxy)(object)students).AddCount);
        Assert.True(user.HasProductAccess(PlatformProduct.SpeedReading));
    }

    [Fact]
    public async Task SpeedReadingTeacherRegistration_DoesNotCreateCoachingTeacherProfile()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "teacher@example.test");
        var users = CreateUserRepository(user);
        var teachers = DispatchProxy.Create<ITeacherRepository, TeacherRepositoryProxy>();
        var handler = new RegisterTeacherCommandHandler(
            CreateIdentityService(userId),
            users,
            teachers,
            DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>(),
            DispatchProxy.Create<IPublishEndpoint, PublishEndpointProxy>(),
            DispatchProxy.Create<IConfigurationService, ConfigurationServiceProxy>());

        var result = await handler.Handle(
            new RegisterTeacherCommand(
                "teacher@example.test", "StrongPassword1!", "Test", "Teacher", null, PlatformProduct.SpeedReading),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, ((TeacherRepositoryProxy)(object)teachers).AddCount);
        Assert.True(user.HasProductAccess(PlatformProduct.SpeedReading));
    }

    private static IIdentityService CreateIdentityService(Guid userId)
    {
        var proxy = DispatchProxy.Create<IIdentityService, IdentityServiceProxy>();
        ((IdentityServiceProxy)(object)proxy).UserId = userId;
        return proxy;
    }

    private static IUserRepository CreateUserRepository(User user)
    {
        var proxy = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        ((UserRepositoryProxy)(object)proxy).User = user;
        return proxy;
    }

    public class IdentityServiceProxy : DispatchProxy
    {
        public Guid UserId { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IIdentityService.RegisterUserAsync) => Task.FromResult(Result.Success(UserId)),
            nameof(IIdentityService.AssignRoleForProductAsync) => Task.FromResult(Result.Success()),
            nameof(IIdentityService.DeleteUserAsync) => Task.FromResult(Result.Success()),
            _ => throw new NotSupportedException($"Unexpected identity-service call: {targetMethod?.Name}")
        };
    }

    public class UserRepositoryProxy : DispatchProxy
    {
        public User User { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IUserRepository.GetByIdAsync) => Task.FromResult<User?>(User),
            nameof(IUserRepository.TrackProductAccessIfNew) => null,
            _ => throw new NotSupportedException($"Unexpected user-repository call: {targetMethod?.Name}")
        };
    }

    public class StudentRepositoryProxy : DispatchProxy
    {
        public int AddCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IStudentRepository.AddAsync))
            {
                AddCount++;
                return Task.CompletedTask;
            }

            throw new NotSupportedException($"Unexpected student-repository call: {targetMethod?.Name}");
        }
    }

    public class TeacherRepositoryProxy : DispatchProxy
    {
        public int AddCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ITeacherRepository.AddAsync))
            {
                AddCount++;
                return Task.CompletedTask;
            }

            throw new NotSupportedException($"Unexpected teacher-repository call: {targetMethod?.Name}");
        }
    }

    public class UnitOfWorkProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IUnitOfWork.SaveChangesAsync)
                ? Task.FromResult(1)
                : targetMethod?.Name == nameof(IUnitOfWork.ClearTracking)
                    ? null
                    : throw new NotSupportedException($"Unexpected unit-of-work call: {targetMethod?.Name}");
    }

    public class PublishEndpointProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Task.CompletedTask;
    }

    public class ConfigurationServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IConfigurationService.GetConfigurationValueAsync) => Task.FromResult<string?>("true"),
            nameof(IConfigurationService.GetManageableConfigurationValueAsync) => Task.FromResult<string?>(null),
            nameof(IConfigurationService.GetPublicConfigurationValueAsync) => Task.FromResult<string?>(null),
            nameof(IConfigurationService.GetAllConfigurationsAsync) => Task.FromResult(new List<ConfigurationDto>()),
            _ => throw new NotSupportedException($"Unexpected configuration-service call: {targetMethod?.Name}")
        };
    }
}
