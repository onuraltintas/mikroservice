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
  var connection=new Npgsql.NpgsqlConnectionStringBuilder(postgres.ConnectionString);
  Assert.Equal("test_db",connection.Database);Assert.Equal(postgres.Port,connection.Port);
  await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
  var migrations=db.Database.GetMigrations().ToArray();var added=migrations.Single(x=>x.EndsWith("StudentPrimaryGoalAndNetMetrics"));
  await db.GetService<IMigrator>().MigrateAsync(migrations[Array.IndexOf(migrations,added)-1]);
  var student=Guid.NewGuid();var goalId=Guid.NewGuid();var now=DateTime.UtcNow;
  await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO coaching.academic_goals (id,student_id,title,category,current_progress,is_completed,created_at,\"Version\",target_practice_exam_code,target_net) VALUES ({goalId},{student},{"Eski hedef"},{"ExamPreparation"},{40},{false},{now},{0},{"TYT"},{85m})");
  var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Korunan plan");plan.Activate();
  var task=StudyPlanTask.Create(plan,new(2026,10,23),"Tamamlanan",30,plannedQuestions:10);task.Complete(25,8);
  db.AddRange(plan,task);await db.SaveChangesAsync();db.ChangeTracker.Clear();
  var before=await db.Database.SqlQuery<string>($"SELECT md5(to_jsonb(t)::text) AS \"Value\" FROM coaching.study_plan_tasks t WHERE \"Id\"={task.Id}").SingleAsync();
  var goalBefore=await db.Database.SqlQuery<string>($"SELECT md5(to_jsonb(t)::text) AS \"Value\" FROM coaching.academic_goals t WHERE id={goalId}").SingleAsync();
  await db.Database.MigrateAsync();
  var goalAfter=await db.Database.SqlQuery<string>($"SELECT md5((to_jsonb(t)-'NetTargetsJson')::text) AS \"Value\" FROM coaching.academic_goals t WHERE id={goalId}").SingleAsync();
  Assert.Equal(goalBefore,goalAfter);
  var goal=await db.AcademicGoals.SingleAsync(x=>x.Id==goalId);
  Assert.Contains(goal.NetTargets,x=>x.ExamCode=="TYT"&&x.TargetNet==85m);Assert.Equal(40,goal.CurrentProgress);
  goal.SetNetTarget("AYT_SAY",60m);await db.SaveChangesAsync();db.ChangeTracker.Clear();
  Assert.Equal(2,(await db.AcademicGoals.SingleAsync(x=>x.Id==goalId)).NetTargets.Count);
  var after=await db.Database.SqlQuery<string>($"SELECT md5(to_jsonb(t)::text) AS \"Value\" FROM coaching.study_plan_tasks t WHERE \"Id\"={task.Id}").SingleAsync();
  Assert.Equal(before,after);
 }
}
