using System.Net.Http.Json;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Notification.Application.Interfaces;

namespace Notification.Infrastructure.ExternalServices;

/// <summary>
/// Roadmap O8: asks Identity for recipients' names and usable e-mail addresses. A failure throws so the
/// consuming message is retried; notification and e-mail ids are idempotent.
/// </summary>
public sealed class IdentityRecipientDirectory(HttpClient http, IConfiguration configuration) : IRecipientDirectory
{
    private readonly string baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
    private readonly string? key = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];

    private sealed record Row(Guid UserId, string? Email, string? FirstName, string? LastName, string? Phone = null);

    public async Task<IReadOnlyDictionary<Guid, RecipientContact>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        var result = new Dictionary<Guid, RecipientContact>();
        if (ids.Length == 0) return result;
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Internal service API key is not configured.");
        foreach (var chunk in ids.Chunk(200))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/internal/notification/recipients") { Content = JsonContent.Create(new { userIds = chunk }) };
            request.Headers.Add(InternalServiceAuthentication.HeaderName, key);
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            foreach (var row in await response.Content.ReadFromJsonAsync<Row[]>(cancellationToken: ct) ?? [])
                result[row.UserId] = new RecipientContact(row.UserId, row.Email, row.FirstName ?? "", row.LastName ?? "") { Phone = row.Phone };
        }
        return result;
    }
}
