using Asp.Versioning;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController,ApiVersion(1.0),Authorize(Roles="Student"),Route("api/coaching/study-planning/weekly-outcome-plans")]
public sealed class WeeklyOutcomePlansController(IWeeklyOutcomePlanService service):ControllerBase
{
    [HttpGet("options")]
    public Task<IActionResult> Options(CancellationToken ct)=>Respond(async()=>Ok(new{success=true,data=await service.OptionsAsync(ct)}));
    [HttpPost("preview"),RequestSizeLimit(65536),EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Preview(WeeklyOutcomePlanRequest request,CancellationToken ct)=>Respond(async()=>Ok(new{success=true,data=await service.PreviewAsync(request,ct)}));
    [HttpPost("drafts"),RequestSizeLimit(65536),EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Create(WeeklyOutcomePlanSave request,CancellationToken ct)=>Respond(async()=>{var plan=await service.CreateAsync(request,ct);return CreatedAtAction(nameof(ManualStudyPlansController.Get),"ManualStudyPlans",new{id=plan.Id},new{success=true,data=plan});});
    private async Task<IActionResult> Respond(Func<Task<IActionResult>> action)
    {
        try{return await action();}
        catch(BusinessRuleException e)when(e.Code.StartsWith("Authorization.",StringComparison.Ordinal)){return StatusCode(403,new{message=e.Message});}
        catch(BusinessRuleException e)when(e.Code=="StudyPlanning.Conflict"){return Conflict(new{message=e.Message});}
        catch(KeyNotFoundException e){return NotFound(new{message=e.Message});}
        catch(ArgumentException e){return BadRequest(new{message=e.Message});}
    }
}
