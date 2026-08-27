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
    private readonly IUserRepository userRepository;

    public InternalReportingController(
        IInstitutionRepository institutionRepository,
        ITeacherRepository teacherRepository,
        IUserRepository userRepository)
    {
        this.institutionRepository = institutionRepository;
        this.teacherRepository = teacherRepository;
        this.userRepository = userRepository;
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

    [HttpPost("speed-reading/users")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(32_768)]
    public async Task<ActionResult<SpeedReadingUserDirectoryResponse>> GetSpeedReadingUsers(
        [FromBody] SpeedReadingUserDirectoryRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest("User IDs are required.");

        var userIds = request.UserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (userIds.Length == 0 || userIds.Length > 500)
        {
            return BadRequest("One to 500 valid user IDs are required.");
        }

        var users = await userRepository.GetSpeedReadingDirectoryAsync(userIds, cancellationToken);
        return Ok(new SpeedReadingUserDirectoryResponse(users));
    }
}
