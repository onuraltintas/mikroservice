using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SpeedReading.Application.Privacy;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Privacy;

/// <summary>
/// Review H2b (finding 9): builds the user's Speed Reading data export. Every section is read from the owned
/// database filtered by the user (or, for child rows, by the user's own parent rows); the row is the entity's
/// mapped scalar fields minus internal working data and secrets.
/// </summary>
public sealed class SpeedReadingPersonalDataExportService(OwnedSpeedReadingDbContext context, TimeProvider timeProvider)
    : ISpeedReadingPersonalDataExport
{
    public const string SchemaVersion = "speed-reading-1.0";

    private static readonly HashSet<string> HiddenFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ProviderResponse", "ProviderToken", "SessionDataJson", "ProcessedActionsJson", "Endpoint", "P256dh", "Auth"
    };

    private sealed record Section(string Name, string Entity, string Filter, string? Parent = null);

    private static readonly Section[] Sections =
    [
        new("profile", "SpeedReadingUserProfile", "UserId"),
        new("gamification", "UserGamification", "UserId"),
        new("achievements", "UserAchievement", "UserId"),
        new("programs", "StudentProgramProgress", "UserId"),
        new("programContinuations", "ProgramContinuation", "StudentId"),
        new("dailyExerciseLogs", "DailyExerciseLog", "UserId"),
        new("exerciseSessions", "ExerciseSession", "StudentId"),
        new("exerciseSessionAnswers", "ExerciseSessionAnswer", "SessionId", "exerciseSessions"),
        new("exerciseResults", "ExerciseSessionResult", "StudentId"),
        new("readingSessions", "ReadingSession", "UserId"),
        new("readingSessionAnswers", "ReadingSessionAnswer", "SessionId", "readingSessions"),
        new("readingAttempts", "StudentReadingAttempt", "UserId"),
        new("assessments", "AssessmentAttempt", "StudentId"),
        new("assessmentExercises", "AssessmentAttemptExercise", "AssessmentAttemptId", "assessments"),
        new("assessmentStudies", "AssessmentStudyEnrollment", "StudentId"),
        new("assignments", "StudentAssignment", "StudentId"),
        new("learningPaths", "StudentLearningPathProgress", "StudentId"),
        new("learningPathNodes", "StudentLearningNodeProgress", "StudentId"),
        new("personalizedLearningItems", "PersonalizedLearningPathItem", "StudentId"),
        new("vocabulary", "UserVocabularyProgress", "UserId"),
        new("reviewItems", "ReviewItem", "UserId"),
        new("reviewCompletions", "ReviewCompletion", "UserId"),
        new("contentFeedback", "LegacyUserContentFeedback", "UserId"),
        new("adaptiveLearningProfiles", "LegacyStudentLearningProfile", "StudentId"),
        new("adaptiveReadingProfiles", "LegacyStudentReadingProfile", "StudentId"),
        new("adaptiveRecommendations", "LegacyContentRecommendation", "StudentId"),
        new("adaptiveDailyGoals", "LegacyDailyGoal", "StudentId"),
        new("adaptiveTextHistory", "LegacyTextRecommendationHistory", "StudentId"),
        new("rsvpSessions", "LegacyRsvpSession", "UserId"),
        new("notifications", "LegacyUserNotification", "UserId"),
        new("notificationPreferences", "LegacyNotificationPreference", "UserId"),
        new("notificationTypePreferences", "LegacyNotificationTypePreference", "UserId"),
        new("pushSubscriptions", "LegacyPushSubscription", "UserId"),
        new("announcementInteractions", "LegacyAnnouncementUserInteraction", "UserId"),
        new("institutionMemberships", "SpeedReadingInstitutionMembership", "UserId"),
        new("teacherAssignmentsAsStudent", "SpeedReadingTeacherStudentAssignment", "StudentUserId"),
        new("teacherAssignmentsAsTeacher", "SpeedReadingTeacherStudentAssignment", "TeacherUserId"),
        new("invitationsAccepted", "SpeedReadingInvitation", "AcceptedByUserId"),
        new("subscriptions", "LegacyUserSubscription", "UserId"),
        new("payments", "LegacyPayment", "UserId"),
        new("bankTransfers", "LegacyBankTransferPaymentRequest", "UserId"),
        new("institutionAccessActions", "LegacyInstitutionAccessAction", "UserId")
    ];

    private static readonly MethodInfo LoadMethod = typeof(SpeedReadingPersonalDataExportService)
        .GetMethod(nameof(LoadAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public async Task<SpeedReadingPersonalDataExportDto> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entities = context.Model.GetEntityTypes()
            .Where(type => !type.IsOwned())
            .GroupBy(type => type.ClrType.Name)
            .ToDictionary(group => group.Key, group => group.First());
        var keys = new Dictionary<string, List<Guid>>();
        var result = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>();

        foreach (var section in Sections)
        {
            if (!entities.TryGetValue(section.Entity, out var entityType)
                || entityType.FindProperty(section.Filter) is not { } filter)
            {
                result[section.Name] = [];
                continue;
            }

            var match = section.Parent is null ? [userId] : keys.GetValueOrDefault(section.Parent) ?? [];
            if (match.Count == 0)
            {
                result[section.Name] = [];
                continue;
            }

            var load = (Task<IReadOnlyList<object>>)LoadMethod.MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [filter, match, cancellationToken])!;
            var rows = await load;
            var id = entityType.FindPrimaryKey()?.Properties is [{ ClrType: var idType } single] && idType == typeof(Guid) ? single : null;
            if (id is not null)
                keys[section.Name] = rows.Select(row => (Guid)Read(id, row)!).ToList();
            result[section.Name] = rows.Select(row => Project(entityType, row)).ToList();
        }

        return new SpeedReadingPersonalDataExportDto(userId, SchemaVersion, timeProvider.GetUtcNow().UtcDateTime, result);
    }

    private async Task<IReadOnlyList<object>> LoadAsync<TEntity>(IProperty filter, List<Guid> match, CancellationToken cancellationToken)
        where TEntity : class
    {
        var row = Expression.Parameter(typeof(TEntity), "row");
        var value = Expression.Call(typeof(EF), nameof(EF.Property), [filter.ClrType], row, Expression.Constant(filter.Name));
        Expression body;
        if (match.Count == 1)
        {
            body = Expression.Equal(value, Expression.Convert(Expression.Constant(match[0]), filter.ClrType));
        }
        else
        {
            var list = filter.ClrType == typeof(Guid)
                ? (object)match
                : match.Select(guid => (Guid?)guid).ToList();
            body = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [filter.ClrType], Expression.Constant(list), value);
        }

        var rows = await context.Set<TEntity>().AsNoTracking()
            .Where(Expression.Lambda<Func<TEntity, bool>>(body, row))
            .ToListAsync(cancellationToken);
        return rows;
    }

    private static IReadOnlyDictionary<string, object?> Project(IEntityType entityType, object row)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in entityType.GetProperties())
        {
            if (property.IsShadowProperty() || Hidden(property.Name))
                continue;
            var value = Read(property, row);
            values[property.Name] = value is Enum ? value.ToString() : value;
        }

        return values;
    }

    private static bool Hidden(string name) =>
        HiddenFields.Contains(name) || name.EndsWith("Hash", StringComparison.Ordinal) || name.EndsWith("Secret", StringComparison.Ordinal);

    private static object? Read(IProperty property, object row) => property.PropertyInfo is { } info
        ? info.GetValue(row)
        : property.FieldInfo?.GetValue(row);
}
