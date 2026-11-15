using System.Net;
using System.Net.Http.Json;
using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Subscription;

namespace SpeedReading.Infrastructure.ExternalServices;

/// <summary>Gives and takes the seats of an institution's plan at the billing service. A failure is the caller's to show: a seat is never assumed.</summary>
public sealed class BillingSeatsClient(HttpClient httpClient, IConfiguration configuration, ILogger<BillingSeatsClient> logger) : ISpeedReadingBillingSeats
{
    private readonly string baseUrl = (configuration["Services:BillingService"] ?? "http://localhost:5006").TrimEnd('/');
    private readonly string? serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];

    public bool Enabled { get; } = configuration.GetValue("Billing:Enabled", false);

    public async Task<BillingSeatPoolResponse?> GetPoolAsync(Guid institutionId, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"?product={BillingProductCodes.SpeedReading}&organizationId={institutionId:D}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BillingSeatPoolResponse>(cancellationToken: cancellationToken);
    }

    public Task<BillingSeatChangeResponse> AssignAsync(Guid institutionId, Guid studentId, Guid actorId, CancellationToken cancellationToken = default) =>
        ChangeAsync("/assign", institutionId, studentId, actorId, null, cancellationToken);

    public Task<BillingSeatChangeResponse> ReleaseAsync(Guid institutionId, Guid studentId, Guid actorId, string? reason, CancellationToken cancellationToken = default) =>
        ChangeAsync("/release", institutionId, studentId, actorId, reason, cancellationToken);

    public async Task ReleaseWhenLeavingAsync(Guid institutionId, Guid studentId, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (!Enabled) return;
        try
        {
            await ChangeAsync("/release", institutionId, studentId, actorId, "Öğrencinin kurum üyeliği sona erdi.", cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "A seat could not be freed at the billing service when a membership ended; the institution's admin can take it back by hand.");
        }
    }

    private async Task<BillingSeatChangeResponse> ChangeAsync(string path, Guid institutionId, Guid studentId, Guid actorId, string? reason,
        CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, path);
        request.Content = JsonContent.Create(new BillingSeatChangeRequest(BillingProductCodes.SpeedReading, null, institutionId, studentId, actorId, reason));
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
