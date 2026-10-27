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
    /// <summary>Roadmap O12: the student's active teachers and whether each link can be ended by the student.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await profiles.ListStudentTeachersAsync(actor.UserId ?? Guid.Empty, ct));

    [HttpDelete("{teacherUserId:guid}")]
    public async Task<IActionResult> Leave(Guid teacherUserId, CancellationToken ct)
    {
        var student = actor.UserId ?? Guid.Empty;
        await profiles.EndAssignmentAsync(student, teacherUserId, student, true, ct);
        return NoContent();
    }
}
