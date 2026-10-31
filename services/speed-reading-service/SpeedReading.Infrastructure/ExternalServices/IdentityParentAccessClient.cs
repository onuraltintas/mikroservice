using System.Net;
using System.Net.Http.Json;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Parents;

namespace SpeedReading.Infrastructure.ExternalServices;

/// <summary>
/// V2 (parent panel): Identity's <c>POST api/internal/parents/authorize-read</c> decides on every parent read. 200 allows,
/// 401/403/404 refuse; transport failures, time-outs and server errors throw so they are never mistaken for an answer.
/// </summary>
public sealed class IdentityParentAccessClient : ISpeedReadingParentAccess
{
    private const string Product = "SpeedReading";
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly string? serviceApiKey;
    private readonly ILogger<IdentityParentAccessClient> logger;

    public IdentityParentAccessClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<IdentityParentAccessClient> logger)
    {
        this.httpClient = httpClient;
        baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
        serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];
        this.logger = logger;
    }

    public async Task<bool> CanReadAsync(Guid parentUserId, Guid studentUserId, CancellationToken cancellationToken = default)
    {
        if (parentUserId == Guid.Empty || studentUserId == Guid.Empty)
            return false;
        if (string.IsNullOrWhiteSpace(serviceApiKey))
            throw new InvalidOperationException("Internal service API key is not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/api/internal/parents/authorize-read")
        {
            Content = JsonContent.Create(new { parentUserId, studentUserId, product = Product })
        };
        request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
                return false;

            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Identity parent read authorization failed");
            throw new InvalidOperationException("Identity authorization service is unavailable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Identity parent read authorization timed out");
            throw new InvalidOperationException("Identity authorization service timed out.", ex);
        }
    }
}
