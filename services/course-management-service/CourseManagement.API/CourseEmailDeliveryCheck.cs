using System.Net.Http.Json;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

/// <summary>How the notification service's delivery of each message ended (19.11.2026).</summary>
public sealed record NotificationDeliveryState(Guid MessageId, string Status);

/// <summary>Asks the notification service (internal key) how the institution e-mails' deliveries ended.</summary>
public class NotificationDeliveryClient(HttpClient http, IConfiguration configuration)
{
    public const string ConsumerType = "CourseManagementEmailConsumer";

    public virtual async Task<IReadOnlyList<NotificationDeliveryState>> GetAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct)
    {
        var notification = configuration["Services:NotificationService"] ?? "http://notification-service:8080";
        var key = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Internal service API key is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{notification.TrimEnd('/')}/api/internal/notifications/email/status")
        {
            Content = JsonContent.Create(new { consumerType = ConsumerType, messageIds })
        };
        request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", key);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotificationDeliveryState[]>(ct) ?? [];
    }
}

/// <summary>
/// Reads back sent institution e-mails' delivery every few minutes (CourseEmailOptions.DeliveryCheckSeconds): one message per address (the id the sender gave),
/// "Sent" or "Failed" once the notification service is done with it. The institution sees "teslim edilemedi" in its
/// sending record. Checks e-mails up to three days old; after that what is not known stays unknown.
/// </summary>
public static class CourseEmailDeliveryCheck
{
    public static readonly TimeSpan Horizon = TimeSpan.FromDays(3);
    private const int Batch = 100;

    public static async Task RunAsync(CourseManagementDbContext db, NotificationDeliveryClient client, DateTimeOffset now, CancellationToken ct,
        TimeSpan? settle = null)
    {
        var latest = now - (settle ?? TimeSpan.FromMinutes(1));
        var since = now - Horizon;
        var emails = await db.Emails.Where(item => item.Status == CourseEmailStatus.Sent && item.Delivery == CourseEmailDelivery.Unknown
                && item.ProcessedAt >= since && item.ProcessedAt <= latest)
            .OrderBy(item => item.ProcessedAt).Take(Batch).ToListAsync(ct);
        if (emails.Count == 0) return;
        var ids = emails.ToDictionary(email => email.Id,
            email => email.RecipientList.Select(address => CourseEmailLinks.MessageId(email.Id, address)).ToArray());
        var states = (await client.GetAsync(ids.Values.SelectMany(item => item).Distinct().ToArray(), ct))
            .ToDictionary(item => item.MessageId, item => item.Status);
        foreach (var email in emails)
        {
            var messages = ids[email.Id];
            if (messages.Length == 0) continue;
            var answers = messages.Select(id => states.GetValueOrDefault(id)).ToArray();
            if (answers.Any(answer => answer is not ("Sent" or "Failed"))) continue;
            email.RecordDelivery(answers.Count(answer => answer == "Sent"), answers.Count(answer => answer == "Failed"), now);
        }
        await db.SaveChangesAsync(ct);
    }
}
