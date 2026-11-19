using System.Net;
using System.Net.Http.Json;

namespace CourseManagement.API;

public sealed record IdentityAccount(Guid UserId, string DisplayName);

/// <summary>
/// Finds an eduivme account by e-mail through Identity's internal endpoint (the one Koçluk uses), so that an owner can
/// invite a teacher by address, and an owner's address to e-mail support notices. Addresses are not stored here.
/// </summary>
public class IdentityAccountLookup(HttpClient http, IConfiguration configuration)
{
    private sealed record AccountResponse(Guid UserId, string? FirstName, string? LastName);
    private sealed record AccountWithEmail(Guid UserId, string? FirstName, string? LastName, string? Email);
    private sealed record AccountsResponse(IReadOnlyList<AccountWithEmail>? Accounts);

    /// <summary>An account's e-mail address, to write to it (an owner's support notices); not stored here.</summary>
    public virtual async Task<string?> FindEmailAsync(Guid userId, CancellationToken ct)
    {
        var (identity, key) = Settings();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{identity.TrimEnd('/')}/api/internal/coaching/accounts")
        {
            Content = JsonContent.Create(new { userIds = new[] { userId } })
        };
        request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", key);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var accounts = await response.Content.ReadFromJsonAsync<AccountsResponse>(ct);
        var email = accounts?.Accounts?.FirstOrDefault(item => item.UserId == userId)?.Email;
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    private (string Identity, string Key) Settings()
    {
        var identity = configuration["Services:IdentityService"];
        var key = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];
        if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Identity lookup is not configured.");
        return (identity, key);
    }

    public virtual async Task<IdentityAccount?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var (identity, key) = Settings();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{identity.TrimEnd('/')}/api/internal/coaching/account-by-email")
        {
            Content = JsonContent.Create(new { email })
        };
        request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", key);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>(ct);
        if (account is null || account.UserId == Guid.Empty) return null;
        var name = $"{account.FirstName} {account.LastName}".Trim();
        return new IdentityAccount(account.UserId, name.Length > 200 ? name[..200] : name);
    }
}
