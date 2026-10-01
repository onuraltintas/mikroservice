using System.Net.Http.Json;
using Coaching.Application.Newsletters;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.ExternalServices;

public sealed class CoachingNewsletterEmailClient : ICoachingNewsletterEmailDelivery
{
    private const string ConsumerType = "CoachingNewsletterConfirmation";
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _serviceApiKey;
    private readonly ILogger<CoachingNewsletterEmailClient> _logger;

    public CoachingNewsletterEmailClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CoachingNewsletterEmailClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["Services:NotificationService"] ?? "http://localhost:5004";
        _serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];
        _logger = logger;
    }

    public async Task QueueConfirmationAsync(
        Guid messageId,
        string recipient,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_serviceApiKey))
        {
            throw new InvalidOperationException("Internal service API key is not configured.");
        }

        var payload = new QueueEmailRequest(
            messageId,
            ConsumerType,
            recipient,
            "Koçluk bülteni aboneliğinizi onaylayın",
            body);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_baseUrl.TrimEnd('/')}/api/internal/notifications/email")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add(InternalServiceAuthentication.HeaderName, _serviceApiKey);

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Notification service email queue request failed for {MessageId}", messageId);
            throw new InvalidOperationException("Notification service email queue is unavailable.", exception);
        }
    }

    private sealed record QueueEmailRequest(
        Guid MessageId,
        string ConsumerType,
        string Recipient,
        string Subject,
        string Body);
}
