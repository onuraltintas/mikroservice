using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingParityRow(
    string SourceTable,
    string OwnedTable,
    string SourceKey,
    string OwnedKey,
    int SourceCount,
    int OwnedCount,
    string SourceChecksum,
    string OwnedChecksum)
{
    public bool IsMatch => SourceCount == OwnedCount && SourceChecksum == OwnedChecksum;
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
    public async Task<OwnedSpeedReadingParityReport> RunAsync(
        CancellationToken cancellationToken = default)
    {
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

        rows.Add(await CompareAsync("Users(active)", "user_profiles", "Id", "user_id", legacy.Users.Where(item => !item.IsDeleted).Select(item => item.Id), owned.UserProfiles.Select(item => item.UserId), cancellationToken));
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
        rows.Add(await CompareAsync("ExerciseReviewItems", "review_items", "Id", "id", legacy.ExerciseReviewItems.Select(item => item.Id), owned.ReviewItems.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("Notifications", "notifications", "Id", "id", legacy.Notifications.Select(item => item.Id), ownedData.Notifications.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("NotificationPreferences", "notification_preferences", "Id", "id", legacy.NotificationPreferences.Select(item => item.Id), ownedData.NotificationPreferences.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("NotificationTypePreferences", "notification_type_preferences", "Id", "id", legacy.NotificationPreferences.Select(item => item.Id), ownedData.NotificationTypePreferences.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("PushSubscriptions", "push_subscriptions", "Id", "id", legacy.PushSubscriptions.Select(item => item.Id), ownedData.PushSubscriptions.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("Announcements", "announcements", "Id", "id", legacy.Announcements.Select(item => item.Id), ownedData.Announcements.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("AnnouncementUserInteractions", "announcement_user_interactions", "Id", "id", legacy.AnnouncementUserInteractions.Select(item => item.Id), ownedData.AnnouncementUserInteractions.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("EmailTemplates", "email_templates", "Id", "id", legacy.EmailTemplates.Select(item => item.Id), ownedData.EmailTemplates.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("EmailCampaigns", "email_campaigns", "Id", "id", legacy.EmailCampaigns.Select(item => item.Id), ownedData.EmailCampaigns.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("EmailCampaignLogs", "email_campaign_logs", "Id", "id", legacy.EmailCampaignLogs.Select(item => item.Id), ownedData.EmailCampaignLogs.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("RSVPSessions", "rsvp_sessions", "Id", "id", legacy.RsvpSessions.Select(item => item.Id), ownedData.RsvpSessions.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("UserContentFeedbacks", "content_feedback", "Id", "id", legacy.UserContentFeedbacks.Select(item => item.Id), owned.ContentFeedbacks.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("StudentLearningProfiles", "adaptive_learning_profiles", "Id", "id", legacy.StudentLearningProfiles.Select(item => item.Id), owned.AdaptiveLearningProfiles.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ContentRecommendations", "adaptive_content_recommendations", "Id", "id", legacy.ContentRecommendations.Select(item => item.Id), owned.AdaptiveContentRecommendations.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("DailyGoals", "adaptive_daily_goals", "Id", "id", legacy.DailyGoals.Select(item => item.Id), owned.AdaptiveDailyGoals.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("StudentReadingProfiles", "adaptive_reading_profiles", "Id", "id", legacy.StudentReadingProfiles.Select(item => item.Id), owned.AdaptiveReadingProfiles.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("TextRecommendationHistories", "adaptive_text_recommendation_history", "Id", "id", legacy.TextRecommendationHistories.Select(item => item.Id), owned.AdaptiveTextRecommendationHistories.Select(item => item.Id), cancellationToken));

        rows.Add(await CompareAsync("ReportTemplates", "report_templates", "Id", "id", legacy.ReportTemplates.Select(item => item.Id), owned.ReportTemplates.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ReportSnapshots", "report_snapshots", "Id", "id", legacy.ReportSnapshots.Select(item => item.Id), owned.ReportSnapshots.Select(item => item.Id), cancellationToken));
        rows.Add(await CompareAsync("ScheduledReports", "scheduled_reports", "Id", "id", legacy.ScheduledReports.Select(item => item.Id), owned.ScheduledReports.Select(item => item.Id), cancellationToken));

        return new OwnedSpeedReadingParityReport(DateTime.UtcNow, rows);
    }

    private static async Task<OwnedSpeedReadingParityRow> CompareAsync(
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
        return new OwnedSpeedReadingParityRow(
            sourceTable,
            ownedTable,
            sourceKey,
            ownedKey,
            source.Count,
            target.Count,
            OwnedSpeedReadingParityHash.Compute(source),
            OwnedSpeedReadingParityHash.Compute(target));
    }
}
