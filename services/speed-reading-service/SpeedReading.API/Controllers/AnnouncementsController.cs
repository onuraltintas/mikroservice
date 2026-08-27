using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Notifications;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/announcements")]
[Authorize]
public sealed class AnnouncementsController(ISpeedReadingAnnouncements announcements) : ControllerBase
{
    [HttpGet("my-announcements")]
    public async Task<IActionResult> GetMine(
        [FromQuery] bool includeDismissed = false,
        [FromQuery] bool onlyPinned = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await announcements.GetMyAsync(
            userId,
            User.Claims
                .Where(claim => claim.Type is ClaimTypes.Role or "role")
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            GetInstitutionId(),
            includeDismissed,
            onlyPinned,
            cancellationToken));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive,
        [FromQuery] bool? isPinned,
        [FromQuery] int? targetAudience,
        [FromQuery] Guid? targetInstitutionId,
        [FromQuery] bool includeExpired = false,
        [FromQuery] int? take = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await announcements.GetAllAsync(
            isActive,
            isPinned,
            targetAudience,
            targetInstitutionId,
            includeExpired,
            take,
            userId,
            cancellationToken));
    }

    [HttpGet("{id:guid}/stats")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> GetStats(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await announcements.GetStatsAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            return Ok(new { id = await announcements.CreateAsync(userId, request, cancellationToken) });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAnnouncementRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await announcements.UpdateAsync(id, request, cancellationToken) ? NoContent() : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default) =>
        await announcements.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/view")]
    public async Task<IActionResult> RecordView(Guid id, CancellationToken cancellationToken = default) =>
        await RecordInteraction(id, (userId, cancellationToken) => announcements.RecordViewAsync(userId, id, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/click")]
    public async Task<IActionResult> RecordClick(Guid id, CancellationToken cancellationToken = default) =>
        await RecordInteraction(id, (userId, cancellationToken) => announcements.RecordClickAsync(userId, id, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken cancellationToken = default) =>
        await RecordInteraction(id, (userId, cancellationToken) => announcements.DismissAsync(userId, id, cancellationToken), cancellationToken);

    private async Task<IActionResult> RecordInteraction(
        Guid id,
        Func<Guid, CancellationToken, Task<bool>> action,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await action(userId, cancellationToken) ? NoContent() : NotFound();
    }

    private Guid? GetInstitutionId()
    {
        var value = User.FindFirstValue("institutionId") ?? User.FindFirstValue("InstitutionId");
        return Guid.TryParse(value, out var institutionId) ? institutionId : null;
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
