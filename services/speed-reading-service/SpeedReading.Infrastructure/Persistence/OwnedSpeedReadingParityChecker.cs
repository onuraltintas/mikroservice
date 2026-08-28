using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingParityRow(
    string SourceTable,
    string OwnedTable,
    string SourceKey,
    string OwnedKey,
    int SourceCount,
    int OwnedCount,
    string SourceChecksum,
    string OwnedChecksum,
    string SourcePayloadChecksum,
    string OwnedPayloadChecksum,
    bool FieldParityAvailable,
    IReadOnlyList<string> MismatchedFields)
{
    public bool IsMatch => SourceCount == OwnedCount
        && SourceChecksum == OwnedChecksum
        && FieldParityAvailable
        && SourcePayloadChecksum == OwnedPayloadChecksum;
}

public sealed record OwnedSpeedReadingParityReport(
    DateTime GeneratedAtUtc,
    IReadOnlyList<OwnedSpeedReadingParityRow> Tables)
{
    public bool IsMatch => Tables.Count > 0 && Tables.All(item => item.IsMatch);
}

public static class OwnedSpeedReadingParityHash
{
    public static string Compute(IEnumerable<Guid> ids)
    {
        var bytes = ids
            .OrderBy(id => id)
            .SelectMany(id => id.ToByteArray())
            .ToArray();

        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public static string ComputePayload(
        IEnumerable<IReadOnlyDictionary<string, string?>> rows)
    {
        var serializedRows = rows
            .Select(row => string.Join("|", row
                .OrderBy(field => field.Key, StringComparer.Ordinal)
                .Select(field => $"{field.Key}={field.Value ?? "<null>"}")))
            .OrderBy(row => row, StringComparer.Ordinal)
            .ToArray();
        var payload = string.Join("\n", serializedRows);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

/// <summary>
/// Compares the source and owned stores by the stable business identifier of
/// every migrated Speed Reading table. It is read-only and deliberately does
/// not run migrations or alter either database.
/// </summary>
public sealed class OwnedSpeedReadingParityChecker(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    private IReadOnlyDictionary<Guid, string> _exerciseTypeNames =
        new Dictionary<Guid, string>();
    private IReadOnlyDictionary<Guid, int> _legacyQuestionOrders =
        new Dictionary<Guid, int>();
    private IReadOnlySet<Guid> _legacyExerciseIds = new HashSet<Guid>();
    private IReadOnlySet<Guid> _legacyReadingTextIds = new HashSet<Guid>();

    public async Task<OwnedSpeedReadingParityReport> RunAsync(
        CancellationToken cancellationToken = default)
    {
        _exerciseTypeNames = await legacy.ExerciseTypes
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        _legacyExerciseIds = await legacy.Exercises
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        _legacyReadingTextIds = await legacy.ReadingTexts
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var legacyQuestionOrderRows = await legacy.ReadingQuestions
            .AsNoTracking()
            .Select(item => new { item.Id, item.ReadingTextId, item.OrderIndex })
            .ToListAsync(cancellationToken);
        _legacyQuestionOrders = NormalizeQuestionOrders(legacyQuestionOrderRows);

        var rows = new List<OwnedSpeedReadingParityRow>();
        ISpeedReadingDataContext ownedData = owned;

        rows.Add(await CompareAsync("ContentBlocks", "cms_content_blocks", "Id", "id", legacy.ContentBlocks.Select(item => item.Id), ownedData.ContentBlocks.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("Pages", "cms_pages", "Id", "id", legacy.Pages.Select(item => item.Id), ownedData.Pages.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("BlogPosts", "cms_blog_posts", "Id", "id", legacy.BlogPosts.Select(item => item.Id), ownedData.BlogPosts.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ContactMessages", "cms_contact_messages", "Id", "id", legacy.ContactMessages.Select(item => item.Id), ownedData.ContactMessages.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("NewsletterSubscribers", "cms_newsletter_subscribers", "Id", "id", legacy.NewsletterSubscribers.Select(item => item.Id), ownedData.NewsletterSubscribers.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("Products", "subscription_products", "Id", "Id", legacy.Products.Select(item => item.Id), ownedData.Products.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("SubscriptionPlans", "subscription_plans", "Id", "Id", legacy.SubscriptionPlans.Select(item => item.Id), ownedData.SubscriptionPlans.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("UserSubscriptions", "user_subscriptions", "Id", "Id", legacy.UserSubscriptions.Select(item => item.Id), ownedData.UserSubscriptions.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("Payments", "payments", "Id", "Id", legacy.Payments.Select(item => item.Id), ownedData.Payments.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("ExerciseTypeCategories", "exercise_type_categories", "Id", "id", legacy.ExerciseTypeCategories.Select(item => item.Id), owned.ExerciseTypeCategories.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ExerciseTypes", "exercise_types", "Id", "id", legacy.ExerciseTypes.Select(item => item.Id), owned.ExerciseTypes.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("Exercises", "exercises", "Id", "id", legacy.Exercises.Select(item => item.Id), owned.Exercises.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ReadingTexts", "reading_texts", "Id", "id", legacy.ReadingTexts.Select(item => item.Id), owned.ReadingTexts.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ReadingQuestions", "reading_questions", "Id", "id", legacy.ReadingQuestions.Select(item => item.Id), owned.ReadingQuestions.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("ExerciseSessions", "exercise_sessions", "Id", "id", legacy.ExerciseSessions.Select(item => item.Id), owned.ExerciseSessions.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("StudentExerciseResults", "exercise_session_results", "Id", "id", legacy.StudentExerciseResults.Select(item => item.Id), owned.ExerciseSessionResults.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ReadingSessions", "reading_sessions", "Id", "id", legacy.ReadingSessions.Select(item => item.Id), owned.ReadingSessions.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("Assignments", "assignments", "Id", "id", legacy.Assignments.Select(item => item.Id), owned.Assignments.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("StudentAssignments", "student_assignments", "Id", "id", legacy.StudentAssignments.Select(item => item.Id), owned.StudentAssignments.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("ExerciseProgramTemplates", "program_templates", "Id", "id", legacy.ExerciseProgramTemplates.Select(item => item.Id), owned.ProgramTemplates.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("StudentProgramProgresses", "student_program_progress", "Id", "id", legacy.StudentProgramProgresses.Select(item => item.Id), owned.StudentProgramProgresses.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("DailyExerciseLogs", "daily_exercise_logs", "Id", "id", legacy.DailyExerciseLogs.Select(item => item.Id), owned.DailyExerciseLogs.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("AgeGroupConfigurations", "age_group_configurations", "Id", "id", legacy.AgeGroupConfigurations.Select(item => item.Id), owned.AgeGroupConfigurations.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("Users", "user_profiles", "Id", "user_id", legacy.Users.Select(item => item.Id), owned.UserProfiles.Select(item => item.UserId), cancellationToken));
        rows.Add(await CompareAsync("LearningPathTemplates", "learning_path_templates", "Id", "id", legacy.LearningPathTemplates.Select(item => item.Id), owned.LearningPathTemplates.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("LearningPathNodes", "learning_path_nodes", "Id", "id", legacy.LearningPathNodes.Select(item => item.Id), owned.LearningPathNodes.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("NodeContents", "learning_path_node_contents", "Id", "id", legacy.NodeContents.Select(item => item.Id), owned.LearningPathNodeContents.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("NodePrerequisites", "learning_path_prerequisites", "Id", "id", legacy.NodePrerequisites.Select(item => item.Id), owned.LearningPathPrerequisites.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("StudentPathProgresses", "student_learning_path_progress", "Id", "id", legacy.StudentPathProgresses.Select(item => item.Id), owned.StudentLearningPathProgresses.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("StudentNodeProgresses", "student_learning_node_progress", "Id", "id", legacy.StudentNodeProgresses.Select(item => item.Id), owned.StudentLearningNodeProgresses.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("PersonalizedLearningPaths", "personalized_learning_path_items", "Id", "id", legacy.PersonalizedLearningPaths.Select(item => item.Id), owned.PersonalizedLearningPathItems.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("AdminAuditRecords", "admin_audit_records", "Id", "id", legacy.AdminAuditRecords.Select(item => item.Id), owned.AdminAuditRecords.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("Achievements", "achievements", "Id", "id", legacy.Achievements.Select(item => item.Id), owned.Achievements.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("UserAchievements", "user_achievements", "Id", "id", legacy.UserAchievements.Select(item => item.Id), owned.UserAchievements.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("UserGameifications", "user_gamification", "Id", "id", legacy.UserGamifications.Select(item => item.Id), owned.UserGamifications.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ExamQuestions", "exam_questions", "Id", "id", legacy.ExamQuestions.Select(item => item.Id), owned.ExamQuestions.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("VisualizationScenes", "visualization_scenes", "Id", "id", legacy.VisualizationScenes.Select(item => item.Id), owned.VisualizationScenes.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("VisualizationQuestions", "visualization_questions", "Id", "id", legacy.VisualizationQuestions.Select(item => item.Id), owned.VisualizationQuestions.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("VocabularyItems", "vocabulary_items", "Id", "id", legacy.VocabularyItems.Select(item => item.Id), owned.VocabularyItems.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("UserVocabularyProgresses", "user_vocabulary_progress", "Id", "id", legacy.UserVocabularyProgresses.Select(item => item.Id), owned.UserVocabularyProgresses.Select(item => item.Id), cancellationToken));
        var reviewTableExists = await legacy.Database
            .SqlQueryRaw<bool>(
                @"SELECT to_regclass('""ExerciseReviewItems""') IS NOT NULL AS ""Value""")
            .SingleAsync(cancellationToken);
        var reviewIds = reviewTableExists
            ? legacy.ExerciseReviewItems.Select(item => item.Id)
            : legacy.Database.SqlQueryRaw<Guid>("SELECT CAST(NULL AS uuid) AS \"Value\" WHERE FALSE");
        rows.Add(await CompareAsync("ExerciseReviewItems", "review_items", "Id", "id", reviewIds, owned.ReviewItems.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("Notifications", "notifications", "Id", "id", legacy.Notifications.Select(item => item.Id), ownedData.Notifications.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("NotificationPreferences", "notification_preferences", "Id", "id", legacy.NotificationPreferences.Select(item => item.Id), ownedData.NotificationPreferences.Select(item => item.Id), cancellationToken));
        var notificationTypePreferenceTableExists = await legacy.Database
            .SqlQueryRaw<bool>(
                @"SELECT to_regclass('""NotificationTypePreferences""') IS NOT NULL AS ""Value""")
            .SingleAsync(cancellationToken);
        var notificationTypePreferenceIds = notificationTypePreferenceTableExists
            ? legacy.NotificationTypePreferences.Select(item => item.Id)
            : legacy.Database.SqlQueryRaw<Guid>("SELECT CAST(NULL AS uuid) AS \"Value\" WHERE FALSE");
        rows.Add(await CompareAsync("NotificationTypePreferences", "notification_type_preferences", "Id", "id", notificationTypePreferenceIds, ownedData.NotificationTypePreferences.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("PushSubscriptions", "push_subscriptions", "Id", "id", legacy.PushSubscriptions.Select(item => item.Id), ownedData.PushSubscriptions.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("Announcements", "announcements", "Id", "id", legacy.Announcements.Select(item => item.Id), ownedData.Announcements.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("AnnouncementUserInteractions", "announcement_user_interactions", "Id", "id", legacy.AnnouncementUserInteractions.Select(item => item.Id), ownedData.AnnouncementUserInteractions.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("EmailTemplates", "email_templates", "Id", "id", legacy.EmailTemplates.Select(item => item.Id), ownedData.EmailTemplates.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("EmailCampaigns", "email_campaigns", "Id", "id", legacy.EmailCampaigns.Select(item => item.Id), ownedData.EmailCampaigns.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("EmailCampaignLogs", "email_campaign_logs", "Id", "id", legacy.EmailCampaignLogs.Select(item => item.Id), ownedData.EmailCampaignLogs.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("RSVPSessions", "rsvp_sessions", "Id", "id", legacy.RsvpSessions.Select(item => item.Id), ownedData.RsvpSessions.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("UserContentFeedbacks", "content_feedback", "Id", "id", legacy.UserContentFeedbacks.Select(item => item.Id), owned.ContentFeedbacks.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("StudentLearningProfiles", "adaptive_learning_profiles", "Id", "id", legacy.Database.SqlQueryRaw<Guid>("SELECT \"Id\" AS \"Value\" FROM \"StudentLearningProfiles\""), owned.AdaptiveLearningProfiles.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ContentRecommendations", "adaptive_content_recommendations", "Id", "id", legacy.Database.SqlQueryRaw<Guid>("SELECT \"Id\" AS \"Value\" FROM \"ContentRecommendations\""), owned.AdaptiveContentRecommendations.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("DailyGoals", "adaptive_daily_goals", "Id", "id", legacy.Database.SqlQueryRaw<Guid>("SELECT \"Id\" AS \"Value\" FROM \"DailyGoals\""), owned.AdaptiveDailyGoals.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("StudentReadingProfiles", "adaptive_reading_profiles", "Id", "id", legacy.StudentReadingProfiles.Select(item => item.Id), owned.AdaptiveReadingProfiles.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("TextRecommendationHistories", "adaptive_text_recommendation_history", "Id", "id", legacy.TextRecommendationHistories.Select(item => item.Id), owned.AdaptiveTextRecommendationHistories.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("ReportTemplates", "report_templates", "Id", "id", legacy.ReportTemplates.Select(item => item.Id), owned.ReportTemplates.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ReportSnapshots", "report_snapshots", "Id", "id", legacy.ReportSnapshots.Select(item => item.Id), owned.ReportSnapshots.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ScheduledReports", "scheduled_reports", "Id", "id", legacy.ScheduledReports.Select(item => item.Id), owned.ScheduledReports.Select(item => item.Id), cancellationToken));

        return new OwnedSpeedReadingParityReport(DateTime.UtcNow, rows);
    }

    private async Task<OwnedSpeedReadingParityRow> CompareAsync(
        string sourceTable,
        string ownedTable,
        string sourceKey,
        string ownedKey,
        IQueryable<Guid> sourceIds,
        IQueryable<Guid> ownedIds,
        CancellationToken cancellationToken)
    {
        var source = await sourceIds.ToListAsync(cancellationToken);
        var target = await ownedIds.ToListAsync(cancellationToken);
        var sourceEntityType = FindEntityClrType(sourceIds.Expression);
        var ownedEntityType = FindEntityClrType(ownedIds.Expression);
        var sourcePayloadChecksum = OwnedSpeedReadingParityHash.ComputePayload([]);
        var ownedPayloadChecksum = OwnedSpeedReadingParityHash.ComputePayload([]);
        IReadOnlyList<string> mismatchedFields = [];
        var bothStoresAreEmpty = source.Count == 0 && target.Count == 0;
        var fieldParityAvailable = bothStoresAreEmpty
            || sourceEntityType is not null && ownedEntityType is not null;

        if (fieldParityAvailable && sourceEntityType is not null && ownedEntityType is not null)
        {
            var sourceRows = await LoadEntityRowsAsync(
                legacy,
                sourceEntityType,
                source.ToHashSet(),
                FindProjectedPropertyName(sourceIds.Expression) ?? "Id",
                cancellationToken);
            var ownedRows = await LoadEntityRowsAsync(
                owned,
                ownedEntityType,
                target.ToHashSet(),
                FindProjectedPropertyName(ownedIds.Expression) ?? "Id",
                cancellationToken);

            var sourceCanonicalRows = sourceRows
                .Select(row => CreateCanonicalRow(
                    sourceEntityType,
                    FindProjectedPropertyName(sourceIds.Expression) ?? "Id",
                    sourceTable,
                    row,
                    isSource: true))
                .ToArray();
            var ownedCanonicalRows = ownedRows
                .Select(row => CreateCanonicalRow(
                    ownedEntityType,
                    FindProjectedPropertyName(ownedIds.Expression) ?? "Id",
                    sourceTable,
                    row,
                    isSource: false))
                .ToArray();

            sourcePayloadChecksum = OwnedSpeedReadingParityHash.ComputePayload(
                sourceCanonicalRows.Select(row => row.Fields));
            ownedPayloadChecksum = OwnedSpeedReadingParityHash.ComputePayload(
                ownedCanonicalRows.Select(row => row.Fields));
            if (sourcePayloadChecksum != ownedPayloadChecksum)
            {
                mismatchedFields = FindMismatchedFields(
                    sourceCanonicalRows,
                    ownedCanonicalRows);
            }
        }

        return new OwnedSpeedReadingParityRow(
            sourceTable,
            ownedTable,
            sourceKey,
            ownedKey,
            source.Count,
            target.Count,
            OwnedSpeedReadingParityHash.Compute(source),
            OwnedSpeedReadingParityHash.Compute(target),
            sourcePayloadChecksum,
            ownedPayloadChecksum,
            fieldParityAvailable,
            mismatchedFields);
    }

    private async Task<IReadOnlyList<object>> LoadEntityRowsAsync(
        DbContext context,
        Type entityType,
        IReadOnlySet<Guid> ids,
        string keyPropertyName,
        CancellationToken cancellationToken)
    {
        var rows = await LoadEntityRowsByTypeAsync(context, entityType, cancellationToken);
        var keyProperty = GetKeyProperty(entityType, keyPropertyName);

        return rows
            .Where(row => TryGetGuid(keyProperty.GetValue(row)) is { } id && ids.Contains(id))
            .ToArray();
    }

    private CanonicalParityRow CreateCanonicalRow(
        Type entityType,
        string keyPropertyName,
        string sourceTable,
        object row,
        bool isSource)
    {
        var key = TryGetGuid(GetKeyProperty(entityType, keyPropertyName).GetValue(row))
            ?? throw new InvalidOperationException(
                $"Parity key property '{keyPropertyName}' on {entityType.Name} was empty.");
        return new CanonicalParityRow(key, Canonicalize(sourceTable, row, isSource));
    }

    private static IReadOnlyList<string> FindMismatchedFields(
        IReadOnlyCollection<CanonicalParityRow> sourceRows,
        IReadOnlyCollection<CanonicalParityRow> ownedRows)
    {
        var sourceById = sourceRows.ToDictionary(row => row.Id);
        var ownedById = ownedRows.ToDictionary(row => row.Id);
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in sourceById.Keys.Union(ownedById.Keys))
        {
            if (!sourceById.TryGetValue(id, out var sourceRow)
                || !ownedById.TryGetValue(id, out var ownedRow))
            {
                fieldNames.Add("<missing-row>");
                continue;
            }

            foreach (var field in sourceRow.Fields.Keys.Union(ownedRow.Fields.Keys))
            {
                sourceRow.Fields.TryGetValue(field, out var sourceValue);
                ownedRow.Fields.TryGetValue(field, out var ownedValue);
                if (!string.Equals(sourceValue, ownedValue, StringComparison.Ordinal))
                    fieldNames.Add(field);
            }
        }

        return fieldNames.OrderBy(field => field, StringComparer.Ordinal).ToArray();
    }

    private static PropertyInfo GetKeyProperty(Type entityType, string keyPropertyName) =>
        entityType.GetProperty(
            keyPropertyName,
            BindingFlags.Instance | BindingFlags.Public)
        ?? throw new InvalidOperationException(
            $"Parity key property '{keyPropertyName}' was not found on {entityType.Name}.");

    private sealed record CanonicalParityRow(
        Guid Id,
        IReadOnlyDictionary<string, string?> Fields);

    private static async Task<IReadOnlyList<object>> LoadEntityRowsByTypeAsync(
        DbContext context,
        Type entityType,
        CancellationToken cancellationToken)
    {
        var loader = typeof(OwnedSpeedReadingParityChecker)
            .GetMethod(nameof(LoadEntityRowsGenericAsync), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(entityType);
        var task = (Task<IReadOnlyList<object>>)loader.Invoke(
            null,
            [context, cancellationToken])!;
        return await task;
    }

    private static async Task<IReadOnlyList<object>> LoadEntityRowsGenericAsync<TEntity>(
        DbContext context,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        return (await context.Set<TEntity>()
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Cast<object>()
            .ToArray();
    }

    private IReadOnlyDictionary<string, string?> Canonicalize(
        string sourceTable,
        object row,
        bool isSource)
    {
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var property in row.GetType().GetProperties(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            if (!IsScalar(property.PropertyType)
                || string.Equals(property.Name, "Id", StringComparison.Ordinal)
                || ShouldSkipField(sourceTable, property.Name, isSource))
            {
                continue;
            }

            var name = NormalizeFieldName(property.Name);
            fields[name] = FormatValue(property.GetValue(row), name);
        }

        foreach (var field in GetSyntheticFields(sourceTable, row, isSource))
        {
            fields[NormalizeFieldName(field.Key)] = FormatValue(
                field.Value,
                NormalizeFieldName(field.Key));
        }

        if (sourceTable == "VocabularyItems")
        {
            foreach (var fieldName in new[] { "synonyms", "antonyms" })
            {
                if (fields.TryGetValue(fieldName, out var value)
                    && string.IsNullOrWhiteSpace(value))
                {
                    fields[fieldName] = null;
                }
            }
        }

        return fields;
    }

    private IReadOnlyDictionary<string, object?> GetSyntheticFields(
        string sourceTable,
        object row,
        bool isSource)
    {
        if (!isSource)
            return new Dictionary<string, object?>();

        if (sourceTable == "Exercises" && row is LegacyExercise exercise)
        {
            return new Dictionary<string, object?>
            {
                ["TypeCode"] = _exerciseTypeNames.GetValueOrDefault(exercise.ExerciseTypeId, string.Empty),
                ["CreatorId"] = exercise.CreatedBy,
                ["IsActive"] = true
            };
        }

        if (sourceTable == "ReadingQuestions" && row is LegacyReadingQuestion question)
        {
            return new Dictionary<string, object?>
            {
                ["OrderIndex"] = _legacyQuestionOrders.GetValueOrDefault(
                    question.Id,
                    Math.Max(0, question.OrderIndex))
            };
        }

        if (sourceTable == "StudentAssignments" && row is LegacyStudentAssignment)
            return new Dictionary<string, object?> { ["IsActive"] = true };

        if (sourceTable == "Users" && row is LegacyUser user)
        {
            return new Dictionary<string, object?>
            {
                ["UserId"] = user.Id,
                ["IsActive"] = !user.IsDeleted
            };
        }

        if (sourceTable == "StudentPathProgresses"
            && row is LegacyStudentPathProgress progress)
        {
            return new Dictionary<string, object?>
            {
                ["IsCompleted"] = progress.IsCompleted
                    || progress.CompletedAt.HasValue
                    || string.Equals(progress.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            };
        }

        if (sourceTable == "PersonalizedLearningPaths"
            && row is LegacyPersonalizedLearningPath personalized)
        {
            return new Dictionary<string, object?>
            {
                ["IsUnlocked"] = personalized.UnlockedAt.HasValue
            };
        }

        if (sourceTable == "StudentExerciseResults"
            && row is LegacyStudentExerciseResult result)
        {
            return new Dictionary<string, object?>
            {
                ["Score"] = result.WeightedKDP,
                ["LegacySessionId"] = result.SessionId
            };
        }

        if (sourceTable == "Notifications" && row is LegacyUserNotification notification)
        {
            return new Dictionary<string, object?>
            {
                ["Status"] = notification.IsRead ? 4 : 0
            };
        }

        if (sourceTable == "EmailTemplates" && row is LegacyEmailTemplate template)
        {
            return new Dictionary<string, object?>
            {
                ["Variables"] = template.AvailableVariables
            };
        }

        if (sourceTable == "NodeContents" && row is LegacyNodeContent content)
        {
            var sourceType = content.SourceContentType.Trim();
            var isExercise = sourceType.Contains("exercise", StringComparison.OrdinalIgnoreCase);
            var isReadingText = sourceType.Contains("reading", StringComparison.OrdinalIgnoreCase)
                || sourceType.Contains("text", StringComparison.OrdinalIgnoreCase);
            if (!isExercise && !isReadingText)
            {
                isExercise = _legacyExerciseIds.Contains(content.SourceContentId)
                    && !_legacyReadingTextIds.Contains(content.SourceContentId);
                isReadingText = _legacyReadingTextIds.Contains(content.SourceContentId)
                    && !_legacyExerciseIds.Contains(content.SourceContentId);
            }

            return new Dictionary<string, object?>
            {
                ["ExerciseId"] = isExercise ? content.SourceContentId : null,
                ["ReadingTextId"] = isReadingText ? content.SourceContentId : null
            };
        }

        return new Dictionary<string, object?>();
    }

    private static bool ShouldSkipField(
        string sourceTable,
        string fieldName,
        bool isSource)
    {
        if (sourceTable == "Users")
        {
            return fieldName is "UserName"
                or "Email"
                or "FirstName"
                or "LastName"
                or "IsDeleted"
                or "CreatedAt"
                or "CreatedBy"
                or "UpdatedAt"
                or "UpdatedBy";
        }

        if (sourceTable == "StudentPathProgresses"
            && isSource
            && fieldName is "Status" or "CompletedAt")
        {
            return true;
        }

        if (sourceTable == "PersonalizedLearningPaths"
            && isSource
            && fieldName == "UnlockedAt")
        {
            return true;
        }

        if (sourceTable == "NodeContents"
            && isSource
            && fieldName is "SourceContentId" or "SourceContentType"
                or "ExerciseId" or "ReadingTextId")
        {
            return true;
        }

        if (fieldName == "Version"
            || fieldName == "IsDeleted"
                && (sourceTable is "ExerciseSessions"
                    or "StudentExerciseResults"
                    or "StudentProgramProgresses"
                    or "DailyExerciseLogs"))
        {
            return true;
        }

        if (sourceTable == "Notifications" && fieldName == "IsRead")
            return true;

        return false;
    }

    private static bool IsScalar(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType.IsEnum
            || underlyingType.IsPrimitive
            || underlyingType == typeof(string)
            || underlyingType == typeof(Guid)
            || underlyingType == typeof(DateTime)
            || underlyingType == typeof(DateTimeOffset)
            || underlyingType == typeof(TimeSpan)
            || underlyingType == typeof(decimal);
    }

    private static string? FormatValue(object? value, string fieldName)
    {
        if (value is null)
            return null;

        if (value is Guid guid)
        {
            return IsAuditField(fieldName) && guid == Guid.Empty
                ? null
                : guid.ToString("D");
        }

        if (value is string text)
        {
            if (IsAuditField(fieldName) && Guid.TryParse(text, out var auditId))
                return auditId == Guid.Empty ? null : auditId.ToString("D");
            if (fieldName.EndsWith("json", StringComparison.OrdinalIgnoreCase)
                && TryNormalizeJson(text, out var normalizedJson))
            {
                return normalizedJson;
            }
            return text;
        }

        if (value is DateTime dateTime)
        {
            var utc = dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : dateTime.Kind == DateTimeKind.Local
                    ? dateTime.ToUniversalTime()
                    : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            return utc.ToString("O", CultureInfo.InvariantCulture);
        }

        if (value is DateTimeOffset dateTimeOffset)
            return dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        if (value is TimeSpan timeSpan)
            return timeSpan.Ticks.ToString(CultureInfo.InvariantCulture);

        if (value.GetType().IsEnum)
            return Convert.ToInt64(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture);

        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
    }

    private static bool IsAuditField(string fieldName) =>
        fieldName is "createdby" or "updatedby" or "deletedby";

    private static string NormalizeFieldName(string name)
    {
        var normalized = new string(name
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return normalized switch
        {
            "targetagegroupconfigurationid" => "targetagegroupid",
            _ => normalized
        };
    }

    private static bool TryNormalizeJson(string text, out string normalized)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            normalized = NormalizeJsonElement(document.RootElement);
            return true;
        }
        catch (JsonException)
        {
            normalized = string.Empty;
            return false;
        }
    }

    private static string NormalizeJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => JsonSerializer.Serialize(property.Name) + ":" + NormalizeJsonElement(property.Value))) + "}",
            JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(NormalizeJsonElement)) + "]",
            JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };

    private static IReadOnlyDictionary<Guid, int> NormalizeQuestionOrders<T>(
        IReadOnlyCollection<T> rows)
        where T : class
    {
        var idProperty = typeof(T).GetProperty("Id")!;
        var readingTextIdProperty = typeof(T).GetProperty("ReadingTextId")!;
        var orderProperty = typeof(T).GetProperty("OrderIndex")!;
        return rows
            .GroupBy(row => (Guid)readingTextIdProperty.GetValue(row)!)
            .SelectMany(group =>
            {
                var used = new HashSet<int>();
                return group
                    .OrderBy(row => (int)orderProperty.GetValue(row)!)
                    .ThenBy(row => (Guid)idProperty.GetValue(row)!)
                    .Select(row =>
                    {
                        var order = Math.Max(0, (int)orderProperty.GetValue(row)!);
                        while (!used.Add(order))
                            order++;
                        return new { Id = (Guid)idProperty.GetValue(row)!, Order = order };
                    });
            })
            .ToDictionary(item => item.Id, item => item.Order);
    }

    private static Guid? TryGetGuid(object? value) =>
        value switch
        {
            Guid guid => guid,
            string text when Guid.TryParse(text, out var guid) => guid,
            _ => null
        };

    private static Type? FindEntityClrType(Expression expression)
    {
        var visitor = new EntityQueryRootVisitor();
        visitor.Visit(expression);
        return visitor.ClrType;
    }

    private static string? FindProjectedPropertyName(Expression expression)
    {
        if (expression is MethodCallExpression methodCall)
        {
            if (string.Equals(methodCall.Method.Name, "Select", StringComparison.Ordinal)
                && methodCall.Arguments.Count > 1
                && StripQuote(methodCall.Arguments[1]) is LambdaExpression lambda)
            {
                return GetMemberName(lambda.Body);
            }

            return FindProjectedPropertyName(methodCall.Arguments[0]);
        }

        return null;
    }

    private static string? GetMemberName(Expression expression) =>
        expression switch
        {
            MemberExpression member => member.Member.Name,
            UnaryExpression unary => GetMemberName(unary.Operand),
            _ => null
        };

    private static Expression StripQuote(Expression expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Quote } unary
            ? unary.Operand
            : expression;

    private sealed class EntityQueryRootVisitor : ExpressionVisitor
    {
        public Type? ClrType { get; private set; }

        protected override Expression VisitExtension(Expression node)
        {
            var entityType = node.GetType()
                .GetProperty("EntityType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(node);
            ClrType = entityType?.GetType()
                .GetProperty("ClrType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(entityType) as Type
                ?? ClrType;
            return base.VisitExtension(node);
        }
    }
}
