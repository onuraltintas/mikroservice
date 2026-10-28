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

/// <summary>
/// Roadmap O13: institution workload per teacher, branches of an admin of several institutions and CSV bulk operations.
/// The institution is the one the admin selected (header X-Coaching-Institution) unless given in the route.
/// </summary>
[Coaching.API.Security.CoachingProfileExceptionFilter]
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
[Route("api/coaching/institution")]
[Route("api/coaching/institutions/{institutionId:guid}")]
public sealed class CoachingInstitutionReportsController(CoachingInstitutionWorkloadService workload, CoachingInstitutionBulkService bulk,
    CoachingProfileStore profiles, ICoachingAccountDirectory accounts, ICurrentUserService actor) : ControllerBase
{
    [HttpGet("workload")]
    public async Task<IActionResult> Workload(Guid? institutionId, CancellationToken ct) =>
        Ok(new { data = await workload.GetAsync(UserId, await Resolve(institutionId, ct), ct) });

    [HttpGet("/api/coaching/institution-branches")]
    public async Task<IActionResult> Branches(CancellationToken ct) => Ok(new { data = await workload.BranchesAsync(UserId, ct) });

    [HttpPost("bulk/student-invitations/preview")]
    [RequestSizeLimit(2_621_440)]
    public async Task<IActionResult> PreviewInvitations([FromBody] InstitutionBulkRequest request, Guid? institutionId, CancellationToken ct) =>
        await Run(id => bulk.PreviewInvitationsAsync(UserId, id, Text(request), ct), institutionId, ct);

    [HttpPost("bulk/student-invitations")]
    [RequestSizeLimit(2_621_440)]
    [MfaCategory(MfaOperationCategories.Institutions)]
    public async Task<IActionResult> ApplyInvitations([FromBody] InstitutionBulkRequest request, Guid? institutionId, CancellationToken ct) =>
        await Run(id => bulk.ApplyInvitationsAsync(UserId, id, Text(request), request.Fingerprint ?? "", ct), institutionId, ct);

    [HttpPost("bulk/teacher-links/preview")]
    [RequestSizeLimit(2_621_440)]
    public async Task<IActionResult> PreviewLinks([FromBody] InstitutionBulkRequest request, Guid? institutionId, CancellationToken ct) =>
        await Run(id => bulk.PreviewTeacherLinksAsync(UserId, id, Text(request), ct), institutionId, ct);

    [HttpPost("bulk/teacher-links")]
    [RequestSizeLimit(2_621_440)]
    [MfaCategory(MfaOperationCategories.Institutions)]
    public async Task<IActionResult> ApplyLinks([FromBody] InstitutionBulkRequest request, Guid? institutionId, CancellationToken ct) =>
        await Run(id => bulk.ApplyTeacherLinksAsync(UserId, id, Text(request), request.Fingerprint ?? "", ct), institutionId, ct);

    [HttpGet("bulk/{kind}/template")]
    public IActionResult Template(string kind) => kind switch
    {
        "student-invitations" => Csv("ogrenci-davet-sablonu.csv", "eposta;ogretmen_eposta\nogrenci@ornek.com;ogretmen@ornek.com\n"),
        "teacher-links" => Csv("ogretmen-atama-sablonu.csv", "ogrenci_eposta;ogretmen_eposta\nogrenci@ornek.com;ogretmen@ornek.com\n"),
        _ => NotFound()
    };

    private IActionResult Csv(string name, string body) =>
        File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(body)).ToArray(), "text/csv; charset=utf-8", name);

    private async Task<IActionResult> Run<T>(Func<Guid, Task<T>> action, Guid? institutionId, CancellationToken ct)
    {
        try { return Ok(new { data = await action(await Resolve(institutionId, ct)) }); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (BusinessRuleException error) when (error.Code == "Bulk.Changed") { return Conflict(new { message = error.Message, code = error.Code }); }
    }

    private static string Text(InstitutionBulkRequest request) => InstitutionExamImportText.From(request.Csv, request.XlsxBase64);

    private Guid UserId => actor.UserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");

    private async Task<Guid> Resolve(Guid? institutionId, CancellationToken ct)
    {
        var scope = await accounts.AuthorizeAdminAsync(UserId, ct) ?? throw new BusinessRuleException("Authorization.Forbidden", "Kurum yetkisi gereklidir.");
        var id = institutionId ?? scope.InstitutionId ?? throw new ArgumentException("Kurum seçin.");
        await profiles.RequireInstitutionAdminAsync(UserId, id, ct);
        return id;
    }
}
