using System.Security.Claims;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var connection=Environment.GetEnvironmentVariable("VERIFY_CONNECTION")??throw new Exception("Missing connection");
var mode=Environment.GetEnvironmentVariable("VERIFY_SCOPE");
var name=new NpgsqlConnectionStringBuilder(connection).Database;
if(name!=(mode=="drill"?"coaching_clean_start_drill_20261018":mode=="live"?"coaching_db":"invalid"))throw new Exception("Wrong database");
await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection,n=>n.MigrationsHistoryTable("__ef_migrations_history","coaching")).Options);
Check(!(await db.Database.GetPendingMigrationsAsync()).Any(),"Pending migrations");
Check(!await db.StudyPlanRevisions.AnyAsync()&&!await db.StudyPlanTasks.AnyAsync()&&!await db.Set<StudyPlanTaskOutcome>().AnyAsync(),"Old test plans remain");
Check(!await db.Set<WeeklyStudyDraft>().AnyAsync(),"Old weekly draft remains");
Check(await db.Set<StudyCatalogOutcome>().CountAsync()==269&&await db.Set<OutcomeDurationEstimate>().CountAsync()==269,"Latest outcomes/estimates incomplete");
Check(await db.Set<StudyCatalogTopicOutcomeShare>().CountAsync()==319&&await db.Set<StudyCatalogTopicOutcome>().CountAsync()==319,"Unused mappings/shares remain");
Check(await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_schema='coaching' AND table_name='study_catalog_lessons' AND column_name='GroupId'").SingleAsync()==0,"Legacy group column remains");
Check(await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema='coaching' AND table_name='study_catalog_lesson_groups'").SingleAsync()==0,"Legacy group table remains");
var lessons=await db.StudyCatalogLessons.Where(x=>x.ExamCode=="LGS"&&x.IsActive).OrderBy(x=>x.Id).ToArrayAsync();Check(lessons.Length==6,"LGS lessons changed");
var ids=lessons.Select(x=>x.Id).ToArray();
var topics=await db.StudyCatalogTopics.Where(x=>Enumerable.Contains(ids,x.LessonId)&&x.IsActive&&!db.StudyCatalogTopics.Any(c=>c.ParentId==x.Id)).OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.Id).ToArrayAsync();
Check(topics.Length==144,"LGS topic catalog changed");
var actor=new Actor(Guid.Parse(Environment.GetEnvironmentVariable("VERIFY_ADMIN_ID")!));
var catalog=new CoachingOutcomeCatalogService(db,new GlobalScope(),actor);
var works=new Dictionary<Guid,List<WeeklyOutcomeWork>>();
foreach(var topic in topics)
{
    var published=await catalog.GetPublishedAsync(topic.Id,2026,default);
    Check(published.Count>0&&published.All(x=>x.IsTopicShare),"Missing approved topic scope");
    Check(published.Sum(x=>x.TotalMinutes)==topic.EstimatedMinutes,"Topic budget mismatch");
    if(!works.ContainsKey(topic.LessonId))works[topic.LessonId]=[];
    foreach(var row in published)works[topic.LessonId].Add(new(topic.Id,topic.Name,row.OutcomeId,row.Code,row.Name,row.EstimateId,row.EstimateNumber,row.TotalMinutes,true));
}
var weekly=lessons.Select((l,i)=>new WeeklyDraftSlot(Guid.NewGuid(),l.Id,l.Name,"LGS",(DayOfWeek)(i+1),1080,90)).ToArray();
var preview=WeeklyOutcomeScheduler.Build(new(2026,10,19),new(2027,6,1),weekly,lessons.Select(l=>new WeeklyLessonWork(l.Id,l.Name,works[l.Id])).ToArray(),false,[]);
Check(preview.Lessons.All(x=>x.RemainingMinutes==0)&&preview.Tasks.Sum(x=>x.Minutes)==11970&&preview.Tasks.Select(x=>x.Outcome.TopicId).Distinct().Count()==144,"Fresh annual preview incomplete");
var service=new CoachingCurriculumManagementService(db,new GlobalScope(),actor);
var exam=await db.Set<StudyCatalogExam>().SingleAsync(x=>x.Code=="LGS");
Check((await service.ChildrenAsync("lessons",exam.Id,null,1,default)).TotalCount==6,"Admin exam hierarchy broken");
try {await service.ChildrenAsync("groups",null,null,1,default);throw new Exception("Legacy groups still exposed");}catch(ArgumentException){}
Console.WriteLine(JsonSerializer.Serialize(new{passed=true,scope=mode,plans=0,tasks=0,weeklyDrafts=0,lessons=6,topics=144,outcomes=269,estimates=269,mappings=319,shares=319,annualMinutes=11970,annualTopics=144,legacyGroupsRemoved=true}));
static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
sealed class GlobalScope:ICoachingAdminScopeAuthorization{public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct)=>Task.FromResult(CoachingAdminScope.Global);}
sealed class Actor(Guid id):ICurrentUserService
{public Guid? UserId=>id;public IEnumerable<string> Roles=>["SystemAdmin"];public string? Email=>null;public string? FullName=>"Authorized clean start";public bool IsAuthenticated=>true;public ClaimsPrincipal? User=>null;}
