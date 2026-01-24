using Identity.Application.Commands.CreateTeacher;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/institution")]
[Authorize] // Requires valid JWT
public class InstitutionController : ControllerBase
{
    private readonly IMediator _mediator;

    public InstitutionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Kuruma yeni bir öğretmen ekler. (Sadece kurum yöneticileri)
    /// Geçici şifre ile birlikte TeacherId döner.
    /// </summary>
    [HttpPost("teachers")]
    [ProducesResponseType(typeof(CreateTeacherResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTeacher([FromBody] CreateTeacherCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            // Return both TeacherId and TemporaryPassword
            return Ok(new { 
                TeacherId = result.Value.TeacherId, 
                TemporaryPassword = result.Value.TemporaryPassword 
            });
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Kuruma yeni bir öğrenci ekler.
    /// Geçici şifre ile birlikte StudentId döner.
    /// </summary>
    [HttpPost("students")]
    [ProducesResponseType(typeof(Identity.Application.Commands.CreateStudent.CreateStudentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStudent([FromBody] Identity.Application.Commands.CreateStudent.CreateStudentCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            // Return both StudentId and TemporaryPassword
            return Ok(new { 
                StudentId = result.Value.StudentId, 
                TemporaryPassword = result.Value.TemporaryPassword 
            });
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Kuruma bağımsız bir öğretmeni davet eder.
    /// </summary>
    [HttpPost("invite-teacher")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InviteTeacher([FromBody] Identity.Application.Commands.InviteTeacher.InviteTeacherCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            return Ok(new { InvitationId = result.Value });
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Kuruma bağımsız bir öğrenciyi davet eder.
    /// </summary>
    [HttpPost("invite-student")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InviteStudent([FromBody] Identity.Application.Commands.InviteStudent.InviteStudentCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            return Ok(new { InvitationId = result.Value });
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Kurumdan bir öğretmeni çıkarır (Bağlantısını koparır).
    /// </summary>
    [HttpDelete("teachers/{teacherId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveTeacher(Guid teacherId)
    {
        var result = await _mediator.Send(new Identity.Application.Commands.RemoveTeacherFromInstitution.RemoveTeacherFromInstitutionCommand(teacherId));
        
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Kurumdan bir öğrenciyi çıkarır (Bağlantısını koparır).
    /// </summary>
    [HttpDelete("students/{studentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveStudent(Guid studentId)
    {
        var result = await _mediator.Send(new Identity.Application.Commands.RemoveStudentFromInstitution.RemoveStudentFromInstitutionCommand(studentId));
        
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return BadRequest(new { Error = result.Error });
    }
}
