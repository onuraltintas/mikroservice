using FluentAssertions;
using SpeedReading.API.Security;

namespace SpeedReading.Application.UnitTests;

public sealed class NewsletterAntiBotTests
{
    [Fact]
    public void Recaptcha_must_match_the_newsletter_action_and_an_allowed_hostname()
    {
        var options = new GoogleRecaptchaOptions
        {
            Enabled = true,
            MinimumScore = 0.5m,
            AllowedHostnames = ["masterhizliokuma.com"]
        };

        var validNewsletterToken = new GoogleRecaptchaVerification(
            true,
            0.9m,
            GoogleRecaptchaRules.NewsletterSignupAction,
            "masterhizliokuma.com");
        var contactToken = validNewsletterToken with { Action = GoogleRecaptchaRules.ContactAction };

        GoogleRecaptchaRules.IsAccepted(options, validNewsletterToken, GoogleRecaptchaRules.NewsletterSignupAction)
            .Should().BeTrue();
        GoogleRecaptchaRules.IsAccepted(options, contactToken, GoogleRecaptchaRules.NewsletterSignupAction)
            .Should().BeFalse();
        GoogleRecaptchaRules.IsAccepted(
            options,
            validNewsletterToken with { Hostname = "attacker.example" },
            GoogleRecaptchaRules.NewsletterSignupAction).Should().BeFalse();
    }
}
