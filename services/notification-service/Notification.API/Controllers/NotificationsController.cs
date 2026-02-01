using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Notification.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationDbContext _dbContext;

    public NotificationsController(NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null) return Unauthorized();

        if (!Guid.TryParse(userIdClaim.Value, out var userId)) return BadRequest("Invalid User Id");

        var notifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

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
    public async Task<IActionResult> GetAllNotifications()
    {
         var notifications = await _dbContext.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
         return Ok(notifications);
    }
}
