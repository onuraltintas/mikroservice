using Asp.Versioning;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

/// <summary>Student panel T6: all of a student's exams in one list, with net trends and subject averages.</summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student,Parent,Teacher,InstitutionAdmin,InstitutionOwner,SystemAdmin")]
[Route("api/coaching/students")]
public sealed class CoachingStudentExamOverviewController(CoachingStudentExamOverviewService exams, ICurrentUserService actor) : ControllerBase
{
    [HttpGet("me/exams"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await exams.GetAsync(UserId, UserId, ct));

    [HttpGet("{studentId:guid}/exams")]
    public async Task<IActionResult> ForStudent(Guid studentId, CancellationToken ct) => Ok(await exams.GetAsync(UserId, studentId, ct));

    private Guid UserId => actor.UserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");
}
