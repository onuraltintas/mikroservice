using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Commands.Login;
using Identity.Application.Commands.GoogleLogin;
using Identity.Application.Commands.RefreshToken;
using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using PlatformProduct = Identity.Domain.Enums.PlatformProduct;
using UserProductAccessSource = Identity.Domain.Enums.UserProductAccessSource;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class SystemAdminMfaLoginTests
{
    [Theory]
    [InlineData(PlatformProduct.Coaching)]
    [InlineData(PlatformProduct.SpeedReading)]
    public async Task FreshGoogleRegistrationProvisionsOnlyTheRequestedProductProfile(PlatformProduct product)
    {
        var user = CreateUserWithRole("Student", product);
        var repository = new StubUserRepository(user, null, new User?[] { null });
        var publisher = System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>();
        var handler = new GoogleLoginCommandHandler(new StubGoogleAuthService(user.Email), repository, new IssuingTokenService(),
            new StubUnitOfWork(), new RejectingIdentityService(Result.Success(user.Id), allowRefreshToken: true),
            new RejectingStudentRepository(), new StubConfigurationService(allowRegistration: "true"), new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance, new AcceptingLegalConsentService(), publisher);
        var result = await handler.Handle(new GoogleLoginCommand("google-token", "127.0.0.1", product), default);
        result.IsSuccess.Should().BeTrue();
        var messages = ((ProductRegistrationProfileOwnershipTests.PublishEndpointProxy)(object)publisher).Messages
            .OfType<EduPlatform.Shared.Contracts.Coaching.CoachingProfileProvisioningRequested>();
        if (product == PlatformProduct.Coaching) Assert.Equal(user.Id, Assert.Single(messages).UserId);
        else Assert.Empty(messages);
        user.HasProductAccess(product).Should().BeTrue();
    }
    [Theory]
    [InlineData(null)]
    [InlineData(PlatformProduct.SpeedReading)]
    public async Task PasswordLogin_WhenProductScopeIsMissingOrDifferent_ShouldRejectBeforeIssuingTokens(
        PlatformProduct? requestedProduct)
    {
        var user = CreateUserWithRole("Student");
        user.ConfirmEmail();
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var tokenService = new RejectingTokenService();
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new AcceptingPasswordHasher(),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false, Product: requestedProduct),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ProductAccessDenied");
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
    }

    [Fact]
    public async Task PasswordLogin_WithMatchingProductAccess_ShouldIssueProductScopedSession()
    {
        var user = CreateUserWithRole("Student");
        user.ConfirmEmail();
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var tokenService = new IssuingTokenService();
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new AcceptingPasswordHasher(),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(
                user.Email,
                "correct-password",
                RememberMe: false,
                Product: PlatformProduct.Coaching),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tokenService.AccessTokenProduct.Should().Be(PlatformProduct.Coaching);
        tokenService.RefreshTokenProduct.Should().Be(PlatformProduct.Coaching);
    }

    [Fact]
    public async Task PasswordLogin_WhenInstitutionOwnerEmailIsNotConfirmed_ShouldRejectLogin()
    {
        var user = CreateUserWithRole("InstitutionOwner");
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.Admin,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new AcceptingPasswordHasher(),
            new RejectingTokenService(),
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false, Product: PlatformProduct.Coaching),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.EmailNotConfirmed");
    }

    [Fact]
    public async Task PasswordLogin_WhenMfaIsDisabled_ShouldIssueNormalSession()
    {
        var user = CreateSystemAdministrator();
        var tokenService = new IssuingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new AcceptingPasswordHasher(),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new StubConfigurationService(),
            mfaService,
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        mfaService.RememberMe.Should().BeNull();
    }

    [Fact]
    public async Task PasswordLogin_WhenMfaIsEnabled_ShouldReturnMfaChallenge()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var tokenService = new RejectingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new AcceptingPasswordHasher(),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new StubConfigurationService(),
            mfaService,
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeTrue();
        result.Value.MfaEnrollmentRequired.Should().BeFalse();
        result.Value.MfaChallengeToken.Should().Be("mfa-challenge");
        result.Value.AccessToken.Should().BeNull();
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
        mfaService.RememberMe.Should().BeFalse();
    }

    [Fact]
    public async Task PasswordLogin_WhenSystemMfaPolicyIsDisabled_ShouldIssueNormalSession()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var tokenService = new IssuingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new AcceptingPasswordHasher(),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new StubConfigurationService(systemMfaMode: MfaPolicyModes.Disabled),
            mfaService,
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-token");
        mfaService.RememberMe.Should().BeNull();
    }

    [Theory]
    [InlineData("InstitutionAdmin", PlatformProduct.Coaching)]
    [InlineData("InstitutionOwner", PlatformProduct.Coaching)]
    [InlineData("Editor", PlatformProduct.Coaching)]
    [InlineData("CourseInstitutionOwner", PlatformProduct.CourseManagement)]
    public async Task PrivilegedAdminPasswordLogin_WhenMfaIsEnabled_ShouldReturnMfaChallenge(string roleName, PlatformProduct product)
    {
        var user = CreateUserWithRole(roleName, product);
        user.GrantProductAccess(
            product,
            UserProductAccessSource.Admin,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        user.ConfirmEmail();
        user.EnableMfa("protected-secret", ["recovery-hash"], DateTimeOffset.UtcNow);
        var tokenService = new RejectingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new AcceptingPasswordHasher(),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new StubConfigurationService(),
            mfaService,
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(
                user.Email,
                "correct-password",
                RememberMe: true,
                Product: product),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeTrue();
        result.Value.MfaChallengeToken.Should().Be("mfa-challenge");
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
        mfaService.RememberMe.Should().BeTrue();
    }

    [Fact]
    public async Task GoogleLogin_WhenMfaIsDisabled_ShouldIssueNormalSession()
    {
        var user = CreateSystemAdministrator();
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        var tokenService = new IssuingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            mfaService,
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        mfaService.RememberMe.Should().BeNull();
    }

    [Fact]
    public async Task GoogleLogin_WhenMfaIsEnabled_ShouldReturnMfaChallenge()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        var tokenService = new RejectingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            mfaService,
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeTrue();
        result.Value.MfaEnrollmentRequired.Should().BeFalse();
        result.Value.MfaChallengeToken.Should().Be("mfa-challenge");
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleLogin_WhenSystemMfaPolicyIsDisabled_ShouldIssueNormalSession()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        var tokenService = new IssuingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new RejectingStudentRepository(),
            new StubConfigurationService(systemMfaMode: MfaPolicyModes.Disabled),
            mfaService,
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-token");
        mfaService.RememberMe.Should().BeNull();
    }

    [Fact]
    public async Task GoogleLogin_ShouldRecordLastLoginAtAfterIssuingSession()
    {
        var user = User.Create(Guid.NewGuid(), "student@example.com");
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            new IssuingTokenService(),
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", PlatformProduct.Coaching),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeFalse();
        user.LastLoginAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(PlatformProduct.SpeedReading)]
    public async Task GoogleLogin_WhenProductScopeIsMissingOrDifferent_ShouldRejectBeforeIssuingTokens(
        PlatformProduct? requestedProduct)
    {
        var user = CreateUserWithRole("Student");
        user.ConfirmEmail();
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var tokenService = new IssuingTokenService();
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", requestedProduct),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ProductAccessDenied");
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleLogin_WithMatchingProductAccess_ShouldIssueProductScopedSession()
    {
        var user = CreateUserWithRole("Student");
        user.ConfirmEmail();
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        user.GrantProductAccess(
            PlatformProduct.SpeedReading,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var tokenService = new IssuingTokenService();
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", PlatformProduct.SpeedReading),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tokenService.AccessTokenProduct.Should().Be(PlatformProduct.SpeedReading);
        tokenService.RefreshTokenProduct.Should().Be(PlatformProduct.SpeedReading);
    }

    [Fact]
    public async Task GoogleLogin_ShouldUseLinkedSubjectBeforeEmailLookup()
    {
        var linkedUser = CreateSystemAdministrator(mfaEnabled: true);
        var emailMatchedUser = CreateSystemAdministrator();
        var tokenService = new RejectingTokenService();
        var mfaService = new StubMfaService(linkedUser.Id);
        var userRepository = new StubUserRepository(emailMatchedUser, linkedUser);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService("changed@example.com", googleId: "google-subject"),
            userRepository,
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            mfaService,
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        mfaService.RememberMe.Should().BeTrue();
        userRepository.ExternalLoginLookupCalled.Should().BeTrue();
        userRepository.EmailLookupCalled.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleLogin_ShouldRequireExplicitLinkForPrivilegedAccount()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            new RejectingTokenService(),
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ExternalLoginLinkRequired");
    }

    [Fact]
    public async Task GoogleLogin_ShouldNotReactivateInactiveAccountAfterRegistrationRace()
    {
        var inactiveUser = CreateSystemAdministrator();
        inactiveUser.Deactivate();
        var userRepository = new StubUserRepository(
            inactiveUser,
            externalUser: null,
            null,
            inactiveUser);
        var identityService = new RejectingIdentityService(Result.Failure<Guid>(
            new Error("Identity.UserExists", "User already exists.")));
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(inactiveUser.Email),
            userRepository,
            new RejectingTokenService(),
            new StubUnitOfWork(),
            identityService,
            new RejectingStudentRepository(),
            new StubConfigurationService(allowRegistration: "true"),
            new StubMfaService(inactiveUser.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", PlatformProduct.Coaching),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.UserInactive");
        inactiveUser.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleLogin_ShouldRejectUnverifiedGoogleEmail()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email, isEmailVerified: false),
            new StubUserRepository(user),
            new RejectingTokenService(),
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidToken");
    }

    [Fact]
    public async Task GoogleLink_ShouldPersistGoogleSubjectForPrivilegedAccount()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var userRepository = new StubUserRepository(user, externalUser: null);
        var handler = new LinkGoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email, googleId: "google-subject"),
            userRepository,
            new StubCurrentUserService(user.Id),
            NullLogger<LinkGoogleLoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LinkGoogleLoginCommand("google-token"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.Logins.Should().ContainSingle(login =>
            login.LoginProvider == "Google" && login.ProviderKey == "google-subject");
    }

    [Fact]
    public async Task LegacySystemAdminRefreshToken_ShouldRequireFreshMfaLogin()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var refreshToken = RefreshToken.Create(
            user.Id, "legacy-refresh", DateTime.UtcNow.AddDays(1), "127.0.0.1",
            isPersistent: true, mfaVerifiedAt: null);
        user.AddRefreshToken(refreshToken);
        var tokenService = new RejectingTokenService();
        var handler = new RefreshTokenCommandHandler(
            new StubUserRepository(user),
            tokenService,
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaRequired");
        refreshToken.IsActive.Should().BeFalse();
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    [Fact]
    public async Task NonAdminRefreshToken_WithoutMfaProof_ShouldRequireFreshMfaLogin()
    {
        var user = CreateUserWithRole("Student", PlatformProduct.Coaching);
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, grantedByUserId: null, DateTimeOffset.UtcNow);
        user.EnableMfa("protected-secret", ["recovery-hash"], DateTimeOffset.UtcNow);
        var refreshToken = RefreshToken.Create(
            user.Id, "student-refresh", DateTime.UtcNow.AddDays(1), "127.0.0.1",
            isPersistent: true, mfaVerifiedAt: null, product: PlatformProduct.Coaching);
        user.AddRefreshToken(refreshToken);
        var tokenService = new RejectingTokenService();
        var handler = new RefreshTokenCommandHandler(
            new StubUserRepository(user),
            tokenService,
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(new RefreshTokenCommand("student-refresh"), CancellationToken.None);

        result.Error.Code.Should().Be("Auth.MfaRequired");
        result.Error.Description.Should().NotContain("Yönetici");
        refreshToken.IsActive.Should().BeFalse();
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshToken_WhenSystemMfaPolicyIsDisabled_ShouldIssueNormalSession()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var refreshToken = RefreshToken.Create(
            user.Id, "refresh-with-disabled-mfa", DateTime.UtcNow.AddDays(1), "127.0.0.1",
            isPersistent: true, mfaVerifiedAt: null);
        user.AddRefreshToken(refreshToken);

        var handler = new RefreshTokenCommandHandler(
            new StubUserRepository(user),
            new IssuingTokenService(),
            new StubConfigurationService(systemMfaMode: MfaPolicyModes.Disabled),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        refreshToken.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SystemAdminRefreshToken_WhenMfaIsDisabled_ShouldIssueNormalSession()
    {
        var user = CreateSystemAdministrator();
        var refreshToken = RefreshToken.Create(
            user.Id, "refresh-token", DateTime.UtcNow.AddDays(1), "127.0.0.1",
            isPersistent: true, mfaVerifiedAt: null);
        user.AddRefreshToken(refreshToken);

        var handler = new RefreshTokenCommandHandler(
            new StubUserRepository(user),
            new IssuingTokenService(),
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        refreshToken.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshToken_WhenRepositoryRotationSucceeds_ShouldIssueReplacementSession()
    {
        var userId = Guid.NewGuid();
        var firstUser = CreateSystemAdministrator(id: userId);
        firstUser.AddRefreshToken(RefreshToken.Create(
            userId, "retry-refresh-token", DateTime.UtcNow.AddDays(1), "127.0.0.1"));
        var reloadedUser = CreateSystemAdministrator(id: userId);
        reloadedUser.AddRefreshToken(RefreshToken.Create(
            userId, "retry-refresh-token", DateTime.UtcNow.AddDays(1), "127.0.0.1"));

        var repository = new StubUserRepository(firstUser);
        repository.SetRefreshUsers(firstUser, reloadedUser);
        var handler = new RefreshTokenCommandHandler(
            repository,
            new IssuingTokenService(),
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand("retry-refresh-token"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.RotationCalls.Should().Be(1);
    }

    [Fact]
    public async Task RefreshToken_ShouldKeepProductScopeOnRotatedSession()
    {
        var user = CreateUserWithRole("Student");
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var refreshToken = RefreshToken.Create(
            user.Id,
            "coaching-refresh-token",
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            product: PlatformProduct.Coaching);
        user.AddRefreshToken(refreshToken);
        var tokenService = new IssuingTokenService();
        var handler = new RefreshTokenCommandHandler(
            new StubUserRepository(user),
            tokenService,
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tokenService.AccessTokenProduct.Should().Be(PlatformProduct.Coaching);
        tokenService.RefreshTokenProduct.Should().Be(PlatformProduct.Coaching);
        user.RefreshTokens.Should().ContainSingle(token =>
            token.IsActive && token.Product == PlatformProduct.Coaching);
    }

    [Fact]
    public async Task StaffProductSwitch_ShouldIssueSessionScopedToTargetProduct()
    {
        var user = CreateUserWithRole("Teacher", PlatformProduct.Coaching);
        AddRole(user, "Teacher", PlatformProduct.SpeedReading);
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        user.GrantProductAccess(
            PlatformProduct.SpeedReading,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var refreshToken = RefreshToken.Create(
            user.Id,
            "coaching-session-for-switch",
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            product: PlatformProduct.Coaching);
        user.AddRefreshToken(refreshToken);
        var tokenService = new IssuingTokenService();
        var handler = new RefreshTokenCommandHandler(
            new StubUserRepository(user),
            tokenService,
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!, PlatformProduct.SpeedReading),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tokenService.AccessTokenProduct.Should().Be(PlatformProduct.SpeedReading);
        tokenService.RefreshTokenProduct.Should().Be(PlatformProduct.SpeedReading);
        refreshToken.IsRevoked.Should().BeTrue();
        user.RefreshTokens.Should().ContainSingle(token =>
            token.IsActive && token.Product == PlatformProduct.SpeedReading);
    }

    [Fact]
    public async Task StaffProductSwitch_WithoutTargetStaffRole_ShouldNotIssueOrRotateSession()
    {
        var user = CreateUserWithRole("Student", PlatformProduct.Coaching);
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        user.GrantProductAccess(
            PlatformProduct.SpeedReading,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var refreshToken = RefreshToken.Create(
            user.Id,
            "student-session-for-switch",
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            product: PlatformProduct.Coaching);
        user.AddRefreshToken(refreshToken);
        var tokenService = new IssuingTokenService();
        var repository = new StubUserRepository(user);
        var handler = new RefreshTokenCommandHandler(
            repository,
            tokenService,
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!, PlatformProduct.SpeedReading),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ProductAccessDenied");
        tokenService.AccessTokenRequested.Should().BeFalse();
        repository.RotationCalls.Should().Be(0);
        refreshToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CourseManagementStudentSession_ShouldAllowOnlyDedicatedCourseSessionUpgrade()
    {
        var user = CreateUserWithRole("CourseStudent", PlatformProduct.CourseManagement);
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        user.GrantProductAccess(
            PlatformProduct.CourseManagement,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var refreshToken = RefreshToken.Create(
            user.Id,
            "course-student-source-session",
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            product: PlatformProduct.Coaching);
        user.AddRefreshToken(refreshToken);
        var tokenService = new IssuingTokenService();
        var repository = new StubUserRepository(user);
        var handler = new RefreshTokenCommandHandler(
            repository,
            tokenService,
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(
                refreshToken.IssuedValue!,
                PlatformProduct.CourseManagement,
                user.Id,
                AllowCourseManagementStudentSession: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tokenService.AccessTokenProduct.Should().Be(PlatformProduct.CourseManagement);
        tokenService.RefreshTokenProduct.Should().Be(PlatformProduct.CourseManagement);
        repository.RotationCalls.Should().Be(1);
    }

    [Fact]
    public async Task StaffProductSwitch_ShouldNotGrantCourseStudentSessionWithoutDedicatedFlag()
    {
        var user = CreateUserWithRole("CourseStudent", PlatformProduct.CourseManagement);
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        user.GrantProductAccess(
            PlatformProduct.CourseManagement,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var refreshToken = RefreshToken.Create(
            user.Id,
            "course-student-for-staff-switch",
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            product: PlatformProduct.Coaching);
        user.AddRefreshToken(refreshToken);
        var tokenService = new IssuingTokenService();
        var repository = new StubUserRepository(user);
        var handler = new RefreshTokenCommandHandler(
            repository,
            tokenService,
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!, PlatformProduct.CourseManagement, user.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ProductAccessDenied");
        tokenService.AccessTokenRequested.Should().BeFalse();
        repository.RotationCalls.Should().Be(0);
        refreshToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task StaffProductSwitch_ToMfaProtectedInstitutionRole_ShouldKeepCurrentSessionActive()
    {
        var user = CreateUserWithRole("Teacher", PlatformProduct.Coaching);
        AddRole(user, "InstitutionAdmin", PlatformProduct.SpeedReading);
        user.EnableMfa("protected-secret", ["recovery-hash"], DateTimeOffset.UtcNow);
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        user.GrantProductAccess(
            PlatformProduct.SpeedReading,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var refreshToken = RefreshToken.Create(
            user.Id,
            "teacher-session-for-admin-switch",
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            product: PlatformProduct.Coaching);
        user.AddRefreshToken(refreshToken);
        var tokenService = new IssuingTokenService();
        var repository = new StubUserRepository(user);
        var handler = new RefreshTokenCommandHandler(
            repository,
            tokenService,
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!, PlatformProduct.SpeedReading),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaRequired");
        refreshToken.IsActive.Should().BeTrue();
        repository.RotationCalls.Should().Be(0);
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    [Fact]
    public async Task StaffProductSwitch_ShouldRequireRefreshTokenToBelongToAuthenticatedUser()
    {
        var user = CreateUserWithRole("Teacher", PlatformProduct.Coaching);
        AddRole(user, "Teacher", PlatformProduct.SpeedReading);
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        user.GrantProductAccess(
            PlatformProduct.SpeedReading,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        var refreshToken = RefreshToken.Create(
            user.Id,
            "refresh-token-from-another-account",
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            product: PlatformProduct.Coaching);
        user.AddRefreshToken(refreshToken);
        var tokenService = new IssuingTokenService();
        var repository = new StubUserRepository(user);
        var handler = new RefreshTokenCommandHandler(
            repository,
            tokenService,
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(
                refreshToken.IssuedValue!,
                PlatformProduct.SpeedReading,
                Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidToken");
        refreshToken.IsActive.Should().BeTrue();
        repository.RotationCalls.Should().Be(0);
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    private static void AddRole(User user, string roleName, PlatformProduct product)
    {
        var role = Role.Create(roleName, roleName);
        var userRole = new UserRole(user.Id, role.Id, product);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(userRole, role);
        user.AddRole(userRole);
    }

    [Fact]
    public async Task RefreshToken_WhenProductAccessWasRevoked_ShouldRevokeSession()
    {
        var user = CreateUserWithRole("Student");
        var now = DateTimeOffset.UtcNow;
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            now);
        var refreshToken = RefreshToken.Create(
            user.Id,
            "revoked-product-refresh-token",
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            product: PlatformProduct.Coaching);
        user.AddRefreshToken(refreshToken);
        user.RevokeProductAccess(PlatformProduct.Coaching, now.AddMinutes(1));

        var handler = new RefreshTokenCommandHandler(
            new StubUserRepository(user),
            new IssuingTokenService(),
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ProductAccessDenied");
        refreshToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshToken_WhenConcurrentRotationOccurs_ShouldReturnInvalidTokenFailure()
    {
        var user = CreateSystemAdministrator();
        var refreshToken = RefreshToken.Create(
            user.Id, "concurrent-refresh-token", DateTime.UtcNow.AddDays(1), "127.0.0.1",
            isPersistent: true, mfaVerifiedAt: null);
        user.AddRefreshToken(refreshToken);

        var repository = new StubUserRepository(user) { RotationSucceeds = false };
        var handler = new RefreshTokenCommandHandler(
            repository,
            new IssuingTokenService(),
            new StubConfigurationService(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidToken");
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("Parent")]
    public async Task PasswordLogin_NonAdminWithEnrolledMfa_ShouldReturnMfaChallenge(string roleName)
    {
        var user = CreateUserWithRole(roleName, PlatformProduct.Coaching);
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, grantedByUserId: null, DateTimeOffset.UtcNow);
        user.ConfirmEmail();
        user.EnableMfa("protected-secret", ["recovery-hash"], DateTimeOffset.UtcNow);
        var tokenService = new RejectingTokenService();
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new AcceptingPasswordHasher(),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false, Product: PlatformProduct.Coaching),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeTrue();
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleLogin_NonAdminWithEnrolledMfa_ShouldReturnMfaChallenge()
    {
        var user = CreateUserWithRole("Student", PlatformProduct.Coaching);
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, grantedByUserId: null, DateTimeOffset.UtcNow);
        user.ConfirmEmail();
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        user.EnableMfa("protected-secret", ["recovery-hash"], DateTimeOffset.UtcNow);
        var tokenService = new RejectingTokenService();
        var handler = CreateGoogleHandler(user, tokenService, externalUser: user);

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", PlatformProduct.Coaching),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeTrue();
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleLogin_LinkingUnconfirmedAccount_ShouldDiscardThePreviouslyChosenPassword()
    {
        // Someone registered the victim's address with a password of their choice and
        // never confirmed it; the verified Google identity must not inherit that password.
        var user = CreateUserWithRole("Student", PlatformProduct.Coaching);
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, grantedByUserId: null, DateTimeOffset.UtcNow);
        var hasher = new EduPlatform.Shared.Security.Services.PasswordHasher();
        hasher.CreatePasswordHash("attacker-chosen-1!", out var hash, out var salt);
        user.SetPassword(hash, salt);
        user.GeneratePasswordResetToken();
        user.EmailConfirmed.Should().BeFalse();
        var handler = CreateGoogleHandler(user, new IssuingTokenService(), allowRefreshToken: true);

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", PlatformProduct.Coaching),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.EmailConfirmed.Should().BeTrue();
        user.Logins.Should().ContainSingle(login => login.LoginProvider == "Google");
        hasher.VerifyPasswordHash("attacker-chosen-1!", user.PasswordHash, user.PasswordSalt).Should().BeFalse();
        user.PasswordResetToken.Should().BeNull();
    }

    [Fact]
    public async Task GoogleLogin_LinkingConfirmedAccount_ShouldKeepItsPassword()
    {
        var user = CreateUserWithRole("Student", PlatformProduct.Coaching);
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, grantedByUserId: null, DateTimeOffset.UtcNow);
        var hasher = new EduPlatform.Shared.Security.Services.PasswordHasher();
        hasher.CreatePasswordHash("owner-password-1!", out var hash, out var salt);
        user.SetPassword(hash, salt);
        user.ConfirmEmail();
        var handler = CreateGoogleHandler(user, new IssuingTokenService(), allowRefreshToken: true);

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", PlatformProduct.Coaching),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        hasher.VerifyPasswordHash("owner-password-1!", user.PasswordHash, user.PasswordSalt).Should().BeTrue();
    }

    [Fact]
    public async Task GoogleLogin_StaffRoleInAnotherProduct_ShouldRequireExplicitLink()
    {
        // Teacher in coaching, student in speed reading: signing in through speed reading
        // must not auto-link the staff account.
        var user = CreateUserWithRole("Teacher", PlatformProduct.Coaching);
        var studentRole = Role.Create("Student", "Student");
        var studentLink = new UserRole(user.Id, studentRole.Id, PlatformProduct.SpeedReading);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(studentLink, studentRole);
        user.AddRole(studentLink);
        user.GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.Admin, grantedByUserId: null, DateTimeOffset.UtcNow);
        user.ConfirmEmail();
        var handler = CreateGoogleHandler(user, new RejectingTokenService());

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", PlatformProduct.SpeedReading),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ExternalLoginLinkRequired");
        user.Logins.Should().BeEmpty();
    }

    private static GoogleLoginCommandHandler CreateGoogleHandler(
        User user,
        ITokenService tokenService,
        User? externalUser = null,
        bool allowRefreshToken = false) => new(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user, externalUser),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: allowRefreshToken),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>());

    private static User CreateSystemAdministrator(bool mfaEnabled = false, Guid? id = null)
    {
        var user = User.Create(id ?? Guid.NewGuid(), "admin@example.com");
        var role = Role.Create("SystemAdmin", "System administrator", isSystemRole: true);
        var userRole = new UserRole(user.Id, role.Id);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(userRole, role);
        user.AddRole(userRole);
        if (mfaEnabled)
        {
            user.EnableMfa("protected-secret", ["recovery-hash"], DateTimeOffset.UtcNow);
        }
        return user;
    }

    private static User CreateUserWithRole(string roleName, PlatformProduct? product = null)
    {
        var user = User.Create(Guid.NewGuid(), "institution-owner@example.com");
        var role = Role.Create(roleName, roleName);
        var userRole = new UserRole(user.Id, role.Id, product);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(userRole, role);
        user.AddRole(userRole);
        return user;
    }

    private sealed class StubUserRepository(User user, User? externalUser = null, params User?[] emailResults) : IUserRepository
    {
        private readonly User? _externalUser = externalUser;
        private readonly Queue<User?> _emailResults = new(emailResults);
        public bool ExternalLoginLookupCalled { get; private set; }
        public bool EmailLookupCalled { get; private set; }
        public bool RotationSucceeds { get; set; } = true;
        public int RotationCalls { get; private set; }
        private readonly Queue<User?> _refreshResults = new();

        public void SetRefreshUsers(params User?[] users)
        {
            foreach (var refreshUser in users)
            {
                _refreshResults.Enqueue(refreshUser);
            }
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            EmailLookupCalled = true;
            return Task.FromResult(_emailResults.Count > 0 ? _emailResults.Dequeue() : user);
        }

        public Task<User?> GetByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            ExternalLoginLookupCalled = true;
            if (_externalUser is not null)
            {
                return Task.FromResult<User?>(_externalUser);
            }

            var hasMatchingLogin = user.Logins.Any(login =>
                login.LoginProvider == loginProvider && login.ProviderKey == providerKey);
            return Task.FromResult(hasMatchingLogin ? user : null);
        }

        public Task<bool> TryAddLoginAsync(User value, UserLogin login, CancellationToken cancellationToken)
        {
            value.AddLogin(login);
            return Task.FromResult(true);
        }

        public Task AddAsync(User value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void TrackProductAccessIfNew(User value, PlatformProduct product) { }
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<User?>(user);
        public Task<IReadOnlyList<SpeedReadingUserDirectoryItem>> GetSpeedReadingDirectoryAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SpeedReadingUserDirectoryItem>>([]);
        public Task<IReadOnlyList<Guid>> GetSpeedReadingAudienceUserIdsAsync(
            string? role,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public void Delete(User value) => throw new NotSupportedException();
        public Task<Identity.Application.Queries.GetAllUsers.PagedList<Identity.Application.Queries.GetUserProfile.UserProfileDto>> GetAllAsync(int page, int pageSize, string? searchTerm, string? role, bool? isActive, Guid? institutionId, CancellationToken cancellationToken, PlatformProduct? product = null) => throw new NotSupportedException();
        public Task<Identity.Application.Queries.GetAllUsers.UserSummaryDto> GetSummaryAsync(Guid? institutionId, CancellationToken cancellationToken, PlatformProduct? product = null) => throw new NotSupportedException();
        public Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken) =>
            Task.FromResult(_refreshResults.Count > 0 ? _refreshResults.Dequeue() : user);
        public Task<bool> RevokeRefreshTokenAsync(
            string refreshToken,
            string revokedByIp,
            string reason,
            CancellationToken cancellationToken)
        {
            var token = user.RefreshTokens.FirstOrDefault(candidate => candidate.Matches(refreshToken));
            token?.Revoke(revokedByIp, reason);
            return Task.FromResult(token is not null);
        }
        public Task<bool> RotateRefreshTokenAsync(
            string refreshToken,
            RefreshToken replacementToken,
            string revokedByIp,
            string reason,
            CancellationToken cancellationToken)
        {
            RotationCalls++;
            if (!RotationSucceeds)
                return Task.FromResult(false);

            var token = user.RefreshTokens.FirstOrDefault(candidate => candidate.Matches(refreshToken));
            if (token is null)
                return Task.FromResult(false);

            token.Revoke(revokedByIp, reason);
            user.AddRefreshToken(replacementToken);
            return Task.FromResult(true);
        }
        public Task RevokeActiveRefreshTokensAsync(Guid userId, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RevokeActiveRefreshTokensForProductAsync(Guid userId, PlatformProduct product, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RevokeActiveRefreshTokensForInstitutionAsync(Guid institutionId, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<List<User>> GetUsersByRolesAsync(List<string> roleNames, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AcceptingPasswordHasher : IPasswordHasher
    {
        public void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt) => throw new NotSupportedException();
        public bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt) => true;
        public bool NeedsRehash(byte[] storedHash, byte[] storedSalt) => false;
    }

    private sealed class RejectingTokenService : ITokenService
    {
        public bool AccessTokenRequested { get; private set; }
        public bool RefreshTokenRequested { get; private set; }
        public Task<int> GetAccessTokenLifetimeMinutesAsync() => Task.FromResult(15);
        public Task<string> GenerateAccessTokenAsync(
            User user,
            DateTimeOffset? mfaVerifiedAt = null,
            PlatformProduct? product = null)
        {
            AccessTokenRequested = true;
            throw new InvalidOperationException("Access token must not be issued before MFA.");
        }
        public RefreshToken GenerateRefreshToken(
            Guid userId,
            string ipAddress,
            bool isPersistent = true,
            DateTimeOffset? mfaVerifiedAt = null,
            PlatformProduct? product = null)
        {
            RefreshTokenRequested = true;
            throw new InvalidOperationException("Refresh token must not be issued before MFA.");
        }
    }

    private sealed class IssuingTokenService : ITokenService
    {
        public bool AccessTokenRequested { get; private set; }
        public bool RefreshTokenRequested { get; private set; }
        public PlatformProduct? AccessTokenProduct { get; private set; }
        public PlatformProduct? RefreshTokenProduct { get; private set; }

        public Task<string> GenerateAccessTokenAsync(
            User user,
            DateTimeOffset? mfaVerifiedAt = null,
            PlatformProduct? product = null)
        {
            AccessTokenRequested = true;
            AccessTokenProduct = product;
            return Task.FromResult("access-token");
        }
        public Task<int> GetAccessTokenLifetimeMinutesAsync() => Task.FromResult(15);
        public RefreshToken GenerateRefreshToken(
            Guid userId,
            string ipAddress,
            bool isPersistent = true,
            DateTimeOffset? mfaVerifiedAt = null,
            PlatformProduct? product = null)
        {
            RefreshTokenRequested = true;
            RefreshTokenProduct = product;
            return RefreshToken.Create(
                userId,
                "refresh-token",
                DateTime.UtcNow.AddDays(1),
                ipAddress,
                isPersistent,
                mfaVerifiedAt,
                product);
        }
    }

    private sealed class StubMfaService(Guid expectedUserId) : IMultiFactorService
    {
        public bool? RememberMe { get; private set; }
        public string CreateChallenge(Guid userId, bool rememberMe, PlatformProduct? product = null)
        {
            userId.Should().Be(expectedUserId);
            RememberMe = rememberMe;
            return "mfa-challenge";
        }
        public Result<MfaChallengePayload> ReadChallenge(string token) => throw new NotSupportedException();
        public MfaSetupResponse CreateSetup(Guid userId, string email) => throw new NotSupportedException();
        public Result<MfaSetupPayload> ReadSetupToken(string token) => throw new NotSupportedException();
        public string ProtectSecret(string secret) => throw new NotSupportedException();
        public string UnprotectSecret(string protectedSecret) => throw new NotSupportedException();
        public long? FindMatchingTimeStep(string protectedSecret, string code) => throw new NotSupportedException();
        public IReadOnlyList<string> GenerateRecoveryCodes() => throw new NotSupportedException();
        public string HashRecoveryCode(string recoveryCode) => throw new NotSupportedException();
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public void ClearTracking() { }
    }

    private sealed class StubGoogleAuthService(
        string email,
        string googleId = "google-id",
        bool isEmailVerified = true,
        string issuer = "https://accounts.google.com") : IGoogleAuthService
    {
        public Task<GoogleUser?> VerifyGoogleTokenAsync(string idToken) =>
            Task.FromResult<GoogleUser?>(new GoogleUser(
                email,
                "System",
                "Admin",
                "",
                googleId,
                isEmailVerified,
                issuer));
    }

    private sealed class RejectingStudentRepository : IStudentRepository
    {
        public Task AddAsync(StudentProfile student, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StudentProfile?> GetExistingForRegistrationAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, Guid? institutionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StudentProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubCurrentUserService(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => Array.Empty<string>();
        public bool IsAuthenticated => true;
        public System.Security.Claims.ClaimsPrincipal? User => null;
    }

    private sealed class StubConfigurationService(
        string? allowRegistration = null,
        string? systemMfaMode = null) : IConfigurationService
    {
        public Task<string?> GetConfigurationValueAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(key == "auth.allowregistration"
                ? allowRegistration
                : key == "security.mfa.system" ? systemMfaMode : null);
        public Task<List<ConfigurationDto>> GetAllConfigurationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetManageableConfigurationValueAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetPublicConfigurationValueAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConfigurationDto> CreateConfigurationAsync(CreateConfigurationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateConfigurationAsync(string key, UpdateConfigurationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RefreshCacheAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
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

    private sealed class RejectingIdentityService(
        Result<Guid>? registerResult = null,
        bool allowRefreshToken = false) : IIdentityService
    {
        public Task<Result> SaveRefreshTokenAsync(Guid userId, RefreshToken refreshToken, CancellationToken cancellationToken) =>
            allowRefreshToken
                ? Task.FromResult(Result.Success())
                : throw new InvalidOperationException("Refresh token must not be persisted before MFA.");
        public Task<Result<Guid>> RegisterUserAsync(string email, string password, string firstName, string lastName, CancellationToken cancellationToken) =>
            registerResult is not null
                ? Task.FromResult(registerResult)
                : throw new NotSupportedException();
        public Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> ActivateUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> AssignRoleForProductAsync(Guid userId, string roleName, PlatformProduct product, CancellationToken cancellationToken, Identity.Domain.Enums.UserProductAccessSource accessSource = Identity.Domain.Enums.UserProductAccessSource.Admin) =>
            registerResult?.IsSuccess == true ? Task.FromResult(Result.Success()) : throw new NotSupportedException();
        public Task<Result<ProvisionedUser>> RegisterUserWithPasswordSetupAsync(string email, string firstName, string lastName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ProvisionedUser>> RegisterUserWithRoleAsync(string email, string firstName, string lastName, string roleName, PlatformProduct? product, string? phoneNumber, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RemoveRoleForProductAsync(Guid userId, string roleName, PlatformProduct product, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<IEnumerable<string>>> GetAvailableRolesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RevokeRefreshTokenAsync(string token, string ipAddress, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> UpdateUserAsync(Guid userId, string firstName, string lastName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
