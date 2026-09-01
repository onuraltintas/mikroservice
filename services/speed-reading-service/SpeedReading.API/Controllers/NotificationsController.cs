using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Notifications;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/notifications")]
[Authorize]
public sealed class NotificationsController(ISpeedReadingNotifications notifications) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? isRead,
        [FromQuery] int? type,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await notifications.GetNotificationsAsync(
                userId, isRead, type, fromDate, toDate, pageNumber, pageSize, cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await notifications.GetUnreadCountAsync(userId, cancellationToken));
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await notifications.GetPreferencesAsync(userId, cancellationToken));
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] IReadOnlyList<NotificationPreferenceSummary> preferences,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            await notifications.UpdatePreferencesAsync(userId, preferences, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe(
        [FromBody] SubscribePushRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var id = await notifications.SubscribePushAsync(
                userId,
                request,
                Request.Headers["User-Agent"].ToString(),
                cancellationToken);
            return Ok(new { id });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("{notificationId:guid}/mark-read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await notifications.MarkAsReadAsync(userId, notificationId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpPut("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var count = await notifications.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(new { markedCount = count });
    }

    [HttpDelete("{notificationId:guid}")]
    public async Task<IActionResult> Delete(Guid notificationId, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await notifications.DeleteAsync(userId, notificationId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SystemAdmin")]
    [HasPermission(PlatformPermissions.SpeedReading.CommunicationsManage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = await notifications.CreateAsync(request, cancellationToken);
            return Ok(new { id = notification.Id });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("all")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    [HasPermission(PlatformPermissions.SpeedReading.CommunicationsManage)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? userId,
        [FromQuery] int? type,
        [FromQuery] bool? isRead,
        [FromQuery] string? userRole,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? searchTerm,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await notifications.GetAllAsync(
            userId, type, isRead, userRole, fromDate, toDate, searchTerm,
            pageNumber, pageSize, cancellationToken));
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    [HasPermission(PlatformPermissions.SpeedReading.CommunicationsManage)]
    public async Task<IActionResult> Bulk(
        [FromBody] BulkNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await notifications.SendBulkAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
