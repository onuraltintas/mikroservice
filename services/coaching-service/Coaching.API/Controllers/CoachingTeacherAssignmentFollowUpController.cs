using Coaching.Application.Authorization;
using Coaching.Application.Profiles;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

public sealed record TeacherAssignmentFollowUpItem(Guid AssignmentId,Guid StudentId,string StudentName,string Title,string? Subject,DateTime DueDate,string Status,bool IsOverdue,int OverdueMinutes);
public sealed record TeacherAssignmentFollowUpPage(IReadOnlyList<TeacherAssignmentFollowUpItem> Items,int TotalCount,int PageNumber,int PageSize,int PendingCount,int OverdueCount);

[ApiController,ApiVersion(1.0),Authorize(Roles="Teacher")]
[Route("api/coaching/teachers/me/assignment-follow-up")]
public sealed class CoachingTeacherAssignmentFollowUpController(CoachingDbContext db,ICoachingAccessPolicy access,
    OwnedCoachingDirectory directory,ICoachingAccountDirectory accounts,TimeProvider? clock=null):ControllerBase
{
 [HttpGet]
 public async Task<IActionResult> Get(string mode="overdue",int pageNumber=1,int pageSize=25,string? search=null,CancellationToken ct=default)
 {
  try
  {
   var teacher=access.RequireCurrentTeacher();
   if(mode is not ("overdue" or "pending")||pageNumber is <1 or >1000||pageSize is <1 or >100||search?.Length>150)
    return BadRequest(new{message="Filtre ve sayfa bilgilerini kontrol edin."});
   var institution=await directory.AuthorizeTeacherTargetsAsync(teacher,[],null,false,ct);
   var waiting=db.AssignmentStudents.AsNoTracking().Where(s=>s.Assignment.TeacherId==teacher&&s.Assignment.InstitutionId==institution
       &&s.Assignment.Status==AssignmentStatus.Active&&s.SubmittedAt==null
       &&(s.Status==StudentAssignmentStatus.Assigned||s.Status==StudentAssignmentStatus.InProgress));
   var studentIds=await waiting.Select(s=>s.StudentId).Distinct().ToArrayAsync(ct);
   var allowed=(await CoachingStudentReadAuthorization.GetAuthorizedStudentIdsAsync(access,directory,studentIds,ct)).ToArray();
   waiting=waiting.Where(s=>Enumerable.Contains(allowed,s.StudentId));
   var now=(clock??TimeProvider.System).GetUtcNow().UtcDateTime;
   var pendingCount=await waiting.CountAsync(ct);var overdueCount=await waiting.CountAsync(s=>s.Assignment.DueDate<now,ct);
   if(mode=="overdue")waiting=waiting.Where(s=>s.Assignment.DueDate<now);
   if(!string.IsNullOrWhiteSpace(search)){var term=search.Trim().ToLowerInvariant();waiting=waiting.Where(s=>s.Assignment.Title.ToLower().Contains(term));}
   var total=await waiting.CountAsync(ct);
   var rows=await waiting.OrderBy(s=>s.Assignment.DueDate).ThenBy(s=>s.AssignmentId).ThenBy(s=>s.StudentId)
       .Skip((pageNumber-1)*pageSize).Take(pageSize)
       .Select(s=>new{s.AssignmentId,s.StudentId,s.Assignment.Title,s.Assignment.Subject,s.Assignment.DueDate,s.Status}).ToArrayAsync(ct);
   var names=rows.Length==0?new Dictionary<Guid,string>():(await accounts.GetAccountsAsync(rows.Select(s=>s.StudentId).Distinct().ToArray(),ct))
       .Where(a=>a.IsEligible("Student")).ToDictionary(a=>a.UserId,a=>(a.FirstName+" "+a.LastName).Trim());
   var items=rows.Select(s=>new TeacherAssignmentFollowUpItem(s.AssignmentId,s.StudentId,names.GetValueOrDefault(s.StudentId,"Öğrenci"),s.Title,s.Subject,s.DueDate,s.Status.ToString(),s.DueDate<now,
       s.DueDate<now?(int)Math.Min(int.MaxValue,Math.Floor((now-s.DueDate).TotalMinutes)):0)).ToArray();
   return Ok(new TeacherAssignmentFollowUpPage(items,total,pageNumber,pageSize,pendingCount,overdueCount));
  }
  catch(BusinessRuleException e) when(e.Code.StartsWith("Authorization.",StringComparison.Ordinal)){return StatusCode(403,new{message=e.Message});}
 }
}
