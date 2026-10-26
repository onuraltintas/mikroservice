using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MassTransit.EntityFrameworkCoreIntegration;
using System.Text.Json;
using EduPlatform.Shared.Contracts.Events.Coaching;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed partial class CoachingWeeklyReviewService(CoachingDbContext db, ICoachingAccessPolicy access,
    CoachingStudyPlanAccess staff, OwnedCoachingDirectory directory, TimeProvider clock, ICoachingEventPublisher events)
{
    private static BusinessRuleException Conflict() => new("WeeklyReview.Conflict", "Haftalık kayıt değişti. Girdileriniz korunuyor; güncel kaydı yükleyip tekrar inceleyin.");
    private static BusinessRuleException Forbidden() => new("Authorization.Forbidden", "Bu haftalık kayıt için yetkiniz yok.");
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);
    private void ValidateWeek(DateOnly week)
    {
        if (week == default || week.DayOfWeek != DayOfWeek.Monday || week > Today) throw new ArgumentException("Başlamış bir haftanın pazartesi tarihini seçin.");
    }
    private async Task<Guid> Require(Guid? requested, bool write, CancellationToken ct)
    {
        if (requested is { } student)
        {
            if (write) await staff.RequireTeacherWriteAsync(student, ct); else await staff.RequireAsync(student, ct);
            await CoachingStudentReadAuthorization.RequireAsync(access, directory, [student], ct);
            return student;
        }
        if (access.CurrentUserId is { } own && access.IsCurrentStudent(own)
            && await db.CoachingStudentProfiles.AnyAsync(x => x.UserId == own && x.IsActive, ct)) return own;
        throw Forbidden();
    }
    public async Task<WeeklyHomeView> HomeAsync(CancellationToken ct)
    {
        var student = await Require(null, false, ct); var today = Today; var current = today.AddDays(-((int)today.DayOfWeek + 6) % 7); var week = current.AddDays(-7);
        var missing = await CoachingReflectionBacklog.MissingAsync(db,[student],today,today,ct);
        var needs = missing.Length>0;
        if(needs)week=missing[0].WeekStart;
        var open = db.Set<WeeklyDecision>().AsNoTracking().Where(x => x.StudentId == student && (x.Status == "Planned" || x.Status == "InProgress"));
        var total = await open.CountAsync(ct); var overdue = await open.CountAsync(x => x.DueOn < today, ct);
        var rows = await open.OrderBy(x => x.DueOn).ThenBy(x => x.Id).Take(5).Select(x => new WeeklyHomeDecision(x.Id, x.ReviewWeekStart, x.Title, x.DueOn, x.DueOn < today)).ToArrayAsync(ct);
        return new(week, current, needs, total, overdue, rows,missing.Length);
    }
    public async Task<WeeklyReviewView> GetAsync(Guid? studentId, DateOnly week, int page, CancellationToken ct, Guid? decisionId = null)
    {
        ValidateWeek(week); if (page is < 1 or > 1000) throw new ArgumentException("Geçerli bir sayfa seçin.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var student = await Require(studentId, false, ct);
            var reflection = await db.Set<WeeklyReflection>().AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student && x.WeekStart == week, ct);
            var query = db.Set<WeeklyDecision>().AsNoTracking().Where(x => x.StudentId == student
                && (x.ReviewWeekStart == week || x.ReviewWeekStart < week && (x.Status == "Planned" || x.Status == "InProgress")));
            var count = await query.CountAsync(ct);
            if (decisionId.HasValue)
            {
                var target = await query.SingleOrDefaultAsync(x => x.Id == decisionId.Value, ct) ?? throw new ArgumentException("Seçilen karar bu hafta içinde bulunamadı.");
                page = await query.CountAsync(x => x.DueOn < target.DueOn || x.DueOn == target.DueOn && x.Id.CompareTo(target.Id) < 0, ct) / 20 + 1;
            }
            var decisions = await query.OrderBy(x => x.DueOn).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20).ToArrayAsync(ct);
            var end = week.AddDays(6);
            var work = await CoachingStudyWorkReports.ReadAsync(db,student,week,end,ct);
            var summary = CoachingStudyWorkReports.Calculate(week,end,work);
            var reviewRows = reflection is null ? [] : await db.Set<WeeklyReflectionReview>().AsNoTracking()
                .Where(x=>x.ReflectionId==reflection.Id).OrderBy(x=>x.CreatedAt).ThenBy(x=>x.Id).ToArrayAsync(ct);
            var reviews=reviewRows.Select(ReviewView).ToArray();
            var results = await ResultsAsync(student, week, ct);
            var latest=reviewRows.LastOrDefault(x=>x.TeacherId==access.CurrentUserId);
            var reason=reflection is null||!studentId.HasValue?null:CoachingReviewFollowUp.Reason(reflection,latest,CoachingReviewFollowUp.Fingerprint(results));
            var result = new WeeklyReviewView(week, Today, reflection is null ? null : View(reflection),
                new(summary.ScheduledTasks, summary.CompletedTasks, summary.PlannedMinutes, summary.ActualMinutes,summary.LegacyDateActualMinutes),
                await WorkViewsAsync(decisions.Select(x => View(x, studentId.HasValue && x.ResponsibleTeacherId == access.CurrentUserId && access.IsCurrentTeacher(x.ResponsibleTeacherId))).ToArray(), student, ct), count, page,
                !studentId.HasValue, studentId.HasValue && access.CurrentUserId is { } actor && access.IsCurrentTeacher(actor), reviews,reason is not null,results,reason);

            await tx.CommitAsync(ct); return result;
        });
    }
    public Task<WeeklyReflectionReviewView> ReviewReflectionAsync(Guid student, DateOnly week, WeeklyReflectionReviewSave request, CancellationToken ct)
    {
        ValidateWeek(week); var note = Text(request.Note, 2000, true);
        if (note.Length < 5 || request.ExpectedReflectionVersion < 0) throw new ArgumentException("İnceleme notu en az 5 karakter olmalıdır.");
        return Write(async () => {
            await Require(student, true, ct); await Lock(student, ct); await Require(student, true, ct);
            var reflection = await db.Set<WeeklyReflection>().SingleOrDefaultAsync(x => x.StudentId == student && x.WeekStart == week, ct)
                ?? throw new ArgumentException("Öğrenci henüz değerlendirme kaydetmedi.");
            if (reflection.Version != request.ExpectedReflectionVersion) throw Conflict();
            var teacher = access.CurrentUserId!.Value;
            var results=await ResultsAsync(student,week,ct);var fingerprint=CoachingReviewFollowUp.Fingerprint(results);
            var latest=await db.Set<WeeklyReflectionReview>().AsNoTracking().Where(x=>x.ReflectionId==reflection.Id&&x.TeacherId==teacher)
                .OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).FirstOrDefaultAsync(ct);
            if(request.ExpectedResultsFingerprint is not null&&request.ExpectedResultsFingerprint!=results.Fingerprint)throw Conflict();
            if(latest is not null&&latest.ReflectionVersion==reflection.Version&&latest.EvidenceFingerprint==fingerprint) {
                if(latest.Note!=note)throw Conflict();
                return ReviewView(latest);
            }
            // Old clients can acknowledge an unchanged reflection snapshot. They cannot silently close changed evidence.
            if(request.ExpectedResultsFingerprint is null) {
                var saved=reflection.ResultsJson is null?null:JsonSerializer.Deserialize<WeeklyResultSnapshot>(reflection.ResultsJson);
                if(latest?.ReflectionVersion==reflection.Version || saved?.Fingerprint!=results.Fingerprint)throw Conflict();
            }
            var review=WeeklyReflectionReview.Create(reflection,teacher,note,fingerprint,JsonSerializer.Serialize(results));
            db.Add(review);await db.SaveChangesAsync(ct);return ReviewView(review);
        },(value,token)=>db.Set<WeeklyReflectionReview>().AsNoTracking().AnyAsync(x=>x.Id==value.Id&&x.TeacherId==value.TeacherId,token),ct);
    }
    private static WeeklyReflectionReviewView ReviewView(WeeklyReflectionReview x)=>new(x.TeacherId,x.ReflectionVersion,x.Note,x.CreatedAt,
        x.EvidenceFingerprint,x.EvidenceSnapshotJson is null?null:JsonSerializer.Deserialize<WeeklyResultSnapshot>(x.EvidenceSnapshotJson),x.Id);

    public Task<WeeklyReflectionView> SaveReflectionAsync(DateOnly week, WeeklyReflectionSave r, CancellationToken ct)
    {
        ValidateWeek(week);
        var successes = Text(r.Successes, 2000, true); var obstacles = Text(r.Obstacles, 2000, false); var focus = Text(r.NextFocus, 2000, true);
        if (r.Rating is < 1 or > 5 || r.ExpectedVersion < 0) throw new ArgumentException("Değerlendirme puanını ve kayıt sürümünü kontrol edin.");
        return Write(async () => {
            var student = await Require(null, true, ct); await Lock(student, ct); await Require(null, true, ct);
            var row = await db.Set<WeeklyReflection>().SingleOrDefaultAsync(x => x.StudentId == student && x.WeekStart == week, ct);
            var created = row is null;
            if (row is null) { if (r.ExpectedVersion.HasValue) throw Conflict(); row = WeeklyReflection.Create(student, week); db.Add(row); }
            else if (!r.ExpectedVersion.HasValue)
            {
                if (row.Successes != successes || row.Obstacles != obstacles || row.NextFocus != focus || row.Rating != r.Rating) throw Conflict();
                if (r.ExpectedResultsFingerprint is null && r.DecisionEffects is null) return View(row);
            }
            else if (row.Version != r.ExpectedVersion) throw Conflict();
            var sameText = row.Successes == successes && row.Obstacles == obstacles && row.NextFocus == focus && row.Rating == r.Rating;
            if (sameText && r.ExpectedResultsFingerprint is null && r.DecisionEffects is null) return View(row);
            var context = await ResultsAsync(student, week, ct);
            if (r.ExpectedResultsFingerprint is not null && r.ExpectedResultsFingerprint != context.Fingerprint) throw Conflict();
            if (r.DecisionEffects is { Count: > 0 } && r.ExpectedResultsFingerprint is null)
                throw new ArgumentException("Karar sonuçlarını güncel verilerle inceleyin.");
            var effects = r.DecisionEffects ?? [];
            if (effects.Count > 500 || effects.Any(x => x is null) || effects.Select(x => x.DecisionId).Distinct().Count() != effects.Count
                || effects.Any(x => !context.Decisions.Any(d => d.DecisionId == x.DecisionId)
                    || x.Effect is not ("Helped" or "PartlyHelped" or "DidNotHelp" or "NotTried")))
                throw new ArgumentException("Bu haftaya ait kararları ve sonuçlarını kontrol edin.");
            var normalized = effects.ToDictionary(x => x.DecisionId, x => new WeeklyDecisionEffect(x.DecisionId, x.Effect, Text(x.Note,1000,true)));
            if (normalized.Values.Any(x => x.Note.Length < 5)) throw new ArgumentException("Karar sonuç notu en az 5 karakter olmalıdır.");
            var previous = row.ResultsJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<WeeklyResultSnapshot>(row.ResultsJson);
            // An older client editing prose must not erase previously recorded decision effects.
            if (r.DecisionEffects is null && previous is not null)
                foreach (var old in previous.Decisions.Where(x => x.Effect is not null && context.Decisions.Any(d => d.DecisionId == x.DecisionId)))
                    normalized.TryAdd(old.DecisionId, new(old.DecisionId, old.Effect!, old.EffectNote ?? ""));
            var decisionsWithEffects = context.Decisions.Select(x => normalized.TryGetValue(x.DecisionId,out var effect)
                ? x with { Effect = effect.Effect, EffectNote = effect.Note } : x).ToArray();
            if (sameText && previous?.Fingerprint == context.Fingerprint
                && System.Text.Json.JsonSerializer.Serialize(previous.Decisions) == System.Text.Json.JsonSerializer.Serialize(decisionsWithEffects)) return View(row);
            if (!created && !r.ExpectedVersion.HasValue) throw Conflict();
            row.EditWithResults(successes, obstacles, focus, r.Rating,
                System.Text.Json.JsonSerializer.Serialize(context with { Decisions = decisionsWithEffects }));
            await db.SaveChangesAsync(ct);
            var candidates = await db.CoachingTeacherStudentAssignments.AsNoTracking().Where(x => x.StudentUserId == student && x.IsActive).Select(x => x.TeacherUserId).Distinct().ToArrayAsync(ct);
            var teachers = new List<Guid>();
            foreach (var teacher in candidates)
                if ((await directory.AuthorizeStudentReadAsync(teacher, [student], ct)).Contains(student)) teachers.Add(teacher);
            if (teachers.Count > 0) { await events.PublishAsync(new WeeklyReflectionSavedEvent(row.Id, student, week, teachers.ToArray()), ct); await db.SaveChangesAsync(ct); }
            return View(row);
        }, (value, token) => db.Set<WeeklyReflection>().AsNoTracking().AnyAsync(x => x.StudentId == access.CurrentUserId && x.WeekStart == week && x.Version == value.Version, token), ct);
    }
    public Task<WeeklyDecisionView> SaveDecisionAsync(Guid studentId, DateOnly week, Guid id, WeeklyDecisionSave r, CancellationToken ct)
    {
        ValidateWeek(week); var title = Text(r.Title, 200, true); var note = Text(r.Note, 2000, false);
        if (id == Guid.Empty || r.ExpectedVersion < 0
            || r.PreviousDecisionId==Guid.Empty || r.SourceReflectionVersion<0
            || r.Status is not ("Planned" or "InProgress" or "Completed" or "Cancelled")) throw new ArgumentException("Kararın tarihini ve durumunu kontrol edin.");
        return Write(async () => {
            await Require(studentId, true, ct); await Lock(studentId, ct); await Require(studentId, true, ct); var teacher = access.CurrentUserId!.Value;
            var row = await db.Set<WeeklyDecision>().SingleOrDefaultAsync(x => x.Id == id, ct);
            var created = row is null;
            if (row is null)
            {
                if (r.ExpectedVersion.HasValue || r.Status != "Planned") throw Conflict();
                // Keep the source review week, but allow a delayed review to produce a future action.
                // The lower bound retains compatibility with historical entries in their original next week.
                if (r.DueOn < week.AddDays(7)) throw new ArgumentException("Yeni kararın tarihi değerlendirme haftasından sonra olmalıdır.");
                var institution = await directory.AuthorizeTeacherTargetsAsync(teacher,[studentId],null,false,ct);
                row = WeeklyDecision.Create(id, studentId, teacher, week, institution);
                var reflection=await db.Set<WeeklyReflection>().AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==studentId&&x.WeekStart==week,ct);
                if(reflection is null) {
                    if(r.SourceReflectionVersion.HasValue||r.PreviousDecisionId.HasValue)throw new ArgumentException("Kaynak değerlendirme henüz kaydedilmedi.");
                } else {
                    if(r.SourceReflectionVersion.HasValue&&r.SourceReflectionVersion!=reflection.Version)throw Conflict();
                    var results=await ResultsAsync(studentId,week,ct);
                    if(r.ExpectedResultsFingerprint is not null&&r.ExpectedResultsFingerprint!=results.Fingerprint)throw Conflict();
                    var saved=reflection.ResultsJson is null?null:JsonSerializer.Deserialize<WeeklyResultSnapshot>(reflection.ResultsJson);
                    WeeklyResultDecision? previous=null;
                    if(r.PreviousDecisionId is {} previousId) {
                        if(!await db.Set<WeeklyDecision>().AnyAsync(x=>x.Id==previousId&&x.StudentId==studentId&&x.ReviewWeekStart<week,ct))throw new ArgumentException("Önceki karar bu öğrenciye ve önceki bir haftaya ait olmalıdır.");
                        previous=results.Decisions.SingleOrDefault(x=>x.DecisionId==previousId)??saved?.Decisions.SingleOrDefault(x=>x.DecisionId==previousId)
                            ??throw new ArgumentException("Önceki karar kaynak haftanın sonuçlarında bulunamadı.");
                        if(saved?.Decisions.SingleOrDefault(x=>x.DecisionId==previousId) is {} assessed)
                            previous=previous with {Effect=assessed.Effect,EffectNote=assessed.EffectNote};
                    }
                    var source=new WeeklyDecisionSourceView(reflection.Id,reflection.Version,reflection.WeekStart,reflection.Successes,reflection.Obstacles,reflection.NextFocus,reflection.Rating,saved,results);
                    row.CaptureOrigin(reflection.Id,reflection.Version,JsonSerializer.Serialize(source),r.PreviousDecisionId,previous is null?null:JsonSerializer.Serialize(previous));
                }
                db.Add(row);
            }
            else
            {
                if (row.StudentId != studentId || row.ResponsibleTeacherId != teacher || row.ReviewWeekStart != week) throw Forbidden();
                if(r.SourceReflectionVersion.HasValue&&r.SourceReflectionVersion!=row.SourceReflectionVersion || r.PreviousDecisionId.HasValue&&r.PreviousDecisionId!=row.PreviousDecisionId)
                    throw new ArgumentException("Kaynak değerlendirme ve önceki karar bağlantısı sonradan değiştirilemez.");
                if (r.DueOn != row.DueOn)
                    throw new ArgumentException("Son tarihi değiştirmek için gerekçeli tarih değişikliği işlemini kullanın.");
                if (!r.ExpectedVersion.HasValue) { if (row.Matches(title, note, r.DueOn, r.Status)) return View(row, true); throw Conflict(); }
                if (row.Version != r.ExpectedVersion) throw Conflict();
                if (row.Matches(title, note, r.DueOn, r.Status)) return View(row, true);
            }
            row.Edit(title, note, r.DueOn, r.Status, clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(ct);
            await events.PublishAsync(new WeeklyDecisionChangedEvent(id, studentId, teacher, week, created), ct); await db.SaveChangesAsync(ct);
            return View(row, true);
        }, (value, token) => db.Set<WeeklyDecision>().AsNoTracking().AnyAsync(x => x.Id == id && x.StudentId == studentId && x.Version == value.Version, token), ct);
    }
    public Task<WeeklyDecisionView> PostponeAsync(Guid student, DateOnly week, Guid id, WeeklyDecisionPostpone request, CancellationToken ct)
        => WriteWorkAsync(student,week,id,async row => {
            var reason=Text(request.Reason,1000,true);
            var last=row.History.LastOrDefault();
            if (request.ExpectedVersion>=0 && row.Version==request.ExpectedVersion+1 && row.DueOn==request.DueOn
                && last is {Action:"Postponement"} && last.Reason==reason && last.ActorUserId==access.CurrentUserId) return;
            Version(row,request.ExpectedVersion);
            row.Postpone(request.DueOn,reason,access.CurrentUserId!.Value,Today,clock.GetUtcNow().UtcDateTime);
            await ChangedWorkAsync(row,ct);
        },ct);

    public Task<WeeklyDecisionView> ChangeDeadlineAsync(Guid student,DateOnly week,Guid id,WeeklyDecisionPostpone request,CancellationToken ct)
        =>WriteWorkAsync(student,week,id,async row=> {
            var reason=Text(request.Reason,1000,true);var last=row.History.LastOrDefault();
            if(request.ExpectedVersion>=0&&row.Version==request.ExpectedVersion+1&&row.DueOn==request.DueOn
                &&last?.Action is ("DeadlineChange" or "Postponement")&&last.Reason==reason&&last.ActorUserId==access.CurrentUserId)return;
            Version(row,request.ExpectedVersion);var history=row.HistoryJson;
            row.ChangeDeadline(request.DueOn,reason,access.CurrentUserId!.Value,Today,clock.GetUtcNow().UtcDateTime);
            if(history!=row.HistoryJson)await ChangedWorkAsync(row,ct);
        },ct);

    private Task Lock(Guid student, CancellationToken ct)
    {
        var key = "coaching-study-plan:" + student;
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }
    private async Task<T> Write<T>(Func<Task<T>> action, Func<T, CancellationToken, Task<bool>> verify, CancellationToken ct)
    {
        T result = default!; var ready = false;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<object?, T>(null, async (_, _, token) => {
            ready = false; await using var tx = await db.Database.BeginTransactionAsync(token); var committed = false;
            var states = db.ChangeTracker.Entries<OutboxState>().ToDictionary(x => x.Entity.OutboxId, x => x.State);
            try { result = await action(); ready = true; await tx.CommitAsync(token); committed = true; return result; }
            catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { throw Conflict(); }
            finally {
                foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is WeeklyReflectionReview or WeeklyReflection or WeeklyDecision
                    or StudyPlanTask or StudyPlanRevision or Assignment or AssignmentStudent or IdempotencyRecord).ToArray()) entry.State = EntityState.Detached;
                if (!committed) {
                    foreach (var entry in db.ChangeTracker.Entries<OutboxMessage>().ToArray()) entry.State = EntityState.Detached;
                    foreach (var entry in db.ChangeTracker.Entries<OutboxState>().ToArray()) entry.State = states.GetValueOrDefault(entry.Entity.OutboxId, EntityState.Added);
                }
            }
        }, async (_, _, token) => {
            var committed = ready && await verify(result, token);
            if (committed) {
                var states = db.ChangeTracker.Entries<OutboxState>().Where(x => x.State == EntityState.Added).ToArray(); var ids = states.Select(x => x.Entity.OutboxId).ToArray();
                var persisted = await db.Set<OutboxState>().AsNoTracking().Where(x => Enumerable.Contains(ids, x.OutboxId)).Select(x => x.OutboxId).ToArrayAsync(token);
                foreach (var entry in states.Where(x => persisted.Contains(x.Entity.OutboxId))) entry.State = EntityState.Unchanged;
            }
            return new ExecutionResult<T>(committed, result);
        }, ct);
    }
    private Task<WeeklyResultSnapshot> ResultsAsync(Guid student,DateOnly week,CancellationToken ct)=>CoachingWeeklyResultReader.ReadAsync(db,clock,student,week,ct);
    private WeeklyDecisionView View(WeeklyDecision x,bool editable)=>CoachingWeeklyWorkReader.View(x,editable,Today);
    private static WeeklyReflectionView View(WeeklyReflection x) => new(x.Version, x.Successes, x.Obstacles, x.NextFocus, x.Rating, x.UpdatedAt ?? x.CreatedAt, x.ResultsJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<WeeklyResultSnapshot>(x.ResultsJson), x.ReflectionHistory.Select(h => new WeeklyReflectionHistoryView(h.Version,h.SavedAt,h.Successes,h.Obstacles,h.NextFocus,h.Rating,
            h.ResultsJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<WeeklyResultSnapshot>(h.ResultsJson))).ToArray());
    private static string Text(string? value, int max, bool required)
    {
        value = value?.Trim() ?? ""; if (value.Length > max || required && value.Length == 0) throw new ArgumentException("Zorunlu alanları ve metin uzunluklarını kontrol edin."); return value;
    }
}
