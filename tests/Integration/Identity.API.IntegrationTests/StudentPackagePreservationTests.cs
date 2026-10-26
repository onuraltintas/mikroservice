using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.IntegrationTests.Fixtures;
namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class StudentPackagePreservationTests(PostgresFixture postgres)
{
 [Fact]
 public async Task AdditiveMigrationsPreserveOldGoalCompletedWorkAndLegacyNet()
 {
  await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
  await db.Database.EnsureDeletedAsync();
  try
  {
   var migrations=db.Database.GetMigrations().ToArray();var added=migrations.Single(x=>x.EndsWith("StudentPrimaryGoalAndNetMetrics"));
   await db.GetService<IMigrator>().MigrateAsync(migrations[Array.IndexOf(migrations,added)-1]);
   // Rows are written with SQL in the schema of that time: today's model has columns those
   // migrations do not know yet.
   var student=Guid.NewGuid();var goalId=Guid.NewGuid();var revisionId=Guid.NewGuid();var taskId=Guid.NewGuid();var now=DateTime.UtcNow;
   await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO coaching.academic_goals (id,student_id,title,category,current_progress,is_completed,created_at,\"Version\",target_practice_exam_code,target_net) VALUES ({goalId},{student},{"Eski hedef"},{"ExamPreparation"},{40},{false},{now},{0},{"TYT"},{85m})");
   await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO coaching.study_plan_revisions (\"Id\",\"StudentId\",\"PlanId\",\"RevisionNumber\",\"Title\",\"Status\",\"IsActive\",\"CreatedAt\",\"Version\") VALUES ({revisionId},{student},{Guid.NewGuid()},{1},{"Korunan plan"},{1},{true},{now},{0})");
   await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO coaching.study_plan_tasks (\"Id\",\"RevisionId\",\"StudentId\",\"PlannedDate\",\"Title\",\"PlannedMinutes\",\"ActualMinutes\",\"IsPinned\",\"IsCompleted\",\"CompletedAt\",\"CreatedAt\",\"Version\",\"PlannedQuestions\",\"ActualQuestions\") VALUES ({taskId},{revisionId},{student},{new DateOnly(2026,10,23)},{"Tamamlanan"},{30},{25},{false},{true},{now},{now},{0},{10},{8})");
   var taskBefore=await db.Database.SqlQuery<string>($"SELECT to_jsonb(t)::text AS \"Value\" FROM coaching.study_plan_tasks t WHERE \"Id\"={taskId}").SingleAsync();
   var goalBefore=await db.Database.SqlQuery<string>($"SELECT to_jsonb(t)::text AS \"Value\" FROM coaching.academic_goals t WHERE id={goalId}").SingleAsync();
   await db.Database.MigrateAsync();
   // Additive: every value that existed before is still there, unchanged.
   Assert.True(await db.Database.SqlQuery<bool>($"SELECT to_jsonb(t) @> {goalBefore}::jsonb AS \"Value\" FROM coaching.academic_goals t WHERE id={goalId}").SingleAsync());
   Assert.True(await db.Database.SqlQuery<bool>($"SELECT to_jsonb(t) @> {taskBefore}::jsonb AS \"Value\" FROM coaching.study_plan_tasks t WHERE \"Id\"={taskId}").SingleAsync());
   var goal=await db.AcademicGoals.SingleAsync(x=>x.Id==goalId);
   Assert.Contains(goal.NetTargets,x=>x.ExamCode=="TYT"&&x.TargetNet==85m);Assert.Equal(40,goal.CurrentProgress);
   goal.SetNetTarget("AYT_SAY",60m);await db.SaveChangesAsync();db.ChangeTracker.Clear();
   Assert.Equal(2,(await db.AcademicGoals.SingleAsync(x=>x.Id==goalId)).NetTargets.Count);
   var task=await db.StudyPlanTasks.SingleAsync(x=>x.Id==taskId);
   Assert.Equal(25,task.ActualMinutes);Assert.Equal(8,task.ActualQuestions);
  }
  finally { await db.Database.EnsureDeletedAsync(); }
 }
}
