using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.DailyProgress;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/daily-progress")]
[Authorize]
public sealed class DailyProgressController(ISpeedReadingDailyProgress dailyProgress) : ControllerBase
{
    [HttpGet("today-exercises")]
    public async Task<ActionResult<IReadOnlyList<DailyExerciseSummary>>> GetTodayExercises(
        CancellationToken cancellationToken = default)
    {
        return Ok(await dailyProgress.GetTodayExercisesAsync(GetCurrentUserId(), cancellationToken));
    }

    [HttpGet("day/{dayNumber:int}")]
    public async Task<ActionResult<IReadOnlyList<DailyExerciseSummary>>> GetExercisesByDay(
        int dayNumber,
        CancellationToken cancellationToken = default)
    {
        return Ok(await dailyProgress.GetExercisesByDayAsync(GetCurrentUserId(), dayNumber, cancellationToken));
    }

    [HttpPost("complete-exercise")]
    public async Task<ActionResult<CompleteDailyExerciseResponse>> CompleteExercise(
        [FromBody] CompleteDailyExerciseRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        return Ok(await dailyProgress.CompleteExerciseAsync(
            GetCurrentUserId(),
            request,
            cancellationToken));
    }

    [HttpGet("my-progress")]
    public async Task<ActionResult<DailyProgressSummary>> GetMyProgress(
        CancellationToken cancellationToken = default)
    {
        var result = await dailyProgress.GetProgressSummaryAsync(GetCurrentUserId(), cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("weekly-stats")]
    public Task<WeeklyProgressSummary> GetWeeklyStats(CancellationToken cancellationToken = default) =>
        dailyProgress.GetWeeklyStatsAsync(GetCurrentUserId(), cancellationToken);

    [HttpGet("calendar")]
    public Task<DailyProgressCalendar> GetCalendar(
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken cancellationToken = default) =>
        dailyProgress.GetCalendarAsync(GetCurrentUserId(), month, year, cancellationToken);

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("A valid authenticated user is required.");
    }
}
