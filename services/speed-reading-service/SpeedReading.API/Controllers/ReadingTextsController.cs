using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/reading-texts")]
[Authorize]
public sealed class ReadingTextsController(
    ILegacySpeedReadingCatalog catalog,
    ISpeedReadingContentAdminWriter adminWriter) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ReadingTextSummary>> GetReadingTexts(
        [FromQuery] Guid? exerciseId,
        [FromQuery] string? category,
        [FromQuery] int? difficultyLevel,
        [FromQuery] string? searchTerm,
        [FromQuery] Guid? targetAgeGroupId,
        [FromQuery] bool? isActive,
        [FromQuery] bool onlyWithQuestions = false,
        CancellationToken cancellationToken = default)
    {
        var canManageContent = User.Claims.Any(claim =>
            claim.Type == "permission" &&
            claim.Value == PlatformPermissions.SpeedReading.ContentManage);
        var effectiveIsActive = canManageContent ? isActive : true;

        return catalog.GetReadingTextsAsync(
            exerciseId,
            category,
            difficultyLevel,
            searchTerm,
            onlyWithQuestions,
            targetAgeGroupId,
            effectiveIsActive,
            cancellationToken);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(
        CancellationToken cancellationToken = default) =>
        Ok(await catalog.GetReadingTextCategoriesAsync(cancellationToken));

    [HttpGet("levels")]
    public async Task<ActionResult<IReadOnlyList<int>>> GetLevels(
        CancellationToken cancellationToken = default) =>
        Ok(await catalog.GetReadingTextDifficultyLevelsAsync(cancellationToken));

    [HttpGet("short")]
    public async Task<ActionResult<IReadOnlyList<ShortReadingTextSummary>>> GetShortReadingTexts(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await catalog.GetShortReadingTextsAsync(limit, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReadingTextDetails>> GetReadingText(
        Guid id,
        [FromQuery] bool includeQuestions = true,
        CancellationToken cancellationToken = default)
    {
        var canManageContent = User.Claims.Any(claim =>
            claim.Type == "permission" &&
            claim.Value == PlatformPermissions.SpeedReading.ContentManage);
        var result = await catalog.GetReadingTextAsync(
            id,
            includeQuestions,
            canManageContent,
            canManageContent,
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<ActionResult<ReadingTextSummary>> CreateReadingText(
        [FromBody] CreateReadingTextRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.CreateReadingTextAsync(
            actorId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<ActionResult<ReadingTextSummary>> UpdateReadingText(
        Guid id,
        [FromBody] UpdateReadingTextRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.UpdateReadingTextAsync(
            actorId,
            id,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> DeleteReadingText(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteReadingTextAsync(
            actorId,
            id,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out userId);
    }
}
