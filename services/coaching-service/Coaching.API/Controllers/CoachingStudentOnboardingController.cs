using Asp.Versioning;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

/// <summary>
/// Paket C (#3): the student's first-login wizard. Under api/coaching/students, which the subscription check leaves open: the
/// wizard comes before the subscription. The option lists are filtered and paged on the server.
/// </summary>
[ApiController, ApiVersion(1.0), Authorize(Roles = "Student")]
[Route("api/coaching/students/me/onboarding")]
public sealed class CoachingStudentOnboardingController(CoachingStudentOnboardingService onboarding) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(CancellationToken ct) => Run(() => onboarding.GetAsync(ct));

    [HttpPut("steps/personal"), RequestSizeLimit(4096), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Personal([FromBody] OnboardingPersonalStep request, CancellationToken ct) => Run(() => onboarding.SavePersonalAsync(request, ct));

    [HttpPut("steps/exam"), RequestSizeLimit(4096), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Exam([FromBody] OnboardingExamStep request, CancellationToken ct) => Run(() => onboarding.SaveExamAsync(request, ct));

    [HttpPut("steps/target"), RequestSizeLimit(4096), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Target([FromBody] OnboardingTargetStep request, CancellationToken ct) => Run(() => onboarding.SaveTargetAsync(request, ct));

    /// <summary>Steps 4–6: invite code, parent link and notifications use their own flows; this records that the student passed them.</summary>
    [HttpPut("steps/{step:int}"), RequestSizeLimit(1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Pass(int step, CancellationToken ct) => Run(() => onboarding.PassStepAsync(step, ct));

    [HttpPost("complete"), RequestSizeLimit(1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Complete(CancellationToken ct) => Run(() => onboarding.CompleteAsync(ct));

    [HttpGet("options/schools/cities"), EnableRateLimiting("target-catalog-search")]
    public Task<IActionResult> SchoolCities(CancellationToken ct) => Run(() => onboarding.SchoolCitiesAsync(ct));

    [HttpGet("options/schools/types"), EnableRateLimiting("target-catalog-search")]
    public Task<IActionResult> SchoolTypes([FromQuery] string? city, CancellationToken ct) => Run(() => onboarding.SchoolTypesAsync(city, ct));

    [HttpGet("options/schools"), EnableRateLimiting("target-catalog-search")]
    public Task<IActionResult> Schools([FromQuery] string? city, [FromQuery] string? type, [FromQuery] string? search, [FromQuery] int page = 1, CancellationToken ct = default) =>
        Run(() => onboarding.SchoolsAsync(city, type, search, page, ct));

    [HttpGet("options/programs/cities"), EnableRateLimiting("target-catalog-search")]
    public Task<IActionResult> ProgramCities([FromQuery] string? programType, [FromQuery] string? scoreType, CancellationToken ct) =>
        Run(() => onboarding.ProgramCitiesAsync(programType, scoreType, ct));

    [HttpGet("options/programs/universities"), EnableRateLimiting("target-catalog-search")]
    public Task<IActionResult> Universities([FromQuery] string? programType, [FromQuery] string? scoreType, [FromQuery] string? city, [FromQuery] string? search,
        CancellationToken ct) => Run(() => onboarding.UniversitiesAsync(programType, scoreType, city, search, ct));

    [HttpGet("options/programs"), EnableRateLimiting("target-catalog-search")]
    public Task<IActionResult> Programs([FromQuery] string? programType, [FromQuery] string? scoreType, [FromQuery] string? city, [FromQuery] string? university,
        [FromQuery] string? search, [FromQuery] int page = 1, CancellationToken ct = default) =>
        Run(() => onboarding.ProgramsAsync(programType, scoreType, city, university, search, page, ct));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(new { success = true, data = await action() }); }
        catch (BusinessRuleException e) when (e.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { success = false, code = e.Code, message = e.Message }); }
        catch (BusinessRuleException e) when (e.Code == "Onboarding.LocationsUnavailable")
        { return StatusCode(503, new { success = false, code = e.Code, message = e.Message }); }
        catch (BusinessRuleException e) when (e.Code.StartsWith("Onboarding.", StringComparison.Ordinal) || e.Code == "StudyPlanning.Conflict")
        { return Conflict(new { success = false, code = e.Code, message = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { success = false, code = "Profiles.NotFound", message = e.Message }); }
        catch (ArgumentException e) { return BadRequest(new { success = false, code = "Onboarding.Validation", message = e.Message }); }
    }
}
