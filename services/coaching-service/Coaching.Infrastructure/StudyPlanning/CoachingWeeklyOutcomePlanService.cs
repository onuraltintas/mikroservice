using System.Security.Cryptography;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingWeeklyOutcomePlanService(CoachingDbContext db,ICoachingAccessPolicy access,ILearningSetupService setup,TimeProvider clock):IWeeklyOutcomePlanService
{
    private Guid Student()=>access.CurrentUserId is {} id&&access.IsCurrentStudent(id)?id:throw new BusinessRuleException("Authorization.Forbidden","Yalnız kendi öğrenci hesabınızla kullanabilirsiniz.");
    private async Task<WeeklyStudyDraft> Draft(Guid student,CancellationToken ct)=>await db.Set<WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student,ct)
        ??throw new KeyNotFoundException("Önce haftalık ders taslağınızı kaydedin.");
    public async Task<WeeklyOutcomePlanOptions> OptionsAsync(CancellationToken ct)
    {
        var draft=await Draft(Student(),ct);var zone=TimeZoneInfo.FindSystemTimeZoneById(draft.TimeZoneId);
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),zone).DateTime);
        var codes=draft.Slots.Select(x=>x.ExamCode).Where(x=>x is not null).Distinct().ToArray();
        var exams=await db.Set<StudyCatalogExam>().AsNoTracking().Where(x=>Enumerable.Contains(codes,x.Code)&&x.IsActive).OrderBy(x=>x.Code).Select(x=>new WeeklyExamReference(x.Name,x.ExamDate)).ToArrayAsync(ct);
        return new(today,WeeklyOutcomeScheduler.DefaultEnd(today),draft.Version,draft.TimeZoneId,exams);
    }
    public async Task<WeeklyOutcomePlanPreview> PreviewAsync(WeeklyOutcomePlanRequest request,CancellationToken ct)=>(await Prepare(request,Student(),ct)).Preview;

    private sealed record Prepared(WeeklyOutcomePlanPreview Preview,WeeklyPlanSource Source,StudyPlanTask[] Protected);
    private async Task<Prepared> Prepare(WeeklyOutcomePlanRequest request,Guid student,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var draft=await Draft(student,ct);
        if(request.ExpectedWeeklyDraftVersion<0||request.ExpectedWeeklyDraftVersion!=draft.Version||draft.Slots.Count==0)throw Conflict("Haftalık taslak boş veya değişmiş. Takvimi yeniden yükleyin.");
        var choices=(await setup.GetCatalogAsync(null,1,ct)).Lessons.ToDictionary(x=>x.Id);
        var lessonIds=draft.Slots.Select(x=>x.LessonId).Distinct().OrderBy(x=>x).ToArray();
        if(lessonIds.Any(x=>!choices.ContainsKey(x)))throw Conflict("Takvimde artık kullanılamayan ders var. Haftalık taslağı güncelleyin.");
        var context=await db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentUserId==student,ct);
        var year=context?.AcademicYearStart??(clock.GetUtcNow().Month>=9?clock.GetUtcNow().Year:clock.GetUtcNow().Year-1);
        var topics=await (from t in db.StudyCatalogTopics.AsNoTracking() join u in db.StudyCatalogUnits on t.UnitId equals u.Id
            where Enumerable.Contains(lessonIds,t.LessonId)&&t.IsActive&&u.IsActive&&u.LessonId==t.LessonId
                &&!db.StudyCatalogTopics.Any(c=>c.ParentId==t.Id)
                &&(t.ParentId==null||db.StudyCatalogTopics.Any(p=>p.Id==t.ParentId&&p.IsActive))
            orderby u.DisplayOrder,t.DisplayOrder,t.Id select new {Topic=t,UnitOrder=u.DisplayOrder}).ToArrayAsync(ct);
        var topicIds=topics.Select(x=>x.Topic.Id).ToArray();
        var rows=await (from m in db.Set<StudyCatalogTopicOutcome>().AsNoTracking()
            join t in db.StudyCatalogTopics on m.TopicId equals t.Id
            join l in db.StudyCatalogLessons on t.LessonId equals l.Id
            join o in db.Set<StudyCatalogOutcome>() on m.OutcomeId equals o.Id
            join edition in db.Set<OfficialCurriculumEdition>() on o.EditionId equals edition.Id
            join e in db.Set<OutcomeDurationEstimate>().Where(e=>e.IsPublished&&!db.Set<OutcomeDurationEstimate>().Any(n=>n.OutcomeId==e.OutcomeId&&n.IsPublished&&n.Number>e.Number)) on o.Id equals e.OutcomeId into estimates
            from estimate in estimates.DefaultIfEmpty()
            where Enumerable.Contains(topicIds,m.TopicId)&&m.IsApproved&&!o.IsArchived&&edition.AcademicYearStart<=year&&edition.AcademicYearEnd>=year
                &&edition.ExamCode==l.ExamCode&&edition.SubjectName==l.Name&&(edition.GradeNumber==null||l.GradeNumber==null||edition.GradeNumber==l.GradeNumber)
            select new {TopicId=t.Id,Outcome=o,Estimate=estimate}).ToArrayAsync(ct);
        var issues=new List<WeeklyLessonIssue>();var work=new List<WeeklyLessonWork>();var seen=new HashSet<Guid>();
        foreach(var lessonId in lessonIds)
        {
            var lesson=choices[lessonId];var lessonTopics=topics.Where(x=>x.Topic.LessonId==lessonId).ToArray();var outcomes=new List<WeeklyOutcomeWork>();
            if(lessonTopics.Length==0)issues.Add(new(lessonId,lesson.Name,"Aktif konu ve kazanım bulunamadı."));
            foreach(var topic in lessonTopics)
            {
                var mapped=rows.Where(x=>x.TopicId==topic.Topic.Id).OrderBy(x=>x.Outcome.DisplayOrder).ThenBy(x=>x.Outcome.Code,StringComparer.Ordinal).ThenBy(x=>x.Outcome.Id).ToArray();
                if(mapped.Length==0)issues.Add(new(lessonId,lesson.Name,topic.Topic.Name+": onaylı kazanım eşleştirmesi yok."));
                foreach(var row in mapped)
                {
                    if(row.Estimate is null){issues.Add(new(lessonId,lesson.Name,row.Outcome.Code+": yayımlanmış çalışma süresi yok."));continue;}
                    if(seen.Add(row.Outcome.Id))outcomes.Add(new(topic.Topic.Id,topic.Topic.Name,row.Outcome.Id,row.Outcome.Code,row.Outcome.Name,row.Estimate.Id,row.Estimate.Number,row.Estimate.TotalMinutes));
                }
            }
            work.Add(new(lessonId,lesson.Name,outcomes));
        }
        var active=await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student&&x.IsActive,ct);
        var protectedTasks=active is null?[]:await db.StudyPlanTasks.AsNoTracking().Where(x=>x.StudentId==student&&x.RevisionId==active.Id&&(x.IsCompleted||x.IsPinned)).OrderBy(x=>x.Id).ToArrayAsync(ct);
        var schedule=WeeklyOutcomeScheduler.Build(request.StartDate,request.EndDate,draft.Slots,work,request.ReuseFreedSlots,
            protectedTasks.Select(x=>new WeeklyBlockedTime(x.PlannedDate,x.PlannedStartMinute,x.PlannedMinutes)).ToArray());
        var fingerprint=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{request,draft.Version,draft.TimeZoneId,Slots=draft.Slots,work,issues,ContextVersion=context?.Version,ActiveId=active?.Id,ActiveVersion=active?.Version,
            Protected=protectedTasks.Select(x=>new{x.Id,x.Version,x.PlannedDate,x.PlannedStartMinute,x.PlannedMinutes,x.IsCompleted,x.IsPinned})})));
        var canCreate=issues.Count==0&&schedule.Tasks.Count>0&&schedule.Lessons.All(x=>x.RemainingMinutes==0)&&schedule.Tasks.Count+protectedTasks.Count(x=>!x.IsCompleted)<=10000;
        return new(new(request,draft.TimeZoneId,fingerprint,schedule,issues,canCreate,protectedTasks.Count(x=>!x.IsCompleted)),
            new(draft.Version,context?.Version,request.StartDate,request.EndDate,request.ReuseFreedSlots,fingerprint,active?.Id,active?.Version),protectedTasks);
    }

    public Task<WeeklyOutcomePlanCreated> CreateAsync(WeeklyOutcomePlanSave request,CancellationToken ct)
    {
        var student=Student();ArgumentNullException.ThrowIfNull(request);
        if(request.Fingerprint is null||request.Fingerprint.Length!=64)throw new ArgumentException("Önce planı önizleyin.");
        var revision=StudyPlanRevision.Create(student,Guid.NewGuid(),1,request.Title);
        async Task<WeeklyOutcomePlanCreated> Save()
        {
            var committed=await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==revision.Id&&x.StudentId==student,ct);
            if(committed is not null)return new(committed.Id,committed.Version);
            var prepared=await Prepare(request.Preview,student,ct);var preview=prepared.Preview;
            if(preview.Fingerprint!=request.Fingerprint)throw Conflict("Haftalık taslak, kazanımlar, süreler veya mevcut çalışmalar değişti. Yeniden önizleyin.");
            if(!preview.CanCreate)throw new ArgumentException("Bütün dersler tarihe sığmalı ve kazanım/süre eksikleri giderilmelidir. Önizlemedeki seçeneklerle planı yeniden hazırlayın.");
            if(await db.StudyPlanRevisions.AnyAsync(x=>x.StudentId==student&&x.Status==StudyPlanStatus.Draft,ct))throw Conflict("Önce mevcut plan taslağınızı yayımlayın veya arşivleyin.");
            revision.SetWeeklySource(prepared.Source);var tasks=new List<StudyPlanTask>();var links=new List<StudyPlanTaskOutcome>();
            foreach(var slice in preview.Schedule.Tasks)
            {
                var task=StudyPlanTask.Create(revision,slice.Date,slice.Outcome.TopicName.Length>200?slice.Outcome.TopicName[..200]:slice.Outcome.TopicName,slice.Minutes,slice.Outcome.TopicId);
                task.SetWeeklyPlacement(slice.LessonId,slice.LessonName,slice.StartMinute);tasks.Add(task);
                var link=StudyPlanTaskOutcome.Create(task.Id,slice.Outcome.OutcomeId,slice.Outcome.EstimateId,slice.Minutes,"Catalog");link.CaptureText(slice.Outcome.Code,slice.Outcome.Name);links.Add(link);
            }
            foreach(var prior in prepared.Protected.Where(x=>!x.IsCompleted))
            {
                var copy=StudyPlanTask.Create(revision,prior.PlannedDate,prior.Title,prior.PlannedMinutes,prior.TopicId,true,prior.SourceName,prior.PlannedQuestions);
                if(prior.PlannedStartMinute.HasValue)copy.SetWeeklyPlacement(prior.WeeklyLessonId!.Value,prior.LessonNameSnapshot!,prior.PlannedStartMinute.Value);
                tasks.Add(copy);
                var oldLinks=await db.Set<StudyPlanTaskOutcome>().AsNoTracking().Where(x=>x.TaskId==prior.Id).ToArrayAsync(ct);
                foreach(var old in oldLinks){var link=StudyPlanTaskOutcome.Create(copy.Id,old.OutcomeId,old.EstimateId,old.AllocatedMinutes,old.Origin);if(old.CodeSnapshot is not null&&old.NameSnapshot is not null)link.CaptureText(old.CodeSnapshot,old.NameSnapshot);links.Add(link);}
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
    private static BusinessRuleException Conflict(string message)=>new("StudyPlanning.Conflict",message);
}
