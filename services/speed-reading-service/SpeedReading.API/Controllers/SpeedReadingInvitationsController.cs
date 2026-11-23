using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;
using SpeedReading.Application.Institutions;
using SpeedReading.Application.Subscription;
using SpeedReading.Domain.Institutions;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/invitations")]
public sealed class SpeedReadingInvitationsController(
    ISpeedReadingInvitations invitations,
    ISpeedReadingEmailDelivery emailDelivery,
    ISpeedReadingUserDirectory userDirectory,
    ISpeedReadingInstitutionDirectory institutionDirectory,
    ISpeedReadingInstitutionAdministrationAuthorization administrationAuthorization,
    IConfiguration configuration,
    ILogger<SpeedReadingInvitationsController> logger,
    ISpeedReadingBillingAccess? billing = null,
    ISpeedReadingBillingSeats? billingSeats = null) : ControllerBase
{
    private const string EmailConsumerType = "SpeedReadingInvitation";
    // User decision 23.11.2026: students train free; an institution needs a plan with a free seat for each new student.
    // Students already in the institution stay; the rule is checked on inviting and the seat is taken on accepting.
    public const string InstitutionNeedsPlan = "Yeni öğrenci eklemek için kurumunuzun bir paketi olmalı. Paketi kurum aboneliği sayfasından seçebilirsiniz.";
    public const string InstitutionPlanFull = "Kurum paketinizdeki öğrenci sayısı doldu. Yeni öğrenci için ek öğrenci alın ya da daha büyük bir paket seçin.";
    // Live test 23.11.2026: a plan that sells no extra seats must not tell the buyer to buy one.
    public const string InstitutionPlanFullNoExtra = "Kurum paketinizdeki öğrenci sayısı doldu. Yeni öğrenci için daha büyük bir paket seçin.";
    public const string InstitutionHasNoSeat = "Davet eden kurumun paketinde boş yer kalmadığı için bu davet şu an kabul edilemiyor. Kurumuna haber ver.";

    private async Task<bool> StaffSeatsRequiredAsync(Guid user, CancellationToken cancellationToken) =>
        billing is { Enabled: true } && billingSeats is { Enabled: true } && (await billing.GetAsync(user, cancellationToken)).StaffSeatsRequired;

    [HttpPost("teachers/me")]
    [Authorize(Roles = "Teacher")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<IActionResult> InviteStudent(
        [FromBody] InviteSpeedReadingStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var teacherUserId))
            return Unauthorized();

        var created = await invitations.CreateAsync(
            request.Email,
            SpeedReadingInstitutionMemberRole.Student,
            null,
            teacherUserId,
            teacherUserId,
            DateTime.UtcNow,
            cancellationToken);
        return await QueueInvitationEmailAsync(created, cancellationToken);
    }

    [HttpPost("institutions/{institutionId:guid}")]
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [HasPermission(PlatformPermissions.Institutions.Manage)]
    [MfaCategory(MfaOperationCategories.SpeedReading)]
    public async Task<IActionResult> InviteInstitutionMember(
        Guid institutionId,
        [FromBody] InviteSpeedReadingInstitutionMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId))
            return Unauthorized();
        if (institutionId == Guid.Empty || !await CanManageInstitutionAsync(actorId, institutionId, cancellationToken))
            return Forbid();
        if (!await InstitutionExistsAsync(institutionId, cancellationToken))
            return NotFound(new { code = "Institution.NotFound", message = "Aktif Hızlı Okuma kurumu bulunamadı." });
        if (!Enum.IsDefined(request.Role))
            return BadRequest(new { code = "Invitation.InvalidRole", message = "Davet rolü öğrenci veya öğretmen olmalıdır." });
        if (request.Role == SpeedReadingInstitutionMemberRole.Teacher && request.TeacherUserId.HasValue)
            return BadRequest(new { code = "Invitation.InvalidScope", message = "Öğretmen davetinde hedef öğretmen seçilemez." });
        if (request.Role == SpeedReadingInstitutionMemberRole.Student && await StaffSeatsRequiredAsync(actorId, cancellationToken))
        {
            var pool = await billingSeats!.GetPoolAsync(institutionId, cancellationToken);
            if (pool is null) return Conflict(new { code = "Invitation.InstitutionNeedsPlan", message = InstitutionNeedsPlan });
            if (pool.Used >= pool.Capacity)
                return Conflict(new { code = "Invitation.InstitutionPlanFull", message = pool.ExtraSeatPrice is null ? InstitutionPlanFullNoExtra : InstitutionPlanFull });
        }

        var created = await invitations.CreateAsync(
            request.Email,
            request.Role,
            institutionId,
            request.TeacherUserId,
            actorId,
            DateTime.UtcNow,
            cancellationToken);
        return await QueueInvitationEmailAsync(created, cancellationToken);
    }

    [HttpGet("sent-pending")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<SpeedReadingPendingInvitation>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySentPending(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId))
            return Unauthorized();

        var result = await invitations.GetPendingByInviterAsync(actorId, DateTime.UtcNow, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{invitationId:guid}/cancel")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId))
            return Unauthorized();

        return await invitations.CancelAsync(invitationId, actorId, DateTime.UtcNow, cancellationToken) switch
        {
            SpeedReadingInvitationCancelResult.Cancelled => NoContent(),
            SpeedReadingInvitationCancelResult.NotFound => NotFound(new
            {
                code = "Invitation.NotFound",
                message = "Bekleyen davet bulunamadı."
            }),
            _ => Conflict(new
            {
                code = "Invitation.NotPending",
                message = "Davet artık beklemede olmadığı için iptal edilemedi."
            })
        };
    }

    [HttpPost("{invitationId:guid}/accept")]
    [Authorize]
    public async Task<IActionResult> Accept(
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId))
            return Unauthorized();

        var response = await userDirectory.GetUsersAsync([userId], cancellationToken);
        var user = response.Users.SingleOrDefault(item => item.UserId == userId && item.IsActive);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return Conflict(new
            {
                code = "Invitation.SpeedReadingAccountRequired",
                message = "Bu daveti kabul etmek için aynı e-posta adresiyle önce Hızlı Okuma hesabı oluşturun veya o hesapla giriş yapın."
            });
        }

        // A new institution student takes a seat first; when joining fails after that, the seat is given back.
        Guid? seatInstitution = null;
        if (await invitations.InstitutionSeatNeededAsync(invitationId, userId, cancellationToken) is { } institution
            && await StaffSeatsRequiredAsync(userId, cancellationToken))
        {
            var seat = await billingSeats!.AssignAsync(institution, userId, userId, cancellationToken);
            if (!seat.Done) return Conflict(new { code = "Invitation.InstitutionHasNoSeat", message = InstitutionHasNoSeat });
            if (seat.Code == "Assigned") seatInstitution = institution;
            billing!.Forget(userId);
        }
        var result = await invitations.AcceptAsync(
            invitationId,
            userId,
            user.Email,
            DateTime.UtcNow,
            cancellationToken);
        if (seatInstitution is { } given && result is not (SpeedReadingInvitationAcceptResult.Accepted or SpeedReadingInvitationAcceptResult.AlreadyAccepted))
            await billingSeats!.ReleaseWhenLeavingAsync(given, userId, userId, cancellationToken);
        return result switch
        {
            SpeedReadingInvitationAcceptResult.Accepted
                or SpeedReadingInvitationAcceptResult.AlreadyAccepted => Ok(new
                {
                    code = "Invitation.Accepted",
                    message = "Hızlı Okuma daveti kabul edildi."
                }),
            SpeedReadingInvitationAcceptResult.NotFound => NotFound(new
            {
                code = "Invitation.NotFound",
                message = "Davet bulunamadı veya bağlantı geçersiz."
            }),
            SpeedReadingInvitationAcceptResult.WrongEmail => Forbid(),
            SpeedReadingInvitationAcceptResult.Expired => Conflict(new
            {
                code = "Invitation.Expired",
                message = "Davet bağlantısının süresi dolmuş. Yeni davet isteyin."
            }),
            SpeedReadingInvitationAcceptResult.ProductRoleRequired => Conflict(new
            {
                code = "Invitation.SpeedReadingRoleRequired",
                message = "Bu daveti kabul etmek için davette belirtilen Hızlı Okuma rolüyle kayıt olun."
            }),
            SpeedReadingInvitationAcceptResult.MembershipRequired => Conflict(new
            {
                code = "Invitation.MembershipRequired",
                message = "Kurum veya hedef öğretmen üyeliği artık etkin değil. Kurum yöneticisinden yeni davet isteyin."
            }),
            SpeedReadingInvitationAcceptResult.TeacherAssignmentConflict => Conflict(new
            {
                code = "Invitation.TeacherAssignmentConflict",
                message = "Öğrenci eş zamanlı olarak başka bir öğretmene atanmış. Kurum yöneticisiyle iletişime geçin."
            }),
            SpeedReadingInvitationAcceptResult.InvalidInvitation => Conflict(new
            {
                code = "Invitation.InvalidScope",
                message = "Bu davet hesabınız için geçerli değil."
            }),
            _ => Conflict(new { code = "Invitation.NotPending", message = "Davet artık kabul edilebilir durumda değil." })
        };
    }

    private async Task<IActionResult> QueueInvitationEmailAsync(
        SpeedReadingInvitationCreateResponse created,
        CancellationToken cancellationToken)
    {
        if (created.Result is SpeedReadingInvitationCreateResult.InvalidScope)
        {
            return BadRequest(new
            {
                code = "Invitation.InvalidScope",
                message = "Davet bilgileri veya kurum/öğretmen kapsamı geçersiz."
            });
        }
        if (created.Result == SpeedReadingInvitationCreateResult.MembershipRequired)
        {
            return Conflict(new
            {
                code = "Invitation.TeacherMembershipRequired",
                message = "Öğrenci davetindeki öğretmen önce bu Hızlı Okuma kurumuna bağlı olmalıdır."
            });
        }
        if (created.Result == SpeedReadingInvitationCreateResult.ProductRoleRequired)
        {
            return Conflict(new
            {
                code = "Invitation.SpeedReadingRoleRequired",
                message = "Davet gönderen öğretmenin Hızlı Okuma öğretmen erişimi etkin değil."
            });
        }

        var invitation = created.Invitation!;
        var portalBaseUrl = GetPortalBaseUrl();
        if (portalBaseUrl is null)
        {
            logger.LogError("Speed Reading PublicApp:BaseUrl must be an absolute HTTP(S) URL before invitations can be emailed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "Invitation.EmailUnavailable",
                message = "Davet oluşturuldu ancak e-posta ayarı hazır değil. Lütfen daha sonra tekrar deneyin."
            });
        }

        var acceptUrl = $"{portalBaseUrl}/auth/accept-invitation?id={Uri.EscapeDataString(invitation.InvitationId.ToString())}";
        var roleLabel = invitation.Role == SpeedReadingInstitutionMemberRole.Teacher ? "öğretmen" : "öğrenci";
        var safeEmail = WebUtility.HtmlEncode(invitation.Email);
        var safeUrl = WebUtility.HtmlEncode(acceptUrl);
        var body = $"""
            <!doctype html>
            <html lang="tr"><body style="font-family:Arial,sans-serif;color:#1f2937;line-height:1.6">
              <h2>Hızlı Okuma davetiniz</h2>
              <p>{safeEmail} adresi için Hızlı Okuma platformunda {roleLabel} daveti oluşturuldu.</p>
              <p>Daveti kabul etmek için aşağıdaki bağlantıyı açın. Hesabınız yoksa önce aynı e-posta adresiyle Hızlı Okuma’ya kayıt olun, ardından bu bağlantıyı yeniden açın.</p>
              <p><a href="{safeUrl}" style="display:inline-block;padding:12px 20px;background:#176b65;color:#fff;text-decoration:none;border-radius:6px">Daveti kabul et</a></p>
              <p>Bu bağlantı 7 gün geçerlidir. Bu daveti beklemiyorsanız bu e-postayı yok sayabilirsiniz.</p>
            </body></html>
            """;

        try
        {
            await emailDelivery.QueueAsync(
                invitation.InvitationId,
                EmailConsumerType,
                invitation.Email,
                "Hızlı Okuma platformuna davet edildiniz",
                body,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Could not queue Speed Reading invitation email {InvitationId}", invitation.InvitationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "Invitation.EmailUnavailable",
                message = "Davet kaydedildi ancak e-posta şu anda gönderilemiyor. Lütfen biraz sonra aynı daveti yeniden deneyin."
            });
        }

        return Accepted(new
        {
            invitationId = invitation.InvitationId,
            status = "Pending",
            message = created.Result == SpeedReadingInvitationCreateResult.AlreadyPending
                ? "Bu adres için bekleyen davet var; davet e-postası güvenle yeniden kuyruğa alındı."
                : "Davet e-postası gönderilmek üzere kuyruğa alındı."
        });
    }

    private string? GetPortalBaseUrl()
    {
        var configuredBaseUrl = configuration["PublicApp:BaseUrl"];
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var publicUri)
            || (publicUri.Scheme != Uri.UriSchemeHttp && publicUri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrWhiteSpace(publicUri.UserInfo)
            || !string.IsNullOrWhiteSpace(publicUri.Query)
            || !string.IsNullOrWhiteSpace(publicUri.Fragment))
        {
            return null;
        }

        return configuredBaseUrl!.TrimEnd('/');
    }

    private async Task<bool> CanManageInstitutionAsync(
        Guid actorId,
        Guid institutionId,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole("SystemAdmin"))
            return true;
        if (!User.IsInRole("InstitutionAdmin") && !User.IsInRole("InstitutionOwner"))
            return false;
        return await administrationAuthorization.CanManageAsync(actorId, institutionId, cancellationToken);
    }

    private async Task<bool> InstitutionExistsAsync(Guid institutionId, CancellationToken cancellationToken)
    {
        var response = await institutionDirectory.GetInstitutionsAsync(cancellationToken);
        return response.Institutions.Any(item => item.InstitutionId == institutionId && item.IsActive);
    }

    private bool TryGetActor(out Guid actorId) => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
        out actorId);
}

public sealed record InviteSpeedReadingStudentRequest(
    [Required, EmailAddress, MaxLength(320)] string Email);

public sealed record InviteSpeedReadingInstitutionMemberRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    SpeedReadingInstitutionMemberRole Role,
    Guid? TeacherUserId = null);
