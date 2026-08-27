using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/exercise-types")]
public sealed class ExerciseTypesController(
    ILegacySpeedReadingCatalog catalog,
    ISpeedReadingCatalogAdminWriter adminWriter) : ControllerBase
{
    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ExerciseTypeCategorySummary>>> GetCategories(
        CancellationToken cancellationToken = default) =>
        Ok(await catalog.GetExerciseTypeCategoriesAsync(cancellationToken));

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<SpeedReadingPage<ExerciseTypeSummary>>> GetExerciseTypes(
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var canManageContent = User.Claims.Any(claim =>
            claim.Type == "permission" &&
            claim.Value == PlatformPermissions.SpeedReading.ContentManage);

        if (isActive == false && !canManageContent)
        {
            return Forbid();
        }

        var effectiveIsActive = isActive ?? (canManageContent ? null : true);
        return Ok(await catalog.GetExerciseTypesAsync(
            categoryId,
            effectiveIsActive,
            pageNumber,
            pageSize,
            cancellationToken));
    }

    [HttpPost]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<ActionResult<ExerciseTypeSummary>> CreateExerciseType(
        [FromBody] CreateExerciseTypeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await adminWriter.CreateExerciseTypeAsync(
            actorId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<ActionResult<ExerciseTypeSummary>> UpdateExerciseType(
        Guid id,
        [FromBody] UpdateExerciseTypeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await adminWriter.UpdateExerciseTypeAsync(
            actorId,
            id,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> DeleteExerciseType(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteExerciseTypeAsync(
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
