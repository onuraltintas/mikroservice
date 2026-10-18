using Asp.Versioning;
using Coaching.Application.Authorization;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize(Roles = "Student")]
[EnableRateLimiting("target-catalog-search")]
[Route("api/coaching/study-planning/topics/{topic:guid}/outcomes")]
public sealed class StudyOutcomeCatalogController(CoachingDbContext db, ICoachingAccessPolicy access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid topic, CancellationToken ct)
    {
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student)) return Forbid();
        var year = await CoachingOutcomeCatalogService.AcademicYearAsync(db, student, ct);
        var context=await db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentUserId==student,ct);
        return Ok(new { success = true, data = await CoachingOutcomeCatalogService.ReadPublishedAsync(db, topic, year, ct,context) });
    }
}
