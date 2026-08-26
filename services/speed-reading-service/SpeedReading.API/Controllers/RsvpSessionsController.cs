using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Rsvp;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/rsvp-sessions")]
[Authorize]
public sealed class RsvpSessionsController(ISpeedReadingRsvp rsvp) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSessions(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await rsvp.GetSessionsAsync(userId, days, cancellationToken));
    }

    [HttpGet("user")]
    public async Task<IActionResult> GetUserSessions(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await rsvp.GetSessionsAsync(userId, 90, cancellationToken));
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await rsvp.GetStatisticsAsync(userId, days, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var session = await rsvp.GetSessionAsync(userId, id, cancellationToken);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateRsvpSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var session = await rsvp.CreateSessionAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSession(
        Guid id,
        [FromBody] UpdateRsvpSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            return await rsvp.UpdateSessionAsync(userId, id, request, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return await rsvp.DeleteSessionAsync(userId, id, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
