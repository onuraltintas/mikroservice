using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/exercises")]
[Authorize]
public sealed class ExercisesController(ILegacySpeedReadingCatalog catalog) : ControllerBase
{
    [HttpGet]
    public Task<SpeedReadingPage<ExerciseSummary>> GetExercises(
        [FromQuery] Guid? exerciseTypeId,
        [FromQuery] int? difficultyLevel,
        [FromQuery] Guid? targetAgeGroupId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        catalog.GetExercisesAsync(
            exerciseTypeId,
            difficultyLevel,
            targetAgeGroupId,
            pageNumber,
            pageSize,
            cancellationToken);
}
