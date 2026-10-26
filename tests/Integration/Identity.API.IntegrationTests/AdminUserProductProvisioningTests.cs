using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Commands.CreateUser;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class AdminUserProductProvisioningTests
{
    [Theory]
    [InlineData(PlatformProduct.Coaching, "Student", true)]
    [InlineData(PlatformProduct.Coaching, "Teacher", true)]
    [InlineData(PlatformProduct.Coaching, "InstitutionAdmin", false)]
    [InlineData(PlatformProduct.Coaching, "InstitutionOwner", false)]
    [InlineData(PlatformProduct.SpeedReading, "Student", false)]
    [InlineData(PlatformProduct.SpeedReading, "Teacher", false)]
    [InlineData(PlatformProduct.SpeedReading, "InstitutionAdmin", false)]
    [InlineData(PlatformProduct.SpeedReading, "InstitutionOwner", false)]
    public async Task Selected_product_scopes_provisioning_access_profile_and_event(PlatformProduct product, string role, bool coachingProfile)
    {
        var fixture = new Fixture();
        var result = await fixture.Handler.Handle(Command(role, product), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(product, fixture.Identity.Product);
        Assert.Equal(role, fixture.Identity.Role);
        Assert.True(fixture.Users.User.HasProductAccess(product));
        Assert.False(fixture.Users.User.HasProductAccess(product == PlatformProduct.Coaching ? PlatformProduct.SpeedReading : PlatformProduct.Coaching));
        // Coaching creates student/teacher profiles itself from this event (Identity no longer writes them).
        Assert.Equal(coachingProfile ? [role] : [], fixture.Publisher.CoachingProfiles.Select(x => x.Role));
        Assert.Equal(product.ToRouteValue(), fixture.Publisher.Event!.Product);
        Assert.Equal(role, fixture.Publisher.Event.Role);
    }

    [Theory]
    [InlineData("Student", true)]
    [InlineData("Teacher", true)]
    [InlineData("InstitutionAdmin", false)]
    [InlineData("InstitutionOwner", false)]
    public async Task Both_services_share_one_account_but_have_distinct_role_and_access_records(string role, bool coachingProfile)
    {
        var fixture = new Fixture();
        var command = Command(role, null) with { Products = [PlatformProduct.SpeedReading, PlatformProduct.Coaching, PlatformProduct.SpeedReading] };
        Assert.True((await fixture.Handler.Handle(command, CancellationToken.None)).IsSuccess);
        Assert.Equal(1, fixture.Identity.RegisterCount);
        Assert.Equal(PlatformProduct.Coaching, fixture.Identity.Product);
        var user = fixture.Users.User;
        Assert.Equal(2, user.ProductAccesses.Count);
        Assert.Equal(2, user.Roles.Count);
        Assert.Equal(user.Roles.First().RoleId, user.Roles.Last().RoleId);
        Assert.Equal(2, user.Roles.Select(r => r.Product).Distinct().Count());
        Assert.Equal(coachingProfile ? 1 : 0, fixture.Publisher.CoachingProfiles.Count);
        Assert.Equal(1, fixture.Publisher.UserCreatedCount);
        Assert.Equal("coaching", fixture.Publisher.Event!.Product);
        user.RemoveRole(user.Roles.First().RoleId, PlatformProduct.Coaching);
        Assert.Equal(PlatformProduct.SpeedReading, Assert.Single(user.Roles).Product);
        Assert.True(user.HasProductAccess(PlatformProduct.SpeedReading));
    }

    [Theory]
    [InlineData(new int[] { })]
    [InlineData(new[] { 1, 3 })]
    [InlineData(new[] { 2, 99 })]
    public async Task Invalid_explicit_service_lists_do_not_fall_back_to_legacy_product(int[] values)
    {
        var fixture = new Fixture();
        var command = Command("Student", PlatformProduct.Coaching) with { Products = values.Select(v => (PlatformProduct)v).ToArray() };
        Assert.False((await new CreateUserCommandValidator().ValidateAsync(command)).IsValid);
        Assert.True((await fixture.Handler.Handle(command, CancellationToken.None)).IsFailure);
        Assert.Equal(0, fixture.Identity.RegisterCount);
    }

    [Fact]
    public async Task Explicit_service_list_takes_precedence_over_legacy_product()
    {
        var fixture = new Fixture();
        var command = Command("Teacher", PlatformProduct.Coaching) with { Products = [PlatformProduct.SpeedReading] };
        Assert.True((await fixture.Handler.Handle(command, CancellationToken.None)).IsSuccess);
        Assert.Equal(PlatformProduct.SpeedReading, fixture.Identity.Product);
        Assert.False(fixture.Users.User.HasProductAccess(PlatformProduct.Coaching));
    }

    [Theory]
    [InlineData(null)]
    [InlineData((PlatformProduct)0)]
    [InlineData(PlatformProduct.CourseManagement)]
    public async Task Invalid_product_does_not_create_an_account(PlatformProduct? product)
    {
        var fixture = new Fixture();
        var command = Command("Student", product);
        Assert.False((await new CreateUserCommandValidator().ValidateAsync(command)).IsValid);
        Assert.True((await fixture.Handler.Handle(command, CancellationToken.None)).IsFailure);
        Assert.Equal(0, fixture.Identity.RegisterCount);
        Assert.Null(fixture.Publisher.Event);
    }

    [Fact]
    public async Task Legacy_constructor_and_json_without_product_keep_coaching_default()
    {
        var fixture = new Fixture();
        var command = System.Text.Json.JsonSerializer.Deserialize<CreateUserCommand>("{\"FirstName\":\"Test\",\"LastName\":\"User\",\"Email\":\"user@example.test\",\"Role\":\"Student\"}")!;
        Assert.Equal(PlatformProduct.Coaching, command.Product);
        Assert.NotNull(typeof(CreateUserCommand).GetConstructor([typeof(string), typeof(string), typeof(string), typeof(string), typeof(string)]));
        Assert.NotNull(typeof(UserCreatedEvent).GetConstructor([typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(DateTime), typeof(DateTime)]));
        Assert.True((await fixture.Handler.Handle(command, CancellationToken.None)).IsSuccess);
        Assert.Equal("coaching", fixture.Publisher.Event!.Product);
    }

    [Fact]
    public async Task SystemAdmin_is_global_even_with_a_previous_product_selection()
    {
        var fixture = new Fixture();
        Assert.True((await fixture.Handler.Handle(Command("SystemAdmin", PlatformProduct.SpeedReading), CancellationToken.None)).IsSuccess);
        Assert.Null(fixture.Identity.Product);
        Assert.False(fixture.Users.User.HasProductAccess(PlatformProduct.SpeedReading));
        Assert.False(fixture.Users.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.Null(fixture.Publisher.Event!.Product);
    }

    [Fact]
    public async Task Existing_email_is_not_overwritten_or_given_a_new_service()
    {
        var fixture = new Fixture();
        fixture.Users.Existing = true;
        var result = await fixture.Handler.Handle(Command("Teacher", PlatformProduct.SpeedReading), CancellationToken.None);
        Assert.Equal("User.Exists", result.Error.Code);
        Assert.Equal(0, fixture.Identity.RegisterCount);
        Assert.False(fixture.Users.User.HasProductAccess(PlatformProduct.SpeedReading));
    }

    [Fact]
    public async Task Failed_profile_or_event_provisioning_invokes_existing_new_account_cleanup()
    {
        var fixture = new Fixture();
        fixture.Publisher.Fail = true;
        Assert.True((await fixture.Handler.Handle(Command("Student", PlatformProduct.Coaching), CancellationToken.None)).IsFailure);
        Assert.Equal(1, fixture.Identity.DeleteCount);
    }

    private static CreateUserCommand Command(string role, PlatformProduct? product) =>
        new("Test", "User", "user@example.test", null, role) { Product = product };

    private sealed class Fixture
    {
        public IdentityProxy Identity { get; }
        public UsersProxy Users { get; }
        public PublisherProxy Publisher { get; }
        public CreateUserCommandHandler Handler { get; }
        public Fixture()
        {
            var identity = DispatchProxy.Create<IIdentityService, IdentityProxy>();
            Identity = (IdentityProxy)(object)identity;
            var users = DispatchProxy.Create<IUserRepository, UsersProxy>();
            Users = (UsersProxy)(object)users;
            Identity.UserId = Users.User.Id;
            Identity.User = Users.User;
            var publisher = DispatchProxy.Create<IPublishEndpoint, PublisherProxy>();
            Publisher = (PublisherProxy)(object)publisher;
            Handler = new CreateUserCommandHandler(identity, users,
                DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>(), publisher, new AdminUser(), NullLogger<CreateUserCommandHandler>.Instance);
        }
    }
    public class IdentityProxy : DispatchProxy
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public PlatformProduct? Product { get; private set; }
        public string? Role { get; private set; }
        public int RegisterCount { get; private set; }
        public int DeleteCount { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IIdentityService.RegisterUserWithRoleAsync))
            {
                RegisterCount++; Role = (string)args![3]!; Product = (PlatformProduct?)args[4];
                User.AddRole(new Identity.Domain.Entities.UserRole(UserId, Guid.NewGuid(), Product));
                return Task.FromResult(Result.Success(new ProvisionedUser(UserId, "one-time-setup-token", DateTime.UtcNow.AddHours(1))));
            }
            if (method.Name == nameof(IIdentityService.DeleteUserAsync)) { DeleteCount++; return Task.FromResult(Result.Success()); }
            throw new NotSupportedException(method.Name);
        }
    }
    public class UsersProxy : DispatchProxy
    {
        public User User { get; } = User.Create(Guid.NewGuid(), "user@example.test", "Test", "User");
        public bool Existing { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IUserRepository.GetByEmailAsync) => Task.FromResult<User?>(Existing ? User : null),
            nameof(IUserRepository.GetByIdAsync) => Task.FromResult<User?>(User),
            nameof(IUserRepository.TrackProductAccessIfNew) => null,
            _ => throw new NotSupportedException(method.Name)
        };
    }
    public class PublisherProxy : DispatchProxy
    {
        public UserCreatedEvent? Event { get; private set; }
        public List<EduPlatform.Shared.Contracts.Coaching.CoachingProfileProvisioningRequested> CoachingProfiles { get; } = [];
        public bool Fail { get; set; }
        public int UserCreatedCount { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (Fail) throw new InvalidOperationException("Test publication failure");
            switch (args![0])
            {
                case UserCreatedEvent created: UserCreatedCount++; Event = created; break;
                case EduPlatform.Shared.Contracts.Coaching.CoachingProfileProvisioningRequested profile: CoachingProfiles.Add(profile); break;
                default: throw new NotSupportedException(args[0]?.GetType().Name);
            }
            return Task.CompletedTask;
        }
    }
    public class UnitOfWorkProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(1);
    }
    private sealed class AdminUser : ICurrentUserService
    {
        public Guid? UserId => Guid.Parse("1d973bab-86d7-4709-a4ec-712f5cafba69");
        public string? Email => "admin@example.test";
        public string? FullName => "Test Admin";
        public IEnumerable<string> Roles => ["SystemAdmin"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
