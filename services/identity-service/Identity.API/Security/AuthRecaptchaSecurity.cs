using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Identity.API.Security;

public sealed record AuthRecaptchaOptions
{
    public const string SectionName = "AuthRecaptcha";

    public bool? Enabled { get; init; }
    public string? SiteKey { get; init; }
    public string? SecretKey { get; init; }
    public string? SecretKeyFile { get; init; }
    public decimal MinimumScore { get; init; } = 0.5m;
    public string[] AllowedHostnames { get; init; } = [];

    public AuthRecaptchaOptions Validate(bool isProduction)
    {
        if (Enabled is not true)
        {
            if (isProduction && Enabled is null)
            {
                throw new InvalidOperationException("Auth CAPTCHA must be explicitly enabled or disabled in production.");
            }

            return this with { Enabled = false };
        }

        var validatedOptions = this;
        if (!string.IsNullOrWhiteSpace(SecretKeyFile))
        {
            try
            {
                validatedOptions = this with { SecretKey = File.ReadAllText(SecretKeyFile).Trim() };
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or ArgumentException
                                              or NotSupportedException)
            {
                throw new InvalidOperationException("Auth CAPTCHA secret key file could not be read.", exception);
            }
        }

        if (validatedOptions.SiteKey is not null && validatedOptions.SecretKey is not null)
        {
            validatedOptions = validatedOptions with
            {
                SiteKey = validatedOptions.SiteKey.Trim(),
                SecretKey = validatedOptions.SecretKey.Trim()
            };
        }

        if (string.IsNullOrWhiteSpace(validatedOptions.SiteKey)
            || string.IsNullOrWhiteSpace(validatedOptions.SecretKey))
        {
            throw new InvalidOperationException("Auth CAPTCHA site and secret keys must be configured.");
        }

        if (validatedOptions.MinimumScore is < 0m or > 1m)
        {
            throw new InvalidOperationException("Auth CAPTCHA minimum score must be between 0 and 1.");
        }

        if (validatedOptions.AllowedHostnames is null
            || validatedOptions.AllowedHostnames.Length == 0
            || validatedOptions.AllowedHostnames.Any(hostname =>
                string.IsNullOrWhiteSpace(hostname)
                || Uri.CheckHostName(hostname.Trim().TrimEnd('.')) != UriHostNameType.Dns))
        {
            throw new InvalidOperationException("Auth CAPTCHA must have valid allowed hostnames configured.");
        }

        return validatedOptions;
    }

    public AuthRecaptchaPublicConfiguration ToPublicConfiguration() =>
        new(Enabled is true, Enabled is true ? SiteKey : null);

    public override string ToString() =>
        $"AuthRecaptchaOptions {{ Enabled = {Enabled}, MinimumScore = {MinimumScore}, AllowedHostnameCount = {AllowedHostnames?.Length ?? 0} }}";
}

public sealed record AuthRecaptchaPublicConfiguration(bool Enabled, string? SiteKey);

public sealed record AuthRecaptchaVerification(
    bool Success,
    decimal? Score,
    string? Action,
    string? Hostname);

public static class AuthRecaptchaRules
{
    public static string? GetRequiredAction(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return null;
        }

        var segments = request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        if (segments.Length < 3
            || !string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[1], "auth", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (segments.Length == 3)
        {
            return segments[2].ToLowerInvariant() switch
            {
                "login" or "google" or "google-login" => "auth_login",
                "forgot-password" or "resend-verification-email" => "auth_recovery",
                "register" => "auth_register",
                _ => null
            };
        }

        if (segments.Length == 4
            && IsProduct(segments[2]))
        {
            return segments[3].ToLowerInvariant() switch
            {
                "login" or "google" or "google-login" => "auth_login",
                _ => null
            };
        }

        if (segments.Length == 5
            && IsProduct(segments[2])
            && string.Equals(segments[3], "register", StringComparison.OrdinalIgnoreCase)
            && segments[4].ToLowerInvariant() is "student" or "teacher" or "institution" or "parent")
        {
            return "auth_register";
        }

        return null;
    }

    public static bool IsAccepted(
        AuthRecaptchaOptions options,
        AuthRecaptchaVerification? verification,
        string expectedAction)
    {
        if (options.Enabled is not true || verification is null || !verification.Success
            || verification.Score is null || verification.Score < options.MinimumScore
            || verification.Score > 1m
            || !string.Equals(verification.Action, expectedAction, StringComparison.Ordinal))
        {
            return false;
        }

        var hostname = verification.Hostname?.Trim().TrimEnd('.');
        return !string.IsNullOrWhiteSpace(hostname)
            && options.AllowedHostnames.Any(allowed =>
                string.Equals(hostname, allowed.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProduct(string product) =>
        string.Equals(product, "coaching", StringComparison.OrdinalIgnoreCase)
        || string.Equals(product, "speed-reading", StringComparison.OrdinalIgnoreCase);
}

public interface IAuthRecaptchaVerifier
{
    Task<bool> VerifyAsync(string? token, string action, CancellationToken cancellationToken);
}

public sealed class GoogleAuthRecaptchaVerifier(
    HttpClient httpClient,
    AuthRecaptchaOptions options,
    ILogger<GoogleAuthRecaptchaVerifier> logger) : IAuthRecaptchaVerifier
{
    public async Task<bool> VerifyAsync(string? token, string action, CancellationToken cancellationToken)
    {
        if (options.Enabled is not true)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            using var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("secret", options.SecretKey!),
                new KeyValuePair<string, string>("response", token)
            ]);

            using var response = await httpClient.PostAsync("siteverify", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<GoogleRecaptchaResponse>(
                cancellationToken: cancellationToken);
            return AuthRecaptchaRules.IsAccepted(
                options,
                result?.ToVerification(),
                action);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
                                          or JsonException
                                          or TaskCanceledException)
        {
            logger.LogWarning("Google CAPTCHA verification was unavailable; rejecting the auth request.");
            return false;
        }
    }

    private sealed record GoogleRecaptchaResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("score")] decimal? Score,
        [property: JsonPropertyName("action")] string? Action,
        [property: JsonPropertyName("hostname")] string? Hostname)
    {
        public AuthRecaptchaVerification ToVerification() => new(Success, Score, Action, Hostname);
    }
}

public sealed class AuthRecaptchaMiddleware(
    RequestDelegate next,
    IAuthRecaptchaVerifier verifier,
    ILogger<AuthRecaptchaMiddleware> logger)
{
    public const string TokenHeaderName = "X-Auth-Recaptcha-Token";

    public async Task InvokeAsync(HttpContext context)
    {
        var action = AuthRecaptchaRules.GetRequiredAction(context.Request);
        if (action is null)
        {
            await next(context);
            return;
        }

        var token = context.Request.Headers[TokenHeaderName].FirstOrDefault();
        if (!await verifier.VerifyAsync(token, action, context.RequestAborted))
        {
            logger.LogInformation("Rejected auth request because CAPTCHA verification failed for action {Action}.", action);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    success = false,
                    code = "Auth.CaptchaFailed",
                    message = "Güvenlik doğrulaması tamamlanamadı. Lütfen tekrar deneyin."
                },
                context.RequestAborted);
            return;
        }

        await next(context);
    }
}
