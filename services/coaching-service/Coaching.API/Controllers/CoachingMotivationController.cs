using Asp.Versioning;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

/// <summary>Roadmap D5: study streak, badges and goal celebration for a student, their parents and their teachers.</summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student,Parent,Teacher,InstitutionAdmin,InstitutionOwner,SystemAdmin")]
[Route("api/coaching/students")]
public sealed class CoachingMotivationController(CoachingMotivationService motivation, ICurrentUserService actor) : ControllerBase
{
    [HttpGet("me/motivation"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await motivation.GetAsync(UserId, UserId, ct));

    [HttpGet("{studentId:guid}/motivation")]
    public async Task<IActionResult> ForStudent(Guid studentId, CancellationToken ct)
    {
        try { return Ok(await motivation.GetAsync(UserId, studentId, ct)); }
        catch (BusinessRuleException error) when (error.Code == "Authorization.Forbidden") { return StatusCode(403, new { message = error.Message }); }
    }

    private Guid UserId => actor.UserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");
}
