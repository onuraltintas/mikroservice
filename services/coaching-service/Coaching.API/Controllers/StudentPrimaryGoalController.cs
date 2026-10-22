using Asp.Versioning;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/primary-goal")]
public sealed class StudentPrimaryGoalController(CoachingDbContext db, ICoachingAccessPolicy access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student)) return Forbid();
        var current = await db.Set<StudentPrimaryGoal>().AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student, ct);
        return Ok(new { data = new StudentPrimaryGoalView(current?.GoalId, current?.Version) });
    }
    [HttpPut]
    public async Task<IActionResult> Save(SelectPrimaryGoal request, CancellationToken ct)
    {
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student)) return Forbid();
        var goal = await db.AcademicGoals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.GoalId && x.StudentId == student, ct);
        if (goal is null) return NotFound();
        if (goal.Category != GoalCategory.ExamPreparation || goal.IsCompleted) return BadRequest(new { message = "Devam eden bir sınav hedefi seçin." });
        var context = await db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x => x.StudentUserId == student, ct);
        if (context?.Purpose == LearningPurpose.Lgs && goal.TargetUniversityProgramId.HasValue
            || context?.Purpose == LearningPurpose.Yks && goal.TargetSchoolId.HasValue)
            return BadRequest(new { message = "Hedef eğitim bağlamındaki sınavla eşleşmiyor." });
        if (context?.Purpose == LearningPurpose.Yks && goal.TargetUniversityProgramId is { } programId)
        {
            var score = await db.TargetUniversityPrograms.Where(x => x.Id == programId).Select(x => x.ScoreType).SingleAsync(ct);
            if (context.PrimaryScoreType is { } expected && score != expected && score != "TYT")
                return BadRequest(new { message = "Programın puan türü profilindeki alanla eşleşmiyor." });
        }
        if (context?.Purpose is not (LearningPurpose.Lgs or LearningPurpose.Yks))
            return BadRequest(new { message = "Önce profilinde sınav hazırlığını belirle." });
        var advanced = context.PrimaryScoreType switch { "SAY" => "AYT_SAY", "EA" => "AYT_EA", "SOZ" => "AYT_SOZ", "YDT" => "YDT_EN", _ => "TYT" };
        if (goal.NetTargets.Any(x => context.Purpose == LearningPurpose.Lgs ? x.ExamCode != "LGS" : x.ExamCode != "TYT" && x.ExamCode != advanced))
            return BadRequest(new { message = "Net hedefinin sınav kapsamı profilinle eşleşmiyor." });
        var current = await db.Set<StudentPrimaryGoal>().SingleOrDefaultAsync(x => x.StudentId == student, ct);
        if (request.ExpectedVersion != current?.Version) return Conflict(new { message = "Ana hedef değişti. Sayfayı yeniden yükleyin." });
        if (current is null) db.Add(current = StudentPrimaryGoal.Create(student, goal.Id)); else current.Select(goal.Id);
        try { await db.SaveChangesAsync(ct); }
        catch (Exception e) when (e is ConcurrencyException or DbUpdateConcurrencyException
            || e is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505" or "23503" } })
        { return Conflict(new { message = "Ana hedef değişti. Sayfayı yeniden yükleyin." }); }
        return Ok(new { data = new StudentPrimaryGoalView(current.GoalId, current.Version) });
    }
}
public sealed record StudentPrimaryGoalView(Guid? GoalId, int? Version);
public sealed record SelectPrimaryGoal(Guid GoalId, int? ExpectedVersion);
