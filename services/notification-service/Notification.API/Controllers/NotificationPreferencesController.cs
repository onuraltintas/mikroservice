using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.Application.Interfaces;

namespace Notification.API.Controllers;

/// <summary>Roadmap O8: the signed-in user's notification choices per category (in the app / by e-mail).</summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/notifications/preferences")]
public sealed class NotificationPreferencesController(INotificationPreferenceService preferences) : ControllerBase
{
    public sealed record SaveRequest(IReadOnlyList<NotificationPreferenceUpdate>? Preferences);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        CurrentUser() is { } user ? Ok(new { data = await preferences.GetAsync(user, ct) }) : Unauthorized();

    [HttpPut]
    [RequestSizeLimit(8_192)]
    public async Task<IActionResult> Save([FromBody] SaveRequest request, CancellationToken ct)
    {
        if (CurrentUser() is not { } user) return Unauthorized();
        try { return Ok(new { data = await preferences.SaveAsync(user, request.Preferences ?? [], ct) }); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }

    public sealed record ConsentRequest(IReadOnlyList<CommercialConsentUpdate>? Consents);

    /// <summary>Roadmap O9: consent to commercial messages (İYS), per channel.</summary>
    [HttpGet("/api/notifications/commercial-consents")]
    public async Task<IActionResult> GetConsents([FromServices] ICommercialConsentService consents, CancellationToken ct) =>
        CurrentUser() is { } user ? Ok(new { data = await consents.GetAsync(user, ct) }) : Unauthorized();

    [HttpPut("/api/notifications/commercial-consents")]
    [RequestSizeLimit(4_096)]
    public async Task<IActionResult> SaveConsents([FromServices] ICommercialConsentService consents, [FromBody] ConsentRequest request, CancellationToken ct)
    {
        if (CurrentUser() is not { } user) return Unauthorized();
        try { return Ok(new { data = await consents.SaveAsync(user, request.Consents ?? [], ct) }); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }

    private Guid? CurrentUser() =>
        Guid.TryParse((User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub"))?.Value, out var id) && id != Guid.Empty ? id : null;
}
