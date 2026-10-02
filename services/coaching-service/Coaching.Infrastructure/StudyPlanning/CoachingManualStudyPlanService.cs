using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingManualStudyPlanService(CoachingDbContext db, ICoachingAccessPolicy access) : IManualStudyPlanService
{
    public Task<StudyPlanPage> ListAsync(int pageNumber, int pageSize, StudyPlanStatus? status, CancellationToken cancellationToken = default)
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
        }, cancellationToken);

    public Task<ManualStudyPlanView?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => LockedAsync<ManualStudyPlanView?>(async student =>
        {
            var revision = await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.StudentId == student, cancellationToken);
            return revision is null ? null : await ViewAsync(revision, cancellationToken);
        }, cancellationToken);

    public Task<ManualStudyPlanView> CreateDraftAsync(ManualStudyPlanInput request, CancellationToken cancellationToken = default)
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
        }, cancellationToken);

    public Task<ManualStudyPlanView> ReplaceDraftAsync(Guid id, int expectedVersion, ManualStudyPlanInput request, CancellationToken cancellationToken = default)
        => LockedAsync(async student =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var revision = await OwnedAsync(id, student, expectedVersion, cancellationToken);
            if (revision.Status != StudyPlanStatus.Draft) throw Conflict("Yalnız taslak plan düzenlenebilir.");
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
        }, cancellationToken);

    public Task<ManualStudyPlanView> PublishAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default)
        => LockedAsync(async student =>
        {
            var revision = await OwnedAsync(id, student, expectedVersion, cancellationToken);
            if (revision.Status != StudyPlanStatus.Draft) throw Conflict("Yalnız taslak plan yayımlanabilir.");
            if (!await db.StudyPlanTasks.AnyAsync(x => x.RevisionId == id && x.StudentId == student, cancellationToken))
                throw Conflict("Yayımlamadan önce en az bir çalışma ekleyin.");
            var previous = await db.StudyPlanRevisions.SingleOrDefaultAsync(x => x.StudentId == student && x.IsActive, cancellationToken);
            if (previous is not null)
            {
                previous.Archive();
                // Release the unique active slot before activating the draft, within the same transaction.
                await db.SaveChangesAsync(cancellationToken);
            }
            revision.Activate();
            await db.SaveChangesAsync(cancellationToken);
            return await ViewAsync(revision, cancellationToken);
        }, cancellationToken);

    private async Task<StudyPlanRevision> OwnedAsync(Guid id, Guid student, int version, CancellationToken cancellationToken)
    {
        var revision = await db.StudyPlanRevisions.SingleOrDefaultAsync(x => x.Id == id && x.StudentId == student, cancellationToken)
            ?? throw new BusinessRuleException("StudyPlanning.NotFound", "Plan bulunamadı.");
        if (revision.Version != version) throw Conflict("Plan değişti. Sayfayı yenileyip tekrar deneyin.");
        return revision;
    }

    public Task<ManualStudyPlanView> CompleteTaskAsync(Guid id, Guid taskId, int expectedVersion, int actualMinutes, CancellationToken cancellationToken = default)
        => LockedAsync(async student =>
        {
            var revision = await OwnedAsync(id, student, expectedVersion, cancellationToken);
            var task = await ActiveTaskAsync(revision, taskId, cancellationToken);
            if (actualMinutes is < 1 or > 1440) throw new ArgumentException("Gerçek çalışma süresi 1–1440 dakika olmalıdır.");
            if (task.IsCompleted)
            {
                if (task.ActualMinutes != actualMinutes) throw Conflict("Tamamlanan çalışmanın süresi değiştirilemez.");
                return await ViewAsync(revision, cancellationToken);
            }
            task.Complete(actualMinutes);
            revision.RecordTaskChange();
            await db.SaveChangesAsync(cancellationToken);
            return await ViewAsync(revision, cancellationToken);
        }, cancellationToken);

    public Task<ManualStudyPlanView> RescheduleTaskAsync(Guid id, Guid taskId, int expectedVersion, DateOnly plannedDate, CancellationToken cancellationToken = default)
        => LockedAsync(async student =>
        {
            var revision = await OwnedAsync(id, student, expectedVersion, cancellationToken);
            var task = await ActiveTaskAsync(revision, taskId, cancellationToken);
            if (task.IsCompleted) throw Conflict("Tamamlanan çalışma başka güne taşınamaz.");
            if (plannedDate == default) throw new ArgumentException("Geçerli çalışma tarihi seçin.");
            if (task.PlannedDate == plannedDate) return await ViewAsync(revision, cancellationToken);
            var dailyMinutes = await db.StudyPlanTasks.Where(x => x.RevisionId == id && x.StudentId == student
                && x.Id != taskId && x.PlannedDate == plannedDate).SumAsync(x => x.PlannedMinutes, cancellationToken);
            if (dailyMinutes + task.PlannedMinutes > 1440) throw new ArgumentException("Bir güne 24 saatten fazla çalışma eklenemez.");
            task.Reschedule(plannedDate);
            revision.RecordTaskChange();
            await db.SaveChangesAsync(cancellationToken);
            return await ViewAsync(revision, cancellationToken);
        }, cancellationToken);

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
        var tasks = request.Tasks.Select(x => StudyPlanTask.Create(revision, x.PlannedDate, x.Title, x.PlannedMinutes, x.TopicId, x.IsPinned)).ToArray();
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
            .OrderBy(x => x.PlannedDate).ThenBy(x => x.Id)
            .Select(x => new ManualStudyTaskView(x.Id, x.PlannedDate, x.Title, x.PlannedMinutes, x.TopicId, x.IsPinned, x.IsCompleted, x.ActualMinutes, x.CompletedAt))
            .ToListAsync(cancellationToken);
        return new(revision.Id, revision.Version, revision.Title, revision.Status, tasks);
    }

    private async Task<T> LockedAsync<T>(Func<Guid, Task<T>> action, CancellationToken cancellationToken)
    {
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
        T completedResult = default!;
        var resultReady = false;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<object?, T>(null, async (_, _, token) =>
        {
            resultReady = false;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var key = "coaching-study-plan:" + student;
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
                var result = await action(student);
                completedResult = result;
                resultReady = true;
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            finally
            {
                // Rolled-back versions/tasks must not survive a retry or another call in this scope.
                foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is StudyPlanRevision or StudyPlanTask).ToArray())
                    entry.State = EntityState.Detached;
            }
        }, async (_, _, token) =>
        {
            // A lost COMMIT acknowledgement is not a failed operation: verify the exact
            // returned revision/version before retrying a non-idempotent write.
            var committed = resultReady && completedResult is ManualStudyPlanView view
                && await db.StudyPlanRevisions.AsNoTracking().AnyAsync(x => x.Id == view.Id && x.StudentId == student
                    && x.Version == view.Version && x.Status == view.Status, token);
            return new ExecutionResult<T>(committed, completedResult);
        }, cancellationToken);
    }

    private static BusinessRuleException Conflict(string message) => new("StudyPlanning.Conflict", message);
}
