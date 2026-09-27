using System.Net.Http.Json;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;

namespace SpeedReading.Infrastructure.ExternalServices;

public sealed class IdentitySpeedReadingMemberEligibilityClient :
    ISpeedReadingInstitutionMemberEligibility,
    ISpeedReadingInstitutionAdministrationAuthorization
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly string? serviceApiKey;
    private readonly ILogger<IdentitySpeedReadingMemberEligibilityClient> logger;

    public IdentitySpeedReadingMemberEligibilityClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<IdentitySpeedReadingMemberEligibilityClient> logger)
    {
        this.httpClient = httpClient;
        baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
        serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];
        this.logger = logger;
    }

    public async Task<bool> IsEligibleAsync(
        Guid userId,
        SpeedReadingInstitutionMemberRole role,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !Enum.IsDefined(role))
            return false;
        if (string.IsNullOrWhiteSpace(serviceApiKey))
            throw new InvalidOperationException("Internal service API key is not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/api/internal/reporting/speed-reading/member-eligibility")
        {
            Content = JsonContent.Create(new SpeedReadingMemberEligibilityRequest(userId, role.ToString()))
        };
        request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SpeedReadingMemberEligibilityResponse>(
                cancellationToken: cancellationToken);
            return result?.IsEligible == true;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Speed Reading member eligibility check failed for {UserId}", userId);
            throw new InvalidOperationException("Identity membership validation is unavailable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Speed Reading member eligibility check timed out for {UserId}", userId);
            throw new InvalidOperationException("Identity membership validation timed out.", ex);
        }
    }

    public async Task<bool> CanManageAsync(
        Guid userId,
        Guid institutionId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || institutionId == Guid.Empty)
            return false;
        if (string.IsNullOrWhiteSpace(serviceApiKey))
            throw new InvalidOperationException("Internal service API key is not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/api/internal/reporting/speed-reading/institution-manager-authorization")
        {
            Content = JsonContent.Create(new SpeedReadingInstitutionManagerAuthorizationRequest(userId, institutionId))
        };
        request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SpeedReadingInstitutionManagerAuthorizationResponse>(
                cancellationToken: cancellationToken);
            return result?.CanManage == true;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Speed Reading institution authorization failed for {UserId}", userId);
            throw new InvalidOperationException("Identity institution authorization is unavailable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Speed Reading institution authorization timed out for {UserId}", userId);
            throw new InvalidOperationException("Identity institution authorization timed out.", ex);
        }
    }
}
