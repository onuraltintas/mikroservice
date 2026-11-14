using Coaching.Application.StudyPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

/// <summary>
/// Paket H4a (H4b review): the student's plan preferences (exam focus and tempo). Saving them never changes her free hours, so a
/// student with hand-made hours (or a routine that no longer matches them) keeps her choices.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/plan-preferences")]
public sealed class StudyPlanPreferencesController(IStudyPlanPreferencesService preferences) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(StudyPlanPreferencesView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Get(CancellationToken cancellationToken = default) => StudyRoutineController.Respond(this, async () =>
        Ok(new { success = true, data = await preferences.GetAsync(cancellationToken) }));

    [HttpPut]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(typeof(StudyPlanPreferencesView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Replace([FromBody] StudyPlanPreferencesUpdate request, CancellationToken cancellationToken = default) =>
        StudyRoutineController.Respond(this, async () => Ok(new { success = true, data = await preferences.ReplaceAsync(request, cancellationToken) }));

    /// <summary>Paket I5: the spaced review choice (any of 7, 30 and 45 days; an empty list for none).</summary>
    [HttpPut("spaced-reviews")]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(typeof(StudyPlanPreferencesView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> SetSpacedReviews([FromBody] SpacedReviewChoice request, CancellationToken cancellationToken = default) =>
        StudyRoutineController.Respond(this, async () => Ok(new { success = true, data = await preferences.SetSpacedReviewsAsync(request, cancellationToken) }));
}

/// <summary>Paket H4a: a teacher or institution administrator reads a student's plan preferences; only the student can change them.</summary>
[ApiController, ApiVersion(1.0)]
[Authorize(Roles = "Teacher,InstitutionAdmin,InstitutionOwner")]
[Route("api/coaching/staff/students/{studentId:guid}/study/plan-preferences")]
public sealed class CoachingStaffStudyPlanPreferencesController(IStudyPlanPreferencesService preferences) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(StudyPlanPreferencesView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Get(Guid studentId, CancellationToken cancellationToken = default) => StudyRoutineController.Respond(this, async () =>
        Ok(new { success = true, data = await preferences.GetForStudentAsync(studentId, cancellationToken) }));
}
