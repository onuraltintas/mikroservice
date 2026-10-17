using Coaching.Application.Authorization;
using Coaching.Application.Profiles;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;
[ApiController,ApiVersion(1.0),Authorize(Roles="Teacher")]
[Route("api/coaching/teachers/me/students/{studentId:guid}/profile")]
public sealed class CoachingTeacherStudentProfileController(CoachingStudyPlanAccess access,CoachingProfileStore profiles,ICurrentUserService user):ControllerBase
{
 [HttpGet]
 public Task<IActionResult> Get(Guid studentId,CancellationToken ct)=>Respond(async()=>{
  await access.RequireTeacherWriteAsync(studentId,ct);
  var profile=await profiles.GetStudentAsync(studentId,ct);return profile is null?NotFound():Ok(profile);
 });
 [HttpPut,RequestSizeLimit(16384),EnableRateLimiting("study-planning-write")]
 public Task<IActionResult> Update(Guid studentId,TeacherStudentProfileUpdate request,CancellationToken ct)=>Respond(async()=>{
  await access.RequireTeacherWriteAsync(studentId,ct);
  return Ok(await profiles.UpdateTeacherStudentDetailsAsync(studentId,user.UserId!.Value,request,ct));
 });
 private async Task<IActionResult> Respond(Func<Task<IActionResult>> action)
 {
  try{return await action();}
  catch(BusinessRuleException e) when(e.Code.StartsWith("Authorization.")){return StatusCode(403,new{message=e.Message});}
  catch(BusinessRuleException e) when(e.Code=="Profiles.Conflict"){return Conflict(new{message=e.Message});}
  catch(Exception e) when(e is DbUpdateConcurrencyException or ConcurrencyException){return Conflict(new{message="Profil değişti. Yeniden yükleyin."});}
  catch(KeyNotFoundException){return NotFound();}
  catch(ArgumentException e){return BadRequest(new{message=e.Message});}
 }
}
