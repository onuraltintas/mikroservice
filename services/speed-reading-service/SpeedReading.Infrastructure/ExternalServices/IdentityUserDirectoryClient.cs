using System.Net.Http.Json;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Assignments;

namespace SpeedReading.Infrastructure.ExternalServices;

public sealed class IdentityUserDirectoryClient : ISpeedReadingUserDirectory
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly string? serviceApiKey;
    private readonly ILogger<IdentityUserDirectoryClient> logger;

    public IdentityUserDirectoryClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<IdentityUserDirectoryClient> logger)
    {
        this.httpClient = httpClient;
        baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
        serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];
        this.logger = logger;
    }

    public async Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
            return new SpeedReadingUserDirectoryResponse([]);
        if (string.IsNullOrWhiteSpace(serviceApiKey))
            throw new InvalidOperationException("Internal service API key is not configured.");

        try
        {
            var users = new List<SpeedReadingUserDirectoryItem>();
            foreach (var batch in ids.Chunk(500))
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{baseUrl.TrimEnd('/')}/api/internal/reporting/speed-reading/users")
                {
                    Content = JsonContent.Create(new SpeedReadingUserDirectoryRequest(batch))
                };
                request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);

                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<SpeedReadingUserDirectoryResponse>(
                    cancellationToken: cancellationToken);
                if (result is not null)
                    users.AddRange(result.Users);
            }

            return new SpeedReadingUserDirectoryResponse(users);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Identity user directory request failed");
            throw new InvalidOperationException("Identity user directory is unavailable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Identity user directory request timed out");
            throw new InvalidOperationException("Identity user directory timed out.", ex);
        }
    }
}
