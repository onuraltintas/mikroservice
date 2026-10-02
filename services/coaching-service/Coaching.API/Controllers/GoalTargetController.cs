using Asp.Versioning;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/goals/{goalId:guid}/target")]
public sealed class GoalTargetController(IGoalTargetService targets) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid goalId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await targets.GetAsync(goalId, cancellationToken);
            return result is null ? Missing() : Ok(new { success = true, data = result });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { success = false, code = ex.Code, message = ex.Message }); }
    }

    [HttpPut]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    public async Task<IActionResult> Replace(Guid goalId, [FromBody] GoalTargetUpdate request, CancellationToken cancellationToken = default)
    {
        try { return Ok(new { success = true, data = await targets.ReplaceAsync(goalId, request, cancellationToken) }); }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { success = false, code = ex.Code, message = ex.Message }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict")
        { return Conflict(new { success = false, code = ex.Code, message = ex.Message }); }
        catch (ConcurrencyException)
        { return Conflict(new { success = false, code = "StudyPlanning.Conflict", message = "Hedef değişti. Güncel kaydı yükleyip tekrar deneyin." }); }
        catch (KeyNotFoundException) { return Missing(); }
        catch (ArgumentException)
        { return BadRequest(new { success = false, code = "StudyPlanning.Validation", message = "Geçerli ve aktif tek bir okul veya üniversite programı seçin. Bağlantıyı kaldırmak için seçimi boş bırakabilirsiniz." }); }
    }

    [HttpPut("score")]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    public async Task<IActionResult> ReplaceScore(Guid goalId, [FromBody] GoalScoreTargetUpdate request, CancellationToken cancellationToken = default)
    {
        try { return Ok(new { success = true, data = await targets.ReplaceScoreAsync(goalId, request, cancellationToken) }); }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { success = false, code = ex.Code, message = ex.Message }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict")
        { return Conflict(new { success = false, code = ex.Code, message = ex.Message }); }
        catch (ConcurrencyException)
        { return Conflict(new { success = false, code = "StudyPlanning.Conflict", message = "Hedef değişti. Yeniden yükleyin." }); }
        catch (KeyNotFoundException) { return Missing(); }
        catch (ArgumentException)
        { return BadRequest(new { success = false, code = "StudyPlanning.Validation", message = "Pozitif hedef puanı, en fazla 999,99 olan puan ölçeği ve sınav türü seçin. Hedef puanı ölçeği aşamaz. Temizlemek için üçünü de boş gönderin." }); }
    }

    private NotFoundObjectResult Missing() => NotFound(new { success = false, message = "Hedef bulunamadı." });
}
