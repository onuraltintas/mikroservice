using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.API.Security;
using SpeedReading.Application.Content;
using SpeedReading.Application.StudentProgram;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/staff-training")]
[Authorize(Roles = "Admin,SystemAdmin,Teacher")]
public sealed class StaffTrainingController(ISpeedReadingStudentProgram programs, ILegacySpeedReadingPrograms catalog) : ControllerBase
{
    [HttpGet("programs")]
    public async Task<IActionResult> GetPrograms(CancellationToken cancellationToken)
    {
        if (!StaffTrainingAccess.IsAllowed(User)) return Forbid();
        var templates = await catalog.GetProgramTemplatesAsync(cancellationToken);
        return Ok(templates.Where(item => !item.IsAssessment).ToList());
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartStudentProgramRequest request, CancellationToken cancellationToken)
    {
        if (!StaffTrainingAccess.IsAllowed(User)) return Forbid();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId))
            return Unauthorized();
        try
        {
            return Ok(await programs.StartStaffTrainingAsync(userId, request.TemplateId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
    }
}
