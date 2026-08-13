using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.API;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/internal/coaching")]
public sealed class InternalCoachingController : ControllerBase
{
    private const int MaxStudentTargets = 100;
    private readonly IInstitutionRepository _institutionRepository;

    public InternalCoachingController(IInstitutionRepository institutionRepository)
    {
        _institutionRepository = institutionRepository;
    }

    [HttpPost("authorize")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Authorize(
        [FromBody] CoachingAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StudentIds is null || request.StudentIds.Count > MaxStudentTargets)
        {
            return BadRequest("Student target count is invalid.");
        }

        var authorization = await _institutionRepository.AuthorizeCoachingTeacherTargetsAsync(
            request.TeacherId,
            request.StudentIds,
            request.InstitutionId,
            request.IsSystemAdministrator,
            cancellationToken);

        return authorization is null
            ? Forbid()
            : Ok(new CoachingAuthorizationResponse(authorization.InstitutionId));
    }
}

public sealed record CoachingAuthorizationRequest(
    Guid TeacherId,
    IReadOnlyCollection<Guid> StudentIds,
    Guid? InstitutionId,
    bool IsSystemAdministrator);

public sealed record CoachingAuthorizationResponse(Guid? InstitutionId);
