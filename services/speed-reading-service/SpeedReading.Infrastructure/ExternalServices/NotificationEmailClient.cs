using System.Net.Http.Json;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.ExternalServices;

public sealed class NotificationEmailClient : ISpeedReadingEmailDelivery
{
    private const string ConsumerType = "SpeedReadingContactReply";
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly string? serviceApiKey;
    private readonly ILogger<NotificationEmailClient> logger;

    public NotificationEmailClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<NotificationEmailClient> logger)
    {
        this.httpClient = httpClient;
        baseUrl = configuration["Services:NotificationService"] ?? "http://localhost:5004";
        serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];
        this.logger = logger;
    }

    public async Task QueueAsync(
        Guid messageId,
        string consumerType,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceApiKey))
        {
            throw new InvalidOperationException("Internal service API key is not configured.");
        }

        var payload = new QueueEmailRequest(
            messageId,
            string.IsNullOrWhiteSpace(consumerType) ? ConsumerType : consumerType,
            recipient,
            subject,
            body);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/api/internal/notifications/email")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add(InternalServiceAuthentication.HeaderName, serviceApiKey);

        try
        {
            using var response = await httpClient.SendAsync(
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
            logger.LogError(exception, "Notification service email queue request failed for {MessageId}", messageId);
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
