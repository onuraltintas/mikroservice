using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Authorization;
using Identity.Application.Commands.InviteStudent;
using Identity.Application.Commands.InviteTeacher;
using Identity.Application.Commands.RemoveStudentFromInstitution;
using Identity.Application.Commands.RemoveTeacherFromInstitution;
using Identity.Application.Commands.UpdateInstitutionStudent;
using Identity.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/institutions/{institutionId:guid}/coaching")]
[Authorize(Roles = "SystemAdmin")]
[Authorize(Policy = "MfaRequired")]
[HasPermission(Permissions.Institutions.Manage)]
[MfaCategory(MfaOperationCategories.Institutions)]
public sealed class CoachingInstitutionMembersController(IMediator mediator) : ControllerBase
{
    [HttpPost("invite-teacher")]
    public async Task<IActionResult> InviteTeacher(Guid institutionId, InviteTeacherCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { InstitutionId = institutionId }, cancellationToken);
        return result.IsSuccess ? Ok(new { invitationId = result.Value }) : Failure(result.Error);
    }

    [HttpPost("invite-student")]
    public async Task<IActionResult> InviteStudent(Guid institutionId, InviteStudentCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { InstitutionId = institutionId }, cancellationToken);
        return result.IsSuccess ? Ok(new { invitationId = result.Value }) : Failure(result.Error);
    }

    [HttpPut("students/{studentId:guid}")]
    public async Task<IActionResult> UpdateStudent(Guid institutionId, Guid studentId,
        InstitutionController.UpdateInstitutionStudentRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateInstitutionStudentCommand(studentId, request.GradeLevel, request.TeacherUserId)
            { InstitutionId = institutionId }, cancellationToken);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    [HttpDelete("students/{studentId:guid}")]
    public async Task<IActionResult> RemoveStudent(Guid institutionId, Guid studentId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveStudentFromInstitutionCommand(studentId)
            { InstitutionId = institutionId }, cancellationToken);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    [HttpDelete("teachers/{teacherId:guid}")]
    public async Task<IActionResult> RemoveTeacher(Guid institutionId, Guid teacherId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveTeacherFromInstitutionCommand(teacherId)
            { InstitutionId = institutionId }, cancellationToken);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    private IActionResult Failure(Error error)
    {
        if (error.Code.Contains("Forbidden", StringComparison.OrdinalIgnoreCase)) return StatusCode(403, new { error });
        if (error.Code.EndsWith("NotFound", StringComparison.OrdinalIgnoreCase)) return NotFound(new { error });
        if (error.Code.EndsWith("DuplicateInvitation", StringComparison.OrdinalIgnoreCase)) return Conflict(new { error });
        return BadRequest(new { error });
    }
}
