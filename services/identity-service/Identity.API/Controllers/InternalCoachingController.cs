using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.API;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
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

    [HttpPost("authorize-student-read")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> AuthorizeStudentRead(
        [FromBody] CoachingStudentReadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ViewerUserId == Guid.Empty
            || request.StudentIds is null
            || request.StudentIds.Count == 0
            || request.StudentIds.Count > MaxStudentTargets)
        {
            return BadRequest("Student read target count is invalid.");
        }

        var authorization = await _institutionRepository.AuthorizeCoachingStudentReadAsync(
            request.ViewerUserId,
            request.StudentIds,
            cancellationToken);

        return authorization is null
            ? Forbid()
            : Ok(new CoachingStudentReadResponse(authorization.AllowedStudentUserIds));
    }

    [HttpPost("report-students")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> GetReportStudents(
        [FromBody] CoachingReportStudentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ViewerUserId == Guid.Empty
            || request.InstitutionId == Guid.Empty
            || request.GradeLevel is < 1 or > 12)
        {
            return BadRequest("Report student scope is invalid.");
        }

        var studentIds = await _institutionRepository.GetCoachingReportStudentUserIdsAsync(
            request.ViewerUserId,
            request.InstitutionId,
            request.GradeLevel,
            cancellationToken);

        return studentIds is null
            ? Forbid()
            : Ok(new CoachingReportStudentResponse(studentIds));
    }
}

public sealed record CoachingAuthorizationRequest(
    Guid TeacherId,
    IReadOnlyCollection<Guid> StudentIds,
    Guid? InstitutionId,
    bool IsSystemAdministrator);

public sealed record CoachingAuthorizationResponse(Guid? InstitutionId);

public sealed record CoachingStudentReadRequest(
    Guid ViewerUserId,
    IReadOnlyCollection<Guid> StudentIds);

public sealed record CoachingStudentReadResponse(
    IReadOnlyCollection<Guid> AllowedStudentUserIds);

public sealed record CoachingReportStudentRequest(
    Guid ViewerUserId,
    Guid InstitutionId,
    int? GradeLevel);

public sealed record CoachingReportStudentResponse(
    IReadOnlyCollection<Guid> StudentUserIds);
