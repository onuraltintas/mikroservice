using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Programs;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingProgramProgression(
    OwnedSpeedReadingDbContext db, ISpeedReadingInstitutionMemberEligibility eligibility) : ISpeedReadingProgramProgression
{
    public async Task<ProgramProgressionSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var policy = await db.ProgramProgressionPolicies.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == Guid.Empty, cancellationToken);
        return new(policy?.AllowEarlyAdvance ?? false, policy?.UpdatedAt);
    }

    public async Task<ProgramProgressionSettings> UpdateSettingsAsync(bool allow, Guid actorId, CancellationToken cancellationToken = default)
    {
        await SavePolicyAsync(Guid.Empty, allow, actorId, cancellationToken);
        return await GetSettingsAsync(cancellationToken);
    }

    public async Task<StudentProgressionPreference> GetPreferenceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureStudentAsync(userId, cancellationToken);
        return await ReadPreferenceAsync(userId, cancellationToken);
    }

    public async Task<StudentProgressionPreference> UpdatePreferenceAsync(Guid userId, string mode, Guid actorId, CancellationToken cancellationToken = default)
    {
        bool? allow = mode switch { "inherit" => null, "allow" => true, "deny" => false,
            _ => throw new ArgumentException("Geçerli bir program ilerleme seçeneği seçin.", nameof(mode)) };
        await EnsureStudentAsync(userId, cancellationToken);
        await SavePolicyAsync(userId, allow, actorId, cancellationToken);
        return await ReadPreferenceAsync(userId, cancellationToken);
    }

    private async Task EnsureStudentAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || !await eligibility.IsEligibleAsync(userId, SpeedReadingInstitutionMemberRole.Student, cancellationToken))
            throw new BusinessRuleException("ProgramProgression.StudentRequired", "Hızlı Okuma öğrenci erişimi olan bir kullanıcı seçin.");
    }

    private async Task<StudentProgressionPreference> ReadPreferenceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var preference = await db.ProgramProgressionPolicies.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return new(preference?.AllowEarlyAdvance is null ? "inherit" : preference.AllowEarlyAdvance.Value ? "allow" : "deny",
            preference?.AllowEarlyAdvance ?? settings.AllowEarlyAdvance, settings.AllowEarlyAdvance);
    }

    private Task SavePolicyAsync(Guid userId, bool? allow, Guid actorId, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("A valid administrator is required.", nameof(actorId));
        var attempt = 0;
        return OwnedSpeedReadingProgramAssignmentLock.ExecuteAsync(db, async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = await OwnedSpeedReadingProgramAssignmentLock.AcquireAsync(db, userId, cancellationToken);
            var policy = await db.ProgramProgressionPolicies.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (policy is null) { policy = new() { UserId = userId }; db.ProgramProgressionPolicies.Add(policy); }
            policy.AllowEarlyAdvance = allow;
            policy.UpdatedAt = DateTime.UtcNow;
            policy.UpdatedBy = actorId;
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task<ProgramDayAdvanceState> GetAdvanceStateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var preference = await ReadPreferenceAsync(userId, cancellationToken);
        var progress = await ActiveProgramAsync(userId, cancellationToken);
        if (progress is null) return new(null, 0, 0, preference.AllowEarlyAdvance, false);
        var nextDay = (progress.CurrentWeek - 1) * 7 + progress.CurrentDay;
        var available = SpeedReadingDailyProgressRules.GetAvailableDay(progress.AssignedDate, DateTime.UtcNow, false, progress.EarlyAdvanceDays);
        var ready = nextDay > available && await PreviousDayCompletedAsync(progress, nextDay - 1, cancellationToken);
        return new(progress.Id, nextDay - 1, nextDay, preference.AllowEarlyAdvance, preference.AllowEarlyAdvance && ready);
    }

    public Task<ProgramDayAdvanceState> AdvanceAsync(Guid userId, AdvanceProgramDayRequest request, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A valid student is required.", nameof(userId));
        var attempt = 0;
        return OwnedSpeedReadingProgramAssignmentLock.ExecuteAsync(db, async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = await OwnedSpeedReadingProgramAssignmentLock.AcquireAsync(db, userId, cancellationToken);
            var progress = await ActiveProgramAsync(userId, cancellationToken);
            if (progress is null || progress.Id != request.ProgramProgressId
                || request.CompletedProgramDay != (progress.CurrentWeek - 1) * 7 + progress.CurrentDay - 1)
                throw new BusinessRuleException("ProgramProgression.StaleDay", "Program günü değişti. Günlük egzersiz listesini yenileyin.");
            if (!(await ReadPreferenceAsync(userId, cancellationToken)).AllowEarlyAdvance)
                throw new BusinessRuleException("ProgramProgression.NotAllowed", "Sonraki program günü takvim günü geldiğinde açılacak.");
            if (!await PreviousDayCompletedAsync(progress, request.CompletedProgramDay, cancellationToken))
                throw new BusinessRuleException("ProgramProgression.DayIncomplete", "Önce günün tüm egzersizlerini tamamlayın.");
            var nextDay = request.CompletedProgramDay + 1;
            var available = SpeedReadingDailyProgressRules.GetAvailableDay(progress.AssignedDate, DateTime.UtcNow, false, progress.EarlyAdvanceDays);
            if (nextDay > available)
            {
                progress.GrantEarlyDays(nextDay - available, userId, DateTime.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return await GetAdvanceStateAsync(userId, cancellationToken);
        });
    }

    private Task<StudentProgramProgress?> ActiveProgramAsync(Guid userId, CancellationToken cancellationToken) =>
        db.StudentProgramProgresses.Where(item => item.UserId == userId && item.IsActive && !item.CompletedDate.HasValue && !item.IsStaffTraining)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> PreviousDayCompletedAsync(StudentProgramProgress progress, int completedDay, CancellationToken cancellationToken)
    {
        if (completedDay < 1) return false;
        var schedule = OwnedSpeedReadingProgramSchedule.Parse(progress.ScheduleJson);
        if (!schedule.Any(slot => (slot.WeekNumber - 1) * 7 + slot.DayNumber == completedDay + 1)) return false;
        var (week, day) = SpeedReadingDailyProgressRules.GetWeekAndDay(completedDay);
        var slots = schedule.Where(slot => slot.WeekNumber == week && slot.DayNumber == day).ToList();
        if (slots.Count == 0) return false;
        var logs = await db.DailyExerciseLogs.AsNoTracking().Where(item => item.UserId == progress.UserId
            && item.StudentProgramProgressId == progress.Id && item.WeekNumber == week && item.DayNumber == day).ToListAsync(cancellationToken);
        return slots.All(slot => OwnedSpeedReadingDailyProgress.FindCompletedSlot(slot, slots, logs) is not null);
    }
}
