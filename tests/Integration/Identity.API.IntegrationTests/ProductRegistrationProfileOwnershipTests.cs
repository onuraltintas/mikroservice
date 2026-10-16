using System.Reflection;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Commands.RegisterStudent;
using Identity.Application.Commands.RegisterTeacher;
using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;
using EduPlatform.Shared.Contracts.Coaching;

namespace Identity.API.IntegrationTests;

public sealed class ProductRegistrationProfileOwnershipTests
{
    [Fact]
    public async Task TeacherRegistrationWithInvalidLegalAcceptanceDoesNotCreateAnIdentityAccount()
    {
        var userId = Guid.NewGuid();
        var identity = CreateIdentityService(userId);
        var handler = new RegisterTeacherCommandHandler(
            identity,
            CreateUserRepository(User.Create(userId, "teacher@example.test")),
            DispatchProxy.Create<ITeacherRepository, TeacherRepositoryProxy>(),
            DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>(),
            DispatchProxy.Create<IPublishEndpoint, PublishEndpointProxy>(),
            DispatchProxy.Create<IConfigurationService, ConfigurationServiceProxy>(),
            new RejectingLegalConsentService(), null!);

        var result = await handler.Handle(
            new RegisterTeacherCommand(
                "teacher@example.test", "StrongPassword1!", "Test", "Teacher", null, PlatformProduct.Coaching),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.LegalAcceptanceRequired", result.Error.Code);
        var identityProxy = (IdentityServiceProxy)(object)identity;
        Assert.Equal(0, identityProxy.RegisterCount);
        Assert.Equal(0, identityProxy.AssignRoleCount);
    }

    [Theory]
    [InlineData(PlatformProduct.SpeedReading)]
    [InlineData(PlatformProduct.Coaching)]
    public async Task StudentRegistration_ProfileIsOwnedByProduct(PlatformProduct product)
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "student@example.test");
        var users = CreateUserRepository(user);
        var students = DispatchProxy.Create<IStudentRepository, StudentRepositoryProxy>();
        var publisher = DispatchProxy.Create<IPublishEndpoint, PublishEndpointProxy>();
        var handler = new RegisterStudentCommandHandler(
            CreateIdentityService(userId),
            users,
            students,
            DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>(),
            publisher,
            DispatchProxy.Create<IConfigurationService, ConfigurationServiceProxy>(),
            new AcceptingLegalConsentService(), null!);

        var result = await handler.Handle(
            new RegisterStudentCommand(
                "student@example.test", "StrongPassword1!", "Test", "Student", null, product),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, ((StudentRepositoryProxy)(object)students).AddCount);
        Assert.True(user.HasProductAccess(product));
        var events = ((PublishEndpointProxy)(object)publisher).Messages.OfType<CoachingProfileProvisioningRequested>();
        if (product == PlatformProduct.Coaching)
        {
            var message = Assert.Single(events);
            Assert.Equal(userId, message.UserId);
            Assert.Equal("Student", message.Role);
        }
        else Assert.Empty(events);
    }

    [Theory]
    [InlineData(PlatformProduct.SpeedReading)]
    [InlineData(PlatformProduct.Coaching)]
    public async Task TeacherRegistration_ProfileIsOwnedByProduct(PlatformProduct product)
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "teacher@example.test");
        var users = CreateUserRepository(user);
        var teachers = DispatchProxy.Create<ITeacherRepository, TeacherRepositoryProxy>();
        var publisher = DispatchProxy.Create<IPublishEndpoint, PublishEndpointProxy>();
        var handler = new RegisterTeacherCommandHandler(
            CreateIdentityService(userId),
            users,
            teachers,
            DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>(),
            publisher,
            DispatchProxy.Create<IConfigurationService, ConfigurationServiceProxy>(),
            new AcceptingLegalConsentService(), null!);

        var result = await handler.Handle(
            new RegisterTeacherCommand(
                "teacher@example.test", "StrongPassword1!", "Test", "Teacher", null, product),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, ((TeacherRepositoryProxy)(object)teachers).AddCount);
        Assert.True(user.HasProductAccess(product));
        var events = ((PublishEndpointProxy)(object)publisher).Messages.OfType<CoachingProfileProvisioningRequested>();
        if (product == PlatformProduct.Coaching)
        {
            var message = Assert.Single(events);
            Assert.Equal(userId, message.UserId);
            Assert.Equal("Teacher", message.Role);
        }
        else Assert.Empty(events);
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
        public int RegisterCount { get; private set; }
        public int AssignRoleCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IIdentityService.RegisterUserAsync):
                    RegisterCount++;
                    return Task.FromResult(Result.Success(UserId));
                case nameof(IIdentityService.AssignRoleForProductAsync):
                    AssignRoleCount++;
                    return Task.FromResult(Result.Success());
                case nameof(IIdentityService.DeleteUserAsync):
                    return Task.FromResult(Result.Success());
                default:
                    throw new NotSupportedException($"Unexpected identity-service call: {targetMethod?.Name}");
            }
        }
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

    public class RoleRepositoryProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException($"Unexpected role-repository call: {targetMethod?.Name}");
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
        public List<object> Messages { get; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (args?[0] is { } message) Messages.Add(message);
            return Task.CompletedTask;
        }
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

    private sealed class AcceptingLegalConsentService : IRegistrationLegalConsentService
    {
        public Task<Result<IReadOnlyList<PlatformLegalPageDto>>> ValidateAsync(
            PlatformProduct product,
            IEnumerable<LegalPageAcceptance>? acceptances,
            CancellationToken cancellationToken = default)
        {
            var pages = RegistrationLegalConsentPolicy.RequiredSlugs(product)
                .Select(slug => new PlatformLegalPageDto(
                    slug, slug, "Test", true, false, null, 1, DateTime.UtcNow, null, null))
                .ToArray();
            return Task.FromResult(Result.Success<IReadOnlyList<PlatformLegalPageDto>>(pages));
        }

        public void TrackAcceptedDocuments(
            Guid userId,
            PlatformProduct product,
            IReadOnlyList<PlatformLegalPageDto> documents,
            string registrationMethod)
        {
        }
    }

    private sealed class RejectingLegalConsentService : IRegistrationLegalConsentService
    {
        public Task<Result<IReadOnlyList<PlatformLegalPageDto>>> ValidateAsync(
            PlatformProduct product,
            IEnumerable<LegalPageAcceptance>? acceptances,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<IReadOnlyList<PlatformLegalPageDto>>(new Error(
                "Auth.LegalAcceptanceRequired",
                "Kayıt için yayımlanmış yasal metinlerin güncel sürümlerini inceleyip onaylamanız gerekir.")));

        public void TrackAcceptedDocuments(
            Guid userId,
            PlatformProduct product,
            IReadOnlyList<PlatformLegalPageDto> documents,
            string registrationMethod)
        {
        }
    }
}
