using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingStudyAvailabilityService(CoachingDbContext db, ICoachingAccessPolicy access) : IStudyAvailabilityService
{
    public async Task<StudyAvailabilityView?> GetAsync(CancellationToken cancellationToken = default)
    {
        var studentId = RequireStudent();
        var preferences = await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        return preferences is null ? null : View(preferences);
    }

    public async Task<StudyAvailabilityView> ReplaceAsync(StudyAvailabilityUpdate request, CancellationToken cancellationToken = default)
    {
        var studentId = RequireStudent();
        ArgumentNullException.ThrowIfNull(request);
        if (request.Windows is null || request.Windows.Count > 42 || request.Windows.Any(x => x is null))
            throw new ArgumentException("Haftalık çalışma saatleri geçersiz; en fazla 42 aralık girilebilir.");
        // Fully validate new values before modifying the tracked preferences.
        var validated = StudyAvailability.Create(studentId, request.TimeZoneId);
        var windows = request.Windows.Select(x => new StudyAvailabilityWindow(x.Day, x.StartMinute, x.EndMinute)).ToArray();
        validated.ReplaceWindows(windows);
        var preferences = await db.StudyAvailability.SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        if (preferences is null)
        {
            if (request.ExpectedVersion.HasValue) throw Conflict();
            preferences = validated;
            db.StudyAvailability.Add(preferences);
        }
        else
        {
            if (request.ExpectedVersion != preferences.Version) throw Conflict();
            preferences.ReplacePreferences(request.TimeZoneId, windows);
        }
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" }) { throw Conflict(); }
        return View(preferences);
    }

    private Guid RequireStudent()
    {
        if (access.CurrentUserId is not { } studentId || !access.IsCurrentStudent(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
        return studentId;
    }
    private static BusinessRuleException Conflict() => new("StudyPlanning.Conflict", "Tercihler başka bir işlemde değişti. Sayfayı yenileyip tekrar deneyin.");
    private static StudyAvailabilityView View(StudyAvailability preferences) => new(preferences.Version, preferences.TimeZoneId,
        preferences.Windows.Select(x => new StudyWindowInput(x.Day, x.StartMinute, x.EndMinute)).ToArray());
}
