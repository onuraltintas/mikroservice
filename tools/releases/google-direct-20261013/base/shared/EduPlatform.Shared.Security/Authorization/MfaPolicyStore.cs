using System.Net.Http.Json;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Shared.Security.Authorization;

/// <summary>
/// Reads the authoritative MFA mode from Identity without sharing its database.
/// An unavailable or invalid response fails closed.
/// </summary>
public sealed class MfaPolicyStore : IMfaPolicyStore
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MfaPolicyStore> _logger;

    public MfaPolicyStore(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MfaPolicyStore> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetModeAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        if (!MfaOperationCategories.IsKnown(category))
        {
            return MfaPolicyModes.Required;
        }

        var serviceUrl = _configuration["Services:IdentityService"];
        var serviceKey = _configuration["INTERNAL_SERVICE_API_KEY"]
            ?? _configuration["Internal:ServiceApiKey"];
        if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(serviceKey)
            || System.Text.Encoding.UTF8.GetByteCount(serviceKey) < InternalServiceAuthentication.MinimumKeyLength)
        {
            _logger.LogWarning("Identity MFA policy client is not configured.");
            return MfaPolicyModes.Required;
        }

        var normalizedCategory = MfaOperationCategories.Normalize(category);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(baseUri, $"/api/internal/mfa-policy/{normalizedCategory}"));
        request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Identity MFA policy request failed for {Category} with status {StatusCode}.",
                    normalizedCategory,
                    (int)response.StatusCode);
                return MfaPolicyModes.Required;
            }

            var policy = await response.Content.ReadFromJsonAsync<MfaPolicyResponse>(cancellationToken);
            if (MfaPolicyModes.IsKnown(policy?.Mode))
            {
                return policy!.Mode!.Trim().ToLowerInvariant();
            }

            _logger.LogWarning("Identity returned an invalid MFA policy for {Category}.", normalizedCategory);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Identity MFA policy request timed out for {Category}.", normalizedCategory);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Identity MFA policy request failed for {Category}.", normalizedCategory);
        }
        catch (System.Text.Json.JsonException exception)
        {
            _logger.LogWarning(exception, "Identity returned malformed MFA policy for {Category}.", normalizedCategory);
        }

        return MfaPolicyModes.Required;
    }

    private sealed record MfaPolicyResponse(string? Mode);
}
