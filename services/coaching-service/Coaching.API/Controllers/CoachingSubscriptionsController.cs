using System.Security.Claims;
using Asp.Versioning;
using Coaching.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

/// <summary>
/// Who may use Koçluk, and the seats an independent teacher gives to their own students. Plans, orders and payments
/// are kept by the billing service ("Abonelik ve Ödeme"); Koçluk asks it and checks who may sit on a seat.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching/subscriptions")]
[Authorize]
public sealed class CoachingSubscriptionsController(ICoachingSubscription subscriptions) : ControllerBase
{
    [HttpGet("my-access")]
    public async Task<IActionResult> GetMyAccess(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        return Ok(new { success = true, data = await subscriptions.GetMyAccessAsync(userId, cancellationToken) });
    }

    /// <summary>A linked parent sees whether the child has Koçluk; the subscription itself is bought in the student's account.</summary>
    [HttpGet("children/{studentId:guid}/access")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetChildAccess(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var parentId)) return Unauthorized();
        var result = await subscriptions.GetChildAccessAsync(parentId, studentId, cancellationToken);
        return result is null ? Forbid() : Ok(new { success = true, data = result });
    }

    [HttpGet("my-teacher-subscription")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetMyTeacherSubscription(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var teacherId)) return Unauthorized();
        var result = await subscriptions.GetMyTeacherSubscriptionAsync(teacherId, cancellationToken);
        return Ok(new { success = true, data = result });
    }

    [HttpPut("my-teacher-subscription/students/{studentId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> AssignMyTeacherStudent(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var teacherId)) return Unauthorized();
        return await subscriptions.AssignMyTeacherStudentSeatAsync(teacherId, studentId, cancellationToken)
            ? Ok(new { success = true, message = "Öğrenci öğretmen planı kontenjanına eklendi." })
            : BadRequest(new { success = false, message = "Öğrenci size atanmamış, bağımsız öğretmen planınız etkin değil veya kontenjan dolu." });
    }

    [HttpDelete("my-teacher-subscription/students/{studentId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> RemoveMyTeacherStudent(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var teacherId)) return Unauthorized();
        return await subscriptions.RemoveMyTeacherStudentSeatAsync(teacherId, studentId, cancellationToken)
            ? Ok(new { success = true, message = "Öğrencinin öğretmen planı erişimi kaldırıldı." })
            : NotFound(new { success = false, message = "Etkin öğretmen planınızda bu öğrenci için kontenjan bulunamadı." });
    }

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);
}
