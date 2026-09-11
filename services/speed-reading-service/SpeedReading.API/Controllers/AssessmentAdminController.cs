using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/admin/assessment-templates")]
[Authorize]
[HasPermission(PlatformPermissions.SpeedReading.SettingsManage)]
public sealed class AssessmentAdminController(
    ISpeedReadingAssessment assessment,
    ISpeedReadingLevelCatalog levelCatalog) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default) =>
        Ok(await assessment.GetTemplatesAsync(cancellationToken));

    [HttpGet("levels")]
    public async Task<IActionResult> GetLevels(CancellationToken cancellationToken = default) =>
        Ok(await levelCatalog.GetAllAsync(cancellationToken));

    [HttpPost("levels")]
    public async Task<IActionResult> CreateLevelCatalog(
        [FromBody] CreateSpeedReadingLevelCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            return Ok(await levelCatalog.CreateAsync(userId, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("levels/{id:guid}")]
    public async Task<IActionResult> UpdateLevelCatalog(
        Guid id,
        [FromBody] UpdateSpeedReadingLevelCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            return await levelCatalog.UpdateAsync(id, userId, request, cancellationToken) ? NoContent() : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpPost("levels/{id:guid}/publish")]
    public async Task<IActionResult> PublishLevelCatalog(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            return await levelCatalog.PublishAsync(id, userId, cancellationToken) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpGet("measurement-capabilities")]
    public IActionResult GetMeasurementCapabilities() => Ok(SpeedReadingMeasurementCapabilities.Definitions);

    [HttpGet("age-group/{ageGroupId:guid}")]
    public async Task<IActionResult> GetByAgeGroup(Guid ageGroupId, CancellationToken cancellationToken = default)
    {
        var result = await assessment.GetTemplateByAgeGroupAsync(ageGroupId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssessmentTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var id = await assessment.CreateTemplateAsync(userId, request, cancellationToken);
            return Ok(id);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAssessmentTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            return await assessment.UpdateTemplateAsync(userId, id, request, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await assessment.DeleteTemplateAsync(userId, id, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
