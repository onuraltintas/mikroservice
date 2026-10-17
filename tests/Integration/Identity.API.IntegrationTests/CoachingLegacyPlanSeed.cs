using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

internal static class CoachingLegacyPlanSeed
{
    // Upgrade tests must write the baseline schema, not today's EF columns.
    internal static async Task InsertAsync(CoachingDbContext db,StudyPlanRevision plan,StudyPlanTask task)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.study_plan_revisions
              ("Id","StudentId","PlanId","RevisionNumber","Title","Status","IsActive","CreatedAt","UpdatedAt","Version")
            VALUES ({plan.Id},{plan.StudentId},{plan.PlanId},{plan.RevisionNumber},{plan.Title},{(int)plan.Status},{plan.IsActive},{plan.CreatedAt},{plan.UpdatedAt},{plan.Version});
            INSERT INTO coaching.study_plan_tasks
              ("Id","RevisionId","StudentId","TopicId","PlannedDate","Title","PlannedMinutes","ActualMinutes","IsPinned","IsCompleted","CompletedAt","CreatedAt","UpdatedAt","Version","SourceName","PlannedQuestions","ActualQuestions")
            VALUES ({task.Id},{task.RevisionId},{task.StudentId},{task.TopicId},{task.PlannedDate},{task.Title},{task.PlannedMinutes},{task.ActualMinutes},{task.IsPinned},{task.IsCompleted},{task.CompletedAt},{task.CreatedAt},{task.UpdatedAt},{task.Version},{task.SourceName},{task.PlannedQuestions},{task.ActualQuestions});
            """);
    }
}
