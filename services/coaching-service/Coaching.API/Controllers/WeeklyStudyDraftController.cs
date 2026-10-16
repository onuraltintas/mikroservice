using Asp.Versioning;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController,ApiVersion(1.0),Authorize(Roles="Student")]
[Route("api/coaching/study-planning/weekly-draft")]
public sealed class WeeklyStudyDraftController(IWeeklyStudyDraftService drafts):ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(CancellationToken ct)=>Respond(async()=>Ok(new{success=true,data=await drafts.GetAsync(ct)}));
    [HttpPut,RequestSizeLimit(128*1024),EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Save(WeeklyDraftInput r,CancellationToken ct)=>Respond(async()=>Ok(new{success=true,data=await drafts.SaveAsync(r,ct)}));
    private async Task<IActionResult> Respond(Func<Task<IActionResult>> action)
    {
        try{return await action();}
        catch(BusinessRuleException e) when(e.Code.StartsWith("Authorization.",StringComparison.Ordinal)){return StatusCode(403,new{message=e.Message,code=e.Code});}
        catch(BusinessRuleException e) when(e.Code=="StudyPlanning.Conflict"){return Conflict(new{message=e.Message,code=e.Code});}
        catch(ArgumentException e){return BadRequest(new{message=e.Message});}
        catch(TimeZoneNotFoundException){return BadRequest(new{message="Geçerli bir saat dilimi seçin."});}
        catch(InvalidTimeZoneException){return BadRequest(new{message="Saat dilimi kullanılamıyor."});}
        catch(KeyNotFoundException){return NotFound(new{message="Öğrenci kurulumu bulunamadı. Çalışma düzeninizi kurun."});}
    }
}
