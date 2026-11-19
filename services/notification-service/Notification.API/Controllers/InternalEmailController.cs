using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;

namespace Notification.API.Controllers;

[ApiController]
[Route("api/internal/notifications")]
[AllowAnonymous]
[Notification.API.InternalServiceKey]
public sealed class InternalEmailController(IEmailDeliveryQueue emailDeliveryQueue, INotificationDbContext db) : ControllerBase
{
    /// <summary>
    /// How queued e-mails ended, for the service that asked for them (Kurs Yönetimi shows "teslim edilemedi" to the
    /// institution). Only status: no recipient, subject or error text leaves. Unknown ids are left out.
    /// </summary>
    [HttpPost("email/status")]
    [RequestSizeLimit(65_536)]
    public async Task<IActionResult> EmailStatus([FromBody] EmailStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ConsumerType) || request.ConsumerType.Length > 100
            || request.MessageIds is null || request.MessageIds.Count is 0 or > 500)
            return BadRequest(new { success = false, message = "A consumer type and 1–500 message ids are required." });
        var ids = request.MessageIds.Distinct().ToArray();
        var rows = await db.EmailDeliveries.AsNoTracking()
            .Where(item => item.ConsumerType == request.ConsumerType && ids.Contains(item.MessageId))
            .Select(item => new { item.MessageId, item.Status }).ToListAsync(cancellationToken);
        return Ok(rows.Select(row => new EmailStatusItem(row.MessageId, row.Status switch
        {
            EmailDeliveryStatus.Sent => "Sent",
            EmailDeliveryStatus.Failed => "Failed",
            _ => "Pending"
        })));
    }

    public sealed record EmailStatusRequest(string? ConsumerType, IReadOnlyCollection<Guid>? MessageIds);
    public sealed record EmailStatusItem(Guid MessageId, string Status);

    [HttpPost("email")]
    [RequestSizeLimit(262_144)]
    [RequestTimeout(milliseconds: 10_000)]
    public async Task<IActionResult> QueueEmail(
        [FromBody] QueueEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MessageId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.ConsumerType)
            || string.IsNullOrWhiteSpace(request.Recipient)
            || string.IsNullOrWhiteSpace(request.Subject)
            || string.IsNullOrWhiteSpace(request.Body)
            || request.ConsumerType.Length > 100
            || request.Recipient.Length > 320
            || request.Subject.Length > 998
            || request.Body.Length > 262_144)
        {
            return BadRequest(new { success = false, message = "A valid email queue request is required." });
        }

        await emailDeliveryQueue.QueueAsync(
            request.MessageId,
            request.ConsumerType.Trim(),
            request.Recipient.Trim(),
            request.Subject.Trim(),
            request.Body,
            request.SubjectUserId,
            cancellationToken);

        return Accepted(new { success = true, messageId = request.MessageId });
    }

    public sealed record QueueEmailRequest(
        Guid MessageId,
        string ConsumerType,
        string Recipient,
        string Subject,
        string Body,
        Guid? SubjectUserId = null);
}
