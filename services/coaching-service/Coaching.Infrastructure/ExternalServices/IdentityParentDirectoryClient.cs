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

    public async Task<IReadOnlyList<CoachingParentLink>> GetParentLinksAsync(IReadOnlyCollection<Guid> studentUserIds, Guid? parentUserId, CancellationToken ct)
    {
        if (studentUserIds.Count == 0 && parentUserId is null) return [];
        if (string.IsNullOrWhiteSpace(serviceApiKey)) throw new InvalidOperationException("Internal service API key is not configured.");
        var links = new List<CoachingParentLink>();
        // The identity endpoint takes at most 200 children per call; the parent is asked once.
        var batches = studentUserIds.Distinct().Chunk(200).Select(x => (IReadOnlyList<Guid>)x).ToList();
        if (batches.Count == 0) batches.Add([]);
        for (var index = 0; index < batches.Count; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/internal/coaching/parent-links")
            { Content = JsonContent.Create(new CoachingParentLinksRequest(batches[index], index == 0 ? parentUserId : null)) };
            request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            links.AddRange(await response.Content.ReadFromJsonAsync<CoachingParentLink[]>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Identity parent links response was empty."));
        }
        return links.DistinctBy(x => (x.ParentUserId, x.StudentUserId)).ToArray();
    }
}
