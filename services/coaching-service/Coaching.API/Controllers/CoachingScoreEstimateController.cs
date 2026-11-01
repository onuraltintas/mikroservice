using System.Text;
using Asp.Versioning;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

/// <summary>Roadmap O5: LGS and YKS score and rank estimates for a student, their parents and their teachers.</summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student,Parent,Teacher,InstitutionAdmin,InstitutionOwner,SystemAdmin")]
[Route("api/coaching/students")]
public sealed class CoachingScoreEstimateController(CoachingScoreEstimateService estimates, ICurrentUserService actor) : ControllerBase
{
    [HttpGet("me/score-estimate"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Mine([FromQuery] decimal? diplomaGrade, CancellationToken ct) =>
        Ok(await estimates.EstimateAsync(UserId, UserId, diplomaGrade, ct));

    [HttpGet("{studentId:guid}/score-estimate")]
    public async Task<IActionResult> ForStudent(Guid studentId, [FromQuery] decimal? diplomaGrade, CancellationToken ct) =>
        Ok(await estimates.EstimateAsync(UserId, studentId, diplomaGrade, ct));

    private Guid UserId => actor.UserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");
}

/// <summary>Roadmap O5: the platform administrator uploads each year's score models.</summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "SystemAdmin")]
[HasPermission(PlatformPermissions.Coaching.View)]
[MfaCategory(MfaOperationCategories.Coaching)]
[Route("api/coaching-admin/score-models")]
public sealed class CoachingScoreModelsAdminController(CoachingScoreEstimateService models, ICurrentUserService actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await models.ListAsync(ct));

    [HttpGet("template")]
    public IActionResult Template() =>
        File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(models.Template() + "\n")).ToArray(), "text/csv; charset=utf-8", "puan-tahmini-sablonu.csv");

    [HttpPost("preview"), HasPermission(PlatformPermissions.Coaching.ContentManage)]
    [RequestSizeLimit(1_048_576), EnableRateLimiting("study-planning-write")]
    public async Task<IActionResult> Preview([FromBody] ScoreModelImportRequest request, CancellationToken ct) =>
        Ok(await models.PreviewAsync(UserId, request, ct));

    [HttpPost("apply"), HasPermission(PlatformPermissions.Coaching.ContentManage)]
    [RequestSizeLimit(1_048_576), EnableRateLimiting("study-planning-write")]
    public async Task<IActionResult> Apply([FromBody] ScoreModelImportRequest request, CancellationToken ct)
    {
        // KM7: every write takes a 5–500 character reason and writes an audit row.
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 5 or > 500)
            return BadRequest(new { message = "5–500 karakterlik işlem gerekçesi girin." });
        try { return Ok(new { applied = await models.ApplyAsync(UserId, request, ct) }); }
        catch (BusinessRuleException error) when (error.Code == "ScoreModel.Changed")
        { return Conflict(new { code = error.Code, message = error.Message }); }
    }

    /// <summary>KM7: uploads, restores and deletes, newest first.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int? year, CancellationToken ct) => Ok(await models.HistoryAsync(year, ct));

    [HttpPost("history/{id:guid}/restore"), HasPermission(PlatformPermissions.Coaching.ContentManage)]
    [RequestSizeLimit(4096), EnableRateLimiting("study-planning-write")]
    public async Task<IActionResult> Restore(Guid id, [FromBody] ScoreModelReasonRequest request, CancellationToken ct)
    {
        try { return Ok(await models.RestoreAsync(UserId, id, request.Reason, ct)); }
        catch (KeyNotFoundException error) { return NotFound(new { message = error.Message }); }
    }

    [HttpDelete("{year:int}/{scoreType}"), HasPermission(PlatformPermissions.Coaching.ContentManage)]
    [RequestSizeLimit(4096), EnableRateLimiting("study-planning-write")]
    public async Task<IActionResult> Delete(int year, string scoreType, [FromBody] ScoreModelReasonRequest request, CancellationToken ct)
    {
        try { await models.DeleteAsync(UserId, year, scoreType, request.Reason, ct); return Ok(new { deleted = true }); }
        catch (KeyNotFoundException error) { return NotFound(new { message = error.Message }); }
    }

    private Guid UserId => actor.UserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");
}
