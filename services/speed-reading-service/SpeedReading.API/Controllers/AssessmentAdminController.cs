using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Assessment;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/admin/assessment-templates")]
[Authorize(Roles = "Admin,Editor,SystemAdmin")]
public sealed class AssessmentAdminController(ISpeedReadingAssessment assessment) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default) =>
        Ok(await assessment.GetTemplatesAsync(cancellationToken));

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
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default) =>
        await assessment.DeleteTemplateAsync(id, cancellationToken) ? NoContent() : NotFound();

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
