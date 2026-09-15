using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
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

public sealed record GoogleRecaptchaVerification(
    bool Success,
    decimal? Score,
    string? Action,
    string? Hostname,
    [property: JsonPropertyName("error-codes")] string[]? ErrorCodes = null);

public static class GoogleRecaptchaRules
{
    public const string ContactAction = "contact_submit";
    public const int MaximumResponseTokenLength = 16_384;

    public static bool HasAcceptableToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) && token.Length <= MaximumResponseTokenLength;

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
        if (!GoogleRecaptchaRules.HasAcceptableToken(token))
        {
            logger.LogWarning(
                "Google reCAPTCHA request was rejected before verification because the response token was missing or exceeded {MaximumLength} characters. TokenLength: {TokenLength}",
                GoogleRecaptchaRules.MaximumResponseTokenLength,
                token?.Length ?? 0);
            return false;
        }

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = options.SecretKey!,
                ["response"] = token!,
                ["remoteip"] = remoteIp ?? string.Empty
            });
            using var response = await httpClient.PostAsync("siteverify", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google reCAPTCHA verification returned HTTP {StatusCode}", response.StatusCode);
                return false;
            }

            var verification = await response.Content.ReadFromJsonAsync<GoogleRecaptchaVerification>(cancellationToken: cancellationToken);
            if (verification is null) return false;

            var accepted = GoogleRecaptchaRules.IsAccepted(options, verification);
            if (!accepted)
            {
                logger.LogWarning(
                    "Google reCAPTCHA verification was rejected. Success: {Success}; Score: {Score}; Action: {Action}; Hostname: {Hostname}; ErrorCodes: {ErrorCodes}",
                    verification.Success,
                    verification.Score,
                    verification.Action,
                    verification.Hostname,
                    verification.ErrorCodes is { Length: > 0 } ? string.Join(',', verification.ErrorCodes) : null);
            }

            return accepted;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Google reCAPTCHA verification request failed");
            return false;
        }
    }
}
