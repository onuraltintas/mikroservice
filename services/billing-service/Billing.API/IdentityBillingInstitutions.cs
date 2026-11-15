using System.Net.Http.Json;
using Billing.Application;
using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Services;

namespace Billing.API;

/// <summary>
/// Asks Identity which institutions a user administers. Buying for an institution is refused while Identity cannot
/// be reached: nobody is taken for an institution's admin on a guess.
/// </summary>
public sealed class IdentityBillingInstitutions(HttpClient http, IConfiguration configuration, ILogger<IdentityBillingInstitutions> logger) : IBillingInstitutions
{
    private readonly Dictionary<Guid, IReadOnlyList<Institution>> _asked = [];

    public async Task<IReadOnlyList<Institution>> GetAdministeredAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Scoped: one request asks once.
        if (_asked.TryGetValue(userId, out var known)) return known;
        var baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
        var key = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"]
            ?? throw new InvalidOperationException("Internal service API key is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/api/internal/billing/administered-institutions?userId={userId:D}");
        request.Headers.Add(InternalServiceAuthentication.HeaderName, key);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<BillingInstitutionsResponse>(cancellationToken);
            return _asked[userId] = (body?.Institutions ?? []).Select(item => new Institution(item.Id, item.Name)).ToArray();
        }
        catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Billing: Identity could not be asked for administered institutions.");
            throw new BusinessRuleException("Billing.IdentityUnavailable", "Kurum bilgisi şu anda doğrulanamıyor. Lütfen biraz sonra yeniden deneyin.");
        }
    }
}
