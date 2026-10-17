using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MassTransit.EntityFrameworkCoreIntegration;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingManualStudyPlanService(CoachingDbContext db, ICoachingAccessPolicy access,
    IAutomaticStudyPlanPreviewService previews, ICoachingEventPublisher events,
    CoachingStudyPlanAccess? staffAccess = null) : IManualStudyPlanService, IAutomaticStudyPlanDraftService
{
    public Task<StudyPlanPage> ListAsync(int pageNumber, int pageSize, StudyPlanStatus? status, CancellationToken ct = default)
        => ListCoreAsync(pageNumber, pageSize, status, ct);
    public Task<StudyPlanPage> ListForStudentAsync(Guid student, int pageNumber, int pageSize, StudyPlanStatus? status, CancellationToken ct = default)
        => ListCoreAsync(pageNumber, pageSize, status, ct, student);
    public Task<ManualStudyPlanView?> GetAsync(Guid id, CancellationToken ct = default) => GetCoreAsync(id, ct);
    public Task<ManualStudyPlanView?> GetForStudentAsync(Guid student, Guid id, CancellationToken ct = default) => GetCoreAsync(id, ct, student);
    public Task<ManualStudyPlanView> CreateDraftAsync(ManualStudyPlanInput request, CancellationToken ct = default) => CreateCoreAsync(request, ct);
    public Task<ManualStudyPlanView> CreateForStudentAsync(Guid student, ManualStudyPlanInput request, CancellationToken ct = default) => CreateCoreAsync(request, ct, student);
    public Task<ManualStudyPlanView> ReplaceDraftAsync(Guid id, int version, ManualStudyPlanInput request, CancellationToken ct = default) => ReplaceCoreAsync(id, version, request, ct);
    public Task<ManualStudyPlanView> ReplaceForStudentAsync(Guid student, Guid id, int version, ManualStudyPlanInput request, CancellationToken ct = default) => ReplaceCoreAsync(id, version, request, ct, student);
    public Task<ManualStudyPlanView> PublishAsync(Guid id, int version, CancellationToken ct = default) => PublishCoreAsync(id, version, ct);
    public Task<ManualStudyPlanView> RestoreArchivedAsync(Guid id,StudyPlanRestoreInput request,CancellationToken ct=default)=>RestoreCoreAsync(id,request,ct);
    public Task<ManualStudyPlanView> RestoreForStudentAsync(Guid student,Guid id,StudyPlanRestoreInput request,CancellationToken ct=default)=>RestoreCoreAsync(id,request,ct,student);
    public Task<ManualStudyPlanView> PublishForStudentAsync(Guid student, Guid id, int version, CancellationToken ct = default) => PublishCoreAsync(id, version, ct, student);
    public Task<ManualStudyPlanView> ArchiveDraftAsync(Guid id, int version, CancellationToken ct = default) => ArchiveCoreAsync(id, version, ct);
    public Task<ManualStudyPlanView> ArchiveForStudentAsync(Guid student, Guid id, int version, CancellationToken ct = default) => ArchiveCoreAsync(id, version, ct, student);
    public Task<ManualStudyPlanView> RescheduleTaskAsync(Guid id, Guid task, int version, DateOnly date, CancellationToken ct = default) => RescheduleCoreAsync(id, task, version, date, ct);
    public Task<ManualStudyPlanView> RescheduleForStudentAsync(Guid student, Guid id, Guid task, int version, DateOnly date, CancellationToken ct = default) => RescheduleCoreAsync(id, task, version, date, ct, student);
    public Task<ManualStudyPlanView> MoveTasksAsync(Guid id, StudyTasksMoveInput request, CancellationToken ct = default) => MoveCoreAsync(id, request, ct);
    public Task<ManualStudyPlanView> MoveForStudentAsync(Guid student, Guid id, StudyTasksMoveInput request, CancellationToken ct = default) => MoveCoreAsync(id, request, ct, student);
    public Task<ManualStudyPlanView> CorrectTitleForStudentAsync(Guid student,Guid id,Guid taskId,int version,string title,CancellationToken ct=default)
        => LockedAsync(async owner=>
        {
            var revision=await OwnedAsync(id,owner,version,ct);var task=await ActiveTaskAsync(revision,taskId,ct);
            if(task.IsCompleted)throw Conflict("Tamamlanan çalışmanın bilgileri değiştirilemez.");
            task.CorrectPlanning(title,task.PlannedDate,task.PlannedMinutes);revision.RecordTaskChange();
            await db.SaveChangesAsync(ct);return await ViewAsync(revision,ct);
        },ct,student,true);

    private Task<ManualStudyPlanView> MoveCoreAsync(Guid id, StudyTasksMoveInput request, CancellationToken ct, Guid? targetStudent=null)
        => LockedAsync(async student =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if(request.TaskIds is null || request.TaskIds.Count is <1 or >500 || request.TaskIds.Any(x=>x==Guid.Empty)
                || request.TaskIds.Distinct().Count()!=request.TaskIds.Count) throw Conflict("Taşınacak çalışmaların kimlikleri geçerli ve benzersiz olmalıdır.");
            var revision=await OwnedAsync(id,student,request.ExpectedVersion,ct);
            if(revision.Status==StudyPlanStatus.Archived) throw Conflict("Arşivlenmiş plan taşınamaz.");
            var ids=request.TaskIds.ToArray();
            var tasks=await db.StudyPlanTasks.Where(x=>x.RevisionId==id&&x.StudentId==student&&Enumerable.Contains(ids,x.Id)).OrderBy(x=>x.PlannedStartMinute).ThenBy(x=>x.Id).ToArrayAsync(ct);
            if(tasks.Length!=ids.Length) throw new BusinessRuleException("StudyPlanning.NotFound","Taşınacak çalışma bulunamadı.");
            if(tasks.Any(x=>x.IsCompleted)) throw Conflict("Tamamlanmış çalışmalar taşınamaz.");
            var first=tasks[0];var cursor=first.PlannedStartMinute;
            foreach(var task in tasks)
            {
                if(cursor is null || task.PlannedDate!=first.PlannedDate || task.PlannedStartMinute!=cursor
                    || task.WeeklyLessonId!=first.WeeklyLessonId || (task.LessonNameSnapshot??task.Title)!=(first.LessonNameSnapshot??first.Title))
                    throw Conflict("Aynı günün bitişik ders parçalarını birlikte taşıyın.");
                cursor+=task.PlannedMinutes;
            }
            var minutes=tasks.Sum(x=>x.PlannedMinutes);
            if(request.PlannedDate==default || request.StartMinute<0 || request.StartMinute+minutes>1440)
                throw new ArgumentException("Dersin tamamı aynı günün içinde kalmalıdır.");
            var others=db.StudyPlanTasks.Where(x=>x.RevisionId==id&&x.StudentId==student&&x.PlannedDate==request.PlannedDate&&!Enumerable.Contains(ids,x.Id));
            if(await others.AnyAsync(x=>x.PlannedStartMinute<request.StartMinute+minutes&&x.PlannedStartMinute+x.PlannedMinutes>request.StartMinute,ct))
                throw Conflict("Bu saat başka bir çalışmayla çakışıyor. Boş bir saat seçin.");
            if(await others.SumAsync(x=>x.PlannedMinutes,ct)+minutes>1440) throw Conflict("Bir güne 24 saatten fazla çalışma eklenemez.");
            if(first.PlannedDate==request.PlannedDate&&first.PlannedStartMinute==request.StartMinute) return await ViewAsync(revision,ct);
            var start=request.StartMinute;
            foreach(var task in tasks){task.Schedule(request.PlannedDate,start);start+=task.PlannedMinutes;}
            revision.RecordScheduleChange();await db.SaveChangesAsync(ct);
            return await ViewAsync(revision,ct);
        },ct,targetStudent,true);

    public Task<ManualStudyPlanView> CreateAutomaticDraftAsync(AutomaticStudyDraftRequest request, CancellationToken cancellationToken = default)
        => LockedAsync(async student =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var revision = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, request.Title);
            var preview = await previews.PreviewAsync(request.Preview, cancellationToken);
            if (request.ExpectedActiveRevisionId != preview.ActiveRevisionId
                || request.ExpectedActiveRevisionVersion != preview.ActiveRevisionVersion)
                throw Conflict("Aktif plan değişti. Yeniden önizleyin.");
            if (await db.StudyPlanRevisions.AnyAsync(x => x.StudentId == student && x.Status == StudyPlanStatus.Draft, cancellationToken))
                throw Conflict("Önce mevcut taslağınızı düzenleyin veya yayımlayın.");
            var ids = preview.Schedule.Tasks.Select(x => x.TopicId).Distinct().ToArray();
            var names = await db.StudyCatalogTopics.AsNoTracking().Where(x => Enumerable.Contains(ids, x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
            var tasks = preview.Schedule.Tasks.Select(x => StudyPlanTask.Create(revision, x.PlannedDate,
                names[x.TopicId], x.PlannedMinutes, x.TopicId)).ToList();
            db.AddRange(StudyOutcomeAllocation.Allocate(tasks, preview.Outcomes ?? []));
            // Preserve pending pinned work, including work outside the generated horizon.
            // Completed work remains in its original revision; actual results are never duplicated.
            if (preview.ActiveRevisionId is { } activeId)
            {
                var pinned = await db.StudyPlanTasks.AsNoTracking().Where(x => x.StudentId == student
                    && x.RevisionId == activeId && x.IsPinned && !x.IsCompleted).ToListAsync(cancellationToken);
                var pinnedIds = pinned.Select(x => x.Id).ToArray();
                var links = await db.Set<StudyPlanTaskOutcome>().AsNoTracking().Where(x => Enumerable.Contains(pinnedIds, x.TaskId)).ToArrayAsync(cancellationToken);
                foreach (var prior in pinned)
                {
                    var copied = StudyPlanTask.Create(revision, prior.PlannedDate, prior.Title, prior.PlannedMinutes, prior.TopicId, true, prior.SourceName, prior.PlannedQuestions);
                    tasks.Add(copied);
                    db.AddRange(links.Where(x => x.TaskId == prior.Id).Select(x => {
                        var link = StudyPlanTaskOutcome.Create(copied.Id, x.OutcomeId, x.EstimateId, x.AllocatedMinutes, x.Origin);
                        if (x.CodeSnapshot is not null && x.NameSnapshot is not null) link.CaptureText(x.CodeSnapshot, x.NameSnapshot);
                        return link;
                    }));
                }
            }
            if (tasks.Count is < 1 or > 500) throw new ArgumentException("Taslak 1-500 çalışma içermelidir; konuları azaltın veya süreyi değiştirin.");
            revision.SetAutomaticSource(preview.AvailabilityVersion, preview.ActiveRevisionId, preview.ActiveRevisionVersion);
            db.StudyPlanRevisions.Add(revision); db.StudyPlanTasks.AddRange(tasks);
            await db.SaveChangesAsync(cancellationToken);
            return await ViewAsync(revision, cancellationToken);
        }, cancellationToken);
    private Task<StudyPlanPage> ListCoreAsync(int pageNumber, int pageSize, StudyPlanStatus? status, CancellationToken cancellationToken, Guid? targetStudent = null)
        => LockedAsync(async student =>
        {
            if (pageNumber is < 1 or > 10000 || pageSize is < 1 or > 50 || (status.HasValue && !Enum.IsDefined(status.Value)))
                throw new ArgumentException("Geçerli sayfa ve plan durumu seçin.");
            var query = db.StudyPlanRevisions.AsNoTracking().Where(x => x.StudentId == student);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            var total = await query.CountAsync(cancellationToken);
            var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)
                .Select(x => new StudyPlanSummary(x.Id, x.Version, x.Title, x.Status)).ToListAsync(cancellationToken);
            return new StudyPlanPage(items, total, pageNumber, pageSize);
        }, cancellationToken, targetStudent);

    private Task<ManualStudyPlanView?> GetCoreAsync(Guid id, CancellationToken cancellationToken, Guid? targetStudent = null)
        => LockedAsync<ManualStudyPlanView?>(async student =>
        {
            var revision = await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.StudentId == student, cancellationToken);
            return revision is null ? null : await ViewAsync(revision, cancellationToken);
        }, cancellationToken, targetStudent);

    private Task<ManualStudyPlanView> CreateCoreAsync(ManualStudyPlanInput request, CancellationToken cancellationToken, Guid? targetStudent = null)
        => LockedAsync(async student =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var revision = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, request.Title);
            var tasks = await ValidateTasksAsync(revision, request, cancellationToken);
            if (await db.StudyPlanRevisions.AnyAsync(x => x.StudentId == student && x.Status == StudyPlanStatus.Draft, cancellationToken))
                throw Conflict("Önce mevcut taslağınızı düzenleyin veya yayımlayın.");
            db.StudyPlanRevisions.Add(revision);
            db.StudyPlanTasks.AddRange(tasks);
            await db.SaveChangesAsync(cancellationToken);
            return await ViewAsync(revision, cancellationToken);
        }, cancellationToken, targetStudent, true);

    private Task<ManualStudyPlanView> ReplaceCoreAsync(Guid id, int expectedVersion, ManualStudyPlanInput request, CancellationToken cancellationToken, Guid? targetStudent = null)
        => LockedAsync(async student =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var revision = await OwnedAsync(id, student, expectedVersion, cancellationToken);
            if (revision.Status != StudyPlanStatus.Draft) throw Conflict("Yalnız taslak plan düzenlenebilir.");
            if(revision.WeeklySource is not null)throw Conflict("Bu plan haftalık takvimden üretildi. Saat ve kazanımları korumak için taslağı arşivleyip önizlemeden yeniden oluşturun.");
            // Validate all replacements before changing any persisted task or title.
            _ = StudyPlanRevision.Create(student, revision.PlanId, revision.RevisionNumber, request.Title);
            var tasks = await ValidateTasksAsync(revision, request, cancellationToken);
            var oldTasks = await db.StudyPlanTasks.Where(x => x.RevisionId == id && x.StudentId == student).ToListAsync(cancellationToken);
            if (oldTasks.Any(x => x.IsCompleted)) throw Conflict("Tamamlanmış çalışmaları içeren taslak değiştirilemez.");
            revision.RenameDraft(request.Title);
            db.StudyPlanTasks.RemoveRange(oldTasks);
            db.StudyPlanTasks.AddRange(tasks);
            await db.SaveChangesAsync(cancellationToken);
            return await ViewAsync(revision, cancellationToken);
        }, cancellationToken, targetStudent, true);

    private Task<ManualStudyPlanView> PublishCoreAsync(Guid id, int expectedVersion, CancellationToken cancellationToken, Guid? targetStudent = null)
        => LockedAsync(async student =>
        {
            var revision = await OwnedAsync(id, student, expectedVersion, cancellationToken);
            if (revision.Status != StudyPlanStatus.Draft) throw Conflict("Yalnız taslak plan yayımlanabilir.");
            if (!await db.StudyPlanTasks.AnyAsync(x => x.RevisionId == id && x.StudentId == student, cancellationToken))
                throw Conflict("Yayımlamadan önce en az bir çalışma ekleyin.");
            var previous = await db.StudyPlanRevisions.SingleOrDefaultAsync(x => x.StudentId == student && x.IsActive, cancellationToken);
            if(revision.WeeklySource is {} weeklySource)
            {
                var weekly=await db.Set<WeeklyStudyDraft>().FromSqlInterpolated($"SELECT * FROM coaching.study_weekly_drafts WHERE \"StudentId\"={student} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                var contextVersion=await db.StudentLearningContexts.Where(x=>x.StudentUserId==student).Select(x=>(int?)x.Version).SingleOrDefaultAsync(cancellationToken);
                if(weekly?.Version!=weeklySource.DraftVersion||contextVersion!=weeklySource.LearningContextVersion||previous?.Id!=weeklySource.ActiveRevisionId||previous?.Version!=weeklySource.ActiveRevisionVersion)
                    throw Conflict("Haftalık takvim, eğitim bağlamı veya aktif çalışmalar değişti. Taslağı arşivleyip güncel önizlemeden yeniden oluşturun.");
            }
            if (revision.AutomaticAvailabilityVersion is { } availabilityVersion)
            {
                // Lock the hours row so a concurrent preference update cannot pass this check then commit first.
                var hours = await db.StudyAvailability.FromSqlInterpolated($"SELECT * FROM coaching.study_availability WHERE \"StudentId\" = {student} FOR UPDATE")
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                if (hours?.Version != availabilityVersion || previous?.Id != revision.AutomaticSourceRevisionId
                    || previous?.Version != revision.AutomaticSourceRevisionVersion)
                    throw Conflict("Plan veya çalışma saatleri değişti. Otomatik taslağı güncel verilerle yeniden hazırlayın.");
            }
            if (previous is not null)
            {
                previous.Archive();
                // Release the unique active slot before activating the draft, within the same transaction.
                await db.SaveChangesAsync(cancellationToken);
            }
            revision.Activate();
            await events.PublishAsync(new StudyPlanPublishedEvent(revision.Id, student), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return await ViewAsync(revision, cancellationToken);
        }, cancellationToken, targetStudent, true);

    private Task<ManualStudyPlanView> RestoreCoreAsync(Guid id,StudyPlanRestoreInput request,CancellationToken ct,Guid? targetStudent=null)
        => LockedAsync(async student=>
        {
            ArgumentNullException.ThrowIfNull(request);
            if(request.ExpectedActiveRevisionId==Guid.Empty||request.ExpectedActiveRevisionId.HasValue!=request.ExpectedActiveRevisionVersion.HasValue||request.ExpectedActiveRevisionVersion<0)
                throw new ArgumentException("Geçerli aktif plan sürümü gereklidir.");
            var revision=await OwnedAsync(id,student,request.ExpectedVersion,ct);
            if(revision.Status!=StudyPlanStatus.Archived)throw Conflict("Yalnız arşiv plan yeniden etkinleştirilebilir.");
            if(!await db.StudyPlanTasks.AnyAsync(x=>x.RevisionId==id&&x.StudentId==student,ct))throw Conflict("Boş plan etkinleştirilemez.");
            var active=await db.StudyPlanRevisions.SingleOrDefaultAsync(x=>x.StudentId==student&&x.IsActive,ct);
            if(active?.Id!=request.ExpectedActiveRevisionId||active?.Version!=request.ExpectedActiveRevisionVersion)throw Conflict("Aktif plan değişti. Sayfayı yenileyip tekrar deneyin.");
            if(active is not null){active.Archive();await db.SaveChangesAsync(ct);}
            revision.RestoreArchived();await events.PublishAsync(new StudyPlanPublishedEvent(revision.Id,student),ct);
            await db.SaveChangesAsync(ct);return await ViewAsync(revision,ct);
        },ct,targetStudent,true);

    private async Task<StudyPlanRevision> OwnedAsync(Guid id, Guid student, int version, CancellationToken cancellationToken)
    {
        var revision = await db.StudyPlanRevisions.SingleOrDefaultAsync(x => x.Id == id && x.StudentId == student, cancellationToken)
            ?? throw new BusinessRuleException("StudyPlanning.NotFound", "Plan bulunamadı.");
        if (revision.Version != version) throw Conflict("Plan değişti. Sayfayı yenileyip tekrar deneyin.");
        return revision;
    }

    private Task<ManualStudyPlanView> ArchiveCoreAsync(Guid id, int expectedVersion, CancellationToken cancellationToken, Guid? targetStudent = null)
        => LockedAsync(async student =>
        {
            var revision = await OwnedAsync(id, student, expectedVersion, cancellationToken);
            if (revision.Status != StudyPlanStatus.Draft) throw Conflict("Yalnız taslak plan arşivlenebilir.");
            revision.Archive();
            await db.SaveChangesAsync(cancellationToken);
            return await ViewAsync(revision, cancellationToken);
        }, cancellationToken, targetStudent, true);

    public Task<ManualStudyPlanView> CompleteTaskAsync(Guid id, Guid taskId, int expectedVersion, int actualMinutes, CancellationToken cancellationToken = default)
        => CompleteTaskAsync(id, taskId, expectedVersion, actualMinutes, null, cancellationToken);

    public Task<ManualStudyPlanView> CompleteTaskAsync(Guid id, Guid taskId, int expectedVersion, int actualMinutes, int? actualQuestions, CancellationToken cancellationToken = default)
        => LockedAsync(async student =>
        {
            var revision = await OwnedAsync(id, student, expectedVersion, cancellationToken);
            var task = await ActiveTaskAsync(revision, taskId, cancellationToken);
            if (actualMinutes is < 1 or > 1440) throw new ArgumentException("Gerçek çalışma süresi 1–1440 dakika olmalıdır.");
            if (actualQuestions is < 0 or > 100000) throw new ArgumentException("Gerçek soru sayısı 0–100000 arasında olmalıdır.");
            if (task.IsCompleted)
            {
                if (task.ActualMinutes != actualMinutes || task.ActualQuestions != actualQuestions) throw Conflict("Tamamlanan çalışmanın süresi ve soru sayısı değiştirilemez.");
                return await ViewAsync(revision, cancellationToken);
            }
            task.Complete(actualMinutes, actualQuestions);
            revision.RecordTaskChange();
            await db.SaveChangesAsync(cancellationToken);
            return await ViewAsync(revision, cancellationToken);
        }, cancellationToken);

    private Task<ManualStudyPlanView> RescheduleCoreAsync(Guid id, Guid taskId, int expectedVersion, DateOnly plannedDate, CancellationToken cancellationToken, Guid? targetStudent = null)
        => LockedAsync(async student =>
        {
            var revision = await OwnedAsync(id, student, expectedVersion, cancellationToken);
            var task = await ActiveTaskAsync(revision, taskId, cancellationToken);
            if (task.IsCompleted) throw Conflict("Tamamlanan çalışma başka güne taşınamaz.");
            if (plannedDate == default) throw new ArgumentException("Geçerli çalışma tarihi seçin.");
            if (task.PlannedDate == plannedDate) return await ViewAsync(revision, cancellationToken);
            if(task.PlannedStartMinute is {} start&&await db.StudyPlanTasks.AnyAsync(x=>x.RevisionId==id&&x.StudentId==student&&x.Id!=taskId&&x.PlannedDate==plannedDate
                &&x.PlannedStartMinute<start+task.PlannedMinutes&&x.PlannedStartMinute+x.PlannedMinutes>start,cancellationToken))
                throw Conflict("Yeni tarihte aynı saatte başka çalışma var. Farklı bir gün seçin.");
            var dailyMinutes = await db.StudyPlanTasks.Where(x => x.RevisionId == id && x.StudentId == student
                && x.Id != taskId && x.PlannedDate == plannedDate).SumAsync(x => x.PlannedMinutes, cancellationToken);
            if (dailyMinutes + task.PlannedMinutes > 1440) throw new ArgumentException("Bir güne 24 saatten fazla çalışma eklenemez.");
            task.Reschedule(plannedDate);
            revision.RecordTaskChange();
            await db.SaveChangesAsync(cancellationToken);
            return await ViewAsync(revision, cancellationToken);
        }, cancellationToken, targetStudent, true);

    private async Task<StudyPlanTask> ActiveTaskAsync(StudyPlanRevision revision, Guid taskId, CancellationToken cancellationToken)
    {
        if (revision.Status != StudyPlanStatus.Active) throw Conflict("Yalnız aktif planın çalışmaları güncellenebilir.");
        return await db.StudyPlanTasks.SingleOrDefaultAsync(x => x.Id == taskId && x.RevisionId == revision.Id
            && x.StudentId == revision.StudentId, cancellationToken)
            ?? throw new BusinessRuleException("StudyPlanning.NotFound", "Çalışma bulunamadı.");
    }

    private async Task<StudyPlanTask[]> ValidateTasksAsync(StudyPlanRevision revision, ManualStudyPlanInput request, CancellationToken cancellationToken)
    {
        if (request.Tasks is null || request.Tasks.Count > 500 || request.Tasks.Any(x => x is null))
            throw new ArgumentException("Plan en fazla 500 çalışma içerebilir.");
        var tasks = request.Tasks.Select(x => StudyPlanTask.Create(revision, x.PlannedDate, x.Title, x.PlannedMinutes, x.TopicId, x.IsPinned, x.SourceName, x.PlannedQuestions)).ToArray();
        for(var i=0;i<tasks.Length;i++) if(request.Tasks[i].PlannedStartMinute is {} start) tasks[i].Schedule(tasks[i].PlannedDate,start);
        foreach(var day in tasks.GroupBy(x=>x.PlannedDate))
        {
            var timed=day.Where(x=>x.PlannedStartMinute.HasValue).OrderBy(x=>x.PlannedStartMinute).ToArray();
            for(var i=1;i<timed.Length;i++) if(timed[i].PlannedStartMinute<timed[i-1].PlannedStartMinute+timed[i-1].PlannedMinutes)
                throw Conflict("Çalışma saatleri çakışıyor. Boş bir saat seçin.");
        }
        if (tasks.GroupBy(x => x.PlannedDate).Any(day => day.Sum(x => x.PlannedMinutes) > 1440))
            throw new ArgumentException("Bir güne 24 saatten fazla çalışma eklenemez.");
        var topics = tasks.Where(x => x.TopicId.HasValue).Select(x => x.TopicId!.Value).Distinct().ToArray();
        if (topics.Length > 0 && await db.StudyCatalogTopics.CountAsync(x => Enumerable.Contains(topics, x.Id) && x.IsActive, cancellationToken) != topics.Length)
            throw new ArgumentException("Seçilen konulardan biri kullanılamıyor.");
        return tasks;
    }

    private async Task<ManualStudyPlanView> ViewAsync(StudyPlanRevision revision, CancellationToken cancellationToken)
    {
        var tasks = await db.StudyPlanTasks.AsNoTracking().Where(x => x.RevisionId == revision.Id && x.StudentId == revision.StudentId)
            .OrderBy(x => x.PlannedDate).ThenBy(x=>x.PlannedStartMinute).ThenBy(x => x.Id)
            .Select(x => new ManualStudyTaskView(x.Id, x.PlannedDate, x.Title, x.PlannedMinutes, x.TopicId, x.IsPinned, x.IsCompleted, x.ActualMinutes, x.CompletedAt, x.SourceName, x.PlannedQuestions, x.ActualQuestions,null,x.PlannedStartMinute,x.WeeklyLessonId,x.LessonNameSnapshot))
            .ToListAsync(cancellationToken);
        var outcomes = await StudyPlanOutcomeReader.ReadAsync(db, tasks.Select(x => x.Id).ToArray(), cancellationToken);
        return new(revision.Id, revision.Version, revision.Title, revision.Status, tasks.Select(x => x with { Outcomes = outcomes[x.Id].ToArray() }).ToArray(),revision.WeeklySource);
    }

    private async Task<T> LockedAsync<T>(Func<Guid, Task<T>> action, CancellationToken cancellationToken,
        Guid? targetStudent = null, bool teacherWrite = false)
    {
        var student = targetStudent ?? access.CurrentUserId ?? Guid.Empty;
        if (targetStudent.HasValue)
        {
            if (staffAccess is null) throw new BusinessRuleException("Authorization.Forbidden", "Personel erişimi doğrulanamadı.");
            if (teacherWrite) await staffAccess.RequireTeacherWriteAsync(student, cancellationToken);
            else await staffAccess.RequireAsync(student, cancellationToken);
        }
        else if (student == Guid.Empty || !access.IsCurrentStudent(student))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
        T completedResult = default!;
        var resultReady = false;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<object?, T>(null, async (_, _, token) =>
        {
            resultReady = false;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var transactionCommitted = false;
            var priorOutboxStates = db.ChangeTracker.Entries<OutboxState>()
                .ToDictionary(x => x.Entity.OutboxId, x => x.State);
            try
            {
                var key = "coaching-study-plan:" + student;
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
                var result = await action(student);
                completedResult = result;
                resultReady = true;
                await transaction.CommitAsync(cancellationToken);
                transactionCommitted = true;
                return result;
            }
            finally
            {
                // Rolled-back versions/tasks must not survive a retry or another call in this scope.
                foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is StudyPlanRevision or StudyPlanTask or StudyPlanTaskOutcome).ToArray())
                    entry.State = EntityState.Detached;
                if (!transactionCommitted)
                {
                    // A rolled-back publication must not be flushed by a later operation in this scope.
                    foreach (var entry in db.ChangeTracker.Entries<OutboxMessage>().ToArray())
                        entry.State = EntityState.Detached;
                    // The scoped MassTransit publisher retains this state instance. Keep it attached,
                    // restoring Added for newly created rows that were never committed.
                    foreach (var entry in db.ChangeTracker.Entries<OutboxState>().ToArray())
                        entry.State = priorOutboxStates.GetValueOrDefault(entry.Entity.OutboxId, EntityState.Added);
                }
            }
        }, async (_, _, token) =>
        {
            // A lost COMMIT acknowledgement is not a failed operation: verify the exact
            // returned revision/version before retrying a non-idempotent write.
            var committed = resultReady && completedResult is ManualStudyPlanView view
                && await db.StudyPlanRevisions.AsNoTracking().AnyAsync(x => x.Id == view.Id && x.StudentId == student
                    && x.Version == view.Version && x.Status == view.Status, token);
            if (committed)
            {
                var states = db.ChangeTracker.Entries<OutboxState>().Where(x => x.State == EntityState.Added).ToArray();
                var ids = states.Select(x => x.Entity.OutboxId).ToArray();
                var persisted = await db.Set<OutboxState>().AsNoTracking().Where(x => Enumerable.Contains(ids, x.OutboxId))
                    .Select(x => x.OutboxId).ToListAsync(token);
                foreach (var state in states.Where(x => persisted.Contains(x.Entity.OutboxId))) state.State = EntityState.Unchanged;
            }
            return new ExecutionResult<T>(committed, completedResult);
        }, cancellationToken);
    }

    private static BusinessRuleException Conflict(string message) => new("StudyPlanning.Conflict", message);
}
