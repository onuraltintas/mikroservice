using System.Reflection;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Services;
using Identity.Application.Commands.RegisterStudent;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;

namespace Identity.API.IntegrationTests;

public sealed class CoachingExistingAccountRegistrationTests
{
    [Fact]
    public async Task VerifiedExistingAccountJoinsCoachingWithoutReplacingItsIdentity()
    {
        var fixture = new Fixture();
        var hash = fixture.User.PasswordHash.ToArray();
        var result = await fixture.Register();

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.User.Id, result.Value);
        Assert.True(fixture.User.HasProductAccess(PlatformProduct.SpeedReading));
        Assert.True(fixture.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.Contains(fixture.User.Roles, role => role.Product == PlatformProduct.Coaching && role.RoleId == fixture.StudentRole!.Id);
        Assert.Equal("Existing", fixture.User.FirstName);
        Assert.Equal(hash, fixture.User.PasswordHash);
        Assert.True(fixture.User.EmailConfirmed);
        Assert.Null(fixture.User.EmailVerificationToken);
        Assert.Equal(0, fixture.ProfilesAdded);
        Assert.Equal(1, fixture.ProvisioningEvents);
        Assert.Equal(1, fixture.Saves);
        Assert.Equal(1, fixture.Legal.Tracked);
        Assert.Equal(0, fixture.Deletes);
    }

    [Theory]
    [InlineData("wrong-password", "Auth.ExistingAccountPasswordRequired")]
    [InlineData("inactive", "Auth.ExistingAccountNotEligible")]
    [InlineData("unconfirmed", "Auth.ExistingAccountNotEligible")]
    [InlineData("mfa", "Auth.ExistingAccountMfaRequired")]
    [InlineData("revoked", "Identity.UserExists")]
    [InlineData("coaching-role", "Identity.UserExists")]
    [InlineData("role-missing", "Identity.RoleNotFound")]
    
