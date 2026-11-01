using Asp.Versioning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize]
[Route("api/coaching/catalog/exams")]
public sealed class CoachingCatalogLookupsController(CoachingDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Exams(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Coaching.Domain.CoachingTime.Turkey));
        return Ok(new { success=true, data=await db.Set<StudyCatalogExam>().AsNoTracking()
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).Select(x => new { x.Id,x.Code,x.Name,x.IsActive,
                // KM1: ExamDate is now the next upcoming session (else the latest one); Sessions lists them all.
                ExamDate = x.Sessions.Where(s => s.Date > today).Select(s => (DateOnly?)s.Date).Min() ?? x.Sessions.Select(s => (DateOnly?)s.Date).Max(),
                Sessions = x.Sessions.OrderBy(s => s.Date).Select(s => new { s.Id, s.Date, s.Label, s.ApplicationStart, s.ApplicationEnd }).ToArray() }).ToArrayAsync(ct) });
    }
}
