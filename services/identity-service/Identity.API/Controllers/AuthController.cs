using Identity.Application.Commands.RegisterInstitution;
using Identity.Application.Commands.RegisterTeacher;
using Identity.Application.Commands.RegisterStudent;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Yeni bir kurum kaydı oluşturur (Kurum + Yönetici).
    /// </summary>
    /// <param name="command">Kayıt bilgileri</param>
    /// <returns>Oluşturulan kurum ID'si</returns>
    [HttpPost("register-institution")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterInstitution([FromBody] RegisterInstitutionCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            return Ok(new { InstitutionId = result.Value });
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Yeni bir bağımsız öğretmen kaydı oluşturur.
    /// </summary>
    [HttpPost("register-teacher")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterTeacher([FromBody] RegisterTeacherCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            return Ok(new { TeacherId = result.Value });
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Yeni bir bağımsız öğrenci kaydı oluşturur.
    /// </summary>
    [HttpPost("register-student")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterStudent([FromBody] RegisterStudentCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            return Ok(new { StudentId = result.Value });
        }

        return BadRequest(new { Error = result.Error });
    }
}
