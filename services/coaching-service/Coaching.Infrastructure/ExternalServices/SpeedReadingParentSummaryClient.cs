using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;

namespace Coaching.Infrastructure.ExternalServices;

/// <summary>
/// V3 (parent panel): the child's speed-reading week for the weekly parent e-mail
/// (speed-reading service, GET api/internal/speed-reading/parent-weekly-summary). Speed reading is optional for a
/// deployment: without Services:SpeedReadingService the section is left out. 204 means the child has no speed-reading
/// activity or profile at all; any other failure is an error, so the caller can try again later.
/// </summary>
public sealed class SpeedReadingParentSummaryClient(HttpClient http, IConfiguration configuration) : ISpeedReadingParentSummaryClient
{
    private readonly string? baseUrl = configuration["Services:SpeedReadingService"]?.Trim().TrimEnd('/');
    private readonly string? key = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];

    public async Task<SpeedReadingParentWeek?> GetWeekAsync(Guid studentUserId, DateOnly weekStart, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Internal service key is required.");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl}/api/internal/speed-reading/parent-weekly-summary?studentUserId={studentUserId}&weekStart={weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        request.Headers.Add(InternalServiceAuthentication.HeaderName, key);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SpeedReadingParentWeek>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Speed-reading weekly summary response was empty.");
    }
}
