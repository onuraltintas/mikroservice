using System.Security.Claims;
using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/learning-paths")]
[Authorize]
public sealed class LearningPathsController(
    ILegacySpeedReadingLearningPaths paths,
    ISpeedReadingContentAdminWriter adminWriter) : ControllerBase
{
    [HttpGet("templates")]
    public Task<IReadOnlyList<LearningPathTemplateSummary>> GetTemplates(
        CancellationToken cancellationToken = default) =>
        paths.GetTemplatesAsync(cancellationToken);

    [HttpGet("templates/admin")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public Task<IReadOnlyList<LearningPathTemplateAdminSummary>> GetTemplatesForAdmin(
        CancellationToken cancellationToken = default) =>
        paths.GetTemplateAdminSummariesAsync(cancellationToken);

    [HttpPost("templates")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<ActionResult<LearningPathTemplateAdminSummary>> CreateTemplate(
        [FromBody] CreateLearningPathTemplateRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.CreateLearningPathTemplateAsync(
            actorId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpPut("templates/{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<ActionResult<LearningPathTemplateAdminSummary>> UpdateTemplate(
        Guid id,
        [FromBody] UpdateLearningPathTemplateRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.UpdateLearningPathTemplateAsync(
            actorId,
            id,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpDelete("templates/{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<IActionResult> DeleteTemplate(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteLearningPathTemplateAsync(
            actorId,
            id,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
    }

    [HttpGet("progress")]
    public async Task<ActionResult<LearningPathProgressSummary>> GetProgress(
        [FromQuery] Guid? templateId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await paths.GetProgressAsync(userId, templateId, cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("personalized")]
    public async Task<ActionResult<SpeedReadingPage<PersonalizedLearningPathItemSummary>>> GetPersonalized(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await paths.GetPersonalizedPathAsync(
            userId,
            pageNumber,
            pageSize,
            cancellationToken));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }
}
