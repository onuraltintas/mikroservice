using System.Text;
using Asp.Versioning;
using Coaching.Application.Profiles;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

/// <summary>Roadmap O4: institution-wide practice exams with CSV import, ranking and branch analysis.</summary>
[Coaching.API.Security.CoachingProfileExceptionFilter]
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
[MfaCategory(MfaOperationCategories.Institutions)]
[Route("api/coaching/institution/exams")]
[Route("api/coaching/institutions/{institutionId:guid}/exams")]
public sealed class CoachingInstitutionExamsController(CoachingInstitutionExamService exams, CoachingProfileStore profiles,
    ICoachingAccountDirectory accounts, ICurrentUserService actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid? institutionId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await exams.ListAsync(UserId, await Resolve(institutionId, ct), pageNumber, pageSize, ct));

    [HttpPost]
    [RequestSizeLimit(4_096)]
    public async Task<IActionResult> Create(Guid? institutionId, [FromBody] InstitutionExamCreate request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await exams.CreateAsync(UserId, await Resolve(institutionId, ct), request, ct));

    [HttpGet("template")]
    public IActionResult Template([FromQuery] string examCode)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(CoachingInstitutionExamService.Template(examCode) + "\n")).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"deneme-sablonu-{examCode.ToLowerInvariant()}.csv");
    }

    [HttpGet("{examId:guid}")]
    public async Task<IActionResult> Get(Guid? institutionId, Guid examId, [FromQuery] string? branch, CancellationToken ct) =>
        Ok(await exams.GetAsync(UserId, await Resolve(institutionId, ct), examId, branch, ct));

    [HttpPost("{examId:guid}/import/preview")]
    [RequestSizeLimit(2_621_440)]
    public async Task<IActionResult> Preview(Guid? institutionId, Guid examId, [FromBody] InstitutionExamImportRequest request, CancellationToken ct) =>
        Ok(await exams.PreviewImportAsync(UserId, await Resolve(institutionId, ct), examId, InstitutionExamImportText.From(request.Csv, request.XlsxBase64), ct));

    [HttpPost("{examId:guid}/import")]
    [RequestSizeLimit(2_621_440)]
    public async Task<IActionResult> Import(Guid? institutionId, Guid examId, [FromBody] InstitutionExamImportRequest request, CancellationToken ct)
    {
        try { return Ok(new { applied = await exams.ApplyImportAsync(UserId, await Resolve(institutionId, ct), examId, InstitutionExamImportText.From(request.Csv, request.XlsxBase64), request.Fingerprint ?? "", ct) }); }
        catch (BusinessRuleException error) when (error.Code.StartsWith("Exam.", StringComparison.Ordinal))
        { return Conflict(new { code = error.Code, message = error.Message }); }
    }

    private Guid UserId => actor.UserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");
    private async Task<Guid> Resolve(Guid? institutionId, CancellationToken ct)
    {
        var scope = await accounts.AuthorizeAdminAsync(UserId, ct);
        if (scope is null) throw new BusinessRuleException("Authorization.Forbidden", "Kurum yetkisi gereklidir.");
        var id = institutionId ?? scope.InstitutionId ?? throw new ArgumentException("Kurum seçin.");
        return await profiles.RequireInstitutionAdminAsync(UserId, id, ct);
    }
}

/// <summary>Roadmap O4: a student's own place in their institution's exams.</summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Route("api/coaching/students/me/institution-exams")]
public sealed class CoachingStudentInstitutionExamsController(CoachingInstitutionExamService exams, ICurrentUserService actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await exams.GetStudentRanksAsync(actor.UserId ?? Guid.Empty, ct));
}

/// <summary>The results as CSV text, or (roadmap O4 follow-up) as a base64 Excel workbook with the same columns.</summary>
public sealed record InstitutionExamImportRequest(string? Csv, string? Fingerprint = null, string? XlsxBase64 = null);
