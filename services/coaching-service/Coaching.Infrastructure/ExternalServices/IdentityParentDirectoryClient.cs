using System.Net.Http.Json;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;

namespace Coaching.Infrastructure.ExternalServices;

public sealed class IdentityParentDirectoryClient(HttpClient http, IConfiguration configuration) : ICoachingParentDirectory
{
    private readonly string baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
    private readonly string? serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];

    public async Task<CoachingParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serviceApiKey)) throw new InvalidOperationException("Internal service API key is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl}/api/internal/coaching/parent-summary-recipients?pageNumber={pageNumber}&pageSize={pageSize}");
        request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CoachingParentSummaryRecipientPage>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Identity parent summary recipients response was empty.");
    }
}
