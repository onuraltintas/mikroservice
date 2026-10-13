using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using Identity.Application.Commands.OpenCourseManagementSession;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using System.Reflection;

namespace Identity.API.IntegrationTests;

public sealed class CourseManagementSessionCommandTests
{
    [Fact]
    public async Task StudentWithCourseAccess_ShouldReceiveIndependentCourseSession()
    {
        var user = CreateUser("CourseStudent", PlatformProduct.CourseManagement);
        user.GrantProductAccess(PlatformProduct.CourseManagement, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        var issuer = new CapturingSessionIssuer();
        var handler = new OpenCourseManagementSessionCommandHandler(CreateRepository(user), issuer);
        var verifiedAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var result = await handler.Handle(
            new OpenCourseManagementSessionCommand(user.Id, "127.0.0.1", verifiedAt),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PlatformProduct.CourseManagement, issuer.Product);
        Assert.Equal(user.Id, issuer.UserId);
        Assert.Equal(verifiedAt, issuer.MfaVerifiedAt);
        Assert.False(issuer.RememberMe);
    }

    [Fact]
    public async Task UserWithoutCourseRole_ShouldNotReceiveCourseSession()
    {
        var user = User.Create(Guid.NewGuid(), "unjoined@example.test");
        user.GrantProductAccess(PlatformProduct.CourseManagement, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        var issuer = new CapturingSessionIssuer();
        var handler = new OpenCourseManagementSessionCommandHandler(CreateRepository(user), issuer);

        var result = await handler.Handle(
            new OpenCourseManagementSessionCommand(user.Id, "127.0.0.1", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.ProductAccessDenied", result.Error.Code);
        Assert.Null(issuer.UserId);
    }

    private static IUserRepository CreateRepository(User user)
    {
        var repository = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        ((UserRepositoryProxy)(object)repository).User = user;
        return repository;
    }

    private static User CreateUser(string roleName, PlatformProduct product)
    {
        var user = User.Create(Guid.NewGuid(), "student@example.test");
        var role = Role.Create(roleName, roleName);
        var userRole = new UserRole(user.Id, role.Id, product);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(userRole, role);
        user.AddRole(userRole);
        return user;
    }

    private sealed class UserRepositoryProxy : DispatchProxy
    {
        public User? User { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IUserRepository.GetByIdAsync))
                return Task.FromResult(User);
            throw new InvalidOperationException($"Unexpected repository call: {targetMethod?.Name}");
        }
    }

    private sealed class CapturingSessionIssuer : IAuthenticationSessionIssuer
    {
        public Guid? UserId { get; private set; }
        public bool RememberMe { get; private set; }
        public DateTimeOffset? MfaVerifiedAt { get; private set; }
        public PlatformProduct? Product { get; private set; }

        public Task<Result<LoginResponse>> IssueAsync(
            User user,
            bool rememberMe,
            string ipAddress,
            DateTimeOffset? mfaVerifiedAt,
            CancellationToken cancellationToken,
            PlatformProduct? product = null)
        {
            UserId = user.Id;
            RememberMe = rememberMe;
            MfaVerifiedAt = mfaVerifiedAt;
            Product = product;
            return Task.FromResult(Result.Success(new LoginResponse("course-access", "course-refresh", DateTime.UtcNow.AddDays(1), rememberMe)));
        }
    }
}
