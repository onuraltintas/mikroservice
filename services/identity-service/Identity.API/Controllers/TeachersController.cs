using Identity.Application.Commands.AssignStudent;
using Identity.Application.Queries.GetAllUsers;
using Identity.Application.Queries.GetTeacherStudents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/teachers")]
[Authorize]
public class TeachersController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeachersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Öğretmenin aktif ve kendisine bağlı öğrencilerini sayfalı döndürür.
    /// </summary>
    [HttpGet("me/students")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(PagedList<TeacherStudentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyStudents(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetTeacherStudentsQuery(pageNumber, pageSize, searchTerm),
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.Error.Code switch
        {
            "Error.Unauthorized" => Unauthorized(),
            "Error.Forbidden" => Forbid(),
            _ => BadRequest(new { Error = result.Error })
        };
    }

    /// <summary>
    /// Öğretmen, bağımsız bir öğrenciyi kendine davet eder.
    /// </summary>
    [HttpPost("invite-student")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InviteStudent([FromBody] AssignStudentCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            return Ok(new { InvitationId = result.Value });
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Öğretmen, bir öğrenciyle olan bağlantısını koparır.
    /// </summary>
    [HttpDelete("students/{studentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveStudent(Guid studentId)
    {
        var result = await _mediator.Send(new Identity.Application.Commands.RemoveStudentFromTeacher.RemoveStudentFromTeacherCommand(studentId));
        
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return BadRequest(new { Error = result.Error });
    }
}
