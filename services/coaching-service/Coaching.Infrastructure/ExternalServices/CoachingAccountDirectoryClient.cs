using System.Net;
using System.Net.Http.Json;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;

namespace Coaching.Infrastructure.ExternalServices;

public sealed class CoachingAccountDirectoryClient(HttpClient http, IConfiguration configuration,
    ICoachingAgreementRepresentativeAuthorizationClient representatives) : ICoachingAccountDirectory
{
    private readonly string baseUrl = (configuration["Services:IdentityService"] ?? "http://localhost:5001").TrimEnd('/');
    private readonly string? key = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];

    public async Task<CoachingLegacySnapshot> GetLegacySnapshotAsync(CancellationToken ct) =>
        await SendAsync<CoachingLegacySnapshot>(HttpMethod.Get, "legacy-profile-snapshot", null, ct)
        ?? throw new InvalidOperationException("Legacy profile snapshot is unavailable.");

    public async Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var result = new List<CoachingAccount>();
        foreach (var batch in userIds.Distinct().Chunk(500))
        {
            var response = await SendAsync<CoachingAccountResponse>(HttpMethod.Post, "accounts", new CoachingAccountRequest(batch), ct);
            result.AddRange(response?.Accounts ?? throw new InvalidOperationException("Identity account response was empty."));
        }
        return result;
    }

    public Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken ct) =>
        SendAsync<CoachingAccount>(HttpMethod.Post, "account-by-email", new CoachingAccountEmailRequest(email), ct);

    public async Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid userId, CancellationToken ct)
    {
        var response = await SendAsync<AdminScope>(HttpMethod.Post, "authorize-admin", new { viewerUserId = userId }, ct);
        return response is null ? null : new(response.IsGlobal, response.InstitutionId);
    }

    public async Task<bool> IsActiveInstitutionAsync(Guid institutionId, CancellationToken ct) =>
        (await SendAsync<InstitutionState>(HttpMethod.Get, $"institutions/{institutionId}/active", null, ct))?.IsActive == true;

    public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewerUserId, Guid studentUserId, CancellationToken ct) =>
        representatives.AuthorizeAsync(viewerUserId, studentUserId, ct);

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Internal service key is required.");
        using var request = new HttpRequestMessage(method, $"{baseUrl}/api/internal/coaching/{path}");
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Add(InternalServiceAuthentication.HeaderName, key);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound) return default;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Identity response was empty.");
    }

    private sealed record AdminScope(bool IsGlobal, Guid? InstitutionId);
    private sealed record InstitutionState(bool IsActive);
}
