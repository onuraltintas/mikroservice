using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.StudyPlanning;

/// <summary>
/// Paket H4a (H4b review): the student's plan preferences (exam focus K6 and tempo), the single source of the two values. Saving them
/// never touches the free hours; a routine profile shows them in its answers and saves them through <see cref="Apply"/>.
/// </summary>
public sealed class CoachingStudyPlanPreferencesService(CoachingDbContext db, ICoachingAccessPolicy access, CoachingStudyPlanAccess staff)
    : IStudyPlanPreferencesService
{
    public const string ConflictMessage = "Plan tercihlerin başka bir yerde değişti; yeniden yükle.";

    public async Task<StudyPlanPreferencesView> GetAsync(CancellationToken cancellationToken = default) =>
        await ReadAsync(db, RequireStudent(), cancellationToken);

    public async Task<StudyPlanPreferencesView> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await ReadAsync(db, await staff.RequireAsync(studentId, cancellationToken), cancellationToken);

    public async Task<StudyPlanPreferencesView> ReplaceAsync(StudyPlanPreferencesUpdate request, CancellationToken cancellationToken = default)
    {
        var studentId = RequireStudent();
        ArgumentNullException.ThrowIfNull(request);
        var (focus, tempo) = Validate(request.ExamFocus, request.Tempo, await CoachingStudyRoutineService.PreparesForYksAsync(db, studentId, cancellationToken));
        var row = await db.Set<StudyPlanPreferences>().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        Apply(db, studentId, row, focus, tempo);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (ConcurrencyException) { throw Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" }) { throw Conflict(); }
        return View(await db.Set<StudyPlanPreferences>().AsNoTracking().SingleAsync(x => x.StudentId == studentId, cancellationToken), true);
    }

    /// <summary>Checks the names (Turkish sentences for the student); a focus is cleared, not refused, when she does not prepare for YKS.</summary>
    public static (StudyExamFocus? Focus, StudyTempo? Tempo) Validate(StudyExamFocus? focus, StudyTempo? tempo, bool preparesForYks)
    {
        if (focus is { } chosen && !Enum.IsDefined(chosen)) throw new StudyPlanValidationException("TYT ve AYT ağırlığını seç.");
        if (tempo is { } pace && !Enum.IsDefined(pace)) throw new StudyPlanValidationException("Tempo Rahat, Dengeli ya da Yoğun olabilir.");
        return (preparesForYks ? focus : null, tempo);
    }

    /// <summary>Adds or changes the student's row (tracked; the caller saves). Also used by the routine's save, in its transaction.</summary>
    public static void Apply(CoachingDbContext db, Guid studentId, StudyPlanPreferences? row, StudyExamFocus? focus, StudyTempo? tempo)
    {
        if (row is null)
        {
            row = StudyPlanPreferences.Create(studentId);
            db.Add(row);
        }
        row.Replace(focus?.ToString(), tempo?.ToString());
    }

    public static async Task<StudyPlanPreferences?> RowAsync(CoachingDbContext db, Guid studentId, CancellationToken cancellationToken) =>
        await db.Set<StudyPlanPreferences>().AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);

    /// <summary>Also used by the admin reader and the data export.</summary>
    public static async Task<StudyPlanPreferencesView> ReadAsync(CoachingDbContext db, Guid studentId, CancellationToken cancellationToken)
    {
        var row = await RowAsync(db, studentId, cancellationToken);
        return row is null ? new(null, null, null, null) : View(row, await CoachingStudyRoutineService.PreparesForYksAsync(db, studentId, cancellationToken));
    }

    public static StudyPlanPreferencesView View(StudyPlanPreferences row, bool preparesForYks) =>
        new(preparesForYks ? Focus(row) : null, Tempo(row), row.Version, row.UpdatedAt);

    public static StudyExamFocus? Focus(StudyPlanPreferences row) => Enum.TryParse<StudyExamFocus>(row.ExamFocus, out var focus) ? focus : null;
    public static StudyTempo? Tempo(StudyPlanPreferences row) => Enum.TryParse<StudyTempo>(row.Tempo, out var tempo) ? tempo : null;

    private Guid RequireStudent()
    {
        if (access.CurrentUserId is not { } studentId || !access.IsCurrentStudent(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
        return studentId;
    }

    private static BusinessRuleException Conflict() => new("StudyPlanning.Conflict", ConflictMessage);
}
