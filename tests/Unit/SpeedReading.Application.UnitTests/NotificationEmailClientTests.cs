using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SpeedReading.Infrastructure.ExternalServices;

namespace SpeedReading.Application.UnitTests;

public sealed class NotificationEmailClientTests
{
    [Fact]
    public async Task Queues_email_through_the_internal_notification_endpoint()
    {
        var handler = new CaptureHandler();
        var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:NotificationService"] = "http://notification-service:8080",
                ["INTERNAL_SERVICE_API_KEY"] = "a-secure-service-key-with-at-least-32-bytes"
            })
            .Build();
        var client = new NotificationEmailClient(httpClient, configuration, NullLogger<NotificationEmailClient>.Instance);

        var messageId = Guid.NewGuid();
        await client.QueueAsync(
            messageId,
            "SpeedReadingContactReply",
            "reader@example.com",
            "Subject",
            "<p>Reply</p>");

        handler.Request.Should().NotBeNull();
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().Should().Be("http://notification-service:8080/api/internal/notifications/email");
        handler.Request.Headers.GetValues("X-Internal-Service-Key")
            .Should().ContainSingle("a-secure-service-key-with-at-least-32-bytes");

        using var payload = JsonDocument.Parse(handler.Payload!);
        payload.RootElement.GetProperty("messageId").GetGuid().Should().Be(messageId);
        payload.RootElement.GetProperty("consumerType").GetString().Should().Be("SpeedReadingContactReply");
        payload.RootElement.GetProperty("recipient").GetString().Should().Be("reader@example.com");
        payload.RootElement.GetProperty("subject").GetString().Should().Be("Subject");
        payload.RootElement.GetProperty("body").GetString().Should().Be("<p>Reply</p>");
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Payload { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Payload = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }
    }
}
