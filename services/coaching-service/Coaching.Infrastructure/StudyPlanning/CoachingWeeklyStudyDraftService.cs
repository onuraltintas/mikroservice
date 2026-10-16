using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingWeeklyStudyDraftService(CoachingDbContext db,ICoachingAccessPolicy access,ILearningSetupService setup):IWeeklyStudyDraftService
{
    public async Task<WeeklyDraftPage> GetAsync(CancellationToken ct)
    {
        var student=Require();var draft=await db.Set<WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student,ct);
        return new(draft is null ? null : View(draft),(await setup.GetCatalogAsync(null,1,ct)).Lessons);
    }
    public Task<WeeklyDraftView> SaveAsync(WeeklyDraftInput r,CancellationToken ct)
    {
        var student=Require();
        if(!db.Database.IsRelational())return SaveCore(r,student,ct);
        async Task<WeeklyDraftView> Locked()
        {
            var key="coaching-study-plan:"+student;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))",ct);
            return await SaveCore(r,student,ct);
        }
        if(db.Database.CurrentTransaction is not null)return Locked();
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
        {
            await using var transaction=await db.Database.BeginTransactionAsync(ct);
            try{var result=await Locked();await transaction.CommitAsync(ct);return result;}
            finally{foreach(var entry in db.ChangeTracker.Entries<WeeklyStudyDraft>().ToArray())entry.State=EntityState.Detached;}
        });
    }
    private async Task<WeeklyDraftView> SaveCore(WeeklyDraftInput r,Guid student,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        if(r.ExpectedVersion<0 || r.Slots is null || r.Slots.Count>336 || r.Slots.Any(x=>x is null)) throw new ArgumentException("Haftalık taslağı kontrol edin.");
        var choices=(await setup.GetCatalogAsync(null,1,ct)).Lessons.ToDictionary(x=>x.Id);
        if(r.Slots.Any(x=>!choices.ContainsKey(x.LessonId))) throw new ArgumentException("Çalışma düzeninize uygun aktif bir ders seçin; dersler değişmiş olabilir.");
        var candidate=WeeklyStudyDraft.Create(student);
        candidate.Replace(r.TimeZoneId,r.Slots.Select(x=>new WeeklyDraftSlot(x.Id,x.LessonId,choices[x.LessonId].Name,choices[x.LessonId].ExamCode,x.Day,x.StartMinute,x.DurationMinutes)).ToArray());
        var draft=await db.Set<WeeklyStudyDraft>().SingleOrDefaultAsync(x=>x.StudentId==student,ct);
        if(draft is not null) await db.Entry(draft).ReloadAsync(ct);
        if(draft is null ? r.ExpectedVersion.HasValue : r.ExpectedVersion!=draft.Version) throw Conflict();
        if(draft is null) db.Add(draft=candidate);else draft.Replace(candidate.TimeZoneId,candidate.Slots);
        try { await db.SaveChangesAsync(ct); }
        catch(ConcurrencyException) { db.Entry(draft).State=EntityState.Detached;throw Conflict(); }
        catch(DbUpdateException e) when(e.InnerException is PostgresException {SqlState:"23505"}) { db.Entry(draft).State=EntityState.Detached;throw Conflict(); }
        return View(draft);
    }
    private Guid Require()=>access.CurrentUserId is {} id && access.IsCurrentStudent(id) ? id : throw new BusinessRuleException("Authorization.Forbidden","Yalnız kendi öğrenci hesabınızla kullanabilirsiniz.");
    private static BusinessRuleException Conflict()=>new("StudyPlanning.Conflict","Haftalık taslak başka bir işlemde değişti. Değişikliklerinizi kontrol edip taslağı yeniden yükleyin.");
    public static WeeklyDraftView View(WeeklyStudyDraft d)=>new(d.Version,d.TimeZoneId,d.Slots);
}
