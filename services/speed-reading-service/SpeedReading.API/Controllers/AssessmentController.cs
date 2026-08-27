using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Assessment;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/assessment")]
[Authorize(Roles = "Student,Admin,SystemAdmin")]
public sealed class AssessmentController(ISpeedReadingAssessment assessment) : ControllerBase
{
    [HttpGet("exercises")]
    public async Task<IActionResult> GetExercises(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await assessment.GetExercisesAsync(userId, cancellationToken));
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await assessment.GetStatusAsync(userId, cancellationToken));
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate(
        [FromBody] AssessmentCalculationRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await assessment.CalculateAsync(userId, request, cancellationToken);
        return result is null
            ? BadRequest("No assessment results found.")
            : Ok(result);
    }

    [HttpPost("skip")]
    public async Task<IActionResult> Skip(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await assessment.SkipAsync(userId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
