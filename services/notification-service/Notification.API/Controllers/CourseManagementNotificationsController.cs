using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notification.Infrastructure.Persistence;

namespace Notification.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/course-management/notifications")]
[Authorize(Policy = "CourseManagementNotifications")]
public sealed class CourseManagementNotificationsController(NotificationDbContext dbContext) : ControllerBase
{
    private const int MaxPageNumber = 1_000;
    private const int MaxPageSize = 100;
    private const string CourseManagementTypePrefix = "CourseManagement.";

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber is < 1 or > MaxPageNumber || pageSize is < 1 or > MaxPageSize)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Error",
                detail: $"pageNumber must be between 1 and {MaxPageNumber}; pageSize must be between 1 and {MaxPageSize}.");
        }

        if (!TryGetUserId(out var userId)) return Unauthorized();
        var query = dbContext.Notifications.AsNoTracking().Where(notification =>
            notification.UserId == userId && notification.Type.StartsWith(CourseManagementTypePrefix));
        var totalCount = await query.CountAsync(cancellationToken);
        var unreadCount = await query.CountAsync(notification => !notification.IsRead, cancellationToken);
        var notifications = await query.OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        Response.Headers.Append("X-Total-Count", totalCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Response.Headers.Append("X-Unread-Count", unreadCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Response.Headers.Append("X-Page-Number", pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Response.Headers.Append("X-Page-Size", pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Ok(notifications);
    }

    [HttpPost("{id:guid}/mark-as-read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var notification = await dbContext.Notifications.FirstOrDefaultAsync(item => item.Id == id
            && item.UserId == userId && item.Type.StartsWith(CourseManagementTypePrefix), cancellationToken);
        if (notification is null) return NotFound();
        notification.MarkAsRead();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPost("mark-all-as-read")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await dbContext.Notifications.Where(item => item.UserId == userId && !item.IsRead
                && item.Type.StartsWith(CourseManagementTypePrefix))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsRead, true), cancellationToken);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var notification = await dbContext.Notifications.FirstOrDefaultAsync(item => item.Id == id
            && item.UserId == userId && item.Type.StartsWith(CourseManagementTypePrefix), cancellationToken);
        if (notification is null) return NotFound();
        dbContext.Notifications.Remove(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(id, out userId) && userId != Guid.Empty;
    }
}
