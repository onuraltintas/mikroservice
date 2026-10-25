using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using MassTransit.EntityFrameworkCoreIntegration;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Profiles;

public sealed partial class CoachingProfileStore
{
    private async Task MutateStudentAsync(Guid student, Func<Task> action, CancellationToken ct)
    {
        async Task Run()
        {
            var key="coaching-study-plan:"+student;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))",ct);
            await action();
        }
        if (!db.Database.IsRelational()) { await action(); return; }
        if (db.Database.CurrentTransaction is not null) { await Run(); return; }
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            var states=db.ChangeTracker.Entries<OutboxState>().ToDictionary(x=>x.Entity.OutboxId,x=>x.State);
            try { await Run(); await tx.CommitAsync(ct); }
            catch
            {
                foreach(var entry in db.ChangeTracker.Entries().Where(x=>x.Entity is WeeklyDecision or CoachingStudentProfile or CoachingTeacherStudentAssignment or OutboxMessage).ToArray()) entry.State=EntityState.Detached;
                foreach(var entry in db.ChangeTracker.Entries<OutboxState>().ToArray()) entry.State=states.GetValueOrDefault(entry.Entity.OutboxId,EntityState.Added);
                throw;
            }
        });
    }

    private async Task LockDecisionStudentAsync(Guid student,CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return;
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Decision handoff requires a transaction.");
        var key="coaching-study-plan:"+student;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))",ct);
    }

    private async Task TransferOpenDecisionsAsync(Guid teacher, Guid student, Guid? institution, Guid actor, CancellationToken ct)
    {
        await LockDecisionStudentAsync(student,ct);
        var stored=await db.CoachingTeacherStudentAssignments.Where(x=>x.StudentUserId==student && x.InstitutionId==institution).ToListAsync(ct);
        var active=stored.Concat(db.CoachingTeacherStudentAssignments.Local.Where(x=>x.StudentUserId==student && x.InstitutionId==institution))
            .DistinctBy(x=>x.Id).Where(x=>x.IsActive).Select(x=>x.TeacherUserId).ToHashSet();
        var rows=await db.Set<WeeklyDecision>().Where(x=>x.StudentId==student && x.HasAssignmentScope && x.InstitutionId==institution
            && x.ResponsibleTeacherId!=teacher && (x.Status=="Planned" || x.Status=="InProgress")).ToListAsync(ct);
        foreach (var row in rows.Where(x=>!active.Contains(x.ResponsibleTeacherId)))
        {
            row.TransferResponsibility(teacher,actor,(clock??TimeProvider.System).GetUtcNow().UtcDateTime);
            if(events is not null)
                await events.PublishAsync(new WeeklyDecisionChangedEvent(row.Id,student,teacher,row.ReviewWeekStart,false,true),ct);
        }
    }
}
