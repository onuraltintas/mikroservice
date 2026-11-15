using System.Net;
using System.Net.Http.Json;
using Coaching.Application.Subscriptions;
using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.ExternalServices;

/// <summary>Gives and takes the seats of a teacher's or an institution's plan at the billing service.</summary>
public sealed class CoachingBillingSeatsClient(HttpClient httpClient, IConfiguration configuration, ILogger<CoachingBillingSeatsClient> logger) : ICoachingBillingSeats
{
    private readonly string baseUrl = (configuration["Services:BillingService"] ?? "http://localhost:5006").TrimEnd('/');
    private readonly string? serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];

    public bool Enabled { get; } = configuration.GetValue("Billing:Enabled", false);

    public async Task<BillingSeatPoolResponse?> GetPoolAsync(CoachingSeatOwner owner, CancellationToken cancellationToken = default)
    {
        var who = owner.TeacherId is { } teacher ? $"ownerUserId={teacher:D}" : $"organizationId={owner.InstitutionId:D}";
        using var request = Request(HttpMethod.Get, $"?product={BillingProductCodes.Coaching}&{who}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BillingSeatPoolResponse>(cancellationToken: cancellationToken);
    }

    public Task<BillingSeatChangeResponse> AssignAsync(CoachingSeatOwner owner, Guid studentId, Guid actorId, CancellationToken cancellationToken = default) =>
        ChangeAsync("/assign", owner, studentId, actorId, null, cancellationToken);

    public Task<BillingSeatChangeResponse> ReleaseAsync(CoachingSeatOwner owner, Guid studentId, Guid actorId, string? reason, CancellationToken cancellationToken = default) =>
        ChangeAsync("/release", owner, studentId, actorId, reason, cancellationToken);

    public async Task ReleaseWhenLeavingAsync(CoachingSeatOwner owner, Guid studentId, string reason, CancellationToken cancellationToken = default)
    {
        if (!Enabled) return;
        try
        {
            // The student's leaving is the act; there is no admin behind it.
            await ChangeAsync("/release", owner, studentId, studentId, reason, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "A seat could not be freed at the billing service when a student left; its owner can take it back by hand.");
        }
    }

    private async Task<BillingSeatChangeResponse> ChangeAsync(string path, CoachingSeatOwner owner, Guid studentId, Guid actorId, string? reason,
        CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, path);
        request.Content = JsonContent.Create(new BillingSeatChangeRequest(BillingProductCodes.Coaching, owner.TeacherId, owner.InstitutionId, studentId, actorId, reason));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BillingSeatChangeResponse>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("The billing service returned an empty answer.");
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        if (!Enabled) throw new InvalidOperationException("The billing service is not enabled in this deployment.");
        if (string.IsNullOrWhiteSpace(serviceApiKey)) throw new InvalidOperationException("Internal service API key is not configured.");
        var request = new HttpRequestMessage(method, $"{baseUrl}/internal/billing/v1/seats{path}");
        request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);
        return request;
    }
}
