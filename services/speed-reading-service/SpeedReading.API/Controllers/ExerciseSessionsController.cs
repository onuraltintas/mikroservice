using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/exercise-sessions")]
[Authorize]
public sealed class ExerciseSessionsController(ISpeedReadingExerciseSessions sessions) : ControllerBase
{
    [HttpPost("start")]
    public async Task<ActionResult<StartExerciseSessionResponse>> Start(
        [FromBody] StartExerciseSessionRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        return Ok(await sessions.StartAsync(GetCurrentUserId(), request, cancellationToken));
    }

    [HttpPost("{sessionId:guid}/validate")]
    public async Task<ActionResult<ExerciseActionValidationResponse>> Validate(
        Guid sessionId,
        [FromBody] ExerciseActionRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        return Ok(await sessions.ValidateActionAsync(
            GetCurrentUserId(),
            sessionId,
            request,
            cancellationToken));
    }

    [HttpPost("{sessionId:guid}/complete")]
    public async Task<ActionResult<ExerciseSessionResult>> Complete(
        Guid sessionId,
        [FromBody] CompleteExerciseSessionRequest? request,
        CancellationToken cancellationToken = default)
    {
        return Ok(await sessions.CompleteAsync(
            GetCurrentUserId(),
            sessionId,
            request ?? new CompleteExerciseSessionRequest(),
            cancellationToken));
    }

    [HttpPost("{sessionId:guid}/pause")]
    public async Task<IActionResult> Pause(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await sessions.PauseAsync(GetCurrentUserId(), sessionId, cancellationToken);
        return Ok(new { message = "Session paused successfully" });
    }

    [HttpPost("{sessionId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await sessions.ResumeAsync(GetCurrentUserId(), sessionId, cancellationToken);
        return Ok(new { message = "Session resumed successfully" });
    }

    [HttpGet("{sessionId:guid}/progress")]
    public Task<ExerciseSessionProgress> GetProgress(
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        sessions.GetProgressAsync(GetCurrentUserId(), sessionId, cancellationToken);

    [HttpGet("{sessionId:guid}")]
    public Task<ExerciseSessionDetails> GetDetails(
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        sessions.GetDetailsAsync(GetCurrentUserId(), sessionId, cancellationToken);

    [HttpGet("active")]
    public Task<IReadOnlyList<ActiveExerciseSession>> GetActive(
        CancellationToken cancellationToken = default) =>
        sessions.GetActiveAsync(GetCurrentUserId(), cancellationToken);

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("A valid authenticated user is required.");
    }
}
