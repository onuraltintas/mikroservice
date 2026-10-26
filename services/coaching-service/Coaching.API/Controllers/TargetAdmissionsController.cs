using Asp.Versioning;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController,ApiVersion(1.0)]
[Authorize(Roles="Student,Teacher,InstitutionAdmin,InstitutionOwner")]
[EnableRateLimiting("target-catalog-search")]
[Route("api/coaching/study-planning/targets/admissions")]
public sealed class TargetAdmissionsController(CoachingDbContext db):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery]TargetAdmissionFilter filter,CancellationToken ct)
    {
        try{return Ok(new{success=true,data=await new TargetAdmissionSearchService(db).SearchAsync(filter,ct)});}
        catch(ArgumentException){return BadRequest(new{success=false,message="Hedef katalog filtrelerini kontrol edin."});}
    }
    [HttpGet("filters")]
    public async Task<IActionResult> Filters([FromQuery] TargetAdmissionFilter filter, CancellationToken ct = default)
    {
        try{return Ok(new{success=true,data=await new TargetAdmissionSearchService(db).FacetsAsync(filter,ct)});}
        catch(ArgumentException){return BadRequest(new{success=false,message="Sınav ve veri yılını kontrol edin."});}
    }
}
