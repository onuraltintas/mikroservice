using Asp.Versioning;
using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[Coaching.API.Security.CoachingProfileExceptionFilter]
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
[MfaCategory(MfaOperationCategories.Institutions)]
[Route("api/coaching/institution")]
[Route("api/coaching/institutions/{institutionId:guid}")]
public sealed class CoachingMembershipsController(CoachingProfileStore profiles,
    ICoachingAccountDirectory accounts, ICurrentUserService actor, CoachingInvitationStore invitations) : ControllerBase
{
    [HttpPost("invite-student")]
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> InviteStudent([FromBody] CoachingStudentInviteRequest request, Guid? institutionId, CancellationToken ct)
    {
        var id = await Resolve(institutionId, ct);
        var row = await invitations.SendInstitutionAsync(UserId, id, request.StudentEmail, "Student", request.TeacherUserId, request.Message, ct);
        return Ok(new { invitationId = row.Id });
    }

    [HttpPost("invite-teacher")]
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> InviteTeacher([FromBody] CoachingTeacherInviteRequest request, Guid? institutionId, CancellationToken ct)
    {
        var id = await Resolve(institutionId, ct);
        var row = await invitations.SendInstitutionAsync(UserId, id, request.TeacherEmail, "Teacher", null, request.Message, ct);
        return Ok(new { invitationId = row.Id });
    }

    [HttpPut("students/{studentUserId:guid}")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> UpdateStudent(Guid studentUserId, [FromBody] CoachingInstitutionStudentUpdate request,
        Guid? institutionId, CancellationToken ct)
    {
        var id = await Resolve(institutionId, ct);
        await profiles.UpdateInstitutionStudentAsync(UserId, id, studentUserId, request.GradeLevel, request.TeacherUserId, ct);
        return NoContent();
    }

    [HttpDelete("students/{studentUserId:guid}")]
    public async Task<IActionResult> RemoveStudent(Guid studentUserId, Guid? institutionId, CancellationToken ct)
    {
        await profiles.SetMembershipAsync(UserId, await Resolve(institutionId, ct), studentUserId, CoachingMemberRole.Student, false, ct);
        return NoContent();
    }

    [HttpDelete("students/{studentUserId:guid}/teachers/{teacherUserId:guid}")]
    public async Task<IActionResult> RemoveStudentTeacher(Guid studentUserId, Guid teacherUserId, Guid? institutionId, CancellationToken ct)
    {
        await profiles.EndInstitutionTeacherLinkAsync(UserId, await Resolve(institutionId, ct), studentUserId, teacherUserId, ct);
        return NoContent();
    }

    /// <summary>How much handed-in work waits for a grade; when there is any, removal needs a replacement teacher.</summary>
    [HttpGet("teachers/{teacherUserId:guid}/removal-preview")]
    public async Task<IActionResult> TeacherRemovalPreview(Guid teacherUserId, Guid? institutionId, CancellationToken ct) =>
        Ok(new { ungradedSubmissionCount = await profiles.CountUngradedSubmissionsAsync(UserId, await Resolve(institutionId, ct), teacherUserId, ct) });

    /// <summary>Removes a teacher; with a replacement their students and open assignments move to that teacher.</summary>
    [HttpDelete("teachers/{teacherUserId:guid}")]
    public async Task<IActionResult> RemoveTeacher(Guid teacherUserId, Guid? institutionId,
        [FromQuery] Guid? replacementTeacherUserId, CancellationToken ct)
    {
        await profiles.RemoveInstitutionTeacherAsync(UserId, await Resolve(institutionId, ct), teacherUserId, replacementTeacherUserId, ct);
        return NoContent();
    }

    private Guid UserId => actor.UserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");
    private async Task<Guid> Resolve(Guid? institutionId, CancellationToken ct)
    {
        var scope = await accounts.AuthorizeAdminAsync(UserId, ct);
        if (scope is null) throw new BusinessRuleException("Authorization.Forbidden", "Kurum yetkisi gereklidir.");
        var id = institutionId ?? scope.InstitutionId ?? throw new ArgumentException("Kurum seçin.");
        await profiles.RequireInstitutionAdminAsync(UserId, id, ct);
        return id;
    }
}
public sealed record CoachingTeacherInviteRequest(string TeacherEmail, string? Message);
public sealed record CoachingInstitutionStudentUpdate(int GradeLevel, Guid? TeacherUserId);
