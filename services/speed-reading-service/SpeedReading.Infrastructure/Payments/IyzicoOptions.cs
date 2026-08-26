using Microsoft.Extensions.Configuration;

namespace SpeedReading.Infrastructure.Payments;

public sealed class IyzicoOptions
{
    public const string SectionName = "Iyzico";

    public string? ApiKey { get; set; }
    public string? SecretKey { get; set; }
    public string BaseUrl { get; set; } = "https://sandbox-api.iyzipay.com";
    public string? CallbackUrl { get; set; }
    public string? SuccessRedirectUrl { get; set; }
    public string Locale { get; set; } = "tr";
    public bool RequireResponseSignature { get; set; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && IsHttpsUrl(BaseUrl)
        && IsHttpsUrl(CallbackUrl)
        && IsHttpsUrl(SuccessRedirectUrl);

    public void ApplyEnvironmentOverrides(IConfiguration configuration)
    {
        ApiKey = FirstValue(ApiKey, configuration["IYZICO_API_KEY"], configuration[$"{SectionName}:ApiKey"]);
        SecretKey = FirstValue(SecretKey, configuration["IYZICO_SECRET_KEY"], configuration[$"{SectionName}:SecretKey"]);
        BaseUrl = FirstValue(BaseUrl, configuration["IYZICO_BASE_URL"], configuration[$"{SectionName}:BaseUrl"])
            ?? BaseUrl;
        CallbackUrl = FirstValue(CallbackUrl, configuration["IYZICO_CALLBACK_URL"], configuration[$"{SectionName}:CallbackUrl"]);
        SuccessRedirectUrl = FirstValue(SuccessRedirectUrl, configuration["IYZICO_SUCCESS_REDIRECT_URL"], configuration[$"{SectionName}:SuccessRedirectUrl"]);
        Locale = FirstValue(Locale, configuration["IYZICO_LOCALE"], configuration[$"{SectionName}:Locale"])
            ?? Locale;

        var requireSignature = FirstValue(
            RequireResponseSignature.ToString(),
            configuration["IYZICO_REQUIRE_RESPONSE_SIGNATURE"],
            configuration[$"{SectionName}:RequireResponseSignature"]);
        if (bool.TryParse(requireSignature, out var parsedRequireSignature))
        {
            RequireResponseSignature = parsedRequireSignature;
        }
    }

    private static string? FirstValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsHttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrWhiteSpace(uri.Host);
}
