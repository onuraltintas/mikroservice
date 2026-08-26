using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.StudentProgram;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/student-program")]
[Authorize]
public sealed class StudentProgramController(ISpeedReadingStudentProgram studentProgram) : ControllerBase
{
    [HttpGet("my-program")]
    public async Task<IActionResult> GetMyProgram(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var program = await studentProgram.GetMyProgramAsync(userId, cancellationToken);
        return program is null
            ? NotFound(new { success = false, message = "Henüz bir programa atanmadınız." })
            : Ok(program);
    }

    [HttpGet("my-programs")]
    public async Task<IActionResult> GetMyPrograms(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await studentProgram.GetMyProgramsAsync(userId, cancellationToken));
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartProgram(
        [FromBody] StartStudentProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await studentProgram.StartProgramAsync(userId, request.TemplateId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}

public sealed record StartStudentProgramRequest(Guid TemplateId);
