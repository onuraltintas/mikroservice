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

    [HttpGet("templates/{id:guid}/admin")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<ActionResult<LearningPathTemplateAdminDetails>> GetTemplateDetailsForAdmin(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await paths.GetTemplateAdminDetailsAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

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

    [HttpPost("nodes")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<ActionResult<LearningPathNodeAdminSummary>> CreateNode(
        [FromBody] CreateLearningPathNodeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.CreateLearningPathNodeAsync(
            actorId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpPut("nodes/{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<ActionResult<LearningPathNodeAdminSummary>> UpdateNode(
        Guid id,
        [FromBody] UpdateLearningPathNodeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.UpdateLearningPathNodeAsync(
            actorId,
            id,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpDelete("nodes/{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<IActionResult> DeleteNode(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteLearningPathNodeAsync(
            actorId,
            id,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("node-contents")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<ActionResult<LearningPathNodeContentSummary>> CreateNodeContent(
        [FromBody] CreateLearningPathNodeContentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.CreateLearningPathNodeContentAsync(
            actorId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpPut("node-contents/{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<ActionResult<LearningPathNodeContentSummary>> UpdateNodeContent(
        Guid id,
        [FromBody] UpdateLearningPathNodeContentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.UpdateLearningPathNodeContentAsync(
            actorId,
            id,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpDelete("node-contents/{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<IActionResult> DeleteNodeContent(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteLearningPathNodeContentAsync(
            actorId,
            id,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("prerequisites")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<IActionResult> CreatePrerequisite(
        [FromBody] CreateLearningPathPrerequisiteRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.CreateLearningPathPrerequisiteAsync(
            actorId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("prerequisites/{nodeId:guid}/{prerequisiteNodeId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<IActionResult> DeletePrerequisite(
        Guid nodeId,
        Guid prerequisiteNodeId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteLearningPathPrerequisiteAsync(
            actorId,
            nodeId,
            prerequisiteNodeId,
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

    [HttpPost("start")]
    public async Task<ActionResult<object>> StartPath(
        [FromBody] StartLearningPathRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var id = await paths.StartPathAsync(userId, request.TemplateId, cancellationToken);
        return Ok(new { id });
    }

    [HttpPost("node/{nodeId:guid}/complete")]
    public async Task<IActionResult> CompleteNode(
        Guid nodeId,
        [FromBody] CompleteLearningPathNodeRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        await paths.CompleteNodeAsync(userId, nodeId, request?.Score, cancellationToken);
        return Ok(new { message = "Node completed" });
    }

    [HttpGet("node/{nodeId:guid}")]
    public async Task<ActionResult<LearningPathNodeSummary>> GetNodeDetails(
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await paths.GetNodeDetailsAsync(userId, nodeId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("personalized/generate")]
    public async Task<ActionResult<object>> GeneratePersonalizedPath(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var itemsCreated = await paths.GeneratePersonalizedPathAsync(userId, cancellationToken);
        return Ok(new { itemsCreated });
    }

    [HttpPost("personalized/{pathItemId:guid}/complete")]
    public async Task<IActionResult> CompletePersonalizedPathItem(
        Guid pathItemId,
        [FromBody] CompletePersonalizedPathItemRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        await paths.CompletePersonalizedPathItemAsync(
            userId,
            pathItemId,
            request?.AchievedScore,
            cancellationToken);
        return Ok(new { message = "Path item completed" });
    }

    [HttpGet("personalized/progress")]
    public async Task<ActionResult<PersonalizedLearningPathProgressSummary>> GetPersonalizedProgress(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await paths.GetPersonalizedProgressAsync(userId, cancellationToken));
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

    [HttpGet("personalized/next")]
    public async Task<ActionResult<PersonalizedLearningPathItemSummary>> GetNextPersonalized(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await paths.GetNextPersonalizedPathItemAsync(userId, cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }
}
