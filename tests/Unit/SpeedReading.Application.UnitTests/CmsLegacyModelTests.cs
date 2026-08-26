using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure;
using SpeedReading.Infrastructure.Legacy;
using System.Reflection;

namespace SpeedReading.Application.UnitTests;

public sealed class CmsLegacyModelTests
{
    [Fact]
    public void Legacy_cms_tables_are_mapped_without_renaming_existing_columns()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Should().Contain(new[]
            {
                "ContentBlocks",
                "Pages",
                "BlogPosts",
                "ContactMessages",
                "NewsletterSubscribers",
                "Products",
                "SubscriptionPlans",
                "UserSubscriptions",
                "Payments"
            });

        var contentBlock = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "ContentBlocks");
        contentBlock.FindProperty("Key")!.GetColumnName().Should().Be("Key");
        contentBlock.FindProperty("Group")!.GetColumnName().Should().Be("Group");
        contentBlock.FindProperty("Value")!.GetColumnName().Should().Be("Value");

        var page = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "Pages");
        page.FindProperty("MetaTitle")!.GetColumnName().Should().Be("MetaTitle");
        page.FindProperty("SeoSettingsNoIndex")!.GetColumnName().Should().Be("SeoSettings_NoIndex");

        var contactMessage = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "ContactMessages");
        contactMessage.FindProperty("IsReplied")!.GetColumnName().Should().Be("IsReplied");
        contactMessage.FindProperty("RepliedBy")!.GetColumnName().Should().Be("RepliedBy");

        var product = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "Products");
        product.FindProperty("IncludedProductSlugsJson")!.GetColumnName().Should().Be("IncludedProductSlugs");

        var subscription = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "UserSubscriptions");
        subscription.FindProperty("IsDeleted")!.GetColumnName().Should().Be("IsDeleted");

        var payment = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "Payments");
        payment.FindProperty("ProviderToken")!.GetColumnName().Should().Be("ProviderToken");
    }

    [Fact]
    public void Subscription_join_query_is_translatable_by_ef_core()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);
        var service = new LegacySpeedReadingSubscription(context);
        var method = typeof(LegacySpeedReadingSubscription)
            .GetMethod("SubscriptionRows", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var query = (IQueryable)method.Invoke(service, null)!;

        query.ToQueryString().Should().Contain("UserSubscriptions");
        query.ToQueryString().Should().Contain("SubscriptionPlans");
        query.ToQueryString().Should().Contain("Products");
    }

    [Fact]
    public void Adaptive_learning_tables_are_mapped_to_the_legacy_schema()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Should().Contain(new[]
            {
                "StudentLearningProfiles",
                "ContentRecommendations",
                "DailyGoals"
            });

        var profile = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "StudentLearningProfiles");
        profile.FindProperty("StudentId")!.GetColumnName().Should().Be("StudentId");
        profile.FindProperty("WeakAreas")!.GetColumnName().Should().Be("WeakAreas");

        var recommendation = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "ContentRecommendations");
        recommendation.FindProperty("ReadingTextId")!.GetColumnName().Should().Be("ReadingTextId");
        recommendation.FindProperty("ConfidenceScore")!.GetColumnName().Should().Be("ConfidenceScore");

        var goal = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "DailyGoals");
        goal.FindProperty("Date")!.GetColumnName().Should().Be("Date");
        goal.FindProperty("ActualMinutes")!.GetColumnName().Should().Be("ActualMinutes");
    }

    [Fact]
    public void Adaptive_text_tables_preserve_profile_arrays_and_recommendation_history_columns()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Should().Contain(new[] { "StudentReadingProfiles", "TextRecommendationHistories" });

        var profile = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "StudentReadingProfiles");
        profile.FindProperty("PreferredCategories")!.GetColumnName().Should().Be("PreferredCategories");
        profile.FindProperty("DifficultCategories")!.GetColumnName().Should().Be("DifficultCategories");

        var history = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "TextRecommendationHistories");
        history.FindProperty("ReasoningJson")!.GetColumnName().Should().Be("ReasoningJson");
        history.FindProperty("StudentLevelAtTime")!.GetColumnName().Should().Be("StudentLevelAtTime");
    }

    [Fact]
    public void Content_feedback_table_preserves_the_legacy_tracking_columns()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        var feedback = context.Model.GetEntityTypes()
            .SingleOrDefault(entity => entity.GetTableName() == "UserContentFeedbacks");

        feedback.Should().NotBeNull();
        feedback!.FindProperty("ContentId")!.GetColumnName().Should().Be("ContentId");
        feedback.FindProperty("SessionDate")!.GetColumnName().Should().Be("SessionDate");
        feedback.FindProperty("ContentDifficultyLevel")!.GetColumnName().Should().Be("ContentDifficultyLevel");
    }

    [Fact]
    public void Visualization_tables_preserve_scene_question_and_soft_delete_columns()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Should().Contain(new[] { "VisualizationScenes", "VisualizationQuestions" });

        var scene = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "VisualizationScenes");
        scene.FindProperty("ExerciseId")!.GetColumnName().Should().Be("ExerciseId");
        scene.FindProperty("TargetAgeGroupConfigurationId")!.GetColumnName().Should().Be("TargetAgeGroupConfigurationId");
        scene.FindProperty("IsDeleted")!.GetColumnName().Should().Be("IsDeleted");

        var question = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "VisualizationQuestions");
        question.FindProperty("OptionsJson")!.GetColumnName().Should().Be("OptionsJson");
        question.FindProperty("SceneId")!.GetColumnName().Should().Be("SceneId");
        question.FindProperty("HintText")!.GetColumnName().Should().Be("HintText");
    }

    [Fact]
    public void Vocabulary_tables_preserve_item_and_spaced_repetition_columns()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Should().Contain(new[] { "VocabularyItems", "UserVocabularyProgresses" });

        var item = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "VocabularyItems");
        item.FindProperty("TargetAgeGroupConfigurationId")!.GetColumnName().Should().Be("TargetAgeGroupConfigurationId");
        item.FindProperty("DifficultyLevel")!.GetColumnName().Should().Be("DifficultyLevel");

        var progress = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "UserVocabularyProgresses");
        progress.FindProperty("VocabularyItemId")!.GetColumnName().Should().Be("VocabularyItemId");
        progress.FindProperty("NextReviewDate")!.GetColumnName().Should().Be("NextReviewDate");
        progress.FindProperty("ConsecutiveCorrectCount")!.GetColumnName().Should().Be("ConsecutiveCorrectCount");
    }

    [Fact]
    public void Exam_question_table_preserves_exam_filters_and_answer_columns()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        var question = context.Model.GetEntityTypes()
            .SingleOrDefault(entity => entity.GetTableName() == "ExamQuestions");

        question.Should().NotBeNull();
        question!.FindProperty("ExamType")!.GetColumnName().Should().Be("ExamType");
        question.FindProperty("CorrectOption")!.GetColumnName().Should().Be("CorrectOption");
        question.FindProperty("TargetAgeGroupConfigurationId")!.GetColumnName().Should().Be("TargetAgeGroupConfigurationId");
    }

    [Fact]
    public void Rsvp_table_preserves_reading_display_and_completion_columns()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        var session = context.Model.GetEntityTypes()
            .SingleOrDefault(entity => entity.GetTableName() == "RSVPSessions");

        session.Should().NotBeNull();
        session!.FindProperty("TextContent")!.GetColumnName().Should().Be("TextContent");
        session.FindProperty("WordsPerMinute")!.GetColumnName().Should().Be("WordsPerMinute");
        session.FindProperty("CompletedAt")!.GetColumnName().Should().Be("CompletedAt");
    }

    [Fact]
    public void Notification_tables_preserve_legacy_fields_and_frontend_compatibility_columns()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Should().Contain(new[]
            {
                "Notifications",
                "NotificationTypePreferences",
                "NotificationPreferences",
                "PushSubscriptions",
                "Announcements",
                "AnnouncementUserInteractions",
                "EmailTemplates",
                "EmailCampaigns",
                "EmailCampaignLogs"
            });

        var notification = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "Notifications");
        notification.FindProperty("ReadAt")!.GetColumnName().Should().Be("ReadAt");
        notification.FindProperty("Priority")!.GetColumnName().Should().Be("Priority");

        var announcement = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "Announcements");
        announcement.FindProperty("EndDate")!.GetColumnName().Should().Be("EndDate");
        announcement.FindProperty("ExpiresAt")!.GetColumnName().Should().Be("ExpiresAt");
        announcement.FindProperty("DisplayType")!.GetColumnName().Should().Be("DisplayType");

        var template = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "EmailTemplates");
        template.FindProperty("Variables")!.GetColumnName().Should().Be("Variables");
        template.FindProperty("Code")!.GetColumnName().Should().Be("Code");

        var campaign = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "EmailCampaigns");
        campaign.FindProperty("Status")!.GetColumnName().Should().Be("Status");
        campaign.FindProperty("PlainTextBody")!.GetColumnName().Should().Be("PlainTextBody");
        campaign.FindProperty("OpenedCount")!.GetColumnName().Should().Be("OpenedCount");
    }

    [Fact]
    public void Age_group_mapping_preserves_recommendation_and_range_columns()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        var ageGroup = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "AgeGroupConfigurations");

        ageGroup.FindProperty("MinAge")!.GetColumnName().Should().Be("MinAge");
        ageGroup.FindProperty("MaxAge")!.GetColumnName().Should().Be("MaxAge");
        ageGroup.FindProperty("RecommendedWPM")!.GetColumnName().Should().Be("RecommendedWPM");
        ageGroup.FindProperty("RecommendedDailyMinutes")!.GetColumnName().Should().Be("RecommendedDailyMinutes");
        ageGroup.FindProperty("DefaultDifficultyLevel")!.GetColumnName().Should().Be("DefaultDifficultyLevel");

        var user = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "Users");
        user.FindProperty("AgeGroupConfigurationId")!.GetColumnName().Should().Be("AgeGroupConfigurationId");
    }

    [Fact]
    public void Review_queue_mapping_preserves_sm2_columns()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);

        var review = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "ExerciseReviewItems");

        review.FindProperty("NextReviewDate")!.GetColumnName().Should().Be("NextReviewDate");
        review.FindProperty("EasinessFactor")!.GetColumnName().Should().Be("EasinessFactor");
        review.FindProperty("IsMastered")!.GetColumnName().Should().Be("IsMastered");
        review.FindProperty("LastScore")!.GetColumnName().Should().Be("LastScore");
    }
}
