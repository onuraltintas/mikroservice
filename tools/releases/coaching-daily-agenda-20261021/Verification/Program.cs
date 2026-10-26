using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Domain.Enums;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var connection=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("WEEKLY_CONNECTION"));
if(args.Single()!="probe"||connection.Database!="coaching_daily_agenda_probe_20261021")throw new Exception("Restored probe database required.");
await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString,x=>x.MigrationsHistoryTable("__ef_migrations_history","coaching")).Options);
if(db.Database.HasPendingModelChanges()||(await db.Database.GetPendingMigrationsAsync()).Any())throw new Exception("Unexpected schema/model change.");
var student=Guid.Parse("473bd2d6-e3db-47c4-be56-eeedec0022ed");var actor=new Actor(student);
var now=DateTimeOffset.UtcNow;var clock=new FixedClock(now);var zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,zone).DateTime);
var plan=await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student&&x.Status==StudyPlanStatus.Active);
var studies=plan is null?[]:await db.StudyPlanTasks.AsNoTracking().Where(x=>x.RevisionId==plan.Id&&x.StudentId==student&&!x.IsCompleted).ToArrayAsync();
var assignments=await db.AssignmentStudents.AsNoTracking().Include(x=>x.Assignment).Where(x=>x.StudentId==student&&x.Assignment.Status==AssignmentStatus.Active&&x.SubmittedAt==null&&(x.Status==StudentAssignmentStatus.Assigned||x.Status==StudentAssignmentStatus.InProgress)).ToArrayAsync();
var decisions=await db.Set<WeeklyDecision>().AsNoTracking().Where(x=>x.StudentId==student&&(x.Status=="Planned"||x.Status=="InProgress")).ToArrayAsync();
var expected=new Dictionary<string,HashSet<string>>{{"today",[]},{"upcoming",[]},{"overdue",[]}};
foreach(var task in studies){var local=TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(plan?.WeeklySource?.TimeZoneId??"Europe/Istanbul"));var day=DateOnly.FromDateTime(local.DateTime);if(task.PlannedDate>day.AddDays(7))continue;var mode=task.PlannedDate<day||task.PlannedDate==day&&task.PlannedStartMinute.HasValue&&task.PlannedStartMinute+task.PlannedMinutes<=local.Hour*60+local.Minute?"overdue":task.PlannedDate==day?"today":"upcoming";expected[mode].Add("study:"+task.Id);}
foreach(var row in assignments){var due=TimeZoneInfo.ConvertTimeFromUtc(row.Assignment.DueDate,zone);if(DateOnly.FromDateTime(due)>today.AddDays(7))continue;expected[row.Assignment.DueDate<now.UtcDateTime?"overdue":DateOnly.FromDateTime(due)==today?"today":"upcoming"].Add("assignment:"+row.AssignmentId);}
foreach(var row in decisions){if(row.DueOn>today.AddDays(7))continue;expected[row.DueOn<today?"overdue":row.DueOn==today?"today":"upcoming"].Add("decision:"+row.Id);}
var controller=new StudentDailyAgendaController(db,new CoachingAccessPolicy(actor),clock);
foreach(var mode in expected.Keys){var found=new HashSet<string>();var page=1;while(true){if(await controller.Get(mode,page) is not OkObjectResult{Value:StudentDailyAgendaPage result})throw new Exception("Agenda request failed");if(result.TotalCount!=expected[mode].Count||result.TodayCount!=expected["today"].Count||result.UpcomingCount!=expected["upcoming"].Count||result.OverdueCount!=expected["overdue"].Count)throw new Exception("Counter mismatch");foreach(var row in result.Items)if(!found.Add(row.Kind+":"+row.RecordId))throw new Exception("Duplicate row");if(page*result.PageSize>=result.TotalCount)break;page++;}if(!found.SetEquals(expected[mode]))throw new Exception("Wrong rows or student scope");Console.WriteLine($"Restored agenda {mode}: {found.Count} exact rows; counts, paging and student scope passed.");}
if(await controller.Get("invalid") is not BadRequestObjectResult)throw new Exception("Invalid mode accepted");
actor.Role="Teacher";if(await controller.Get() is not ObjectResult{StatusCode:403})throw new Exception("Teacher accepted on student endpoint");
Console.WriteLine("Restored production daily agenda probe passed; no migrations or business writes.");
sealed class FixedClock(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
sealed class Actor(Guid id):ICurrentUserService{public string Role="Student";public Guid? UserId=>id;public string? Email=>null;public string? FullName=>"Test";public IEnumerable<string> Roles=>[Role];public System.Security.Claims.ClaimsPrincipal? User=>null;public bool IsAuthenticated=>true;}
