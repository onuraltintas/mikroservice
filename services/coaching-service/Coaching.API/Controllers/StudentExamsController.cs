using Asp.Versioning;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/exams")]
public sealed class StudentExamsController(IStudentExamService exams) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Respond(async () => Ok(new { success = true, data = await exams.ListAsync(pageNumber, pageSize, cancellationToken) }));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default) => Respond(async () => {
        var result = await exams.GetAsync(id, cancellationToken);
        return result is null ? NotFound(new { success = false, message = "Sonuç bulunamadı." }) : Ok(new { success = true, data = result });
    });

    [HttpPost]
    [RequestSizeLimit(64 * 1024)]
    [EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Create([FromBody] StudentExamInput request, CancellationToken cancellationToken = default) => Respond(async () => {
        var result = await exams.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, new { success = true, data = result });
    });

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(64 * 1024)]
    [EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Replace(Guid id, [FromBody] ReplaceStudentExamRequest request, CancellationToken cancellationToken = default)
        => Respond(async () => Ok(new { success = true, data = await exams.ReplaceAsync(id, request, cancellationToken) }));

    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Delete(Guid id, [FromQuery, BindRequired] int expectedVersion, CancellationToken cancellationToken = default)
        => Respond(async () => { await exams.DeleteAsync(id, expectedVersion, cancellationToken); return NoContent(); });

    private static async Task<IActionResult> Respond(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (BusinessRuleException ex) when (ex.Code == "Authorization.Forbidden")
        { return new ObjectResult(new { success = false, message = ex.Message, code = ex.Code }) { StatusCode = 403 }; }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict")
        { return new ConflictObjectResult(new { success = false, message = ex.Message, code = ex.Code }); }
        catch (ConcurrencyException)
        { return new ConflictObjectResult(new { success = false, message = "Sonuç değişti. Yeniden yükleyin." }); }
        catch (KeyNotFoundException)
        { return new NotFoundObjectResult(new { success = false, message = "Sonuç bulunamadı." }); }
        catch (ArgumentException)
        { return new BadRequestObjectResult(new { success = false, message = "Sınav bilgilerini, ders/konu seçimini ve soru sayılarını kontrol edin." }); }
    }
}
