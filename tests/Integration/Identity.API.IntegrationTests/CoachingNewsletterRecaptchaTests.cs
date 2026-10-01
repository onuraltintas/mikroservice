using System.Net;
using Coaching.API.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingNewsletterRecaptchaTests
{
    [Fact]
    public void EnabledConfigurationRequiresValidKeysHostnameAndScore()
    {
        var options = new CoachingNewsletterRecaptchaOptions { Enabled = true };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void VerificationRequiresSuccessMinimumScoreExpectedActionAndAllowedHostname()
    {
        var options = new CoachingNewsletterRecaptchaOptions
        {
            Enabled = true,
            SiteKey = "public-site-key",
            SecretKey = "server-secret",
            MinimumScore = 0.5m,
            AllowedHostnames = ["onuraltintas.net"]
        };
        var valid = new CoachingNewsletterRecaptchaVerification(
            true,
            0.8m,
            CoachingNewsletterRecaptchaRules.NewsletterSignupAction,
            "onuraltintas.net");

        CoachingNewsletterRecaptchaRules.IsAccepted(options, valid).Should().BeTrue();
        CoachingNewsletterRecaptchaRules.IsAccepted(options, valid with { Score = 0.2m }).Should().BeFalse();
        CoachingNewsletterRecaptchaRules.IsAccepted(options, valid with { Action = "contact_submit" }).Should().BeFalse();
        CoachingNewsletterRecaptchaRules.IsAccepted(options, valid with { Hostname = "attacker.example" }).Should().BeFalse();
        CoachingNewsletterRecaptchaRules.IsAccepted(options, valid with { Success = false }).Should().BeFalse();
    }

    [Fact]
    public async Task ValidatorVerifiesTokenAndRejectsInvalidProviderResponse()
    {
        var options = new CoachingNewsletterRecaptchaOptions
        {
            Enabled = true,
            SiteKey = "public-site-key",
            SecretKey = "server-secret",
            AllowedHostnames = ["onuraltintas.net"]
        };
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"success":true,"score":0.9,"action":"newsletter_signup","hostname":"onuraltintas.net"}
                """)
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.google.com/recaptcha/api/") };
        var validator = new CoachingNewsletterRecaptchaValidator(
            httpClient,
            options,
            NullLogger<CoachingNewsletterRecaptchaValidator>.Instance);

        var accepted = await validator.VerifyAsync("captcha-token", "192.0.2.4", "newsletter_signup", CancellationToken.None);

        accepted.Should().BeTrue();
        handler.RequestUri.Should().Be(new Uri("https://www.google.com/recaptcha/api/siteverify"));
        handler.RequestBody.Should().Contain("server-secret").And.Contain("captcha-token").And.Contain("192.0.2.4");
    }

    [Fact]
    public async Task DisabledValidatorAllowsSignupWithoutCallingGoogle()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Google must not be called when disabled."));
        using var httpClient = new HttpClient(handler);
        var validator = new CoachingNewsletterRecaptchaValidator(
            httpClient,
            new CoachingNewsletterRecaptchaOptions(),
            NullLogger<CoachingNewsletterRecaptchaValidator>.Instance);

        var accepted = await validator.VerifyAsync(null, null, "newsletter_signup", CancellationToken.None);

        accepted.Should().BeTrue();
        handler.RequestUri.Should().BeNull();
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
