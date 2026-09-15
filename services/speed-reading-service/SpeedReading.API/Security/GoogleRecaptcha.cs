using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SpeedReading.API.Security;

public sealed class GoogleRecaptchaOptions
{
    public const string SectionName = "GoogleRecaptcha";

    public bool Enabled { get; set; }
    public string? SiteKey { get; set; }
    public string? SecretKey { get; set; }
    public decimal MinimumScore { get; set; } = 0.5m;
    public string[] AllowedHostnames { get; set; } = [];

    public void ApplyEnvironmentOverrides(IConfiguration configuration)
    {
        if (bool.TryParse(configuration["GOOGLE_RECAPTCHA_ENABLED"], out var enabled)) Enabled = enabled;
        SiteKey = configuration["GOOGLE_RECAPTCHA_SITE_KEY"] ?? SiteKey;
        SecretKey = configuration["GOOGLE_RECAPTCHA_SECRET_KEY"] ?? SecretKey;

        if (decimal.TryParse(
                configuration["GOOGLE_RECAPTCHA_MINIMUM_SCORE"],
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var minimumScore))
        {
            MinimumScore = minimumScore;
        }

        var allowedHostnames = configuration["GOOGLE_RECAPTCHA_ALLOWED_HOSTNAMES"];
        if (!string.IsNullOrWhiteSpace(allowedHostnames))
        {
            AllowedHostnames = allowedHostnames
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    public void Validate()
    {
        AllowedHostnames ??= [];
        if (!Enabled) return;

        if (string.IsNullOrWhiteSpace(SiteKey)
            || string.IsNullOrWhiteSpace(SecretKey)
            || AllowedHostnames.Length == 0
            || MinimumScore is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Google reCAPTCHA requires a site key, secret key, allowed hostname, and a score from 0 to 1.");
        }
    }

    public GoogleRecaptchaPublicConfiguration ToPublicConfiguration() =>
        new(Enabled, Enabled ? SiteKey : null);
}

public sealed record GoogleRecaptchaPublicConfiguration(bool Enabled, string? SiteKey);

public sealed record GoogleRecaptchaVerification(bool Success, decimal? Score, string? Action, string? Hostname);

public static class GoogleRecaptchaRules
{
    public const string ContactAction = "contact_submit";

    public static bool IsAccepted(GoogleRecaptchaOptions options, GoogleRecaptchaVerification verification)
    {
        if (!verification.Success
            || !verification.Score.HasValue
            || verification.Score.Value < options.MinimumScore
            || !string.Equals(verification.Action, ContactAction, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(verification.Hostname))
        {
            return false;
        }

        return options.AllowedHostnames.Any(hostname =>
            string.Equals(hostname, verification.Hostname, StringComparison.OrdinalIgnoreCase));
    }
}

public interface IGoogleRecaptchaValidator
{
    Task<bool> VerifyContactAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}

public sealed class GoogleRecaptchaValidator(
    HttpClient httpClient,
    GoogleRecaptchaOptions options,
    ILogger<GoogleRecaptchaValidator> logger) : IGoogleRecaptchaValidator
{
    public async Task<bool> VerifyContactAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        if (!options.Enabled) return true;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2_048) return false;

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
                logger.LogWarning("Google reCAPTCHA verification returned HTTP {StatusCode}", response.StatusCode);
                return false;
            }

            var verification = await response.Content.ReadFromJsonAsync<GoogleRecaptchaVerification>(cancellationToken: cancellationToken);
            return verification is not null && GoogleRecaptchaRules.IsAccepted(options, verification);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Google reCAPTCHA verification request failed");
            return false;
        }
    }
}
