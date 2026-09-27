using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Commands.UpdateUserProfile;
using Identity.API.Controllers;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetAllUsers;
using Identity.Application.Queries.GetUserProfile;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace Identity.API.IntegrationTests;

public sealed class ProductScopedUserQueryTests
{
    [Theory]
    [InlineData("coaching", PlatformProduct.Coaching)]
    [InlineData("speed-reading", PlatformProduct.SpeedReading)]
    public async Task UserList_ShouldUseTheAuthenticatedProductScope(
        string productClaim,
        PlatformProduct expectedProduct)
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new UserController(
            mediator,
            new TestCurrentUserService(Guid.NewGuid(), ["Teacher"], productClaim),
            accessManagement: null!);

        var result = await controller.GetAllUsers();

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedProduct, Assert.IsType<GetAllUsersQuery>(capture.Request).Product);
    }

    [Fact]
    public async Task SystemAdministratorUserList_ShouldRemainGlobalForMembershipReconciliation()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new UserController(
            mediator,
            new TestCurrentUserService(Guid.NewGuid(), ["SystemAdmin"], "coaching"),
            accessManagement: null!);

        var result = await controller.GetAllUsers();

        Assert.IsType<OkObjectResult>(result);
        Assert.Null(Assert.IsType<GetAllUsersQuery>(capture.Request).Product);
    }

    [Theory]
    [InlineData("coaching", PlatformProduct.Coaching)]
    [InlineData("speed-reading", PlatformProduct.SpeedReading)]
    public async Task UserDetails_ShouldUseTheAuthenticatedProductScope(
        string productClaim,
        PlatformProduct expectedProduct)
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new UserController(
            mediator,
            new TestCurrentUserService(Guid.NewGuid(), ["Teacher"], productClaim),
            accessManagement: null!);

        var result = await controller.GetUserById(Guid.NewGuid());

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedProduct, Assert.IsType<GetUserProfileQuery>(capture.Request).Product);
    }

    [Theory]
    [InlineData("coaching", PlatformProduct.Coaching)]
    [InlineData("speed-reading", PlatformProduct.SpeedReading)]
    public async Task MyProfile_ShouldUseTheAuthenticatedProductScope(
        string productClaim,
        PlatformProduct expectedProduct)
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var userId = Guid.NewGuid();
        var controller = new UserController(
            mediator,
            new TestCurrentUserService(userId, ["Student"], productClaim),
            accessManagement: null!);

        var result = await controller.GetMyProfile();

        Assert.IsType<OkObjectResult>(result);
        var query = Assert.IsType<GetUserProfileQuery>(capture.Request);
        Assert.Equal(userId, query.UserId);
        Assert.Equal(expectedProduct, query.Product);
    }

    [Fact]
    public async Task MyProfileUpdate_ShouldOverrideBodyProductWithAuthenticatedProductScope()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new UserController(
            mediator,
            new TestCurrentUserService(Guid.NewGuid(), ["Student"], "speed-reading"),
            accessManagement: null!);

        var result = await controller.UpdateProfile(new UpdateUserProfileCommand(
            "Reader",
            "User",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Product: PlatformProduct.Coaching));

        Assert.IsType<NoContentResult>(result);
        var command = Assert.IsType<UpdateUserProfileCommand>(capture.Request);
        Assert.Equal(PlatformProduct.SpeedReading, command.Product);
    }

    [Fact]
    public async Task SpeedReadingTeacherDirectory_ShouldRequireSpeedReadingProductScope()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new UserController(
            mediator,
            new TestCurrentUserService(Guid.NewGuid(), ["Teacher"], "coaching"),
            accessManagement: null!);

        var result = await controller.GetSpeedReadingTeachers();

        Assert.IsType<ForbidResult>(result);
        Assert.Null(capture.Request);
    }

    [Fact]
    public async Task UserProfile_ShouldRejectAUserOutsideTheRequestedProduct()
    {
        var user = User.Create(Guid.NewGuid(), "coaching-only@example.test");
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var userRepository = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        ((UserRepositoryProxy)(object)userRepository).User = user;
        var handler = new GetUserProfileQueryHandler(
            userRepository,
            teacherRepository: null!,
            studentRepository: null!,
            institutionRepository: null!,
            new TestCurrentUserService(user.Id, ["Student"], "speed-reading"));

        var result = await handler.Handle(
            new GetUserProfileQuery(user.Id, PlatformProduct.SpeedReading),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Error.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task SpeedReadingProfile_ShouldNotExposeCoachingRoleOrProfileData()
    {
        var user = User.Create(Guid.NewGuid(), "cross-platform@example.test");
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
        user.GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
        AddRole(user, "Teacher", PlatformProduct.Coaching);
        AddRole(user, "Student", PlatformProduct.SpeedReading);
        var userRepository = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        ((UserRepositoryProxy)(object)userRepository).User = user;
        var handler = new GetUserProfileQueryHandler(
            userRepository,
            teacherRepository: null!,
            studentRepository: null!,
            institutionRepository: null!,
            new TestCurrentUserService(user.Id, ["Student"], "speed-reading"));

        var result = await handler.Handle(
            new GetUserProfileQuery(user.Id, PlatformProduct.SpeedReading),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value.ProductRoles, role =>
        {
            Assert.Equal("Student", role.Role);
            Assert.Equal("speed-reading", role.Product);
        });
        Assert.Collection(result.Value.ProductAccesses, access => Assert.Equal("speed-reading", access.Product));
        Assert.Null(result.Value.TeacherDetails);
        Assert.Null(result.Value.StudentDetails);
    }

    private static void AddRole(User user, string name, PlatformProduct product)
    {
        var role = Role.Create(name, name);
        var userRole = new Identity.Domain.Entities.UserRole(user.Id, role.Id, product);
        typeof(Identity.Domain.Entities.UserRole).GetProperty(nameof(Identity.Domain.Entities.UserRole.Role))!
            .SetValue(userRole, role);
        user.AddRole(userRole);
    }

    public class CapturingMediatorProxy : DispatchProxy
    {
        public object? Request { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Request = args?.FirstOrDefault(argument => argument is not CancellationToken);
            if (Request is GetAllUsersQuery)
            {
                return Task.FromResult(Result.Success(new PagedList<UserProfileDto>([], 0, 1, 10)));
            }

            if (Request is GetUserProfileQuery)
            {
                return Task.FromResult(Result.Success(new UserProfileDto()));
            }

            if (Request is UpdateUserProfileCommand)
            {
                return Task.FromResult(Result.Success());
            }

            throw new NotSupportedException($"Unexpected mediator request: {Request?.GetType().Name}");
        }
    }

    public class UserRepositoryProxy : DispatchProxy
    {
        public User User { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IUserRepository.GetByIdAsync)
                ? Task.FromResult<User?>(User)
                : throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}");
    }

    private sealed class TestCurrentUserService(
        Guid userId,
        IReadOnlyCollection<string> roles,
        string product) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => "test@example.test";
        public string? FullName => "Test User";
        public IEnumerable<string> Roles => roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal User => new(new ClaimsIdentity(
            [new Claim("platform_product", product)],
            authenticationType: "test"));
    }
}
