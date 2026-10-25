using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TRACKING_CONNECTION"));
var op = args.Single();
if (op is not ("probe" or "seed" or "cleanup") || connection.Database != (op == "probe" ? "coaching_tracking_probe_20261020" : "coaching_db")) throw new InvalidOperationException("Unexpected operation or database.");
var teacher = Guid.Parse("b184504c-a3fa-4f7d-ad40-71fe418eaa10");
var student = Guid.Parse("473bd2d6-e3db-47c4-be56-eeedec0022ed");
var options = new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString).Options;
await using var db = new CoachingDbContext(options);
await using var tx = await db.Database.BeginTransactionAsync();
var user = new Actor(teacher); var accounts = new Accounts(teacher);
if (op == "probe")
{
    await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
    var api = new CoachingTeacherTrackingController(db, new CoachingAccessPolicy(user), new OwnedCoachingDirectory(db, accounts, user), accounts);
    var now = DateOnly.FromDateTime(DateTime.UtcNow);
    foreach (var mode in new[] { "all", "study", "assignment", "review" })
    {
        var result = await api.Get(now.AddDays(-30), now, mode);
        if (result is not OkObjectResult { Value: TeacherTrackingPage page }) throw new InvalidOperationException("Probe query failed.");
        Console.WriteLine(JsonSerializer.Serialize(new { mode, page.TotalCount, page.OverdueStudyCount, page.OverdueAssignmentCount, page.AwaitingReviewCount }));
    }
    await tx.RollbackAsync(); return;
}
var relationship = await db.CoachingTeacherStudentAssignments.SingleAsync(x => x.TeacherUserId == teacher && x.StudentUserId == student && x.IsActive);
var ids = Enumerable.Range(1, 26).Select(i => Guid.Parse($"ab202610-2026-4020-8020-{i:000000000000}")).ToArray();
var taskId = Guid.Parse("ab202610-2026-4020-9020-000000000001");
if (op == "seed")
{
    if (await db.Assignments.AnyAsync(x => Enumerable.Contains(ids, x.Id))) throw new InvalidOperationException("Fixture already exists.");
    for (var i = 0; i < ids.Length; i++)
    {
        var a = Assignment.Create(teacher, $"CANLI TAKİP TESTİ 20261020 · {i + 1:00}", DateTime.UtcNow.AddDays(-1), institutionId: relationship.InstitutionId);
        db.Entry(a).Property(x => x.Id).CurrentValue = ids[i];
        a.AssignToStudent(student); db.Add(a);
    }
    var plan = await db.StudyPlanRevisions.AsNoTracking().SingleAsync(x => x.StudentId == student && x.Status == StudyPlanStatus.Active);
    var task = StudyPlanTask.Create(plan, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), "CANLI TAKİP TESTİ 20261020 · Geçici çalışma", 30);
    task.Schedule(task.PlannedDate, 420);
    db.Entry(task).Property(x => x.Id).CurrentValue = taskId; db.Add(task);
    await db.SaveChangesAsync();
    Console.WriteLine(JsonSerializer.Serialize(new { operation = op, assignments = ids.Select(id => id.ToString()).ToArray() }));
}
else
{
    var assignments = await db.Assignments.Where(x => Enumerable.Contains(ids, x.Id)).ToArrayAsync();
    if (assignments.Length != 26 || assignments.Any(x => x.TeacherId != teacher || !x.Title.StartsWith("CANLI TAKİP TESTİ 20261020"))) throw new InvalidOperationException("Unexpected fixture records.");
    foreach (var a in assignments) if (a.Status != AssignmentStatus.Cancelled) a.Cancel();
    var task = await db.StudyPlanTasks.SingleAsync(x => x.Id == taskId);
    if (task.StudentId != student || task.IsCompleted || !task.Title.StartsWith("CANLI TAKİP TESTİ 20261020") || await db.Set<StudyPlanTaskOutcome>().AnyAsync(x => x.TaskId == taskId)) throw new InvalidOperationException("Temporary task changed; do not clean up.");
    db.Remove(task);
    await db.SaveChangesAsync(); Console.WriteLine(JsonSerializer.Serialize(new { operation = op, cancelled = assignments.Length }));
}
await tx.CommitAsync();

sealed class Actor(Guid id) : ICurrentUserService
{
    public Guid? UserId => id; public string? Email => null; public string? FullName => "Test öğretmeni";
    public IEnumerable<string> Roles => ["Teacher"]; public System.Security.Claims.ClaimsPrincipal? User => null;
    public bool IsAuthenticated => true;
}
sealed class Accounts(Guid teacher) : ICoachingAccountDirectory
{
    public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id => new CoachingAccount(id, id == teacher ? "Koç" : "Öğrenci", "Test", "test@example.test", true, true, [id == teacher ? "Teacher" : "Student"], null)).ToArray());
    public Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken ct) => throw new NotSupportedException();
    public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> IsActiveInstitutionAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
    public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer, Guid target, CancellationToken ct) => throw new NotSupportedException();
}
