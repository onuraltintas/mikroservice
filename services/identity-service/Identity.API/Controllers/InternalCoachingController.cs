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
    [HttpGet("legacy-profile-snapshot")]
    [AllowAnonymous]
    [InternalServiceKey]
    public async Task<IActionResult> LegacyProfileSnapshot(
        [FromServices] Identity.Infrastructure.Services.CoachingLegacyProfileReader reader, CancellationToken ct) => Ok(await reader.ReadAsync(ct));

    [HttpPost("accounts")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(32_768)]
    public async Task<IActionResult> Accounts(
        [FromBody] EduPlatform.Shared.Contracts.Coaching.CoachingAccountRequest request,
        [FromServices] ICoachingAccountReader accounts, CancellationToken ct)
    {
        if (request.UserIds is null || request.UserIds.Count > 500 || request.UserIds.Any(id => id == Guid.Empty)) return BadRequest();
        return Ok(new EduPlatform.Shared.Contracts.Coaching.CoachingAccountResponse(await accounts.GetAsync(request.UserIds, ct)));
    }

    [HttpPost("account-by-email")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(4_096)]
    public async Task<IActionResult> AccountByEmail(
        [FromBody] EduPlatform.Shared.Contracts.Coaching.CoachingAccountEmailRequest request,
        [FromServices] ICoachingAccountReader accounts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254) return BadRequest();
        var account = await accounts.FindByEmailAsync(request.Email, ct);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpGet("institutions/{institutionId:guid}/active")]
    [AllowAnonymous]
    [InternalServiceKey]
    public async Task<IActionResult> InstitutionActive(Guid institutionId, CancellationToken ct) =>
        Ok(new { isActive = (await _institutionRepository.GetByIdAsync(institutionId, ct)) is { IsActive: true } });

    private const int MaxStudentTargets = 100;
    private const int MaxReportPageSize = 100;
    private const int MaxReportPageNumber = 1000;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IParentStudentRelationshipRepository _parentStudentRelationshipRepository;

    public InternalCoachingController(
        IInstitutionRepository institutionRepository,
        IParentStudentRelationshipRepository parentStudentRelationshipRepository)
    {
        _institutionRepository = institutionRepository;
        _parentStudentRelationshipRepository = parentStudentRelationshipRepository;
    }

    [HttpPost("authorize-agreement-representative")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(4_096)]
    public async Task<IActionResult> AuthorizeAgreementRepresentative(
        [FromBody] CoachingAgreementRepresentativeAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RepresentativeUserId == Guid.Empty || request.StudentUserId == Guid.Empty)
            return BadRequest("Agreement representative scope is invalid.");

        var authorization = await _parentStudentRelationshipRepository
            .AuthorizeCoachingAgreementRepresentativeAsync(
                request.RepresentativeUserId,
                request.StudentUserId,
                cancellationToken);

        return authorization is null
            ? Forbid()
            : Ok(new CoachingAgreementRepresentativeAuthorizationResponse(
                authorization.RelationshipId,
                authorization.PartyRole));
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

    [HttpPost("institution-student")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(4_096)]
    public async Task<IActionResult> IsInstitutionStudent([FromBody] CoachingInstitutionStudentRequest request, CancellationToken cancellationToken)
    {
        if (request.InstitutionId == Guid.Empty || request.StudentId == Guid.Empty) return BadRequest();
        return Ok(new { isActive = await _institutionRepository.IsActiveCoachingInstitutionStudentAsync(request.InstitutionId, request.StudentId, cancellationToken) });
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

    /// <summary>Verified parents who want the weekly Coaching summary of their child (roadmap Y7).</summary>
    [HttpGet("parent-summary-recipients")]
    [AllowAnonymous]
    [InternalServiceKey]
    public async Task<IActionResult> GetParentSummaryRecipients(
        [FromServices] Identity.Application.ParentLinks.IParentLinkService parentLinks,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber is < 1 or > 10_000 || pageSize is < 1 or > 500) return BadRequest("Paging is invalid.");
        return Ok(await parentLinks.GetWeeklySummaryRecipientsAsync(pageNumber, pageSize, cancellationToken));
    }

    /// <summary>Roadmap O3: verified parent links of the given children, and of the given parent.</summary>
    [HttpPost("parent-links")]
    [AllowAnonymous]
    [InternalServiceKey]
    public async Task<IActionResult> GetParentLinks(
        [FromServices] Identity.Application.ParentLinks.IParentLinkService parentLinks,
        [FromBody] CoachingParentLinksRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StudentUserIds is null || request.StudentUserIds.Count > 200 || request.StudentUserIds.Any(x => x == Guid.Empty)
            || request.ParentUserId == Guid.Empty) return BadRequest("The parent link lookup is invalid.");
        return Ok(await parentLinks.GetVerifiedLinksAsync(request.StudentUserIds, request.ParentUserId, cancellationToken));
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
            request.TargetTeacherUserId,
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
            || request.PageSize is < 1 or > MaxReportPageSize
            || request.Search?.Length > 100
            || request.TeacherUserId == Guid.Empty)
        {
            return BadRequest("Report student page scope is invalid.");
        }

        var page = await _institutionRepository.GetCoachingReportStudentPageAsync(
            request.ViewerUserId,
            request.InstitutionId,
            request.GradeLevel,
            request.PageNumber,
            request.PageSize,
            cancellationToken,
            request.Search,
            request.TeacherUserId);

        return page is null
            ? Forbid()
            : Ok(new CoachingReportStudentPageResponse(
                page.StudentUserIds,
                page.TotalCount,
                page.Students));
    }

    [HttpPost("report-teacher-page")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> GetReportTeacherPage(
        [FromBody] CoachingReportTeacherPageRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ViewerUserId == Guid.Empty
            || request.InstitutionId == Guid.Empty
            || request.PageNumber is < 1 or > MaxReportPageNumber
            || request.PageSize is < 1 or > MaxReportPageSize
            || request.Search?.Length > 100)
        {
            return BadRequest("Report teacher page scope is invalid.");
        }

        var page = await _institutionRepository.GetCoachingReportTeacherPageAsync(
            request.ViewerUserId, request.InstitutionId, request.PageNumber,
            request.PageSize, request.Search, cancellationToken);
        return page is null ? Forbid() : Ok(page);
    }
}

