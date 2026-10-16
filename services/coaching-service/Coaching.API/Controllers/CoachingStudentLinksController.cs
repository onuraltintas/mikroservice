using Asp.Versioning;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Coaching.API.Security.CoachingProfileExceptionFilter]
[Route("api/coaching/students/me/teachers")]
public sealed class CoachingStudentLinksController(CoachingProfileStore profiles, ICurrentUserService actor) : ControllerBase
{
    [HttpDelete("{teacherUserId:guid}")]
    public async Task<IActionResult> Leave(Guid teacherUserId, CancellationToken ct)
    {
        var student = actor.UserId ?? Guid.Empty;
        await profiles.EndAssignmentAsync(student, teacherUserId, student, true, ct);
        return NoContent();
    }
}
