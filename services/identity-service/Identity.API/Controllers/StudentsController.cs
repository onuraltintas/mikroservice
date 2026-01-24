using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Öğrenci, bir öğretmenle olan bağlantısını koparır (Dersi bırakır).
    /// </summary>
    [HttpDelete("teachers/{teacherId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LeaveTeacher(Guid teacherId)
    {
        var result = await _mediator.Send(new Identity.Application.Commands.LeaveTeacher.LeaveTeacherCommand(teacherId));
        
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return BadRequest(new { Error = result.Error });
    }
}
