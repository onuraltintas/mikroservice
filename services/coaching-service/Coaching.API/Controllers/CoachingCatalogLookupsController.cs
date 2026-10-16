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
    public async Task<IActionResult> Exams(CancellationToken ct) => Ok(new { success=true, data=await db.Set<StudyCatalogExam>().AsNoTracking()
        .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).Select(x => new { x.Id,x.Code,x.Name,x.IsActive }).ToArrayAsync(ct) });
}
