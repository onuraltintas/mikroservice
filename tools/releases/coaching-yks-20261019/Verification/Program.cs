using System.Security.Claims;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var connection=Environment.GetEnvironmentVariable("VERIFY_CONNECTION")??throw new Exception("Missing verification connection");
var mode=Environment.GetEnvironmentVariable("VERIFY_SCOPE");var operation=Environment.GetEnvironmentVariable("VERIFY_OPERATION");
var database=new NpgsqlConnectionStringBuilder(connection).Database;
Check(database==(mode=="drill"?"coaching_yks_drill_20261019_r3":mode=="live"?"coaching_db":"invalid"),"Unexpected database");
Check(operation is "import" or "verify","Unexpected operation");
await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection,n=>n.MigrationsHistoryTable("__ef_migrations_history","coaching")).Options);
Check(!(await db.Database.GetPendingMigrationsAsync()).Any(),"Pending migrations");
var package=JsonSerializer.Deserialize<YksDataPackage>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"yks-2026-basis-v1.json")),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
Check(package.Topics.Count==1075&&package.Outcomes.Count==3184&&package.EquivalentTopics.Count==47,"Unexpected package coverage");
if(operation=="import")
{
 var actor=new Actor(Guid.Parse(Environment.GetEnvironmentVariable("VERIFY_ADMIN_ID")!));
 var importer=new CoachingYksCatalogImportService(db,new GlobalScope(),actor);
 var first=await importer.ImportAsync(package,"Kullanıcı tarafından onaylanan TYT, AYT ve yalnız İngilizce YDT aktarımı",default);
 Check(first==await importer.ImportAsync(package,"Aynı onaylı paketin mükerrer kayıt üretmediğini doğrula",default),"Import replay changed records");
}
var codes=package.Subjects.Select(s=>s.Code).ToArray();
var editions=await db.Set<OfficialCurriculumEdition>().Where(e=>Enumerable.Contains(codes,e.Code)).ToArrayAsync();Check(editions.Length==14,"Programs incomplete");
var editionIds=editions.Select(e=>e.Id).ToArray();
var outcomes=await db.Set<StudyCatalogOutcome>().Where(o=>Enumerable.Contains(editionIds,o.EditionId)).ToArrayAsync();
var ids=outcomes.Select(o=>o.Id).ToArray();Check(outcomes.Length==3184&&outcomes.Count(o=>o.SourceKind=="Official")==492&&outcomes.Count(o=>o.SourceKind=="ExamSkill")==2692,"Source classification incomplete");
Check(outcomes.All(o=>o.SourceKind=="Official"?o.SourcePage>0:o.SourcePage is null),"Invented source page");
var estimates=await db.Set<OutcomeDurationEstimate>().Where(e=>Enumerable.Contains(ids,e.OutcomeId)).ToArrayAsync();
Check(estimates.Length==3184&&estimates.All(e=>e.IsPublished&&e.Method=="ReviewedStudy"&&e.Confidence=="Low")&&estimates.Sum(e=>e.TotalMinutes)==68058,"Approved study budgets incomplete");
var mappings=await db.Set<StudyCatalogTopicOutcome>().Where(m=>Enumerable.Contains(ids,m.OutcomeId)).ToArrayAsync();
var shares=await db.Set<StudyCatalogTopicOutcomeShare>().Where(s=>Enumerable.Contains(ids,s.OutcomeId)).ToArrayAsync();
Check(mappings.Length==3249&&mappings.All(m=>m.IsApproved)&&shares.Length==3249,"Approved scope mappings incomplete");
foreach(var estimate in estimates)
{
 var portions=shares.Where(s=>s.EstimateId==estimate.Id).ToArray();
 Check(portions.Sum(s=>s.LearningMinutes)==estimate.LearningMinutes&&portions.Sum(s=>s.PracticeMinutes)==estimate.PracticeMinutes&&portions.Sum(s=>s.CheckMinutes)==estimate.CheckMinutes,"Scope budget mismatch");
}
Check(await db.StudyCatalogLessons.CountAsync(l=>l.ExamCode=="YDT"&&(l.Name.Contains("Arap")||l.Name.Contains("Arabic")))==0,"Arabic YDT remains");
Check(await db.Set<StudyCatalogOutcome>().CountAsync(o=>db.Set<OfficialCurriculumEdition>().Any(e=>e.Id==o.EditionId&&e.ExamCode=="LGS"))==269,"LGS outcomes changed");
var scopeCount=await db.Set<StudyCatalogYksScope>().CountAsync();Check(scopeCount==package.Topics.Sum(t=>t.Scopes.Count),"Exam scopes incomplete");
Console.WriteLine(JsonSerializer.Serialize(new{passed=true,mode,operation,programs=14,officialOutcomes=492,examSkills=2692,approvedEstimates=3184,topics=1075,equivalentTopics=47,mappings=mappings.Length,shares=shares.Length,scopeCount,studyMinutes=68058,arabicYdt=0}));
static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
sealed class GlobalScope:ICoachingAdminScopeAuthorization{public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct)=>Task.FromResult(CoachingAdminScope.Global);}
sealed class Actor(Guid id):ICurrentUserService
{public Guid? UserId=>id;public IEnumerable<string> Roles=>["SystemAdmin"];public string? Email=>null;public string? FullName=>"Authorized YKS release";public bool IsAuthenticated=>true;public ClaimsPrincipal? User=>null;}
