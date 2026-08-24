using System.Net.Http.Json;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.ExternalServices;

public sealed class IdentityInstitutionDirectoryClient : ISpeedReadingInstitutionDirectory
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly string? serviceApiKey;
    private readonly ILogger<IdentityInstitutionDirectoryClient> logger;

    public IdentityInstitutionDirectoryClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<IdentityInstitutionDirectoryClient> logger)
    {
        this.httpClient = httpClient;
        baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
        serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];
        this.logger = logger;
    }

    public async Task<SpeedReadingInstitutionScopeResponse> GetInstitutionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceApiKey))
        {
            throw new InvalidOperationException("Internal service API key is not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl.TrimEnd('/')}/api/internal/reporting/speed-reading/institutions");
        request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SpeedReadingInstitutionScopeResponse>(
                       cancellationToken: cancellationToken)
                   ?? new SpeedReadingInstitutionScopeResponse([]);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Identity institution directory request failed");
            throw new InvalidOperationException("Identity institution directory is unavailable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Identity institution directory request timed out");
            throw new InvalidOperationException("Identity institution directory timed out.", ex);
        }
    }
}
