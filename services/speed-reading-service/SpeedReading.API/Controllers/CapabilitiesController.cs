using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Configuration;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading")]
public sealed class CapabilitiesController(SpeedReadingServiceOptions options) : ControllerBase
{
    [HttpGet("capabilities")]
    [AllowAnonymous]
    public ActionResult<SpeedReadingCapabilitiesResponse> GetCapabilities() =>
        Ok(new SpeedReadingCapabilitiesResponse(
            options.Mode.ToString(),
            options.CoachingIntegrationEnabled,
            options.NotificationIntegrationEnabled,
            options.SubscriptionIntegrationEnabled));
}

public sealed record SpeedReadingCapabilitiesResponse(
    string Mode,
    bool CoachingIntegrationEnabled,
    bool NotificationIntegrationEnabled,
    bool SubscriptionIntegrationEnabled);
