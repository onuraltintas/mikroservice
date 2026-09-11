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
    private const int MaxReportPageSize = 100;
    private const int MaxReportPageNumber = 1000;
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

    [HttpPost("authorize-admin")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> AuthorizeAdmin(
        [FromBody] CoachingAdminAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ViewerUserId == Guid.Empty)
        {
            return BadRequest("Admin scope viewer is invalid.");
        }

        var authorization = await _institutionRepository.AuthorizeCoachingAdminAsync(
            request.ViewerUserId,
            cancellationToken);

        return authorization is null
            ? Forbid()
            : Ok(new CoachingAdminAuthorizationResponse(
                authorization.IsGlobal,
                authorization.InstitutionId));
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

    [HttpPost("search-students")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(4_096)]
    public async Task<IActionResult> SearchSpeedReadingStudents(
        [FromBody] SpeedReadingStudentSearchRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || request.ViewerUserId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.SearchTerm)
            || request.SearchTerm.Trim().Length is < 2 or > 100)
        {
            return BadRequest("Student search request is invalid.");
        }

        var result = await _institutionRepository.SearchSpeedReadingStudentsAsync(
            request.ViewerUserId,
            request.SearchTerm,
            cancellationToken);
        return result is null
            ? Forbid()
            : Ok(new SpeedReadingStudentSearchResponse(result.StudentUserIds, result.HasMore));
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

    [HttpPost("report-student-page")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> GetReportStudentPage(
        [FromBody] CoachingReportStudentPageRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ViewerUserId == Guid.Empty
            || request.InstitutionId == Guid.Empty
            || request.GradeLevel is < 1 or > 12
            || request.PageNumber is < 1 or > MaxReportPageNumber
            || request.PageSize is < 1 or > MaxReportPageSize)
        {
            return BadRequest("Report student page scope is invalid.");
        }

        var page = await _institutionRepository.GetCoachingReportStudentPageAsync(
            request.ViewerUserId,
            request.InstitutionId,
            request.GradeLevel,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return page is null
            ? Forbid()
            : Ok(new CoachingReportStudentPageResponse(
                page.StudentUserIds,
                page.TotalCount));
    }
}

public sealed record CoachingAuthorizationRequest(
    Guid TeacherId,
    IReadOnlyCollection<Guid> StudentIds,
    Guid? InstitutionId,
    bool IsSystemAdministrator);

public sealed record CoachingAuthorizationResponse(Guid? InstitutionId);

public sealed record CoachingAdminAuthorizationRequest(Guid ViewerUserId);

public sealed record CoachingAdminAuthorizationResponse(bool IsGlobal, Guid? InstitutionId);

public sealed record CoachingStudentReadRequest(
    Guid ViewerUserId,
    IReadOnlyCollection<Guid> StudentIds);

public sealed record CoachingStudentReadResponse(
    IReadOnlyCollection<Guid> AllowedStudentUserIds);

public sealed record SpeedReadingStudentSearchRequest(Guid ViewerUserId, string SearchTerm);

public sealed record SpeedReadingStudentSearchResponse(
    IReadOnlyCollection<Guid> StudentUserIds,
    bool HasMore);

public sealed record CoachingReportStudentRequest(
    Guid ViewerUserId,
    Guid InstitutionId,
    int? GradeLevel);

public sealed record CoachingReportStudentResponse(
    IReadOnlyCollection<Guid> StudentUserIds);

public sealed record CoachingReportStudentPageRequest(
    Guid ViewerUserId,
    Guid InstitutionId,
    int? GradeLevel,
    int PageNumber,
    int PageSize);

public sealed record CoachingReportStudentPageResponse(
    IReadOnlyCollection<Guid> StudentUserIds,
    int TotalCount);
