using EduPlatform.Shared.Contracts.Reporting;
using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.API;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/internal/reporting")]
public sealed class InternalReportingController : ControllerBase
{
    private readonly IInstitutionRepository institutionRepository;

    public InternalReportingController(IInstitutionRepository institutionRepository)
    {
        this.institutionRepository = institutionRepository;
    }

    [HttpGet("speed-reading/institutions")]
    [AllowAnonymous]
    [InternalServiceKey]
    public async Task<ActionResult<SpeedReadingInstitutionScopeResponse>> GetSpeedReadingInstitutions(
        CancellationToken cancellationToken)
    {
        var institutions = await institutionRepository.GetSpeedReadingInstitutionScopeAsync(cancellationToken);
        return Ok(new SpeedReadingInstitutionScopeResponse(institutions));
    }
}
