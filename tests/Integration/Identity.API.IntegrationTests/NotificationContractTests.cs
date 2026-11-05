using FluentAssertions;
using Identity.Domain.Entities;
using Notification.Application.Configuration;
using Notification.Domain.Entities;
using System.Text.Json;

namespace Identity.API.IntegrationTests;

public sealed class NotificationContractTests
{
    [Fact]
    public void PasswordSetupToken_IsShortLivedAndCryptographicallyRandom()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "student@example.test",
            "Ada",
            "Lovelace");

        var firstToken = user.GeneratePasswordResetToken();
        var firstExpiry = user.PasswordResetTokenExpiresAt;
        var secondToken = user.GeneratePasswordResetToken();

        firstToken.Should().HaveLength(43);
        firstToken.Should().NotBe(secondToken);
        user.PasswordResetTokenMatches(secondToken).Should().BeTrue();
        user.PasswordResetTokenMatches(firstToken).Should().BeFalse("a new link replaces the previous one");
        firstExpiry.Should().BeAfter(DateTime.UtcNow.AddHours(1));
        firstExpiry.Should().BeBefore(DateTime.UtcNow.AddHours(3));
    }

    [Fact]
    public void UserProvisioningResponse_ShouldNotExposeASecret()
    {
        var response = new Identity.Application.Commands.CreateUser.CreateUserResponse(Guid.NewGuid());

        JsonSerializer.Serialize(response)
            .Should().NotContain("TemporaryPassword");
    }

    [Fact]
    public void PublicAppUrlOptions_BuildsVerificationLinkFromConfiguredOrigin()
    {
        var options = new PublicAppUrlOptions { BaseUrl = "https://staging.example.test/" };
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var link = options.BuildEmailVerificationLink(userId, "token with & unsafe");

        link.Should().Be(
            "https://staging.example.test/auth/confirm-email?userId=11111111-1111-1111-1111-111111111111&token=token%20with%20%26%20unsafe");
    }

    [Fact]
    public void PublicAppUrlOptions_BuildsPasswordResetLinkWithEncodedValues()
    {
        var options = new PublicAppUrlOptions { BaseUrl = "https://staging.example.test" };

        var link = options.BuildPasswordResetLink("reset token", "ada+test@example.test");

        link.Should().Be(
            "https://staging.example.test/auth/reset-password?token=reset%20token&email=ada%2Btest%40example.test");
    }

    [Fact]
    public void PublicAppUrlOptions_UsesSpeedReadingOriginForSpeedReadingRoles()
    {
        var options = new PublicAppUrlOptions
        {
            BaseUrl = "https://eduivme.example.test",
            SpeedReadingBaseUrl = "https://masterhizliokuma.example.test/"
        };

        options.BuildEmailVerificationLink(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "verification-token",
                "Student")
            .Should().StartWith("https://masterhizliokuma.example.test/auth/verify-email");

        options.BuildPasswordResetLink("reset-token", "student@example.test", "Teacher")
            .Should().StartWith("https://masterhizliokuma.example.test/auth/reset-password");
    }

    // User decision 05.11.2026: Coaching links use the Coaching site (PublicApp:BaseUrl = onuraltintas.net in
    // production); management accounts (SystemAdmin, Editor) keep the admin panel (PublicApp:AdminPanelUrl).
    [Theory]
    [InlineData("Student", "coaching", "https://onuraltintas.example.test")]
    [InlineData("Parent", "coaching", "https://onuraltintas.example.test")]
    [InlineData("Teacher", "coaching", "https://onuraltintas.example.test")]
    [InlineData("Parent", null, "https://onuraltintas.example.test")]
    [InlineData("SystemAdmin", null, "https://eduivme.example.test")]
    [InlineData("Editor", null, "https://eduivme.example.test")]
    [InlineData("SystemAdmin", "coaching", "https://eduivme.example.test")]
    [InlineData("Student", "speed-reading", "https://masterhizliokuma.example.test")]
    public void PublicAppUrlOptions_SendsCoachingToTheCoachingSiteAndManagementToTheAdminPanel(string role, string? product, string origin)
    {
        var options = new PublicAppUrlOptions
        {
            BaseUrl = "https://onuraltintas.example.test",
            SpeedReadingBaseUrl = "https://masterhizliokuma.example.test",
            AdminPanelUrl = "https://eduivme.example.test"
        };
        var userId = Guid.NewGuid();

        options.ResolveBaseUrl(role, product).Should().Be(origin);
        options.BuildLoginLink(role, product).Should().Be($"{origin}/auth/login");
        options.BuildPasswordResetLink("t", "a@example.test", role, product).Should().StartWith($"{origin}/auth/reset-password?");
        options.BuildEmailVerificationLink(userId, "t", role, product).Should().StartWith(origin + "/auth/");
        options.AdminPanelLink("/dashboard/coaching/subscriptions").Should().Be("https://eduivme.example.test/dashboard/coaching/subscriptions");
    }

    [Fact]
    public void PublicAppUrlOptions_ManagementFallsBackToTheBaseUrlWithoutAnAdminPanelUrl()
    {
        var options = new PublicAppUrlOptions { BaseUrl = "https://eduivme.example.test" };
        options.ResolveBaseUrl("SystemAdmin", null).Should().Be("https://eduivme.example.test");
    }

    // Second review L5: an unknown product never throws; the role decides as for older messages.
    [Theory]
    [InlineData("Student", "something-new", true)]
    [InlineData("Teacher", "", true)]
    [InlineData("SystemAdmin", "something-new", false)]
    public void PublicAppUrlOptions_UnknownProductFallsBackToTheRole(string role, string product, bool speedReading)
    {
        var options = new PublicAppUrlOptions { BaseUrl = "https://eduivme.example.test", SpeedReadingBaseUrl = "https://masterhizliokuma.example.test" };
        options.ResolveBaseUrl(role, product).Should().Be(speedReading ? "https://masterhizliokuma.example.test" : "https://eduivme.example.test");
        PublicAppUrlOptions.GetTemplateName("Auth_VerifyEmail", role, product).Should().Be(speedReading ? "SpeedReading_Auth_VerifyEmail" : "Auth_VerifyEmail");
        PublicAppUrlOptions.IsKnownProduct(product).Should().BeFalse();
        PublicAppUrlOptions.IsKnownProduct("coaching").Should().BeTrue();
        PublicAppUrlOptions.IsKnownProduct(null).Should().BeTrue("no product is the old message shape");
    }

    [Fact]
    public void PublicAppUrlOptions_VerificationPathsMatchTheClientRoutes()
    {
        PublicAppUrlOptions.CoachingEmailVerificationPath.Should().Be("/auth/confirm-email");
        PublicAppUrlOptions.SpeedReadingEmailVerificationPath.Should().Be("/auth/verify-email");
        var options = new PublicAppUrlOptions { BaseUrl = "https://eduivme.example.test", SpeedReadingBaseUrl = "https://masterhizliokuma.example.test" };
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        options.BuildEmailVerificationLink(userId, "t", "Student", "coaching")
            .Should().Be("https://eduivme.example.test/auth/confirm-email?userId=11111111-1111-1111-1111-111111111111&token=t");
        options.BuildEmailVerificationLink(userId, "t", "Student", "speed-reading")
            .Should().Be("https://masterhizliokuma.example.test/auth/verify-email?token=t&userId=11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public void PublicAppUrlOptions_UsesCentralOriginForCentralRoles()
    {
        var options = new PublicAppUrlOptions
        {
            BaseUrl = "https://eduivme.example.test",
            SpeedReadingBaseUrl = "https://masterhizliokuma.example.test"
        };

        options.BuildLoginLink("SystemAdmin")
            .Should().Be("https://eduivme.example.test/auth/login");
        PublicAppUrlOptions.IsSpeedReadingRole("Editor").Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("localhost:4200")]
    [InlineData("https://user:secret@example.test")]
    [InlineData("https://example.test/path?token=leak")]
    [InlineData("ftp://example.test")]
    public void PublicAppUrlOptions_RejectsUnsafeOrigins(string baseUrl)
    {
        PublicAppUrlOptions.IsValidBaseUrl(baseUrl).Should().BeFalse();
    }

    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://[::1]")]
    public void PublicAppUrlOptions_RejectsLoopbackOriginsInProduction(string baseUrl)
    {
        PublicAppUrlOptions.IsValidForEnvironment(baseUrl, isProduction: true).Should().BeFalse();
        PublicAppUrlOptions.IsValidForEnvironment(baseUrl, isProduction: false).Should().BeTrue();
    }

    [Fact]
    public void SupportRequest_SameIdempotencyKeyWithChangedPayload_IsNotEquivalent()
    {
        var supportRequest = new SupportRequest(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            "ada@example.test",
            "Access",
            "I need help signing in.",
            "support-request-0001");

        supportRequest.HasSamePayload(
                "Ada",
                "Lovelace",
                "ada@example.test",
                "Access",
                "A different request body.")
            .Should().BeFalse();
    }

    [Fact]
    public void SupportRequest_RepeatedEquivalentPayload_IsEquivalentAfterNormalization()
    {
        var supportRequest = new SupportRequest(
            Guid.NewGuid(),
            " Ada ",
            "Lovelace",
            "ada@example.test",
            "Access",
            "I need help signing in.",
            "support-request-0001");

        supportRequest.HasSamePayload(
                "Ada",
                "Lovelace",
                "ADA@EXAMPLE.TEST",
                "Access",
                "I need help signing in.")
            .Should().BeTrue();
    }
}
