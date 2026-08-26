using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.QuestionBank;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/exam-questions")]
[Authorize]
public sealed class QuestionBankController(ISpeedReadingQuestionBank questionBank) : ControllerBase
{
    [HttpGet]
    public Task<QuestionBankPage> GetQuestions(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? examType = null,
        [FromQuery] int? difficulty = null,
        [FromQuery] int? category = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? ageGroupId = null,
        CancellationToken cancellationToken = default) =>
        questionBank.GetQuestionsAsync(
            pageNumber,
            pageSize,
            examType,
            difficulty,
            category,
            searchTerm,
            ageGroupId,
            cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetQuestion(Guid id, CancellationToken cancellationToken = default)
    {
        var question = await questionBank.GetQuestionAsync(id, cancellationToken);
        return question is null ? NotFound() : Ok(question);
    }

    [HttpPost]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> CreateQuestion(
        [FromBody] ExamQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await questionBank.CreateQuestionAsync(request, actorId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> UpdateQuestion(
        Guid id,
        [FromBody] ExamQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        try
        {
            return await questionBank.UpdateQuestionAsync(id, request, actorId, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> DeleteQuestion(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await questionBank.DeleteQuestionAsync(id, actorId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("{id:guid}/hard")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> HardDeleteQuestion(Guid id, CancellationToken cancellationToken = default) =>
        await questionBank.HardDeleteQuestionAsync(id, cancellationToken)
            ? NoContent()
            : NotFound();

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