    public async Task IneligibleOrUnprovenAccountCannotGainCoachingAccess(string scenario, string expectedCode)
    {
        var fixture = new Fixture(confirmed: scenario != "unconfirmed");
        if (scenario == "inactive") fixture.User.Deactivate();
        if (scenario == "mfa") fixture.User.EnableMfa("protected-test", ["recovery-test"], DateTimeOffset.UtcNow);
        if (scenario == "revoked")
        {
            fixture.User.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
            fixture.User.RevokeProductAccess(PlatformProduct.Coaching, DateTimeOffset.UtcNow);
        }
        if (scenario == "coaching-role") fixture.User.AddRole(new Identity.Domain.Entities.UserRole(fixture.User.Id, Guid.NewGuid(), PlatformProduct.Coaching));
        if (scenario == "role-missing") fixture.StudentRole = null;
        if (scenario is "inactive-profile" or "institution-profile")
        {
            fixture.ExistingProfile = StudentProfile.Create(fixture.User.Id, "Existing", "Student",
                scenario == "institution-profile" ? Guid.NewGuid() : null);
            if (scenario == "inactive-profile") fixture.ExistingProfile.Deactivate();
        }

        var result = await fixture.Register(scenario == "wrong-password" ? "WrongPassword1!" : Fixture.Password);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.False(fixture.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.Equal(0, fixture.ProfilesAdded);
        Assert.Equal(0, fixture.Saves);
        Assert.Equal(0, fixture.Legal.Tracked);
        Assert.Equal(0, fixture.Deletes);
    }

    [Fact]
    public async Task AlreadyRegisteredCoachingAccountRemainsAConflict()
    {
        var fixture = new Fixture();
        fixture.User.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        var result = await fixture.Register();
        Assert.Equal("Identity.UserExists", result.Error.Code);
        Assert.Equal(0, fixture.Saves);
    }

    [Fact]
    public async Task ExistingProfileIsPreserved()
    {
        var fixture = new Fixture();
        fixture.ExistingProfile = StudentProfile.Create(fixture.User.Id, "Existing", "Student", null);
        Assert.True((await fixture.Register()).IsSuccess);
        Assert.Equal(0, fixture.ProfilesAdded);
        Assert.Equal(1, fixture.Saves);
    }

    [Fact]
    public async Task MissingLegalAcceptancePreventsAccountLinking()
    {
        var fixture = new Fixture();
        fixture.Legal.Accept = false;
        Assert.Equal("Auth.LegalAcceptanceRequired", (await fixture.Register()).Error.Code);
        Assert.Equal(0, fixture.RegisterCalls);
        Assert.Equal(0, fixture.Saves);
        Assert.False(fixture.User.HasProductAccess(PlatformProduct.Coaching));
    }

    [Fact]
    public async Task OtherProductRegistrationDoesNotUseTheCoachingJoinPath()
    {
        var fixture = new Fixture();
        Assert.Equal("Identity.UserExists", (await fixture.Register(product: PlatformProduct.SpeedReading)).Error.Code);
        Assert.False(fixture.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.Equal(0, fixture.Saves);
    }

    [Fact]
    public async Task PersistenceFailureNeverDeletesTheExistingIdentity()
    {
        var fixture = new Fixture { FailSave = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Register());
        Assert.Equal(0, fixture.Deletes);
        Assert.True(fixture.User.HasProductAccess(PlatformProduct.SpeedReading));
    }

    private sealed class Fixture
    {
        public const string Password = "ExistingPassword1!";
        public User User { get; }
        public Role? StudentRole = Role.Create("Student", "Student", true);
        public StudentProfile? ExistingProfile;
        public readonly LegalConsent Legal = new();
        public int ProfilesAdded, Saves, Deletes, RegisterCalls, ProvisioningEvents;
        public bool FailSave;
        private readonly RegisterStudentCommandHandler handler;

        public Fixture(bool confirmed = true)
        {
            User = User.Create(Guid.NewGuid(), "existing@example.test", "Existing", "Student");
            var hasher = new PasswordHasher();
            hasher.CreatePasswordHash(Password, out var hash, out var salt);
            User.SetPassword(hash, salt);
            if (confirmed) User.ConfirmEmail();
            User.GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
            var sourceRole = Role.Create("Student", "Student", true);
            var sourceMembership = new Identity.Domain.Entities.UserRole(User.Id, sourceRole.Id, PlatformProduct.SpeedReading);
            typeof(Identity.Domain.Entities.UserRole).GetProperty("Role")!.SetValue(sourceMembership, sourceRole);
            User.AddRole(sourceMembership);
            handler = new RegisterStudentCommandHandler(
                Stub<IIdentityService>((method, _) => method.Name switch
                {
                    nameof(IIdentityService.RegisterUserAsync) => RegisterExisting(),
                    nameof(IIdentityService.DeleteUserAsync) => Delete(),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Stub<IUserRepository>((method, _) => method.Name switch
                {
                    nameof(IUserRepository.GetByEmailAsync) => Task.FromResult<User?>(User),
                    nameof(IUserRepository.TrackProductAccessIfNew) => null,
                    _ => throw new NotSupportedException(method.Name)
                }),
                Stub<IStudentRepository>((method, _) => method.Name switch
                {
                    nameof(IStudentRepository.GetExistingForRegistrationAsync) => Task.FromResult(ExistingProfile),
                    nameof(IStudentRepository.AddAsync) => AddProfile(),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Stub<IUnitOfWork>((_, _) => Save()),
                Stub<IPublishEndpoint>((method, _) => throw new NotSupportedException(method.Name)),
                Stub<IConfigurationService>((_, _) => Task.FromResult<string?>("true")),
                Legal, CreateEnrollment(hasher));
        }

        private ExistingAccountEnrollment CreateEnrollment(PasswordHasher hasher) => new(
            Stub<IUserRepository>((method, _) => method.Name == nameof(IUserRepository.GetByEmailAsync) ? Task.FromResult<User?>(User) : null),
            Stub<IInstitutionRepository>((method, _) => throw new NotSupportedException(method.Name)),
            Stub<IRoleRepository>((_, _) => Task.FromResult(StudentRole)), hasher,
            Stub<IMultiFactorService>((method, _) => throw new NotSupportedException(method.Name)),
            Stub<IUnitOfWork>((m, _) => m.Name == "SaveChangesAsync" ? Save() : null), Legal, TimeProvider.System, new TestAuthenticationLock(), Stub<IPublishEndpoint>((_, args) => { if (args?[0] is EduPlatform.Shared.Contracts.Coaching.CoachingProfileProvisioningRequested) ProvisioningEvents++; return Task.CompletedTask; }));

        public Task<Result<Guid>> Register(string password = Password, PlatformProduct product = PlatformProduct.Coaching) =>
            handler.Handle(new RegisterStudentCommand(User.Email, password, "Replacement", "Name", null, product), CancellationToken.None);
        private Task<Result<Guid>> RegisterExisting() { RegisterCalls++; return Task.FromResult(Result.Failure<Guid>(new Error("Identity.UserExists", "Existing account"))); }
        private Task<Result> Delete() { Deletes++; return Task.FromResult(Result.Success()); }
        private Task AddProfile() { ProfilesAdded++; return Task.CompletedTask; }
        private Task<int> Save() { Saves++; if (FailSave) throw new InvalidOperationException("Database write failed"); return Task.FromResult(1); }
    }

    public class StubProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> InvokeCall = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => InvokeCall(targetMethod!, args);
    }
    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        var proxy = DispatchProxy.Create<T, StubProxy>();
        ((StubProxy)(object)proxy).InvokeCall = invoke;
        return proxy;
    }

    private sealed class LegalConsent : IRegistrationLegalConsentService
    {
        public bool Accept = true;
        public int Tracked;
        public Task<Result<IReadOnlyList<PlatformLegalPageDto>>> ValidateAsync(PlatformProduct product,
            IEnumerable<LegalPageAcceptance>? acceptances, CancellationToken cancellationToken = default) =>
            Task.FromResult(Accept ? Result.Success<IReadOnlyList<PlatformLegalPageDto>>([])
                : Result.Failure<IReadOnlyList<PlatformLegalPageDto>>(new Error("Auth.LegalAcceptanceRequired", "Current legal acceptance required")));
        public void TrackAcceptedDocuments(Guid userId, PlatformProduct product, IReadOnlyList<PlatformLegalPageDto> documents, string registrationMethod) => Tracked++;
    }
}
