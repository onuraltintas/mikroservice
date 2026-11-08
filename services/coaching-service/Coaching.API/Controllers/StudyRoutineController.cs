using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

/// <summary>H3: the student's "günlük düzen" answers, which set her free hours.</summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/routine")]
public sealed class StudyRoutineController(IStudyRoutineService routines) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(StudyRoutineView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Get(CancellationToken cancellationToken = default) => Respond(async () =>
    {
        var result = await routines.GetAsync(cancellationToken);
        return result is null ? NotFound(new { success = false, message = "Günlük düzenin henüz belirlenmedi.", code = "StudyPlanning.RoutineMissing" })
            : Ok(new { success = true, data = result });
    });

    [HttpPut]
    [RequestSizeLimit(8 * 1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(typeof(StudyRoutineView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Replace([FromBody] StudyRoutineUpdate request, CancellationToken cancellationToken = default) =>
        Respond(async () => Ok(new { success = true, data = await routines.ReplaceAsync(request, cancellationToken) }));

    internal static async Task<IActionResult> Respond(ControllerBase controller, Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return controller.StatusCode(403, new { success = false, message = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict")
        { return controller.Conflict(new { success = false, message = ex.Message, code = ex.Code }); }
        catch (ConcurrencyException)
        { return controller.Conflict(new { success = false, message = "Günlük düzenin başka bir yerde değişti. Sayfayı yenileyip yeniden dene.", code = "StudyPlanning.Conflict" }); }
        catch (StudyPlanValidationException ex)
        { return controller.BadRequest(new { success = false, message = ex.Message, code = "StudyPlanning.Validation" }); }
        catch (ArgumentException)
        { return controller.BadRequest(new { success = false, message = "Günlük düzen cevaplarını kontrol et.", code = "StudyPlanning.Validation" }); }
    }

    private Task<IActionResult> Respond(Func<Task<IActionResult>> action) => Respond(this, action);
}

/// <summary>H3: a teacher or institution administrator reads a student's routine; only the student can change it.</summary>
[ApiController, ApiVersion(1.0)]
[Authorize(Roles = "Teacher,InstitutionAdmin,InstitutionOwner")]
[Route("api/coaching/staff/students/{studentId:guid}/study/routine")]
public sealed class CoachingStaffStudyRoutineController(IStudyRoutineService routines) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(StudyRoutineView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Get(Guid studentId, CancellationToken cancellationToken = default) => StudyRoutineController.Respond(this, async () =>
        Ok(new { success = true, data = await routines.GetForStudentAsync(studentId, cancellationToken) }));
}
