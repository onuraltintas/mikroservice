using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Notifications;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/email-templates")]
[Authorize(Roles = "Admin,SystemAdmin")]
public sealed class EmailTemplatesController(ISpeedReadingEmailTemplates templates) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default) =>
        Ok(await templates.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await templates.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmailTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await templates.CreateAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmailTemplateRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await templates.UpdateAsync(id, request, cancellationToken) ? NoContent() : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default) =>
        await templates.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/preview")]
    public async Task<IActionResult> Preview(
        Guid id,
        [FromBody] IReadOnlyDictionary<string, string>? variables,
        CancellationToken cancellationToken = default)
    {
        var result = await templates.PreviewAsync(id, variables, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
