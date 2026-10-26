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
    private readonly IUserRepository userRepository;

    public InternalReportingController(
        IInstitutionRepository institutionRepository,
        IUserRepository userRepository)
    {
        this.institutionRepository = institutionRepository;
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

    [HttpPost("speed-reading/user-audience")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(4_096)]
    public async Task<ActionResult<SpeedReadingUserAudienceResponse>> GetSpeedReadingUserAudience(
        [FromBody] SpeedReadingUserAudienceRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.Role is { Length: > 100 })
            return BadRequest("Role is too long.");

        var userIds = await userRepository.GetSpeedReadingAudienceUserIdsAsync(
            request?.Role,
            cancellationToken);
        return Ok(new SpeedReadingUserAudienceResponse(userIds));
    }

    [HttpPost("speed-reading/member-eligibility")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(4_096)]
    public async Task<ActionResult<SpeedReadingMemberEligibilityResponse>> CheckSpeedReadingMemberEligibility(
        [FromBody] SpeedReadingMemberEligibilityRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || request.UserId == Guid.Empty
            || !(string.Equals(request.Role, "Student", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.Role, "Teacher", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest("An eligible user and Speed Reading role are required.");
        }

        var isEligible = await userRepository.IsSpeedReadingMembershipEligibleAsync(
            request.UserId,
            request.Role,
            cancellationToken);
        return Ok(new SpeedReadingMemberEligibilityResponse(isEligible));
    }

    [HttpPost("speed-reading/institution-manager-authorization")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(4_096)]
    public async Task<ActionResult<SpeedReadingInstitutionManagerAuthorizationResponse>>
        CheckSpeedReadingInstitutionManagerAuthorization(
            [FromBody] SpeedReadingInstitutionManagerAuthorizationRequest? request,
            CancellationToken cancellationToken)
    {
        if (request is null || request.UserId == Guid.Empty || request.InstitutionId == Guid.Empty)
            return BadRequest("An active user and institution are required.");

        var canManage = await institutionRepository.CanManageSpeedReadingInstitutionAsync(
            request.UserId,
            request.InstitutionId,
            cancellationToken);
        return Ok(new SpeedReadingInstitutionManagerAuthorizationResponse(canManage));
    }
}
