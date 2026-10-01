using Identity.API.Security;
using FluentAssertions;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.API.IntegrationTests;

public sealed class AuthRecaptchaSecurityTests
{
    [Theory]
    [InlineData("/api/auth/login", "auth_login")]
    [InlineData("/api/auth/coaching/login", "auth_login")]
    [InlineData("/api/auth/speed-reading/login", "auth_login")]
    [InlineData("/api/auth/google", "auth_login")]
    [InlineData("/api/auth/coaching/google-login", "auth_login")]
    [InlineData("/api/auth/coaching/google-register-complete", "auth_register")]
    [InlineData("/api/auth/speed-reading/google-register-complete", "auth_register")]
    [InlineData("/api/auth/coaching/register/student", "auth_register")]
    [InlineData("/api/auth/speed-reading/register/teacher", "auth_register")]
    [InlineData("/api/auth/coaching/register/institution", "auth_register")]
    [InlineData("/api/auth/coaching/register/parent", "auth_register")]
    [InlineData("/api/auth/forgot-password", "auth_recovery")]
    [InlineData("/api/auth/resend-verification-email", "auth_recovery")]
    public void ProtectedAuthPost_MapsToExpectedRecaptchaAction(string path, string expectedAction)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;

        AuthRecaptchaRules.GetRequiredAction(context.Request).Should().Be(expectedAction);
    }

    [Theory]
    [InlineData("GET", "/api/auth/captcha-config")]
    [InlineData("POST", "/api/auth/mfa/verify")]
    [InlineData("POST", "/api/auth/reset-password")]
    [InlineData("POST", "/api/users")]
    public void OtherEndpoints_DoNotRequireLoginRecaptcha(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        AuthRecaptchaRules.GetRequiredAction(context.Request).Should().BeNull();
    }

    [Fact]
    public void RecaptchaAcceptance_RequiresSuccessScoreActionAndAllowedHostname()
    {
        var options = ValidOptions();
        var valid = new AuthRecaptchaVerification(true, 0.8m, "auth_login", "EDUIVME.COM");

        AuthRecaptchaRules.IsAccepted(options, valid, "auth_login").Should().BeTrue();
        AuthRecaptchaRules.IsAccepted(options, valid with { Success = false }, "auth_login").Should().BeFalse();
        AuthRecaptchaRules.IsAccepted(options, valid with { Score = 0.49m }, "auth_login").Should().BeFalse();
        AuthRecaptchaRules.IsAccepted(options, valid with { Score = 1.01m }, "auth_login").Should().BeFalse();
        AuthRecaptchaRules.IsAccepted(options, valid with { Action = "auth_register" }, "auth_login").Should().BeFalse();
        AuthRecaptchaRules.IsAccepted(options, valid with { Hostname = "untrusted.example" }, "auth_login").Should().BeFalse();
    }

    [Fact]
    public void EnabledConfiguration_RequiresKeysHostsAndValidScore()
    {
        var options = ValidOptions();

        options.Validate(isProduction: true);

        FluentActions.Invoking(() => (options with { SecretKey = " " }).Validate(isProduction: true))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => (options with { SecretKey = " ", SecretKeyFile = "missing-auth-recaptcha-secret" })
                .Validate(isProduction: true))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => (options with { AllowedHostnames = [] }).Validate(isProduction: true))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => (options with { MinimumScore = 1.1m }).Validate(isProduction: true))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnabledConfiguration_LoadsPrivateKeyFromConfiguredSecretFile()
    {
        var secretPath = Path.Combine(Path.GetTempPath(), $"auth-recaptcha-secret-{Guid.NewGuid():N}");
        File.WriteAllText(secretPath, "private-key\r\n");

        try
        {
            var options = ValidOptions() with { SecretKey = " ", SecretKeyFile = secretPath };

            var validatedOptions = options.Validate(isProduction: true);

            validatedOptions.SecretKey.Should().Be("private-key");
            options.SecretKey.Should().Be(" ");
        }
        finally
        {
            File.Delete(secretPath);
        }
    }

    [Fact]
    public void ExplicitlyDisabledConfiguration_IsAllowedInProduction_AndDoesNotPublishASiteKey()
    {
        var options = new AuthRecaptchaOptions { Enabled = false };

        options.Validate(isProduction: true).Enabled.Should().BeFalse();
        options.ToPublicConfiguration().Should().BeEquivalentTo(new AuthRecaptchaPublicConfiguration(false, null));
    }

    [Fact]
    public void UnconfiguredConfiguration_IsRejectedInProduction()
    {
        new AuthRecaptchaOptions().Validate(isProduction: false).Enabled.Should().BeFalse();
        FluentActions.Invoking(() => new AuthRecaptchaOptions().Validate(isProduction: true))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task DisabledVerifier_AllowsAuthWithoutCallingGoogle()
    {
        var googleCalled = false;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            googleCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        var verifier = new GoogleAuthRecaptchaVerifier(
            httpClient,
            new AuthRecaptchaOptions { Enabled = false }.Validate(isProduction: true),
            NullLogger<GoogleAuthRecaptchaVerifier>.Instance);

        var accepted = await verifier.VerifyAsync(null, "auth_login", CancellationToken.None);

        accepted.Should().BeTrue();
        googleCalled.Should().BeFalse();
    }

    [Fact]
    public void PublicConfiguration_ContainsOnlyThePublicSiteKey()
    {
        var publicConfiguration = ValidOptions().ToPublicConfiguration();

        publicConfiguration.Enabled.Should().BeTrue();
        publicConfiguration.SiteKey.Should().Be("public-key");
        JsonSerializer.Serialize(publicConfiguration).Should().NotContain("private-key");
        JsonSerializer.Serialize(publicConfiguration).Should().NotContain("secretKey");
        ValidOptions().ToString().Should().NotContain("private-key");
    }

    [Fact]
    public async Task Verifier_AcceptsGoogleResponseOnlyWhenItMatchesConfiguredActionAndHost()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"score\":0.8,\"action\":\"auth_login\",\"hostname\":\"eduivme.com\"}",
                    Encoding.UTF8,
                    "application/json")
            })))
        {
            BaseAddress = new Uri("https://www.google.com/recaptcha/api/")
        };
        var verifier = new GoogleAuthRecaptchaVerifier(
            httpClient,
            ValidOptions(),
            NullLogger<GoogleAuthRecaptchaVerifier>.Instance);

        var accepted = await verifier.VerifyAsync("one-time-token", "auth_login", CancellationToken.None);

        accepted.Should().BeTrue();
    }

    [Fact]
    public async Task Verifier_FailsClosedWhenGoogleIsUnavailableOrTokenIsMissing()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("offline"));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.google.com/recaptcha/api/")
        };
        var verifier = new GoogleAuthRecaptchaVerifier(
            httpClient,
            ValidOptions(),
            NullLogger<GoogleAuthRecaptchaVerifier>.Instance);

        (await verifier.VerifyAsync(null, "auth_login", CancellationToken.None)).Should().BeFalse();
        (await verifier.VerifyAsync("token", "auth_login", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task EnabledMiddleware_RejectsInvalidTokenBeforeCallingAuthEndpoint()
    {
        var verifier = new StubAuthRecaptchaVerifier(accepted: false);
        var nextCalled = false;
        var middleware = new AuthRecaptchaMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            verifier,
            NullLogger<AuthRecaptchaMiddleware>.Instance);
        var context = AuthRequest("/api/auth/login", "invalid-token");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
        verifier.LastAction.Should().Be("auth_login");
        verifier.LastToken.Should().Be("invalid-token");
    }

    [Fact]
    public async Task EnabledMiddleware_AllowsValidTokenAndSkipsUnprotectedEndpoints()
    {
        var verifier = new StubAuthRecaptchaVerifier(accepted: true);
        var nextCalled = false;
        var middleware = new AuthRecaptchaMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            verifier,
            NullLogger<AuthRecaptchaMiddleware>.Instance);
        var context = AuthRequest("/api/auth/login", "valid-token");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        verifier.LastToken.Should().Be("valid-token");

        nextCalled = false;
        var unprotectedContext = AuthRequest("/api/auth/mfa/verify", "must-not-be-verified");
        await middleware.InvokeAsync(unprotectedContext);

        nextCalled.Should().BeTrue();
        verifier.VerificationCount.Should().Be(1);
    }

    private static DefaultHttpContext AuthRequest(string path, string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Request.Headers[AuthRecaptchaMiddleware.TokenHeaderName] = token;
        return context;
    }

    private static AuthRecaptchaOptions ValidOptions() => new()
    {
        Enabled = true,
        SiteKey = "public-key",
        SecretKey = "private-key",
        MinimumScore = 0.5m,
        AllowedHostnames = ["eduivme.com", "onuraltintas.net", "masterhizliokuma.com"]
    };

    private sealed class StubAuthRecaptchaVerifier(bool accepted) : IAuthRecaptchaVerifier
    {
        public string? LastToken { get; private set; }
        public string? LastAction { get; private set; }
        public int VerificationCount { get; private set; }

        public Task<bool> VerifyAsync(string? token, string action, CancellationToken cancellationToken)
        {
            VerificationCount++;
            LastToken = token;
            LastAction = action;
            return Task.FromResult(accepted);
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => sendAsync(request, cancellationToken);
    }
}
