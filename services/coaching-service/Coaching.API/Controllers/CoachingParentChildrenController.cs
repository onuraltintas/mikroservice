using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

/// <summary>
/// V3 (parent panel): what a linked parent sees of the child's Coaching work besides the existing student endpoints
/// (assignments, sessions, exams, progress). Every request asks Identity's authorize-read (product Coaching): a verified
/// link, the child's Coaching access and the child's "visible to my parent" switch.
/// </summary>
[ApiController, ApiVersion(1.0), Authorize(Roles = "Parent")]
[Route("api/coaching/parents/me/children/{studentId:guid}")]
public sealed class CoachingParentChildrenController(CoachingDbContext db, CoachingParentChildService parents, TimeProvider clock) : ControllerBase
{
    /// <summary>This week of the child (the child's own time zone): plan, overdue work, next session, exam countdown, attention items.</summary>
    [HttpGet("week")]
    public Task<IActionResult> Week(Guid studentId, CancellationToken ct = default) => Run(async () =>
    {
        var child = await parents.RequireAsync(studentId, ct);
        // The same overdue rules (and the same first page) as the student's own agenda.
        var page = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var result = await StudentDailyAgendaQuery.ReadAsync(db, studentId, "overdue", 1, clock.GetUtcNow(), ct);
            await tx.CommitAsync(ct);
            return result;
        });
        var overdue = page.Items.Take(10).Select(x => new ParentChildOverdueItem(x.Title, x.Date, x.Kind)).ToArray();
        return await parents.GetWeekAsync(child, overdue, ct);
    });

    /// <summary>Planned, completed and studied minutes of the last weeks (1–12, default 8), oldest first.</summary>
    [HttpGet("adherence")]
    public Task<IActionResult> Adherence(Guid studentId, int weeks = 8, CancellationToken ct = default) => Run(async () =>
        await parents.GetAdherenceAsync(await parents.RequireAsync(studentId, ct), weeks, ct));

    /// <summary>The teacher's weekly notes and decisions of the last weeks (1–12, default 4), newest first; read-only.</summary>
    [HttpGet("weekly-reviews")]
    public Task<IActionResult> WeeklyReviews(Guid studentId, int weeks = 4, CancellationToken ct = default) => Run(async () =>
        await parents.GetWeeklyReviewsAsync(await parents.RequireAsync(studentId, ct), weeks, ct));

    private async Task<IActionResult> Run<T>(Func<Task<T>> read)
    {
        try { return Ok(new { success = true, data = await read() }); }
        catch (BusinessRuleException error) when (error.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = error.Message, code = error.Code }); }
        catch (ArgumentException error)
        { return BadRequest(new { success = false, message = error.Message }); }
    }
}
