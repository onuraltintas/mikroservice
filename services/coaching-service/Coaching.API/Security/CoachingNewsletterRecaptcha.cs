using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Coaching.API.Security;

public sealed class CoachingNewsletterRecaptchaOptions
{
    public const string SectionName = "CoachingNewsletterRecaptcha";

    public bool Enabled { get; set; }
    public string? SiteKey { get; set; }
    public string? SecretKey { get; set; }
    public decimal MinimumScore { get; set; } = 0.5m;
    public string[] AllowedHostnames { get; set; } = [];

    public void ApplyEnvironmentOverrides(IConfiguration configuration)
    {
        if (bool.TryParse(configuration["COACHING_NEWSLETTER_RECAPTCHA_ENABLED"], out var enabled))
            Enabled = enabled;
        SiteKey = configuration["COACHING_NEWSLETTER_RECAPTCHA_SITE_KEY"] ?? SiteKey;
        SecretKey = configuration["COACHING_NEWSLETTER_RECAPTCHA_SECRET_KEY"] ?? SecretKey;
        if (decimal.TryParse(
                configuration["COACHING_NEWSLETTER_RECAPTCHA_MINIMUM_SCORE"],
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var minimumScore))
        {
            MinimumScore = minimumScore;
        }

        var hostnames = configuration["COACHING_NEWSLETTER_RECAPTCHA_ALLOWED_HOSTNAMES"];
        if (!string.IsNullOrWhiteSpace(hostnames))
        {
            AllowedHostnames = hostnames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    public void Validate()
    {
        AllowedHostnames ??= [];
        if (!Enabled)
            return;

        if (string.IsNullOrWhiteSpace(SiteKey)
            || string.IsNullOrWhiteSpace(SecretKey)
            || AllowedHostnames.Length == 0
            || MinimumScore is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Enabled Coaching newsletter reCAPTCHA requires site/secret keys, allowed hostnames, and a score from 0 to 1.");
        }
    }

    public CoachingNewsletterRecaptchaPublicConfiguration ToPublicConfiguration() =>
        new(Enabled, Enabled ? SiteKey : null);
}

public sealed record CoachingNewsletterRecaptchaPublicConfiguration(bool Enabled, string? SiteKey);

public sealed record CoachingNewsletterRecaptchaVerification(
    bool Success,
    decimal? Score,
    string? Action,
    string? Hostname,
    [property: JsonPropertyName("error-codes")] string[]? ErrorCodes = null);

public static class CoachingNewsletterRecaptchaRules
{
    public const string NewsletterSignupAction = "newsletter_signup";
    public const string PaymentRequestAction = "coaching_eft_submit";
    public const int MaximumResponseTokenLength = 16_384;

    public static bool IsAccepted(
        CoachingNewsletterRecaptchaOptions options,
        CoachingNewsletterRecaptchaVerification verification,
        string expectedAction = NewsletterSignupAction)
    {
        if (expectedAction is not (NewsletterSignupAction or PaymentRequestAction)
            || !verification.Success
            || !verification.Score.HasValue
            || verification.Score.Value > 1
            || verification.Score.Value < options.MinimumScore
            || !string.Equals(verification.Action, expectedAction, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(verification.Hostname))
        {
            return false;
        }

        return options.AllowedHostnames.Any(hostname =>
            string.Equals(hostname, verification.Hostname, StringComparison.OrdinalIgnoreCase));
    }
}

public interface ICoachingNewsletterRecaptchaValidator
{
    Task<bool> VerifyAsync(
        string? token,
        string? remoteIp,
        string action,
        CancellationToken cancellationToken);
}

public sealed class CoachingNewsletterRecaptchaValidator(
    HttpClient httpClient,
    CoachingNewsletterRecaptchaOptions options,
    ILogger<CoachingNewsletterRecaptchaValidator> logger) : ICoachingNewsletterRecaptchaValidator
{
    public async Task<bool> VerifyAsync(
        string? token,
        string? remoteIp,
        string action,
        CancellationToken cancellationToken)
    {
        if (!options.Enabled)
            return true;

        if (string.IsNullOrWhiteSpace(token) || token.Length > CoachingNewsletterRecaptchaRules.MaximumResponseTokenLength)
            return false;

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = options.SecretKey!,
                ["response"] = token,
                ["remoteip"] = remoteIp ?? string.Empty
            });
            using var response = await httpClient.PostAsync("siteverify", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Coaching newsletter reCAPTCHA verification returned HTTP {StatusCode}", response.StatusCode);
                return false;
            }

            var verification = await response.Content.ReadFromJsonAsync<CoachingNewsletterRecaptchaVerification>(
                cancellationToken: cancellationToken);
            return verification is not null
                && CoachingNewsletterRecaptchaRules.IsAccepted(options, verification, action);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Coaching newsletter reCAPTCHA verification failed");
            return false;
        }
    }
}
