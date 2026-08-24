using EduPlatform.Shared.Contracts.Reporting;
using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.API;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/internal/reporting")]
public sealed class InternalReportingController : ControllerBase
{
    private readonly IInstitutionRepository institutionRepository;
    private readonly ITeacherRepository teacherRepository;

    public InternalReportingController(
        IInstitutionRepository institutionRepository,
        ITeacherRepository teacherRepository)
    {
        this.institutionRepository = institutionRepository;
        this.teacherRepository = teacherRepository;
    }

    [HttpGet("speed-reading/institutions")]
    [AllowAnonymous]
    [InternalServiceKey]
    public async Task<ActionResult<SpeedReadingInstitutionScopeResponse>> GetSpeedReadingInstitutions(
        CancellationToken cancellationToken)
    {
        var institutions = await institutionRepository.GetSpeedReadingInstitutionScopeAsync(cancellationToken);
        return Ok(new SpeedReadingInstitutionScopeResponse(institutions));
    }

    [HttpPost("speed-reading/teacher-students")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(16_384)]
    public async Task<ActionResult<SpeedReadingTeacherStudentScopeResponse>> GetSpeedReadingTeacherStudents(
        [FromBody] SpeedReadingTeacherStudentScopeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ViewerUserId == Guid.Empty)
        {
            return BadRequest("Teacher scope viewer is invalid.");
        }

        var scope = await teacherRepository.GetSpeedReadingTeacherStudentScopeAsync(
            request.ViewerUserId,
            request.TargetTeacherUserId,
            cancellationToken);
        return scope is null ? Forbid() : Ok(scope);
    }
}
