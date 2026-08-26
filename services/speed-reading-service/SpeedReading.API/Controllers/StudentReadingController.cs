using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.StudentReading;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/student-reading")]
[Authorize]
public sealed class StudentReadingController(ISpeedReadingStudentReading studentReading) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await studentReading.GetCategoriesAsync(userId, cancellationToken));
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] string? category,
        [FromQuery] int? minLevel,
        [FromQuery] int? maxLevel,
        [FromQuery] int? specificLevel,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await studentReading.GetAvailableTextsAsync(
            userId, category, minLevel, maxLevel, specificLevel, cancellationToken));
    }

    [HttpGet("{textId:guid}/start")]
    public async Task<IActionResult> Start(Guid textId, CancellationToken cancellationToken = default)
    {
        var result = await studentReading.StartAsync(textId, cancellationToken);
        return result is null ? NotFound("Reading text not found.") : Ok(result);
    }

    [HttpPost("{textId:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid textId,
        [FromBody] CompleteStudentReadingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await studentReading.CompleteAsync(userId, textId, request, cancellationToken);
        return result is null ? NotFound("Reading text not found.") : Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] Guid? readingTextId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? category,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await studentReading.GetHistoryAsync(
            userId,
            readingTextId,
            dateFrom ?? startDate,
            dateTo ?? endDate,
            category,
            cancellationToken));
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionDetails(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await studentReading.GetSessionDetailsAsync(userId, sessionId, cancellationToken);
        return result is null ? NotFound("Session not found.") : Ok(result);
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await studentReading.GetStatisticsAsync(userId, cancellationToken));
    }

    [HttpGet("statistics/wpm-progression")]
    public async Task<IActionResult> GetWpmProgression(CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await studentReading.GetWpmProgressionAsync(userId, cancellationToken));
    }

    [HttpGet("statistics/comprehension-progression")]
    public async Task<IActionResult> GetComprehensionProgression(CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await studentReading.GetComprehensionProgressionAsync(userId, cancellationToken));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
