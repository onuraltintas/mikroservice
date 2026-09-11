using System.Net;
using System.Net.Http.Json;
using SpeedReading.Application.Content;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SpeedReading.Infrastructure.ExternalServices;

public sealed class IdentityProgressAccessClient : ISpeedReadingProgressAccess
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly string? serviceApiKey;
    private readonly ILogger<IdentityProgressAccessClient> logger;

    public IdentityProgressAccessClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<IdentityProgressAccessClient> logger)
    {
        this.httpClient = httpClient;
        baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
        serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];
        this.logger = logger;
    }

    public async Task<SpeedReadingProgressAccessScope?> GetScopeAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        if (viewerUserId == Guid.Empty)
            return null;
        if (string.IsNullOrWhiteSpace(serviceApiKey))
            throw new InvalidOperationException("Internal service API key is not configured.");

        var authorization = await SendAsync<AdminAuthorizationResponse>(
            "authorize-admin",
            new { ViewerUserId = viewerUserId },
            cancellationToken);
        if (authorization is null)
            return null;
        if (authorization.IsGlobal)
            return new SpeedReadingProgressAccessScope(viewerUserId, true, Array.Empty<Guid>());
        if (authorization.InstitutionId is not { } institutionId || institutionId == Guid.Empty)
            return null;

        var students = await SendAsync<ReportStudentsResponse>(
            "report-students",
            new { ViewerUserId = viewerUserId, InstitutionId = institutionId, GradeLevel = (int?)null },
            cancellationToken);
        return students is null
            ? null
            : new SpeedReadingProgressAccessScope(viewerUserId, false, students.StudentUserIds);
    }

    public async Task<IReadOnlyCollection<Guid>> SearchStudentUserIdsAsync(
        Guid viewerUserId,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (viewerUserId == Guid.Empty || string.IsNullOrWhiteSpace(searchTerm))
            return Array.Empty<Guid>();

        var result = await SendAsync<StudentSearchResponse>(
            "search-students",
            new { ViewerUserId = viewerUserId, SearchTerm = searchTerm.Trim() },
            cancellationToken);
        return result?.StudentUserIds ?? Array.Empty<Guid>();
    }

    private async Task<T?> SendAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/api/internal/coaching/{path}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                return default;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Identity progress access request failed for {Path}", path);
            throw new InvalidOperationException("Identity authorization service is unavailable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Identity progress access request timed out for {Path}", path);
            throw new InvalidOperationException("Identity authorization service timed out.", ex);
        }
    }

    private sealed record AdminAuthorizationResponse(bool IsGlobal, Guid? InstitutionId);
    private sealed record ReportStudentsResponse(IReadOnlyCollection<Guid> StudentUserIds);
    private sealed record StudentSearchResponse(IReadOnlyCollection<Guid> StudentUserIds, bool HasMore);
}
