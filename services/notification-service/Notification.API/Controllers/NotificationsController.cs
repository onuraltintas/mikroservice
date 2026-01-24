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
