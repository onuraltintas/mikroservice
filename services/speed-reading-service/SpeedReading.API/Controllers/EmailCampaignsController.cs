using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Notifications;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/email-campaigns")]
[Authorize(Roles = "Admin,SystemAdmin")]
public sealed class EmailCampaignsController(ISpeedReadingEmailCampaigns campaigns) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? status, CancellationToken cancellationToken = default) =>
        Ok(await campaigns.GetAllAsync(status, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await campaigns.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmailCampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            return Ok(await campaigns.CreateAsync(userId, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmailCampaignRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await campaigns.UpdateAsync(id, request, cancellationToken) ? NoContent() : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default) =>
        await campaigns.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, [FromBody] SendEmailCampaignRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await campaigns.SendAsync(id, request, cancellationToken);
            return result is null ? NotFound() : Ok(new { message = "Campaign send initiated", totalRecipients = result.TotalRecipients, campaign = result });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("{id:guid}/stats")]
    public async Task<IActionResult> GetStats(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await campaigns.GetStatsAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
