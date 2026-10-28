using System.Security.Cryptography;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingWeeklyOutcomePlanService(CoachingDbContext db,ICoachingAccessPolicy access,ILearningSetupService setup,TimeProvider clock,CoachingStudyPlanAccess? staffAccess=null):IWeeklyOutcomePlanService
{
    private Guid Student()=>access.CurrentUserId is {} id&&access.IsCurrentStudent(id)?id:throw new BusinessRuleException("Authorization.Forbidden","Yalnız kendi öğrenci hesabınızla kullanabilirsiniz.");
    private async Task<WeeklyStudyDraft> Draft(Guid student,CancellationToken ct)=>await db.Set<WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student,ct)
        ??throw new KeyNotFoundException("Önce haftalık ders taslağınızı kaydedin.");
    public async Task<WeeklyOutcomePlanOptions> OptionsAsync(CancellationToken ct)
    {
        return await Options(Student(),ct);
    }
    public async Task<WeeklyOutcomePlanOptions> OptionsForStudentAsync(Guid student,CancellationToken ct)
    {
        if(staffAccess is null)throw Forbidden();await staffAccess.RequireAsync(student,ct);return await Options(student,ct);
    }
    private async Task<WeeklyOutcomePlanOptions> Options(Guid student,CancellationToken ct)
    {
        var draft=await Draft(student,ct);var today=Today(draft.TimeZoneId);
        var codes=draft.Slots.Select(x=>x.ExamCode).Where(x=>x is not null).Distinct().ToArray();
        var rows=await db.Set<StudyCatalogExam>().AsNoTracking().Where(x=>Enumerable.Contains(codes,x.Code)&&x.IsActive).OrderBy(x=>x.Code).ToArrayAsync(ct);
        var exams=rows.Select(x=>new WeeklyExamReference(x.Name,x.ExamDate)).ToArray();
        // Roadmap O7: the plan ends the day before the exam: the student's own date, else the admin's nearest upcoming date.
        var catalogDate=rows.Where(x=>x.ExamDate>today).Select(x=>x.ExamDate).Min();
        var own=await db.StudentLearningContexts.AsNoTracking().Where(x=>x.StudentUserId==student).Select(x=>x.TargetExamDate).SingleOrDefaultAsync(ct);
        var (examDate,source)=own>today?(own,"Student"):catalogDate.HasValue?(catalogDate,"Catalog"):((DateOnly?)null,"Default");
        var end=examDate is {} date?(date.AddDays(-1)<today?today:date.AddDays(-1)):WeeklyOutcomeScheduler.DefaultEnd(today);
        return new(today,end,draft.Version,draft.TimeZoneId,exams,examDate,source,catalogDate);
    }
    private DateOnly Today(string zoneId)=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),TimeZoneInfo.FindSystemTimeZoneById(zoneId)).DateTime);

    public async Task<WeeklyOutcomePlanOptions> SetExamDateAsync(WeeklyExamDateRequest request,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);var student=Student();var draft=await Draft(student,ct);
        var context=await db.StudentLearningContexts.SingleOrDefaultAsync(x=>x.StudentUserId==student,ct)
            ??throw new KeyNotFoundException("Önce çalışma kurulumunu tamamlayın.");
        context.SetTargetExamDate(request.ExamDate,Today(draft.TimeZoneId));
        await db.SaveChangesAsync(ct);
        return await Options(student,ct);
    }
    public async Task<WeeklyOutcomePlanPreview> PreviewAsync(WeeklyOutcomePlanRequest request,CancellationToken ct)=>(await Prepare(request,Student(),ct)).Preview;
    public async Task<WeeklyOutcomePlanPreview> PreviewForStudentAsync(Guid student,WeeklyOutcomePlanRequest request,CancellationToken ct)
    {if(staffAccess is null)throw Forbidden();await staffAccess.RequireAsync(student,ct);return (await Prepare(request,student,ct)).Preview;}

    private sealed record Prepared(WeeklyOutcomePlanPreview Preview,WeeklyPlanSource Source,StudyPlanTask[] Protected,StudyPlanTaskOutcome[] PinnedLinks, CoachingStudyContinuation.Placement[] Placements);
    private async Task<Prepared> Prepare(WeeklyOutcomePlanRequest request,Guid student,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(request.EngineVersion is not (1 or WeeklyPlanEngine.CurrentVersion))throw new ArgumentException("Bilinmeyen plan motoru sürümü.");
        var v2=request.EngineVersion==WeeklyPlanEngine.CurrentVersion;
        var draft=await Draft(student,ct);
        if(request.ExpectedWeeklyDraftVersion<0||request.ExpectedWeeklyDraftVersion!=draft.Version||draft.Slots.Count==0)throw Conflict("Haftalık taslak boş veya değişmiş. Takvimi yeniden yükleyin.");
        var choices=(staffAccess is not null && access.CurrentUserId != student
            ? await setup.GetCatalogForStudentAsync(student,null,1,ct)
            : await setup.GetCatalogAsync(null,1,ct)).Lessons.ToDictionary(x=>x.Id);
        var lessonIds=draft.Slots.Select(x=>x.LessonId).Distinct().OrderBy(x=>x).ToArray();
        if(lessonIds.Any(x=>!choices.ContainsKey(x)))throw Conflict("Takvimde artık kullanılamayan ders var. Haftalık taslağı güncelleyin.");
        var context=await db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentUserId==student,ct);
        var year=context?.AcademicYearStart??(clock.GetUtcNow().Month>=9?clock.GetUtcNow().Year:clock.GetUtcNow().Year-1);
        var catalogTopics=await (from t in YksCatalogEligibility.Topics(db,context).AsNoTracking() join u in db.StudyCatalogUnits on t.UnitId equals u.Id
            where Enumerable.Contains(lessonIds,t.LessonId)&&t.IsActive&&u.IsActive&&u.LessonId==t.LessonId
                &&!db.StudyCatalogTopics.Any(c=>c.ParentId==t.Id)
                &&(t.ParentId==null||db.StudyCatalogTopics.Any(p=>p.Id==t.ParentId&&p.IsActive))
            orderby u.DisplayOrder,t.ParentId==null?t.DisplayOrder:db.StudyCatalogTopics.Where(p=>p.Id==t.ParentId).Select(p=>p.DisplayOrder).First(),t.DisplayOrder,t.Id
            select new {Topic=t,UnitOrder=u.DisplayOrder}).ToArrayAsync(ct);
        var topicStates=context is null?new Dictionary<Guid,DeclaredTopicStatus>():await db.Set<StudentTopicState>().AsNoTracking()
            .Where(x=>x.ContextId==context.Id).ToDictionaryAsync(x=>x.TopicId,x=>x.DeclaredStatus,ct);
        var completedTopicIds=topicStates.Where(x=>x.Value==DeclaredTopicStatus.Studied).Select(x=>x.Key).ToArray();
        // Version 1 drops topics the student declared studied; version 2 keeps them as a short refresh (WeeklyPlanEngine.Shape).
        var topics=v2?catalogTopics:catalogTopics.Where(x=>!completedTopicIds.Contains(x.Topic.Id)).ToArray();
        var topicIds=topics.Select(x=>x.Topic.Id).ToArray();
        var rows=await (from m in db.Set<StudyCatalogTopicOutcome>().AsNoTracking()
            join t in db.StudyCatalogTopics on m.TopicId equals t.Id
            join l in db.StudyCatalogLessons on t.LessonId equals l.Id
            join o in db.Set<StudyCatalogOutcome>() on m.OutcomeId equals o.Id
            join edition in db.Set<OfficialCurriculumEdition>() on o.EditionId equals edition.Id
            join e in db.Set<OutcomeDurationEstimate>().Where(e=>e.IsPublished&&!db.Set<OutcomeDurationEstimate>().Any(n=>n.OutcomeId==e.OutcomeId&&n.IsPublished&&n.Number>e.Number)) on o.Id equals e.OutcomeId into estimates
            from estimate in estimates.DefaultIfEmpty()
            join portion in db.Set<StudyCatalogTopicOutcomeShare>()
                on new { TopicId=t.Id,OutcomeId=o.Id,EstimateId=estimate==null?Guid.Empty:estimate.Id }
                equals new { portion.TopicId,portion.OutcomeId,portion.EstimateId } into portions
            from share in portions.DefaultIfEmpty()
            where Enumerable.Contains(topicIds,m.TopicId)&&m.IsApproved&&!o.IsArchived&&edition.AcademicYearStart<=year&&edition.AcademicYearEnd>=year
                &&(context==null||context.Purpose!=LearningPurpose.Yks||db.Set<StudyCatalogYksScope>().Any(s=>s.TopicId==t.Id&&s.ScoreType==context.PrimaryScoreType
                    &&(s.ExamCode!="YDT"||s.ForeignLanguage==context.ForeignLanguage)&&!Enumerable.Contains(s.ExcludedOutcomeIds,o.Id)))
                &&((edition.ExamCode==l.ExamCode&&edition.SubjectName==l.Name&&(edition.GradeNumber==null||l.GradeNumber==null||edition.GradeNumber==l.GradeNumber))
                    ||db.Set<OfficialCurriculumLessonBinding>().Any(b=>b.EditionId==edition.Id&&b.LessonId==l.Id))
            select new {TopicId=t.Id,Outcome=o,Estimate=estimate,Share=share,
                RequiresShare=estimate!=null&&db.Set<StudyCatalogTopicOutcomeShare>().Any(s=>s.EstimateId==estimate.Id)}).ToArrayAsync(ct);
        var issues=new List<WeeklyLessonIssue>();var work=new List<WeeklyLessonWork>();var seen=new HashSet<(Guid Outcome,Guid Topic)>();
        foreach(var lessonId in lessonIds)
        {
            var lesson=choices[lessonId];var lessonTopics=topics.Where(x=>x.Topic.LessonId==lessonId).ToArray();var outcomes=new List<WeeklyOutcomeWork>();
            if(lessonTopics.Length==0)
            {
                if(catalogTopics.Any(x=>x.Topic.LessonId==lessonId))continue;
                issues.Add(new(lessonId,lesson.Name,"Aktif konu ve kazanım bulunamadı."));
            }
            foreach(var topic in lessonTopics)
            {
                var mapped=rows.Where(x=>x.TopicId==topic.Topic.Id).OrderBy(x=>x.Outcome.DisplayOrder).ThenBy(x=>x.Outcome.Code,StringComparer.Ordinal).ThenBy(x=>x.Outcome.Id).ToArray();
                if(mapped.Length==0)issues.Add(new(lessonId,lesson.Name,topic.Topic.Name+": onaylı kazanım eşleştirmesi yok."));
                foreach(var row in mapped)
                {
                    if(row.Estimate is null){issues.Add(new(lessonId,lesson.Name,row.Outcome.Code+": yayımlanmış çalışma süresi yok."));continue;}
                    if(row.RequiresShare&&row.Share is null){issues.Add(new(lessonId,lesson.Name,topic.Topic.Name+": kapsam süre payı eksik."));continue;}
                    if(seen.Add((row.Outcome.Id,row.Share is null?Guid.Empty:topic.Topic.Id)))
                    {
                        var total=row.Share?.TotalMinutes??row.Estimate.TotalMinutes;var practice=row.Share?.PracticeMinutes??row.Estimate.PracticeMinutes;
                        outcomes.Add(new(topic.Topic.Id,topic.Topic.Name,row.Outcome.Id,row.Outcome.Code,row.Outcome.Name,row.Estimate.Id,row.Estimate.Number,
                            total,row.Share is not null,PracticeShare:total>0?Math.Round(practice/(double)total,3):0));
                    }
                }
            }
            work.Add(new(lessonId,lesson.Name,outcomes));
        }
        var preferences=context is null?new Dictionary<Guid,WeeklyPlanEngine.LessonPreference>():await db.StudentCoursePreferences.AsNoTracking()
            .Where(x=>x.ContextId==context.Id).ToDictionaryAsync(x=>x.LessonId,x=>new WeeklyPlanEngine.LessonPreference(x.Priority,x.SelfReportedDifficulty),ct);
        var examErrorTopics=v2?await ExamErrorTopicsAsync(student,ct):new HashSet<Guid>();
        if(v2)work=WeeklyPlanEngine.Shape(work,topicStates,preferences,examErrorTopics).ToList();
        var active=await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student&&x.IsActive,ct);
        var sources=await CoachingStudyContinuation.SourcesAsync(db,student,ct);
        var completed=active is null?[]:await db.StudyPlanTasks.AsNoTracking().Where(x=>x.StudentId==student&&x.RevisionId==active.Id&&x.IsCompleted).ToArrayAsync(ct);
        var protectedTasks=sources.Concat(completed).OrderBy(x=>x.Id).ToArray();
        var placements=CoachingStudyContinuation.Place(sources,request.StartDate,request.EndDate,
            draft.Slots.Select(x=>(x.Day,x.StartMinute,x.StartMinute+x.DurationMinutes,(Guid?)x.LessonId)).ToArray());
        var pending=protectedTasks.Where(x=>!x.IsCompleted).ToDictionary(x=>x.Id);
        var pendingIds=pending.Keys.ToArray();var pinnedLinks=await db.Set<StudyPlanTaskOutcome>().AsNoTracking().Where(x=>Enumerable.Contains(pendingIds,x.TaskId)).OrderBy(x=>x.TaskId).ThenBy(x=>x.OutcomeId).ToArrayAsync(ct);
        // Completed allocations are durable evidence across all published revisions.
        // ActualMinutes measures effort, not how much curriculum the completed task covered.
        var relevantOutcomeIds=work.SelectMany(x=>x.Outcomes).Select(x=>x.OutcomeId).Distinct().ToArray();
        var completedLinks=await CoachingCompletedOutcomeCredits.ReadAsync(db,student,relevantOutcomeIds,ct);
        var evidenceVersions=completedLinks.Select(x=>x.Link.EstimateId).Concat(pinnedLinks.Select(x=>x.EstimateId)).Distinct().ToArray();
        var scopedVersions=(await db.Set<StudyCatalogTopicOutcomeShare>().Where(x=>Enumerable.Contains(evidenceVersions,x.EstimateId))
            .Select(x=>x.EstimateId).Distinct().ToArrayAsync(ct)).ToHashSet();
        var catalogWork=work.ToArray();
        var remaining=catalogWork.SelectMany(x=>x.Outcomes).ToDictionary(x=>(x.TopicId,x.OutcomeId),x=>x.Minutes);
        void Credit(StudyPlanTask task,StudyPlanTaskOutcome link,List<(Guid LessonId,DateOnly Date,int Minutes)>? scheduledCredits)
        {
            var left=link.AllocatedMinutes;
            foreach(var lesson in catalogWork)
                foreach(var outcome in lesson.Outcomes.Where(x=>x.OutcomeId==link.OutcomeId
                    &&(!scopedVersions.Contains(link.EstimateId)||x.TopicId==task.TopicId)).OrderByDescending(x=>x.TopicId==task.TopicId))
                {
                    var key=(outcome.TopicId,outcome.OutcomeId);var minutes=Math.Min(left,remaining[key]);
                    if(minutes>0)scheduledCredits?.Add((lesson.LessonId,task.PlannedDate,minutes));
                    remaining[key]-=minutes;left-=minutes;
                }
        }
        // A carried task has one logical identity even when it exists in several revisions.
        foreach(var evidence in completedLinks.DistinctBy(x=>(x.Task.OriginalTaskId??x.Task.Id,x.Link.OutcomeId)))
            Credit(evidence.Task,evidence.Link,null);
        // Roadmap O7: a studied topic stays as a short refresh even when earlier completed work covered its curriculum.
        foreach(var refresh in catalogWork.SelectMany(x=>x.Outcomes).Where(x=>x.Kind=="Refresh"))remaining[(refresh.TopicId,refresh.OutcomeId)]=refresh.Minutes;
        var originalWork=catalogWork.Select(lesson=>lesson with {Outcomes=lesson.Outcomes
            .Select(outcome=>outcome with {Minutes=remaining[(outcome.TopicId,outcome.OutcomeId)]}).ToArray()}).ToArray();
        var credits=new List<(Guid LessonId,DateOnly Date,int Minutes)>();
        foreach(var placement in placements)
        {
            var coverage=new List<(Guid LessonId,DateOnly Date,int Minutes)>();
            foreach(var link in pinnedLinks.Where(x=>x.TaskId==placement.Source.Id))Credit(placement.Source,link,coverage);
            var groups=coverage.GroupBy(x=>x.LessonId).ToArray();var total=coverage.Sum(x=>x.Minutes);var creditedMinutes=Math.Min(placement.Minutes,total);var left=creditedMinutes;
            for(var i=0;i<groups.Length;i++) {var minutes=i==groups.Length-1?left:(int)((long)creditedMinutes*groups[i].Sum(x=>x.Minutes)/total);left-=minutes;
                credits.Add((groups[i].Key,placement.Date,minutes));}
        }
        work=originalWork.Select(lesson=>lesson with {Outcomes=lesson.Outcomes
            .Select(outcome=>outcome with {Minutes=remaining[(outcome.TopicId,outcome.OutcomeId)]}).Where(x=>x.Minutes>0).ToArray()}).ToList();
        var blocked=placements.Select(x=>new WeeklyBlockedTime(x.Date,x.Start,x.Minutes))
            .Concat(completed.Select(x=>new WeeklyBlockedTime(x.PlannedDate,x.PlannedStartMinute,x.PlannedMinutes))).ToArray();
        var rules=v2?WeeklyPlanEngine.Rules:null;
        var schedule=WeeklyOutcomeScheduler.Build(request.StartDate,request.EndDate,draft.Slots,work,request.ReuseFreedSlots,blocked,rules);
        var yieldWeights=new Dictionary<Guid,int>();
        if(v2&&request.PrioritizeWhenShort&&schedule.Lessons.Where(x=>x.RemainingMinutes>0).Select(x=>x.LessonId).ToArray() is {Length:>0} shortLessons)
        {
            var planned=work.SelectMany(x=>x.Outcomes).Select(x=>x.TopicId).Distinct().ToArray();
            yieldWeights=await db.StudyCatalogTopics.AsNoTracking().Where(x=>Enumerable.Contains(planned,x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.YieldWeight,ct);
            work=WeeklyPlanEngine.PrioritizeByYield(work,shortLessons,yieldWeights,examErrorTopics).ToList();
            schedule=WeeklyOutcomeScheduler.Build(request.StartDate,request.EndDate,draft.Slots,work,request.ReuseFreedSlots,blocked,rules);
        }
        var placedMinutes=schedule.Tasks.Where(x=>x.Kind!="Spaced").GroupBy(x=>(x.Outcome.TopicId,x.Outcome.OutcomeId)).ToDictionary(x=>x.Key,x=>x.Sum(t=>t.Minutes));
        var leftOut=!v2?null:work.SelectMany(lesson=>lesson.Outcomes.Select(outcome=>(lesson,outcome)))
            .Select(x=>(x.lesson,x.outcome,Left:x.outcome.Minutes-placedMinutes.GetValueOrDefault((x.outcome.TopicId,x.outcome.OutcomeId))))
            .Where(x=>x.Left>0).GroupBy(x=>(x.lesson.LessonId,x.lesson.LessonName,x.outcome.TopicId,x.outcome.TopicName))
            .Select(x=>new WeeklyLeftOutTopic(x.Key.LessonId,x.Key.LessonName,x.Key.TopicId,x.Key.TopicName,x.Sum(y=>y.Left))).ToArray();
        var summaries=schedule.Lessons.Select(summary=>{
            var original=originalWork.Single(x=>x.LessonId==summary.LessonId);var ownCredits=credits.Where(x=>x.LessonId==summary.LessonId).ToArray();
            var required=work.Single(x=>x.LessonId==summary.LessonId).Outcomes.Sum(x=>x.Minutes)+ownCredits.Sum(x=>x.Minutes);var scheduled=summary.ScheduledMinutes+ownCredits.Where(x=>x.Date<=request.EndDate).Sum(x=>x.Minutes);
            var lastCredit=ownCredits.Length==0?(DateOnly?)null:ownCredits.Max(x=>x.Date);var finish=summary.RequiredMinutes==0?lastCredit:summary.FinishDate;
            if(finish.HasValue&&lastCredit>finish)finish=lastCredit;
            var weeks=Math.Max(1,(int)Math.Ceiling((request.EndDate.DayNumber-request.StartDate.DayNumber+1)/7d));
            return summary with {OutcomeCount=original.Outcomes.Select(x=>x.OutcomeId).Distinct().Count(),RequiredMinutes=required,ScheduledMinutes=scheduled,RemainingMinutes=required-scheduled,FinishDate=finish,ExtraWeeklyMinutes=(int)Math.Ceiling((required-scheduled)/(weeks*30d))*30};
        }).ToArray();
        schedule=schedule with {Lessons=summaries,RecommendedEndDate=summaries.All(x=>x.FinishDate.HasValue)?summaries.Max(x=>x.FinishDate):null};
        var fingerprint=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{request,draft.Version,draft.TimeZoneId,Slots=draft.Slots,originalWork,work,issues,ContextVersion=context?.Version,Rules=new{topicStates=topicStates.OrderBy(x=>x.Key).ToArray(),preferences=preferences.OrderBy(x=>x.Key).ToArray(),examErrorTopics=examErrorTopics.Order().ToArray(),yieldWeights=yieldWeights.OrderBy(x=>x.Key).ToArray()},ActiveId=active?.Id,ActiveVersion=active?.Version,pinnedLinks,
            Completed=completedLinks.Select(x=>new{x.Task.Id,x.Task.Version,x.Task.OriginalTaskId,x.Task.TopicId,x.Link.OutcomeId,x.Link.EstimateId,x.Link.AllocatedMinutes}),
            Protected=protectedTasks.Select(x=>new{x.Id,x.Version,x.PlannedDate,x.PlannedStartMinute,x.PlannedMinutes,x.IsCompleted,x.IsPinned,x.RemainingMinutes,x.RemainingQuestions}),Placements=placements.Select(x=>new{x.Source.Id,x.Date,x.Start,x.Minutes})})));
        // Roadmap O7: with PrioritizeWhenShort a plan that does not fit may be saved; the left-out topics are listed.
        var fits=schedule.Lessons.All(x=>x.RemainingMinutes==0)||v2&&request.PrioritizeWhenShort;
        var canCreate=issues.Count==0&&schedule.Tasks.Count+pending.Count>0&&fits&&schedule.Tasks.Count+pending.Count<=10000;
        return new(new(request,draft.TimeZoneId,fingerprint,schedule,issues,canCreate,pending.Count,placements.Select(x=>new WeeklyProtectedTask(x.Date,x.Start,x.Source.Title,x.Minutes)).ToArray(),leftOut),
            new(draft.Version,context?.Version,request.StartDate,request.EndDate,request.ReuseFreedSlots,fingerprint,active?.Id,active?.Version,draft.TimeZoneId),protectedTasks,pinnedLinks,placements);
    }

    public Task<WeeklyOutcomePlanCreated> CreateAsync(WeeklyOutcomePlanSave request,CancellationToken ct)
    {
        return Create(Student(),request,ct);
    }
    public async Task<WeeklyOutcomePlanCreated> CreateForStudentAsync(Guid student,WeeklyOutcomePlanSave request,CancellationToken ct)
    {if(staffAccess is null)throw Forbidden();await staffAccess.RequireTeacherWriteAsync(student,ct);return await Create(student,request,ct);}
    private Task<WeeklyOutcomePlanCreated> Create(Guid student,WeeklyOutcomePlanSave request,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(request.Fingerprint is null||request.Fingerprint.Length!=64)throw new ArgumentException("Önce planı önizleyin.");
        var revision=StudyPlanRevision.Create(student,Guid.NewGuid(),1,request.Title,access.CurrentUserId is {} actor && access.IsCurrentTeacher(actor) ? actor : null);
        async Task<WeeklyOutcomePlanCreated> Save()
        {
            var committed=await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==revision.Id&&x.StudentId==student,ct);
            if(committed is not null)return new(committed.Id,committed.Version);
            var prepared=await Prepare(request.Preview,student,ct);var preview=prepared.Preview;
            if(preview.Fingerprint!=request.Fingerprint)throw Conflict("Haftalık taslak, kazanımlar, süreler veya mevcut çalışmalar değişti. Yeniden önizleyin.");
            if(!preview.CanCreate)throw new ArgumentException("Bütün dersler tarihe sığmalı ve kazanım/süre eksikleri giderilmelidir. Önizlemedeki seçeneklerle planı yeniden hazırlayın.");
            if(await db.StudyPlanRevisions.AnyAsync(x=>x.StudentId==student&&x.Status==StudyPlanStatus.Draft,ct))throw Conflict("Önce mevcut plan taslağınızı yayımlayın veya arşivleyin.");
            revision.SetWeeklySource(prepared.Source);var tasks=new List<StudyPlanTask>();var links=new List<StudyPlanTaskOutcome>();
            var v2=request.Preview.EngineVersion==WeeklyPlanEngine.CurrentVersion;
            foreach(var slice in preview.Schedule.Tasks)
            {
                var title=slice.Kind=="Spaced"?"Tekrar · "+slice.Outcome.TopicName:slice.Outcome.TopicName;
                var task=StudyPlanTask.Create(revision,slice.Date,title.Length>200?title[..200]:title,slice.Minutes,slice.Outcome.TopicId,plannedQuestions:slice.PlannedQuestions);
                task.SetWeeklyPlacement(slice.LessonId,slice.LessonName,slice.StartMinute);if(v2)task.SetPlanDecision(slice.Kind,slice.Reason);tasks.Add(task);
                // A spaced review revisits finished curriculum; it must not count as new outcome coverage.
                if(slice.Kind=="Spaced")continue;
                var link=StudyPlanTaskOutcome.Create(task.Id,slice.Outcome.OutcomeId,slice.Outcome.EstimateId,slice.Minutes,"Catalog");link.CaptureText(slice.Outcome.Code,slice.Outcome.Name);links.Add(link);
            }
            foreach(var placement in prepared.Placements)
            {
                var copy=CoachingStudyContinuation.Copy(revision,placement);tasks.Add(copy);
                links.AddRange(prepared.PinnedLinks.Where(x=>x.TaskId==placement.Source.Id).Select(x=>CoachingStudyContinuation.CopyLink(copy,x)));
            }
            db.Add(revision);db.AddRange(tasks);db.AddRange(links);await db.SaveChangesAsync(ct);return new(revision.Id,revision.Version);
        }
        if(!db.Database.IsRelational())return Save();
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
        {
            await using var transaction=await db.Database.BeginTransactionAsync(ct);
            try
            {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(4188, 100)",ct);
                var key="coaching-study-plan:"+student;await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))",ct);
                var result=await Save();await transaction.CommitAsync(ct);return result;
            }
            finally{foreach(var entry in db.ChangeTracker.Entries().Where(x=>x.Entity is StudyPlanRevision or StudyPlanTask or StudyPlanTaskOutcome).ToArray())entry.State=EntityState.Detached;}
        });
    }
    /// <summary>Topics that the last three practice exams of the past 90 days show as weak (same rule as the exam-based review).</summary>
    private async Task<HashSet<Guid>> ExamErrorTopicsAsync(Guid student,CancellationToken ct)
    {
        var now=clock.GetUtcNow().UtcDateTime;var since=now.AddDays(-90);
        var results=await db.ExamResults.AsNoTracking().Include(x=>x.Exam).Where(x=>x.StudentId==student&&x.PracticeExamJson!=null
            &&x.Exam.ExamDate>=since&&x.Exam.ExamDate<=now).OrderByDescending(x=>x.Exam.ExamDate).ThenBy(x=>x.Id).Take(1000).ToArrayAsync(ct);
        var evidence=results.Select(x=>new PracticeEvidence(x.ExamId,x.Exam.Title,x.Exam.ExamDate,
            x.Exam.StudentOwnerId.HasValue?"StudentReported":"TeacherRecorded",x.GetPracticeExam()!,x.CreatedAt,x.GetTopicAnalysis()));
        return PracticeAdaptationCalculator.Calculate(evidence).SelectMany(x=>x.Topics).Select(x=>x.TopicId).ToHashSet();
    }
    private static BusinessRuleException Forbidden()=>new("Authorization.Forbidden","Öğretmen erişimi doğrulanamadı.");
    private static BusinessRuleException Conflict(string message)=>new("StudyPlanning.Conflict",message);
}
