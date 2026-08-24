using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EduPlatform.Shared.Contracts.Authorization;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/exercise-types")]
public sealed class ExerciseTypesController(ILegacySpeedReadingCatalog catalog) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<SpeedReadingPage<ExerciseTypeSummary>>> GetExerciseTypes(
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var canManageContent = User.Claims.Any(claim =>
            claim.Type == "permission" &&
            claim.Value == PlatformPermissions.SpeedReading.ContentManage);

        if (isActive == false && !canManageContent)
        {
            return Forbid();
        }

        var effectiveIsActive = isActive ?? (canManageContent ? null : true);
        return Ok(await catalog.GetExerciseTypesAsync(
            categoryId,
            effectiveIsActive,
            pageNumber,
            pageSize,
            cancellationToken));
    }
}
