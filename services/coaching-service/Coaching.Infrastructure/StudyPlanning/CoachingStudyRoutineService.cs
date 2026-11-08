using System.Text.Json;
using System.Text.Json.Serialization;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.StudyPlanning;

/// <summary>
/// H3 (user decision K2): the student's routine profile. Saving it validates the answers, turns them into windows and replaces the
/// student's free hours in the same save; each record keeps its own version check.
/// </summary>
public sealed class CoachingStudyRoutineService(CoachingDbContext db, ICoachingAccessPolicy access, CoachingStudyPlanAccess staff)
    : IStudyRoutineService
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public async Task<StudyRoutineView?> GetAsync(CancellationToken cancellationToken = default) =>
        await ReadAsync(RequireStudent(), cancellationToken);

    public async Task<StudyRoutineView?> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await ReadAsync(await staff.RequireAsync(studentId, cancellationToken), cancellationToken);

    public async Task<StudyRoutineView> ReplaceAsync(StudyRoutineUpdate request, CancellationToken cancellationToken = default)
    {
        var studentId = RequireStudent();
        ArgumentNullException.ThrowIfNull(request);
        var purpose = await db.StudentLearningContexts.AsNoTracking().Where(x => x.StudentUserId == studentId)
            .Select(x => (LearningPurpose?)x.Purpose).SingleOrDefaultAsync(cancellationToken);
        var answers = StudyRoutine.Validate(request.Answers, purpose == LearningPurpose.Yks);
        var windows = StudyRoutine.ToWindows(answers).Windows;

        // Both version checks come before any change, so a refused save leaves nothing modified.
        var availability = await db.StudyAvailability.SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        if (request.ExpectedAvailabilityVersion != availability?.Version) throw AvailabilityConflict();
        var profile = await db.Set<StudyRoutineProfile>().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        if (request.ExpectedVersion != profile?.Version) throw RoutineConflict();

        int availabilityVersion;
        if (availability is null)
        {
            availability = StudyAvailability.Create(studentId, CoachingTime.ZoneId);
            availability.ReplaceWindows(windows);
            db.StudyAvailability.Add(availability);
            availabilityVersion = 0;
        }
        else
        {
            availability.ReplacePreferences(CoachingTime.ZoneId, windows);   // always modified (UpdatedAt), so the save adds one
            availabilityVersion = availability.Version + 1;
        }
        if (profile is null)
        {
            profile = StudyRoutineProfile.Create(studentId);
            db.Add(profile);
        }
        profile.Replace(JsonSerializer.Serialize(answers, Json), availabilityVersion);

        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" }) { throw RoutineConflict(); }
        return View(profile, availability);
    }

    private async Task<StudyRoutineView?> ReadAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var profile = await db.Set<StudyRoutineProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        if (profile is null) return null;
        var availability = await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        return View(profile, availability);
    }

    /// <summary>Also used by the coaching data export.</summary>
    public static StudyRoutineView View(StudyRoutineProfile profile, StudyAvailability? availability)
    {
        var answers = JsonSerializer.Deserialize<StudyRoutineAnswers>(profile.AnswersJson, Json)
            ?? throw new InvalidOperationException("Stored routine answers are empty.");
        var derived = StudyRoutine.ToWindows(answers);
        return new StudyRoutineView(profile.Version, profile.SchemaVersion, answers,
            derived.Windows.Select(x => new StudyWindowInput(x.Day, x.StartMinute, x.EndMinute)).ToArray(), derived.DayBudgets,
            profile.AvailabilityVersion, availability?.Version, availability is not null && availability.Version == profile.AvailabilityVersion,
            profile.UpdatedAt);
    }

    private Guid RequireStudent()
    {
        if (access.CurrentUserId is not { } studentId || !access.IsCurrentStudent(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
        return studentId;
    }

    private static BusinessRuleException RoutineConflict() =>
        new("StudyPlanning.Conflict", "Günlük düzenin başka bir yerde değişti. Sayfayı yenileyip yeniden dene.");
    private static BusinessRuleException AvailabilityConflict() =>
        new("StudyPlanning.Conflict", "Çalışma saatlerin başka bir yerde değişti. Sayfayı yenileyip yeniden dene.");
}
