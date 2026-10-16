using Asp.Versioning;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[Coaching.API.Security.CoachingProfileExceptionFilter]
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Teacher")]
[Route("api/coaching/teachers")]
public sealed class CoachingRosterController(OwnedCoachingDirectory directory, ICurrentUserService actor,
    CoachingInvitationStore invitations) : ControllerBase
{
    [HttpGet("me/students")]
    public async Task<IActionResult> Students([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null, [FromQuery] int? gradeLevel = null, [FromQuery] bool? isActive = true,
        [FromQuery] Guid[]? studentUserIds = null, CancellationToken ct = default) =>
        Ok(await directory.GetTeacherRosterAsync(actor.UserId ?? Guid.Empty, pageNumber, pageSize, searchTerm, gradeLevel,
            isActive, studentUserIds is { Length: > 0 } ? studentUserIds : null, ct));

    [HttpPost("invite-student")]
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> Invite([FromBody] CoachingStudentInviteRequest request, CancellationToken ct)
    {
        var invitation = await invitations.SendTeacherAsync(actor.UserId ?? Guid.Empty, request.StudentEmail, request.Message, ct);
        return Ok(new { invitationId = invitation.Id });
    }
}
public sealed record CoachingStudentInviteRequest(string StudentEmail, string? Message, Guid? TeacherUserId = null);
