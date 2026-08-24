using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/progress")]
[Authorize]
public sealed class ProgressController(
    ILegacySpeedReadingProgress progress,
    ILegacySpeedReadingPrograms programs) : ControllerBase
{
    [HttpGet("reading-history")]
    public async Task<ActionResult<IReadOnlyList<ReadingSessionSummary>>> GetReadingHistory(
        [FromQuery] Guid? readingTextId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await progress.GetReadingHistoryAsync(
            userId,
            readingTextId,
            dateFrom,
            dateTo,
            cancellationToken));
    }

    [HttpGet("reading-statistics")]
    public async Task<ActionResult<ReadingStatistics>> GetReadingStatistics(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await progress.GetReadingStatisticsAsync(userId, cancellationToken));
    }

    [HttpGet("exercise-results")]
    public async Task<ActionResult<SpeedReadingPage<ExerciseResultSummary>>> GetExerciseResults(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await progress.GetExerciseResultsAsync(
            userId,
            pageNumber,
            pageSize,
            cancellationToken));
    }

    [HttpGet("active-exercise-sessions")]
    public async Task<ActionResult<IReadOnlyList<ExerciseSessionSummary>>> GetActiveExerciseSessions(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await progress.GetActiveExerciseSessionsAsync(userId, cancellationToken));
    }

    [HttpGet("programs")]
    public async Task<ActionResult<IReadOnlyList<StudentProgramProgressSummary>>> GetPrograms(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await programs.GetStudentProgressAsync(userId, cancellationToken));
    }

    [HttpGet("daily-exercise-logs")]
    public async Task<ActionResult<IReadOnlyList<DailyExerciseLogSummary>>> GetDailyExerciseLogs(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await programs.GetDailyExerciseLogsAsync(
            userId,
            dateFrom,
            dateTo,
            limit,
            cancellationToken));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }
}
