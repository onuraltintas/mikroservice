using System.Net.Http.Json;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.ExternalServices;

public sealed class IdentityTeacherAccessClient : ISpeedReadingTeacherAccess
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly string? serviceApiKey;
    private readonly ILogger<IdentityTeacherAccessClient> logger;

    public IdentityTeacherAccessClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<IdentityTeacherAccessClient> logger)
    {
        this.httpClient = httpClient;
        baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
        serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];
        this.logger = logger;
    }

    public async Task<bool> CanReadStudentAsync(
        Guid viewerUserId,
        Guid studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (viewerUserId == Guid.Empty || studentUserId == Guid.Empty)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceApiKey))
        {
            throw new InvalidOperationException("Internal service API key is not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/api/internal/coaching/authorize-student-read")
        {
            Content = JsonContent.Create(new
            {
                ViewerUserId = viewerUserId,
                StudentIds = new[] { studentUserId }
            })
        };
        request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode is System.Net.HttpStatusCode.Forbidden
                or System.Net.HttpStatusCode.Unauthorized)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<StudentReadAuthorizationResponse>(
                cancellationToken: cancellationToken);
            return result?.AllowedStudentUserIds?.Contains(studentUserId) == true;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(
                ex,
                "Identity teacher student authorization failed for viewer {ViewerUserId} and student {StudentUserId}",
                viewerUserId,
                studentUserId);
            throw new InvalidOperationException("Identity authorization service is unavailable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "Identity teacher student authorization timed out for viewer {ViewerUserId} and student {StudentUserId}",
                viewerUserId,
                studentUserId);
            throw new InvalidOperationException("Identity authorization service timed out.", ex);
        }
    }

    private sealed record StudentReadAuthorizationResponse(Guid[]? AllowedStudentUserIds);
}
