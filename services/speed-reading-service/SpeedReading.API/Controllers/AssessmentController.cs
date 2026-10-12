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
    [HttpPost("attempts")]
    public async Task<IActionResult> StartAttempt(
        [FromBody] StartAssessmentAttemptRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await assessment.StartAttemptAsync(
            userId,
            request ?? new StartAssessmentAttemptRequest(),
            cancellationToken));
    }

    [HttpGet("attempts")]
    public async Task<IActionResult> GetAttemptHistory(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await assessment.GetAttemptHistoryAsync(userId, cancellationToken));
    }

    [HttpGet("comparison")]
    public async Task<IActionResult> GetComparison(CancellationToken cancellationToken = default, [FromQuery] Guid? programProgressId = null)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(programProgressId.HasValue
            ? await assessment.GetProgramComparisonAsync(userId, programProgressId.Value, cancellationToken)
            : await assessment.GetComparisonAsync(userId, cancellationToken));
    }

    [HttpGet("phase-plan")]
    public async Task<IActionResult> GetPhasePlan(CancellationToken cancellationToken = default, [FromQuery] Guid? programProgressId = null)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(programProgressId.HasValue
            ? await assessment.GetProgramPhasePlanAsync(userId, programProgressId.Value, cancellationToken)
            : await assessment.GetPhasePlanAsync(userId, cancellationToken));
    }

    [HttpGet("follow-ups")]
    public async Task<IActionResult> GetFollowUps(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await assessment.GetFollowUpPlansAsync(userId, cancellationToken));
    }

    [HttpGet("exercises")]
    public async Task<IActionResult> GetExercises(CancellationToken cancellationToken = default, [FromQuery] Guid? attemptId = null)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(attemptId.HasValue ? await assessment.GetAttemptExercisesAsync(userId, attemptId.Value, cancellationToken)
            : await assessment.GetExercisesAsync(userId, cancellationToken));
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
