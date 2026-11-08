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
/// student's free hours in one transaction; each record keeps its own version check, and the profile stores the availability
/// version that was actually saved.
/// </summary>
public sealed class CoachingStudyRoutineService(CoachingDbContext db, ICoachingAccessPolicy access, CoachingStudyPlanAccess staff)
    : IStudyRoutineService
{
    public const string AvailabilityConflictMessage = "Çalışma saatlerin başka bir yerde değişti; yeniden yükle.";
    public const string RoutineConflictMessage = "Günlük düzenin başka bir yerde değişti; yeniden yükle.";
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public async Task<StudyRoutineView?> GetAsync(CancellationToken cancellationToken = default) =>
        await ReadAsync(RequireStudent(), cancellationToken);

    public async Task<StudyRoutineView?> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await ReadAsync(await staff.RequireAsync(studentId, cancellationToken), cancellationToken);

    public async Task<StudyRoutineView> ReplaceAsync(StudyRoutineUpdate request, CancellationToken cancellationToken = default)
    {
        var studentId = RequireStudent();
        ArgumentNullException.ThrowIfNull(request);
        var yks = await PreparesForYksAsync(db, studentId, cancellationToken);
        var education = await db.CoachingStudentProfiles.AsNoTracking().Where(x => x.UserId == studentId)
            .Select(x => (Coaching.Domain.Profiles.CoachingEducationStatus?)x.EducationStatus).SingleOrDefaultAsync(cancellationToken);
        var answers = StudyRoutine.Validate(request.Answers, yks, education);
        var windows = StudyRoutine.ToWindows(answers).Windows;
        var answersJson = JsonSerializer.Serialize(answers, Json);

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();   // a retried attempt starts from the database again
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // Both version checks come before any change.
            var availability = await db.StudyAvailability.SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
            if (request.ExpectedAvailabilityVersion != availability?.Version) throw AvailabilityConflict();
            var profile = await db.Set<StudyRoutineProfile>().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
            if (request.ExpectedVersion != profile?.Version) throw RoutineConflict();

            // 1. The free hours. A conflict here is the free hours' (a hand edit or a first save in another tab).
            if (availability is null)
            {
                availability = StudyAvailability.Create(studentId, CoachingTime.ZoneId);
                availability.ReplaceWindows(windows);
                db.StudyAvailability.Add(availability);
            }
            else availability.ReplacePreferences(CoachingTime.ZoneId, windows);
            await SaveAsync(AvailabilityConflict, cancellationToken);

            // 2. The profile, with the availability version that was just saved.
            if (profile is null)
            {
                profile = StudyRoutineProfile.Create(studentId);
                db.Add(profile);
            }
            profile.Replace(answersJson, availability.Version);
            // Paket H4a: the routine's focus and tempo are the plan preferences (their single source), saved with it.
            var preferences = await db.Set<StudyPlanPreferences>().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
            CoachingStudyPlanPreferencesService.Apply(db, studentId, preferences, answers.ExamFocus, answers.Tempo);
            await SaveAsync(RoutineConflict, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return View(profile, availability, yks, await db.Set<StudyPlanPreferences>().SingleAsync(x => x.StudentId == studentId, cancellationToken));
        });
    }

    private async Task SaveAsync(Func<BusinessRuleException> conflict, CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (ConcurrencyException) { throw conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" }) { throw conflict(); }
    }

    private async Task<StudyRoutineView?> ReadAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var profile = await db.Set<StudyRoutineProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        if (profile is null) return null;
        var availability = await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        return View(profile, availability, await PreparesForYksAsync(db, studentId, cancellationToken),
            await CoachingStudyPlanPreferencesService.RowAsync(db, studentId, cancellationToken));
    }

    internal static async Task<bool> PreparesForYksAsync(CoachingDbContext db, Guid studentId, CancellationToken cancellationToken) =>
        await db.StudentLearningContexts.AsNoTracking().AnyAsync(x => x.StudentUserId == studentId && x.Purpose == LearningPurpose.Yks, cancellationToken);

    /// <summary>
    /// Also used by the coaching data export and the admin reader. Throws <see cref="JsonException"/> on unreadable answers. Paket H4a:
    /// the exam focus and tempo shown are the plan preferences (<paramref name="preferences"/>), their single source; answers saved
    /// before the preferences existed show their own.
    /// </summary>
    public static StudyRoutineView View(StudyRoutineProfile profile, StudyAvailability? availability, bool preparesForYks,
        StudyPlanPreferences? preferences = null)
    {
        var answers = JsonSerializer.Deserialize<StudyRoutineAnswers>(profile.AnswersJson, Json)
            ?? throw new JsonException("Stored routine answers are empty.");
        if (preferences is not null)
            answers = answers with { ExamFocus = CoachingStudyPlanPreferencesService.Focus(preferences), Tempo = CoachingStudyPlanPreferencesService.Tempo(preferences) };
        if (!preparesForYks && answers.ExamFocus is not null) answers = answers with { ExamFocus = null };
        var derived = StudyRoutine.ToWindows(answers);
        return new StudyRoutineView(profile.Version, profile.SchemaVersion, answers,
            derived.Windows.Select(x => new StudyWindowInput(x.Day, x.StartMinute, x.EndMinute)).ToArray(), derived.DayBudgets,
            derived.DaysWithoutWindows, profile.AvailabilityVersion, availability?.Version,
            availability is not null && availability.Version == profile.AvailabilityVersion, profile.UpdatedAt);
    }

    private Guid RequireStudent()
    {
        if (access.CurrentUserId is not { } studentId || !access.IsCurrentStudent(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
        return studentId;
    }

    private static BusinessRuleException RoutineConflict() => new("StudyPlanning.Conflict", RoutineConflictMessage);
    private static BusinessRuleException AvailabilityConflict() => new("StudyPlanning.Conflict", AvailabilityConflictMessage);
}
