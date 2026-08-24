using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/reading-texts")]
[Authorize]
public sealed class ReadingTextsController(ILegacySpeedReadingCatalog catalog) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ReadingTextSummary>> GetReadingTexts(
        [FromQuery] Guid? exerciseId,
        [FromQuery] string? category,
        [FromQuery] int? difficultyLevel,
        [FromQuery] string? searchTerm,
        [FromQuery] bool onlyWithQuestions = false,
        CancellationToken cancellationToken = default) =>
        catalog.GetReadingTextsAsync(
            exerciseId,
            category,
            difficultyLevel,
            searchTerm,
            onlyWithQuestions,
            cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReadingTextDetails>> GetReadingText(
        Guid id,
        [FromQuery] bool includeQuestions = true,
        CancellationToken cancellationToken = default)
    {
        var result = await catalog.GetReadingTextAsync(id, includeQuestions, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
