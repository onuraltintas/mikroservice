using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/program-templates")]
[Authorize]
public sealed class ProgramTemplatesController(ILegacySpeedReadingPrograms programs) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ExerciseProgramTemplateSummary>> GetProgramTemplates(
        CancellationToken cancellationToken = default) =>
        programs.GetProgramTemplatesAsync(cancellationToken);
}
