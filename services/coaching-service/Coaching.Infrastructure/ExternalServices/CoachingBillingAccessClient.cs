using System.Net;
using System.Net.Http.Json;
using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Coaching.Application.Subscriptions;

namespace Coaching.Infrastructure.ExternalServices;

/// <summary>What is remembered survives the short-lived client: one instance for the whole service.</summary>
public sealed class CoachingBillingCacheState
{
    private long generation;
    public long Generation => Interlocked.Read(ref generation);
    public void Advance() => Interlocked.Increment(ref generation);
}

/// <summary>
/// Asks the billing service whether a user has Koçluk. An answer is used for a minute; if billing cannot be
/// reached, the last answer of the past day stands, and a user billing was never asked about is let in: an outage of
/// billing stops new purchases, not studying.
/// </summary>
public sealed class CoachingBillingAccessClient(
    HttpClient httpClient,
    IConfiguration configuration,
    IMemoryCache cache,
    CoachingBillingCacheState state,
    TimeProvider clock,
    ILogger<CoachingBillingAccessClient> logger) : ICoachingBillingAccess
{
    public static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan RememberedFor = TimeSpan.FromHours(24);

    private readonly string baseUrl = configuration["Services:BillingService"] ?? "http://localhost:5006";
    private readonly string? serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];

    public bool Enabled { get; } = configuration.GetValue("Billing:Enabled", false);

    public async Task<CoachingBillingDecision> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!Enabled) throw new InvalidOperationException("The billing service is not enabled in this deployment.");
        var generation = state.Generation;
        if (cache.TryGetValue(FreshKey(userId, generation), out CoachingBillingDecision? fresh) && fresh is not null) return fresh;
        // A deployment that cannot ask at all is broken, not "billing is briefly away": it fails instead of opening the product.
        if (string.IsNullOrWhiteSpace(serviceApiKey)) throw new InvalidOperationException("Internal service API key is not configured.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{baseUrl.TrimEnd('/')}/internal/billing/v1/entitlements?userId={userId:D}&product={BillingProductCodes.Coaching}");
            request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            BillingProductAccessResponse? access = null;
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
                access = await response.Content.ReadFromJsonAsync<BillingProductAccessResponse>(cancellationToken: cancellationToken)
                    ?? throw new CoachingBillingAnswerException("The billing service returned an empty answer.");
            }
            var decision = CoachingBillingRules.Decide(access, clock.GetUtcNow().UtcDateTime);
            cache.Set(FreshKey(userId, generation), decision, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = FreshFor, Size = 1 });
            cache.Set(RememberedKey(userId), decision, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = RememberedFor, Size = 1 });
            return decision;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or CoachingBillingAnswerException or System.Text.Json.JsonException
            && !cancellationToken.IsCancellationRequested)
        {
            if (cache.TryGetValue(RememberedKey(userId), out CoachingBillingDecision? remembered) && remembered is not null)
            {
                logger.LogWarning(exception, "The billing service could not be asked; the last known answer is used.");
                // An entitlement that has run out in the meantime does not outlive itself.
                return remembered is { RequireSubscription: true, EndsAt: { } ends } && ends <= clock.GetUtcNow().UtcDateTime
                    ? remembered with { Allowed = false }
                    : remembered;
            }
            logger.LogError(exception, "The billing service could not be asked and nothing is remembered; the user is let in.");
            return new CoachingBillingDecision(true, false, null, false);
        }
    }

    public void Forget(Guid userId)
    {
        cache.Remove(FreshKey(userId, state.Generation));
        cache.Remove(RememberedKey(userId));
    }

    public void ForgetAll() => state.Advance();

    private static string FreshKey(Guid userId, long generation) => $"coaching-billing-access:{generation}:{userId:N}";
    // Remembered answers are kept across a settings change: they are only used when billing cannot be asked.
    private static string RememberedKey(Guid userId) => $"coaching-billing-access-last:{userId:N}";
}

public sealed class CoachingBillingAnswerException(string message) : Exception(message);
