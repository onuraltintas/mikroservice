using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Notification.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private const int MaxPageNumber = 1_000;
    private const int MaxPageSize = 100;

    private readonly NotificationDbContext _dbContext;

    public NotificationsController(NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber is < 1 or > MaxPageNumber || pageSize is < 1 or > MaxPageSize)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Error",
                detail: $"pageNumber must be between 1 and {MaxPageNumber}; pageSize must be between 1 and {MaxPageSize}.",
                type: "https://eduplatform.dev/problems/validation-error",
                instance: HttpContext.Request.Path);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null) return Unauthorized();

        if (!Guid.TryParse(userIdClaim.Value, out var userId)) return BadRequest("Invalid User Id");

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);
        var unreadCount = await query.CountAsync(n => !n.IsRead, cancellationToken);
        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        Response.Headers.Append("X-Total-Count", totalCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Response.Headers.Append("X-Unread-Count", unreadCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Response.Headers.Append("X-Page-Number", pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Response.Headers.Append("X-Page-Size", pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return Ok(notifications);
    }

    [HttpPost("{id}/mark-as-read")]
    [Authorize]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null) return Unauthorized();
        if (!Guid.TryParse(userIdClaim.Value, out var userId)) return BadRequest("Invalid User Id");

        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notification == null) return NotFound();

        notification.MarkAsRead();
        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("mark-all-as-read")]
    [Authorize]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null) return Unauthorized();
        if (!Guid.TryParse(userIdClaim.Value, out var userId)) return BadRequest("Invalid User Id");

        var notifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.MarkAsRead();
        }

        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null) return Unauthorized();
        if (!Guid.TryParse(userIdClaim.Value, out var userId)) return BadRequest("Invalid User Id");

        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notification == null) return NotFound();

        _dbContext.Notifications.Remove(notification);
        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    // TEST ONLY ENDPOINT
    [HttpGet("test-all")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> GetAllNotifications()
    {
         var notifications = await _dbContext.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
         return Ok(notifications);
    }
}