public sealed record CoachingAuthorizationRequest(
    Guid TeacherId,
    IReadOnlyCollection<Guid> StudentIds,
    Guid? InstitutionId,
    bool IsSystemAdministrator);

public sealed record CoachingAuthorizationResponse(Guid? InstitutionId);

public sealed record CoachingParentLinksRequest(IReadOnlyList<Guid> StudentUserIds, Guid? ParentUserId);

public sealed record CoachingAgreementRepresentativeAuthorizationRequest(
    Guid RepresentativeUserId,
    Guid StudentUserId);

public sealed record CoachingAgreementRepresentativeAuthorizationResponse(
    Guid RelationshipId,
    string PartyRole);

public sealed record CoachingAdminAuthorizationRequest(Guid ViewerUserId);
public sealed record CoachingInstitutionStudentRequest(Guid InstitutionId, Guid StudentId);

public sealed record CoachingAdminAuthorizationResponse(bool IsGlobal, Guid? InstitutionId);

public sealed record CoachingStudentReadRequest(
    Guid ViewerUserId,
    IReadOnlyCollection<Guid> StudentIds,
    Guid? TargetTeacherUserId = null);

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
    int PageSize,
    string? Search = null,
    Guid? TeacherUserId = null);

public sealed record CoachingReportTeacherPageRequest(
    Guid ViewerUserId,
    Guid InstitutionId,
    int PageNumber,
    int PageSize,
    string? Search = null);

public sealed record CoachingReportStudentPageResponse(
    IReadOnlyCollection<Guid> StudentUserIds,
    int TotalCount,
    IReadOnlyCollection<CoachingReportStudentItem>? Students);
