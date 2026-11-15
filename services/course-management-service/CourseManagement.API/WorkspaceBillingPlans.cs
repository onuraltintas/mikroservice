using System.Net;
using System.Net.Http.Json;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CourseManagement.API;

/// <summary>
/// What the billing service says about a workspace owner's Kurs Yönetimi plan.
/// <paramref name="RequireSubscription"/> false: the product asks for no subscription, nothing is limited.
/// <paramref name="EndsAt"/> null with a required subscription: the owner has no plan now; the workspace is read-only.
/// <paramref name="ActiveStudentLimit"/> null: the plan sets no limit.
/// </summary>
public sealed record WorkspaceBillingPlan(bool RequireSubscription, int? ActiveStudentLimit, DateTimeOffset? EndsAt)
{
    public static readonly WorkspaceBillingPlan Open = new(false, null, null);

    public bool HasPlan => RequireSubscription && EndsAt is not null;

    public bool CanWriteAt(DateTimeOffset now) => !RequireSubscription || (EndsAt is { } ends && now.ToUniversalTime() < ends);

    public bool CanHold(int activeStudents) =>
        !RequireSubscription || (EndsAt is not null && (ActiveStudentLimit is not { } limit || activeStudents <= limit));
}

/// <summary>
/// Where the deployment runs the billing service (Billing:Enabled), a workspace's plan and active-student limit are the
/// owner's "course-management.workspace" entitlement there; the workspace subscription table is not used. An answer is
/// used for a minute; if billing cannot be reached the last answer of the past day stands, and an owner billing was
/// never asked about is not limited.
/// </summary>
public sealed class WorkspaceBillingPlans(
    HttpClient httpClient,
    IConfiguration configuration,
    IMemoryCache cache,
    TimeProvider clock,
    ILogger<WorkspaceBillingPlans> logger)
{
    public const string WorkspaceEntitlement = "course-management.workspace";
    public static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan RememberedFor = TimeSpan.FromHours(24);

    private readonly string baseUrl = configuration["Services:BillingService"] ?? "http://localhost:5006";
    private readonly string? serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];

    public bool Enabled { get; } = configuration.GetValue("Billing:Enabled", false);

    public static WorkspaceBillingPlan Decide(BillingProductAccessResponse? access, DateTime utcNow)
    {
        if (access is not { RequireSubscription: true }) return WorkspaceBillingPlan.Open;
        var current = (access.Entitlements ?? [])
            .Where(item => item.Key == WorkspaceEntitlement && item.EndsAt > utcNow).ToArray();
        if (current.Length == 0) return new WorkspaceBillingPlan(true, null, null);
        // Several plans at once: the widest limit and the latest end count.
        int? limit = current.Any(item => item.LimitValue is null) ? null : current.Max(item => item.LimitValue);
        return new WorkspaceBillingPlan(true, limit,
            new DateTimeOffset(DateTime.SpecifyKind(current.Max(item => item.EndsAt), DateTimeKind.Utc)));
    }

    public async Task<WorkspaceBillingPlan> GetAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        if (!Enabled) throw new InvalidOperationException("The billing service is not enabled in this deployment.");
        if (cache.TryGetValue(FreshKey(ownerUserId), out WorkspaceBillingPlan? fresh) && fresh is not null) return fresh;
        // A deployment that cannot ask at all is broken, not "billing is briefly away".
        if (string.IsNullOrWhiteSpace(serviceApiKey)) throw new InvalidOperationException("Internal service API key is not configured.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{baseUrl.TrimEnd('/')}/internal/billing/v1/entitlements?userId={ownerUserId:D}&product={BillingProductCodes.CourseManagement}");
            request.Headers.Add("X-Internal-Service-Key", serviceApiKey);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            BillingProductAccessResponse? access = null;
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
                access = await response.Content.ReadFromJsonAsync<BillingProductAccessResponse>(cancellationToken: cancellationToken)
                    ?? throw new HttpRequestException("The billing service returned an empty answer.");
            }
            var plan = Decide(access, clock.GetUtcNow().UtcDateTime);
            cache.Set(FreshKey(ownerUserId), plan, FreshFor);
            cache.Set(RememberedKey(ownerUserId), plan, RememberedFor);
            return plan;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
            && !cancellationToken.IsCancellationRequested)
        {
            if (cache.TryGetValue(RememberedKey(ownerUserId), out WorkspaceBillingPlan? remembered) && remembered is not null)
            {
                logger.LogWarning(exception, "The billing service could not be asked; the last known plan is used.");
                return remembered;
            }
            logger.LogError(exception, "The billing service could not be asked and nothing is remembered; the workspace is not limited.");
            return WorkspaceBillingPlan.Open;
        }
    }

    public async Task<WorkspaceBillingPlan> GetForWorkspaceAsync(CourseManagementDbContext db, Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var owner = await db.Workspaces.AsNoTracking().Where(item => item.Id == workspaceId)
            .Select(item => (Guid?)item.OwnerUserId).SingleOrDefaultAsync(cancellationToken);
        return owner is { } ownerUserId ? await GetAsync(ownerUserId, cancellationToken) : WorkspaceBillingPlan.Open;
    }

    private static string FreshKey(Guid ownerUserId) => $"workspace-plan:{ownerUserId:N}";
    private static string RememberedKey(Guid ownerUserId) => $"workspace-plan-last:{ownerUserId:N}";
}
