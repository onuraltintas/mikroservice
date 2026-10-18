using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

try { await Run(); }
catch(Exception e) { Console.Error.WriteLine("Scope revision failed: "+e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);Environment.ExitCode=1; }

static async Task Run()
{
    var connection=Environment.GetEnvironmentVariable("VERIFY_CONNECTION")??throw new Exception("Missing connection");
    var mode=Environment.GetEnvironmentVariable("VERIFY_SCOPE");
    Check(mode is "drill" or "live","Invalid scope");
    Check(new NpgsqlConnectionStringBuilder(connection).Database==(mode=="live"?"coaching_db":"coaching_scope_shares_drill_20261018"),"Wrong database");
    Check(Environment.GetEnvironmentVariable("VERIFY_APPROVAL")=="user-approved-scope-shares-20261018","Explicit approval required");
    var actor=Guid.Parse(Environment.GetEnvironmentVariable("VERIFY_ADMIN_ID")??"");Check(actor!=Guid.Empty,"Admin required");
    var bytes=await File.ReadAllBytesAsync("/verification/lgs-scope-revision-2026-v2.json");
    var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
    var package=JsonSerializer.Deserialize<Revision>(bytes,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
    Check(package.Code=="lgs-scope-revision-20261018-v2"&&package.AcademicYearStart==2026&&package.Topics.Length==144
        &&package.Outcomes.Length==269&&package.Outcomes.Sum(x=>x.Shares.Length)==319,"Unexpected revision");
    await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection,
        n=>n.MigrationsHistoryTable("__ef_migrations_history","coaching")).Options);
    Check(!(await db.Database.GetPendingMigrationsAsync()).Any(),"Migration required");
    var oldPlans=await db.StudyPlanRevisions.CountAsync();var oldTasks=await db.StudyPlanTasks.CountAsync();
    var oldLinks=await db.Set<StudyPlanTaskOutcome>().CountAsync();
    var completed=await db.StudyPlanTasks.CountAsync(x=>x.IsCompleted);
    var actual=await db.StudyPlanTasks.SumAsync(x=>x.ActualMinutes??0);
    await using var tx=await db.Database.BeginTransactionAsync();
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(4188,100)");
    var sourceIds=package.Topics.Select(x=>x.SourceId).ToArray();
    var topics=await db.StudyCatalogTopics.Where(x=>x.Source=="approved-catalog"&&Enumerable.Contains(sourceIds,x.SourceId)&&x.IsActive).ToDictionaryAsync(x=>x.SourceId);
    Check(topics.Count==144,"Every approved topic must still exist");
    var lessons=await db.StudyCatalogLessons.Where(x=>x.ExamCode=="LGS"&&x.IsActive).ToDictionaryAsync(x=>x.Id);
    var editions=await db.Set<OfficialCurriculumEdition>().Where(x=>x.ExamCode=="LGS"&&x.AcademicYearStart==2026&&x.AcademicYearEnd==2026).ToDictionaryAsync(x=>x.Id);
    var editionIds=editions.Keys.ToArray();
    var outcomes=(await db.Set<StudyCatalogOutcome>().Where(x=>Enumerable.Contains(editionIds,x.EditionId)&&!x.IsArchived).ToArrayAsync())
        .ToDictionary(x=>(editions[x.EditionId].SubjectName,x.Code));
    Check(outcomes.Count==269,"Unexpected official outcome catalog");
    var desired=new Dictionary<(Guid Topic,Guid Outcome),string>();
    foreach(var topic in package.Topics)
    {
        var current=topics[topic.SourceId];
        Check(current.Name.Trim()==topic.Name&&lessons[current.LessonId].Name==topic.Lesson,"Topic ownership/name changed: "+JsonSerializer.Serialize(new{sourceId=topic.SourceId,actualName=current.Name,expectedName=topic.Name,actualLesson=lessons[current.LessonId].Name,expectedLesson=topic.Lesson}));
        foreach(var code in topic.OutcomeCodes)desired.Add((current.Id,outcomes[(topic.Lesson,code)].Id),topic.ScopeDescription);
    }
    var changed=0;var ids=outcomes.Values.Select(x=>x.Id).ToArray();
    var mappings=await db.Set<StudyCatalogTopicOutcome>().Where(x=>Enumerable.Contains(ids,x.OutcomeId)).ToDictionaryAsync(x=>(x.TopicId,x.OutcomeId));
    foreach(var (key,mapping) in mappings)
        if(!desired.ContainsKey(key)&&mapping.IsApproved)
        {mapping.Review(false,"Kapsam revizyonu: bu bağlantı tarihçe için korunuyor; yeni planda ilgili alt konu payını kullan.",actor);changed++;}
    foreach(var (key,scope) in desired)
    {
        var evidence="LGS kapsam revizyonu 18.10.2026: "+scope;
        if(!mappings.TryGetValue(key,out var mapping))
        {mapping=StudyCatalogTopicOutcome.Create(key.Topic,key.Outcome,evidence);db.Add(mapping);mappings.Add(key,mapping);}
        if(!mapping.IsApproved||mapping.Evidence!=evidence){mapping.Review(true,evidence,actor);changed++;}
    }
    foreach(var target in package.Topics)
    {
        var topic=topics[target.SourceId];var minutes=target.LearningMinutes+target.PracticeMinutes+target.CheckMinutes;
        if(topic.EstimatedMinutes!=minutes){topic.Edit(topic.Name,topic.DisplayOrder,minutes);changed++;}
    }
    var retired=await db.StudyCatalogTopics.SingleAsync(x=>x.Source=="approved-catalog"&&x.SourceId==package.RetireUnusedTopicSourceId);
    if(retired.IsActive)
    {
        Check(!await db.StudyPlanTasks.AnyAsync(x=>x.TopicId==retired.Id)
            &&!await db.Set<StudentTopicState>().AnyAsync(x=>x.TopicId==retired.Id),"Do not retire a topic referenced by student work");
        retired.SetActive(false);changed++;
    }
    await db.SaveChangesAsync();
    var inserted=0;
    foreach(var source in package.Outcomes)
    {
        var outcome=outcomes[(source.Lesson,source.Code)];Check(editions[outcome.EditionId].Code==source.EditionCode,"Official edition changed");
        var evidence=source.Evidence+" Paket SHA256: "+hash;
        var versions=await db.Set<OutcomeDurationEstimate>().Where(x=>x.OutcomeId==outcome.Id).ToArrayAsync();
        var existing=versions.SingleOrDefault(x=>x.Evidence==evidence);
        if(existing is not null)
        {
            Check(existing.IsPublished&&existing.Number==versions.Where(x=>x.IsPublished).Max(x=>x.Number)
                &&existing.LearningMinutes==source.LearningMinutes&&existing.PracticeMinutes==source.PracticeMinutes&&existing.CheckMinutes==source.CheckMinutes,"Revision was edited after publication");
            continue;
        }
        var prior=versions.Where(x=>x.IsPublished).MaxBy(x=>x.Number)!;
        Check(prior.LearningMinutes==source.ExpectedPriorLearning&&prior.PracticeMinutes==source.ExpectedPriorPractice&&prior.CheckMinutes==source.ExpectedPriorCheck,"Published duration changed since review");
        var estimate=OutcomeDurationEstimate.Create(outcome.Id,versions.Max(x=>x.Number)+1,source.LearningMinutes,source.PracticeMinutes,source.CheckMinutes,
            "ReviewedStudy",source.Confidence,evidence,actor);
        var shares=source.Shares.Select(x=>StudyCatalogTopicOutcomeShare.Create(topics[x.TopicSourceId].Id,estimate,x.LearningMinutes,x.PracticeMinutes,x.CheckMinutes,x.ScopeDescription)).ToArray();
        OutcomeScopeBudget.Validate(estimate,shares,desired.Keys.Where(x=>x.Outcome==outcome.Id).Select(x=>x.Topic).ToArray());
        estimate.Publish();db.Add(estimate);db.AddRange(shares);inserted++;
    }
    if(inserted>0||changed>0)
        db.AdminAuditRecords.Add(new AdminAuditRecord(Guid.NewGuid(),DateTimeOffset.UtcNow,"Coaching",actor.ToString(),"SystemAdmin",null,"COMMAND",
            "/controlled-import/lgs-scope-shares",200,Guid.NewGuid().ToString(),null,null,"OutcomeScopeRevision","StudyOutcome",package.Code,
            JsonSerializer.Serialize(new{reason="Kullanıcının ortak kazanım, süre payı ve Din Kültürü eşleştirmelerini düzeltme talimatı",fingerprint=hash})));
    await db.SaveChangesAsync();
    var reader=new CoachingOutcomeCatalogService(db,new GlobalScope(),new Actor(actor));
    var resolved=new List<ResolvedStudyOutcome>();var work=new Dictionary<Guid,List<WeeklyOutcomeWork>>();
    foreach(var source in package.Topics)
    {
        var topic=topics[source.SourceId];var published=await reader.GetPublishedAsync(topic.Id,2026,default);
        Check(published.Count==source.OutcomeCodes.Length&&published.All(x=>x.IsTopicShare&&source.OutcomeCodes.Contains(x.Code)),"Published topic scope mismatch");
        Check(published.Sum(x=>x.LearningMinutes)==source.LearningMinutes&&published.Sum(x=>x.PracticeMinutes)==source.PracticeMinutes
            &&published.Sum(x=>x.CheckMinutes)==source.CheckMinutes,"Topic component budgets changed");
        if(!work.ContainsKey(topic.LessonId))work[topic.LessonId]=[];
        foreach(var row in published)
        {
            resolved.Add(new(topic.Id,row.OutcomeId,row.EstimateId,row.TotalMinutes,"Catalog",row.Code,row.Name,true));
            work[topic.LessonId].Add(new(topic.Id,topic.Name,row.OutcomeId,row.Code,row.Name,row.EstimateId,row.EstimateNumber,row.TotalMinutes,true));
        }
    }
    var portions=StudyOutcomeAllocation.Deduplicate(resolved);
    Check(portions.Count==319&&portions.Select(x=>x.OutcomeId).Distinct().Count()==269&&portions.Sum(x=>x.Minutes)==11970,"Whole curriculum scope conservation failed");
    var lessonOrder=work.Keys.OrderBy(x=>x).ToArray();
    var slots=lessonOrder.Select((id,i)=>new WeeklyDraftSlot(Guid.NewGuid(),id,lessons[id].Name,"LGS",(DayOfWeek)(i+1),1080,90)).ToArray();
    var preview=WeeklyOutcomeScheduler.Build(new(2026,10,19),new(2027,6,1),slots,
        lessonOrder.Select(id=>new WeeklyLessonWork(id,lessons[id].Name,work[id])).ToArray(),false,[]);
    Check(preview.Lessons.All(x=>x.RemainingMinutes==0)&&preview.Tasks.Sum(x=>x.Minutes)==11970
        &&preview.Tasks.Select(x=>x.Outcome.TopicId).Distinct().Count()==144,"Annual preview skipped a section");
    Check(oldPlans==await db.StudyPlanRevisions.CountAsync()&&oldTasks==await db.StudyPlanTasks.CountAsync()
        &&oldLinks==await db.Set<StudyPlanTaskOutcome>().CountAsync(),"Existing plan/work counts changed");
    await tx.CommitAsync();
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,scope=mode,newDurationVersions=inserted,metadataChanges=changed,
        outcomes=269,topicShares=319,activeTopics=144,newStudyMinutes=11970,annualPreviewTopics=144,
        preservedPlans=oldPlans,preservedTasks=oldTasks,preservedOutcomeLinks=oldLinks,preservedCompleted=completed,preservedActualMinutes=actual,
        lessons=package.Outcomes.GroupBy(x=>x.Lesson).Select(g=>new{lesson=g.Key,minutes=g.Sum(x=>x.LearningMinutes+x.PracticeMinutes+x.CheckMinutes)})}));
}
static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
record Revision(string Code,int AcademicYearStart,string RetireUnusedTopicSourceId,TopicChange[] Topics,OutcomeChange[] Outcomes);
record TopicChange(string SourceId,string Name,string Lesson,string ScopeDescription,string[] OutcomeCodes,int LearningMinutes,int PracticeMinutes,int CheckMinutes);
record OutcomeChange(string Lesson,string Code,string ExamCode,string EditionCode,string Confidence,string Evidence,int ExpectedPriorLearning,int ExpectedPriorPractice,
    int ExpectedPriorCheck,int LearningMinutes,int PracticeMinutes,int CheckMinutes,ShareChange[] Shares);
record ShareChange(string TopicSourceId,string ScopeDescription,int LearningMinutes,int PracticeMinutes,int CheckMinutes);
sealed class GlobalScope:ICoachingAdminScopeAuthorization{public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct)=>Task.FromResult(CoachingAdminScope.Global);}
sealed class Actor(Guid id):ICurrentUserService
{public Guid? UserId=>id;public IEnumerable<string> Roles=>["SystemAdmin"];public string? Email=>null;public string? FullName=>"Authorized scope revision";public bool IsAuthenticated=>true;public ClaimsPrincipal? User=>null;}
