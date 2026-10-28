using Coaching.Application.Authorization;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Coaching.API.Controllers;

public sealed record TeacherTrackingItem(string Kind, Guid RecordId, Guid StudentId, string StudentName,
    string Title, Guid? PlanId, DateOnly Date, int? StartMinute, int? Minutes, string TimeZoneId, DateOnly? WeekStart = null, string? ReviewReason = null);
public sealed record TeacherTrackingPage(IReadOnlyList<TeacherTrackingItem> Items, int TotalCount,
    int PageNumber, int PageSize, int OverdueStudyCount, int OverdueAssignmentCount, int AwaitingReviewCount,
    int AwaitingReflectionCount = 0, int OverdueDecisionCount = 0, int AwaitingWeeklyReviewCount = 0);

[ApiController, ApiVersion(1.0), Authorize(Roles = "Teacher")]
[Route("api/coaching/teachers/me/tracking")]
public sealed class CoachingTeacherTrackingController(CoachingDbContext db, ICoachingAccessPolicy access,
    OwnedCoachingDirectory directory, ICoachingAccountDirectory accounts, TimeProvider? clock = null) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get([FromQuery, BindRequired] DateOnly fromDate,
        [FromQuery, BindRequired] DateOnly toDate, string mode = "all", string? search = null,
        int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
        =>db.Database.CreateExecutionStrategy().ExecuteAsync(()=>GetCoreAsync(fromDate,toDate,mode,search,pageNumber,pageSize,ct));

    private async Task<IActionResult> GetCoreAsync(DateOnly fromDate,DateOnly toDate,string mode,string? search,int pageNumber,int pageSize,CancellationToken ct)
    {
        try
        {
            var teacher = access.RequireCurrentTeacher();
            StudyReportCalculator.ValidatePeriod(fromDate, toDate);
            if (mode is not ("all" or "study" or "assignment" or "review" or "reflection" or "decision" or "weekly-review") || search?.Length > 200
                || pageNumber < 1 || pageSize is < 1 or > 100)
                return BadRequest(new { message = "Filtre ve sayfa bilgilerini kontrol edin." });

            // Authorization and names come from Identity over HTTP. Resolve them before the snapshot so a slow
            // Identity response never holds a pooled connection inside an open transaction.
            var institution = await directory.AuthorizeTeacherTargetsAsync(teacher, [], null, false, ct);
            var candidates = await db.CoachingTeacherStudentAssignments.AsNoTracking()
                .Where(x => x.TeacherUserId == teacher && x.IsActive && x.InstitutionId == institution)
                .Select(x => x.StudentUserId).Distinct().ToArrayAsync(ct);
            var allowed = (await CoachingStudentReadAuthorization.GetAuthorizedStudentIdsAsync(access, directory, candidates, ct)).ToArray();
            var names = allowed.Length == 0 ? new Dictionary<Guid, string>()
                : (await accounts.GetAccountsAsync(allowed, ct)).Where(x => x.IsEligible("Student"))
                    .ToDictionary(x => x.UserId, x => (x.FirstName + " " + x.LastName).Trim());
            allowed = names.Keys.ToArray();
            await using var snapshot=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
            var now = (clock ?? TimeProvider.System).GetUtcNow();
            var plans = await db.StudyPlanRevisions.AsNoTracking()
                .Where(x => Enumerable.Contains(allowed, x.StudentId) && x.Status == StudyPlanStatus.Active)
                .Select(x => new { x.Id, x.StudentId, x.WeeklySource }).ToArrayAsync(ct);
            var planIds = plans.Select(x => x.Id).ToArray();
            var tasks = await db.StudyPlanTasks.AsNoTracking().Where(x => Enumerable.Contains(planIds, x.RevisionId)
                && Enumerable.Contains(allowed, x.StudentId) && !x.IsCompleted && x.PlannedDate >= fromDate && x.PlannedDate <= toDate)
                .Select(x => new { x.Id, x.RevisionId, x.StudentId, x.Title, x.PlannedDate, x.PlannedStartMinute, x.PlannedMinutes })
                .Take(10001).ToArrayAsync(ct);

            var turkey = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDate.ToDateTime(TimeOnly.MinValue), turkey);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), turkey);
            var utcNow = now.UtcDateTime;
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, turkey).DateTime);
            // Pending reflection and review backlogs include earlier weeks, even before the selected activity start.
            var missing = await CoachingReflectionBacklog.MissingAsync(db,allowed,today,toDate,ct);

            var decisions = await db.Set<WeeklyDecision>().AsNoTracking().Where(x => x.ResponsibleTeacherId == teacher && Enumerable.Contains(allowed, x.StudentId)
                && (x.Status == "Planned" || x.Status == "InProgress") && x.DueOn < today && x.DueOn >= fromDate && x.DueOn <= toDate)
                .Select(x => new { x.Id, x.StudentId, x.Title, x.DueOn, x.ReviewWeekStart }).Take(10001).ToArrayAsync(ct);
            var assignments = await db.AssignmentStudents.AsNoTracking()
                .Where(x => Enumerable.Contains(allowed, x.StudentId) && x.Assignment.TeacherId == teacher
                    && x.Assignment.InstitutionId == institution && x.Assignment.Status != AssignmentStatus.Cancelled
                    && (x.Assignment.Status == AssignmentStatus.Active && x.SubmittedAt == null
                        && (x.Status == StudentAssignmentStatus.Assigned || x.Status == StudentAssignmentStatus.InProgress)
                        && x.Assignment.DueDate < utcNow && x.Assignment.DueDate >= fromUtc && x.Assignment.DueDate < toUtc
                        || x.Status == StudentAssignmentStatus.Submitted && x.SubmittedAt >= fromUtc && x.SubmittedAt < toUtc))
                .Select(x => new { x.AssignmentId, x.StudentId, x.Assignment.Title, x.Assignment.DueDate, x.SubmittedAt, x.Status })
                .Take(10001).ToArrayAsync(ct);
            // Never show truncated totals as a complete result. The same bound is used by study overview.
            if (tasks.Length + assignments.Length + missing.Length + decisions.Length > 10000)
                return UnprocessableEntity(new { message = "Dönem çok fazla kayıt içeriyor. Daha kısa bir tarih aralığı seçin." });

            var planMap = plans.ToDictionary(x => x.Id);
            var items = new List<TeacherTrackingItem>();
            foreach (var task in tasks)
            {
                var plan = planMap[task.RevisionId];
                if (plan.StudentId != task.StudentId) continue;
                var zone = plan.WeeklySource?.TimeZoneId ?? "Europe/Istanbul";
                var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(zone));
                var planToday = DateOnly.FromDateTime(local.DateTime);
                if (task.PlannedDate > planToday || task.PlannedDate == planToday
                    && (!task.PlannedStartMinute.HasValue || task.PlannedStartMinute + task.PlannedMinutes > local.Hour * 60 + local.Minute)) continue;
                items.Add(new("study", task.Id, task.StudentId, names[task.StudentId], task.Title, task.RevisionId,
                    task.PlannedDate, task.PlannedStartMinute, task.PlannedMinutes, zone));
            }
            foreach (var row in assignments)
            {
                var review = row.Status == StudentAssignmentStatus.Submitted;
                var local = TimeZoneInfo.ConvertTimeFromUtc(review ? row.SubmittedAt!.Value : row.DueDate, turkey);
                items.Add(new(review ? "review" : "assignment", row.AssignmentId, row.StudentId, names[row.StudentId],
                    row.Title, null, DateOnly.FromDateTime(local), local.Hour * 60 + local.Minute, null, "Europe/Istanbul"));
            }
            items.AddRange(missing.Select(x => new TeacherTrackingItem("reflection", x.StudentId, x.StudentId, names[x.StudentId], "Haftalık değerlendirme bekleniyor", null, x.WeekStart.AddDays(6), null, null, "Europe/Istanbul", x.WeekStart)));
            items.AddRange(decisions.Select(x => new TeacherTrackingItem("decision", x.Id, x.StudentId, names[x.StudentId], x.Title, null, x.DueOn, null, null, "Europe/Istanbul", x.ReviewWeekStart)));
            var term = Normalize(search ?? "");
            var matching = items.Where(x => term.Length == 0 || Normalize(x.StudentName + " " + x.Title).Contains(term)).ToArray();
            var ordered=matching.Where(x=>mode=="all"||x.Kind==mode).Order(TrackingOrder).ToArray();
            var page=new PendingPageWindow(pageNumber,pageSize);var next=0;var reviewCount=0;
            await foreach(var review in CoachingReviewFollowUp.ScanPendingAsync(db,allowed,teacher,toDate,clock??TimeProvider.System,ct))
            {
                var item=new TeacherTrackingItem("weekly-review",review.Id,review.StudentId,names[review.StudentId],ReviewTitle(review.Reason),null,
                    review.WeekStart.AddDays(6),null,null,"Europe/Istanbul",review.WeekStart,review.Reason);
                if(term.Length>0&&!Normalize(item.StudentName+" "+item.Title).Contains(term))continue;
                reviewCount++;
                if(mode=="all") {
                    while(next<ordered.Length&&TrackingOrder.Compare(ordered[next],item)<=0)page.Add(ordered[next++]);
                    page.Add(item);
                }
                else if(mode=="weekly-review")page.Add(item);
            }
            while(next<ordered.Length)page.Add(ordered[next++]);
            var response=new TeacherTrackingPage(page.Items,page.TotalCount,page.PageNumber,pageSize,
                matching.Count(x=>x.Kind=="study"),matching.Count(x=>x.Kind=="assignment"),matching.Count(x=>x.Kind=="review"),
                matching.Count(x=>x.Kind=="reflection"),matching.Count(x=>x.Kind=="decision"),reviewCount);
            await snapshot.CommitAsync(ct);
            return Ok(response);
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { message = ex.Message }); }
        catch (ArgumentException)
        { return BadRequest(new { message = "Tarih aralığını, aramayı ve sayfa bilgilerini kontrol edin." }); }
    }

    private static string ReviewTitle(string reason)=>reason=="ResultsChanged"?"İnceleme sonrasında sonuçlar değişti":
        reason=="LegacyEvidenceMissing"?"Eski incelemenin sonuçları yeniden doğrulanmalı":"Haftalık değerlendirme inceleme bekliyor";
    private static readonly IComparer<TeacherTrackingItem> TrackingOrder=Comparer<TeacherTrackingItem>.Create((a,b)=> {
        var order=a.Date.CompareTo(b.Date);if(order!=0)return order;
        order=(a.StartMinute??-1).CompareTo(b.StartMinute??-1);if(order!=0)return order;
        order=StringComparer.Ordinal.Compare(a.Kind,b.Kind);if(order!=0)return order;
        order=a.RecordId.CompareTo(b.RecordId);if(order!=0)return order;
        order=a.StudentId.CompareTo(b.StudentId);return order!=0?order:Nullable.Compare(a.WeekStart,b.WeekStart);
    });
    // Exact totals without keeping the pending history in memory. Keep the requested page
    // and the last page so a completed last job can clamp a now-out-of-range request safely.
    private sealed class PendingPageWindow(int requestedPage,int size)
    {
        private readonly List<TeacherTrackingItem> selected=[];
        private readonly List<TeacherTrackingItem> last=[];
        private readonly long offset=((long)requestedPage-1)*size;
        public int TotalCount {get;private set;}
        private int LastPage=>TotalCount==0?1:(TotalCount-1)/size+1;
        public int PageNumber=>Math.Min(requestedPage,LastPage);
        public IReadOnlyList<TeacherTrackingItem> Items=>requestedPage>LastPage?last:selected;
        public void Add(TeacherTrackingItem item) {
            if(TotalCount%size==0)last.Clear();last.Add(item);
            if(TotalCount>=offset&&TotalCount<offset+size)selected.Add(item);
            TotalCount++;
        }
    }

    private static string Normalize(string value) => value.Trim().Replace('İ', 'i').Replace('ı', 'i').ToLowerInvariant();
}
