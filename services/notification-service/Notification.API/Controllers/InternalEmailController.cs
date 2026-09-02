using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Notification.Application.Interfaces;

namespace Notification.API.Controllers;

[ApiController]
[Route("api/internal/notifications")]
[Notification.API.InternalServiceKey]
public sealed class InternalEmailController(IEmailDeliveryQueue emailDeliveryQueue) : ControllerBase
{
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
            cancellationToken);

        return Accepted(new { success = true, messageId = request.MessageId });
    }

    public sealed record QueueEmailRequest(
        Guid MessageId,
        string ConsumerType,
        string Recipient,
        string Subject,
        string Body);
}
