using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Kernel.Primitives;
using EduPlatform.Shared.Infrastructure.Middleware;
using SpeedReading.Domain.Assignments;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.LearningPaths;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.QuestionBank;
using SpeedReading.Domain.Visualization;
using SpeedReading.Domain.Vocabulary;
using SpeedReading.Infrastructure.Legacy;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Profiles;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// EF Core context for data owned by the Speed Reading bounded context.
/// It intentionally has no legacy entity sets.
/// </summary>
public sealed class OwnedSpeedReadingDbContext(
    DbContextOptions<OwnedSpeedReadingDbContext> options) : DbContext(options), ISpeedReadingDataContext
{
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseTypeCategory> ExerciseTypeCategories => Set<ExerciseTypeCategory>();
    public DbSet<ExerciseType> ExerciseTypes => Set<ExerciseType>();
    public DbSet<ReadingText> ReadingTexts => Set<ReadingText>();
    public DbSet<ReadingQuestion> ReadingQuestions => Set<ReadingQuestion>();
    public DbSet<ExerciseSession> ExerciseSessions => Set<ExerciseSession>();
    public DbSet<ExerciseSessionAnswer> ExerciseSessionAnswers => Set<ExerciseSessionAnswer>();
    public DbSet<ExerciseSessionResult> ExerciseSessionResults => Set<ExerciseSessionResult>();
    public DbSet<ReadingSession> ReadingSessions => Set<ReadingSession>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<StudentAssignment> StudentAssignments => Set<StudentAssignment>();
    public DbSet<AgeGroupConfiguration> AgeGroupConfigurations => Set<AgeGroupConfiguration>();
    public DbSet<ProgramTemplate> ProgramTemplates => Set<ProgramTemplate>();
    public DbSet<StudentProgramProgress> StudentProgramProgresses => Set<StudentProgramProgress>();
    public DbSet<DailyExerciseLog> DailyExerciseLogs => Set<DailyExerciseLog>();
    public DbSet<SpeedReadingUserProfile> UserProfiles => Set<SpeedReadingUserProfile>();
    public DbSet<LearningPathTemplate> LearningPathTemplates => Set<LearningPathTemplate>();
    public DbSet<LearningPathNode> LearningPathNodes => Set<LearningPathNode>();
    public DbSet<LearningPathNodeContent> LearningPathNodeContents => Set<LearningPathNodeContent>();
    public DbSet<LearningPathPrerequisite> LearningPathPrerequisites => Set<LearningPathPrerequisite>();
    public DbSet<StudentLearningPathProgress> StudentLearningPathProgresses => Set<StudentLearningPathProgress>();
    public DbSet<StudentLearningNodeProgress> StudentLearningNodeProgresses => Set<StudentLearningNodeProgress>();
    public DbSet<PersonalizedLearningPathItem> PersonalizedLearningPathItems => Set<PersonalizedLearningPathItem>();
    public DbSet<AdminAuditRecord> AdminAuditRecords => Set<AdminAuditRecord>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<UserGamification> UserGamifications => Set<UserGamification>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<VisualizationScene> VisualizationScenes => Set<VisualizationScene>();
    public DbSet<VisualizationQuestion> VisualizationQuestions => Set<VisualizationQuestion>();
    public DbSet<VocabularyItem> VocabularyItems => Set<VocabularyItem>();
    public DbSet<UserVocabularyProgress> UserVocabularyProgresses => Set<UserVocabularyProgress>();
    DbSet<LegacyProduct> ISpeedReadingDataContext.Products => Set<LegacyProduct>();
    DbSet<LegacyContentBlock> ISpeedReadingDataContext.ContentBlocks => Set<LegacyContentBlock>();
    DbSet<LegacyPage> ISpeedReadingDataContext.Pages => Set<LegacyPage>();
    DbSet<LegacyBlogPost> ISpeedReadingDataContext.BlogPosts => Set<LegacyBlogPost>();
    DbSet<LegacyContactMessage> ISpeedReadingDataContext.ContactMessages => Set<LegacyContactMessage>();
    DbSet<LegacyNewsletterSubscriber> ISpeedReadingDataContext.NewsletterSubscribers => Set<LegacyNewsletterSubscriber>();
    DbSet<LegacySubscriptionPlan> ISpeedReadingDataContext.SubscriptionPlans => Set<LegacySubscriptionPlan>();
    DbSet<LegacyUserSubscription> ISpeedReadingDataContext.UserSubscriptions => Set<LegacyUserSubscription>();
    DbSet<LegacyPayment> ISpeedReadingDataContext.Payments => Set<LegacyPayment>();
    DbSet<LegacyUserNotification> ISpeedReadingDataContext.Notifications => Set<LegacyUserNotification>();
    DbSet<LegacyNotificationPreference> ISpeedReadingDataContext.NotificationPreferences => Set<LegacyNotificationPreference>();
    DbSet<LegacyNotificationTypePreference> ISpeedReadingDataContext.NotificationTypePreferences => Set<LegacyNotificationTypePreference>();
    DbSet<LegacyPushSubscription> ISpeedReadingDataContext.PushSubscriptions => Set<LegacyPushSubscription>();
    DbSet<LegacyAnnouncement> ISpeedReadingDataContext.Announcements => Set<LegacyAnnouncement>();
    DbSet<LegacyAnnouncementUserInteraction> ISpeedReadingDataContext.AnnouncementUserInteractions => Set<LegacyAnnouncementUserInteraction>();
    DbSet<LegacyEmailTemplate> ISpeedReadingDataContext.EmailTemplates => Set<LegacyEmailTemplate>();
    DbSet<LegacyEmailCampaign> ISpeedReadingDataContext.EmailCampaigns => Set<LegacyEmailCampaign>();
    DbSet<LegacyEmailCampaignLog> ISpeedReadingDataContext.EmailCampaignLogs => Set<LegacyEmailCampaignLog>();
    DbSet<LegacyRsvpSession> ISpeedReadingDataContext.RsvpSessions => Set<LegacyRsvpSession>();
    internal DbSet<OwnedIdempotencyRecord> IdempotencyRecords => Set<OwnedIdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("speed_reading");

        ConfigureEntity(modelBuilder.Entity<Exercise>());
        ConfigureEntity(modelBuilder.Entity<ExerciseTypeCategory>());
        ConfigureEntity(modelBuilder.Entity<ExerciseType>());
        ConfigureEntity(modelBuilder.Entity<ReadingText>());
        ConfigureEntity(modelBuilder.Entity<ReadingQuestion>());
        ConfigureEntity(modelBuilder.Entity<ExerciseSession>());
        ConfigureEntity(modelBuilder.Entity<ExerciseSessionAnswer>());
        ConfigureEntity(modelBuilder.Entity<ExerciseSessionResult>());
        ConfigureEntity(modelBuilder.Entity<ReadingSession>());
        ConfigureEntity(modelBuilder.Entity<Assignment>());
        ConfigureEntity(modelBuilder.Entity<StudentAssignment>());
        ConfigureEntity(modelBuilder.Entity<AgeGroupConfiguration>());
        ConfigureEntity(modelBuilder.Entity<ProgramTemplate>());
        ConfigureEntity(modelBuilder.Entity<StudentProgramProgress>());
        ConfigureEntity(modelBuilder.Entity<DailyExerciseLog>());
        ConfigureEntity(modelBuilder.Entity<SpeedReadingUserProfile>());
        ConfigureEntity(modelBuilder.Entity<LearningPathTemplate>());
        ConfigureEntity(modelBuilder.Entity<LearningPathNode>());
        ConfigureEntity(modelBuilder.Entity<LearningPathNodeContent>());
        ConfigureEntity(modelBuilder.Entity<LearningPathPrerequisite>());
        ConfigureEntity(modelBuilder.Entity<StudentLearningPathProgress>());
        ConfigureEntity(modelBuilder.Entity<StudentLearningNodeProgress>());
        ConfigureEntity(modelBuilder.Entity<PersonalizedLearningPathItem>());
        ConfigureEntity(modelBuilder.Entity<Achievement>());
        ConfigureEntity(modelBuilder.Entity<UserAchievement>());
        ConfigureEntity(modelBuilder.Entity<UserGamification>());
        ConfigureEntity(modelBuilder.Entity<ExamQuestion>());
        ConfigureEntity(modelBuilder.Entity<VisualizationScene>());
        ConfigureEntity(modelBuilder.Entity<VisualizationQuestion>());
        ConfigureEntity(modelBuilder.Entity<VocabularyItem>());
        ConfigureEntity(modelBuilder.Entity<UserVocabularyProgress>());
        modelBuilder.Entity<AdminAuditRecord>(entity =>
        {
            entity.ToTable("admin_audit_records");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ServiceName).HasMaxLength(150).IsRequired();
            entity.Property(item => item.ActorUserId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ActorRoles).HasMaxLength(500).IsRequired();
            entity.Property(item => item.TenantId).HasMaxLength(100);
            entity.Property(item => item.HttpMethod).HasMaxLength(10).IsRequired();
            entity.Property(item => item.Path).HasMaxLength(500).IsRequired();
            entity.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ClientIp).HasMaxLength(64);
            entity.Property(item => item.UserAgent).HasMaxLength(256);
            entity.Property(item => item.Action).HasMaxLength(32);
            entity.Property(item => item.ResourceType).HasMaxLength(100);
            entity.Property(item => item.ResourceId).HasMaxLength(100);
            entity.Property(item => item.ChangedFieldsJson).HasMaxLength(2_000);
            entity.HasIndex(item => new { item.OccurredAt, item.Id });
            entity.HasIndex(item => new { item.ActorUserId, item.OccurredAt });
            entity.HasIndex(item => new { item.ResourceType, item.ResourceId, item.OccurredAt });
        });

        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.ToTable("achievements");
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Tier).HasMaxLength(50).IsRequired();
            entity.Property(item => item.IconUrl).HasMaxLength(500).IsRequired();
            entity.Property(item => item.IconEmoji).HasMaxLength(10).IsRequired();
            entity.Property(item => item.CriteriaType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CriteriaValue).HasMaxLength(4_000).IsRequired();
            entity.Property(item => item.TriggerType).HasMaxLength(100);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => item.Category);
            entity.HasIndex(item => item.Tier);
            entity.HasIndex(item => item.IsActive);
            entity.HasIndex(item => item.SortOrder);
        });

        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.ToTable("user_achievements");
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.UserId, item.AchievementId, item.IsDeleted }).IsUnique();
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.UnlockedAt);
            entity.HasIndex(item => new { item.UserId, item.IsShowcased });
            entity.HasOne<Achievement>()
                .WithMany()
                .HasForeignKey(item => item.AchievementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserGamification>(entity =>
        {
            entity.ToTable("user_gamification");
            entity.Property(item => item.LevelTitle).HasMaxLength(200).IsRequired();
            entity.Property(item => item.LevelIcon).HasMaxLength(50).IsRequired();
            entity.Property(item => item.MaxComprehensionScore).HasPrecision(18, 2);
            entity.Property(item => item.MaxRSVPComprehension).HasPrecision(18, 2);
            entity.Property(item => item.CompletedExerciseTypesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.LearnedVocabularyCategoriesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.LearnedVocabularyCategoriesMapJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.LearnedVocabularyDifficultiesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => item.UserId).IsUnique();
            entity.HasIndex(item => item.TotalXP);
            entity.HasIndex(item => item.CurrentLevel);
            entity.HasIndex(item => item.CurrentStreak);
        });
        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.ToTable("exam_questions");
            entity.Property(item => item.Content).IsRequired();
            entity.Property(item => item.Question).IsRequired();
            entity.Property(item => item.OptionA).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OptionB).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OptionC).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OptionD).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OptionE).HasMaxLength(500);
            entity.Property(item => item.CorrectOption).HasMaxLength(1).IsRequired();
            entity.Property(item => item.Topic).HasMaxLength(300);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.ExamType, item.Difficulty, item.Category });
            entity.HasIndex(item => item.TargetAgeGroupId);
            entity.HasIndex(item => item.CreatedAt);
        });
        modelBuilder.Entity<VisualizationScene>(entity =>
        {
            entity.ToTable("visualization_scenes");
            entity.Property(item => item.Description).HasMaxLength(4_000).IsRequired();
            entity.Property(item => item.ImageUrl).HasMaxLength(1_000);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.ExerciseId, item.IsDeleted, item.DisplayOrder });
            entity.HasIndex(item => item.TargetAgeGroupId);
        });
        modelBuilder.Entity<VisualizationQuestion>(entity =>
        {
            entity.ToTable("visualization_questions");
            entity.Property(item => item.QuestionText).IsRequired();
            entity.Property(item => item.OptionsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.CorrectAnswer).HasMaxLength(500).IsRequired();
            entity.Property(item => item.QuestionType).HasMaxLength(50).IsRequired();
            entity.Property(item => item.HintText).HasMaxLength(2_000);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.SceneId, item.IsDeleted, item.DisplayOrder });
            entity.HasOne<VisualizationScene>().WithMany().HasForeignKey(item => item.SceneId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<VocabularyItem>(entity =>
        {
            entity.ToTable("vocabulary_items");
            entity.Property(item => item.Word).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Definition).HasMaxLength(2_000).IsRequired();
            entity.Property(item => item.ExampleSentence).HasMaxLength(2_000);
            entity.Property(item => item.Synonyms).HasMaxLength(2_000);
            entity.Property(item => item.Antonyms).HasMaxLength(2_000);
            entity.Property(item => item.Category).HasMaxLength(200).IsRequired();
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.Category, item.DifficultyLevel, item.IsDeleted });
            entity.HasIndex(item => item.TargetAgeGroupId);
        });
        modelBuilder.Entity<UserVocabularyProgress>(entity =>
        {
            entity.ToTable("user_vocabulary_progress");
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.UserId, item.VocabularyItemId, item.IsDeleted });
            entity.HasIndex(item => new { item.UserId, item.NextReviewDate, item.IsDeleted });
            entity.HasOne<VocabularyItem>().WithMany().HasForeignKey(item => item.VocabularyItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<LegacyProduct>(entity =>
        {
            entity.ToTable("subscription_products");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Slug).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1_000).IsRequired();
            entity.Property(item => item.IncludedProductSlugsJson).HasColumnName("included_product_slugs").HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => item.Slug).IsUnique();
        });
        modelBuilder.Entity<LegacySubscriptionPlan>(entity =>
        {
            entity.ToTable("subscription_plans");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1_000).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Price).HasPrecision(10, 2);
            entity.Property(item => item.Features).HasColumnType("jsonb");
            entity.HasIndex(item => item.ProductId);
            entity.HasIndex(item => item.Slug).IsUnique();
            entity.HasOne<LegacyProduct>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LegacyUserSubscription>(entity =>
        {
            entity.ToTable("user_subscriptions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserName).HasMaxLength(200);
            entity.Property(item => item.UserEmail).HasMaxLength(256);
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(2_000);
            entity.HasIndex(item => new { item.UserId, item.Status });
            entity.HasIndex(item => new { item.UserId, item.PlanId });
            entity.HasIndex(item => new { item.UserId, item.ProductId });
            entity.HasOne<LegacySubscriptionPlan>().WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LegacyProduct>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LegacyPayment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserEmail).HasMaxLength(255).IsRequired();
            entity.Property(item => item.UserName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Amount).HasPrecision(10, 2);
            entity.Property(item => item.Currency).HasMaxLength(10).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Provider).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ProviderToken).HasMaxLength(500);
            entity.Property(item => item.ProviderPaymentId).HasMaxLength(500);
            entity.Property(item => item.ProviderResponse).HasColumnType("jsonb");
            entity.Property(item => item.ErrorMessage).HasMaxLength(2_000);
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.PlanId);
            entity.HasIndex(item => item.ProviderToken).IsUnique().HasFilter("\"ProviderToken\" IS NOT NULL");
            entity.HasOne<LegacySubscriptionPlan>().WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LegacyContentBlock>(entity =>
        {
            ConfigureLegacyEntity(entity, "cms_content_blocks");
            entity.Property(item => item.Key).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Group).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Label).HasMaxLength(200);
            entity.Property(item => item.Value).IsRequired();
            entity.HasIndex(item => item.Group);
            entity.HasIndex(item => item.Key);
        });
        modelBuilder.Entity<LegacyPage>(entity =>
        {
            ConfigureLegacyEntity(entity, "cms_pages");
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Content).IsRequired();
            entity.Property(item => item.MetaTitle).HasMaxLength(200);
            entity.Property(item => item.MetaDescription).HasMaxLength(500);
            entity.Property(item => item.MetaKeywords).HasMaxLength(500);
            entity.Property(item => item.CanonicalUrl).HasMaxLength(500);
            entity.Property(item => item.OgTitle).HasMaxLength(200);
            entity.Property(item => item.OgDescription).HasMaxLength(500);
            entity.Property(item => item.OgImage).HasMaxLength(500);
            entity.Property(item => item.SeoSettingsNoIndex).HasColumnName("SeoSettings_NoIndex");
            entity.HasIndex(item => item.Slug);
        });
        modelBuilder.Entity<LegacyBlogPost>(entity =>
        {
            ConfigureLegacyEntity(entity, "cms_blog_posts");
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Summary).HasMaxLength(500);
            entity.Property(item => item.CoverImageUrl).HasMaxLength(500);
            entity.Property(item => item.Tags).HasMaxLength(500);
            entity.Property(item => item.Author).HasMaxLength(100);
            entity.Property(item => item.MetaTitle).HasMaxLength(200);
            entity.Property(item => item.MetaDescription).HasMaxLength(500);
            entity.Property(item => item.MetaKeywords).HasMaxLength(500);
            entity.Property(item => item.CanonicalUrl).HasMaxLength(500);
            entity.Property(item => item.OgTitle).HasMaxLength(200);
            entity.Property(item => item.OgDescription).HasMaxLength(500);
            entity.Property(item => item.OgImage).HasMaxLength(500);
            entity.Property(item => item.SeoSettingsNoIndex).HasColumnName("SeoSettings_NoIndex");
            entity.HasIndex(item => item.PublishedAt);
            entity.HasIndex(item => item.Slug);
        });
        modelBuilder.Entity<LegacyContactMessage>(entity =>
        {
            ConfigureLegacyEntity(entity, "cms_contact_messages");
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Message).IsRequired();
            entity.Property(item => item.ReplyContent);
            entity.HasIndex(item => item.CreatedAt);
            entity.HasIndex(item => item.IsRead);
        });
        modelBuilder.Entity<LegacyNewsletterSubscriber>(entity =>
        {
            ConfigureLegacyEntity(entity, "cms_newsletter_subscribers");
            entity.Property(item => item.Email).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(50);
            entity.HasIndex(item => item.Email).IsUnique();
        });
        modelBuilder.Entity<LegacyUserNotification>(entity =>
        {
            ConfigureNotificationEntity(entity, "notifications");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Type).HasColumnName("type");
            entity.Property(item => item.Channel).HasColumnName("channel");
            entity.Property(item => item.Status).HasColumnName("status");
            entity.Property(item => item.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(item => item.Message).HasColumnName("message").HasMaxLength(1_000).IsRequired();
            entity.Property(item => item.Data).HasColumnName("data");
            entity.Property(item => item.ActionUrl).HasColumnName("action_url").HasMaxLength(500);
            entity.Property(item => item.IconUrl).HasColumnName("icon_url").HasMaxLength(500);
            entity.Property(item => item.SentAt).HasColumnName("sent_at");
            entity.Property(item => item.ReadAt).HasColumnName("read_at");
            entity.Property(item => item.Priority).HasColumnName("priority");
            entity.Property(item => item.UserName).HasColumnName("user_name").HasMaxLength(200);
            entity.Property(item => item.UserEmail).HasColumnName("user_email").HasMaxLength(256);
            entity.Property(item => item.UserRole).HasColumnName("user_role").HasMaxLength(100);
            entity.Property(item => item.ErrorMessage).HasColumnName("error_message").HasMaxLength(2_000);
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => new { item.UserId, item.Status });
        });
        modelBuilder.Entity<LegacyNotificationPreference>(entity =>
        {
            ConfigureNotificationEntity(entity, "notification_preferences");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.EmailEnabled).HasColumnName("email_enabled");
            entity.Property(item => item.PushEnabled).HasColumnName("push_enabled");
            entity.Property(item => item.InAppEnabled).HasColumnName("in_app_enabled");
            entity.Property(item => item.SmsEnabled).HasColumnName("sms_enabled");
            entity.Property(item => item.AchievementsEnabled).HasColumnName("achievements_enabled");
            entity.Property(item => item.LevelUpEnabled).HasColumnName("level_up_enabled");
            entity.Property(item => item.DailyReminderEnabled).HasColumnName("daily_reminder_enabled");
            entity.Property(item => item.StreakMilestoneEnabled).HasColumnName("streak_milestone_enabled");
            entity.Property(item => item.Email).HasColumnName("email").HasMaxLength(256);
            entity.Property(item => item.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
            entity.HasIndex(item => item.UserId).IsUnique();
        });
        modelBuilder.Entity<LegacyNotificationTypePreference>(entity =>
        {
            ConfigureNotificationEntity(entity, "notification_type_preferences");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.NotificationType).HasColumnName("notification_type");
            entity.Property(item => item.EnableInApp).HasColumnName("enable_in_app");
            entity.Property(item => item.EnableEmail).HasColumnName("enable_email");
            entity.Property(item => item.EnablePush).HasColumnName("enable_push");
            entity.Property(item => item.PreferredTime).HasColumnName("preferred_time").HasMaxLength(10);
            entity.HasIndex(item => new { item.UserId, item.NotificationType }).IsUnique();
        });
        modelBuilder.Entity<LegacyPushSubscription>(entity =>
        {
            ConfigureNotificationEntity(entity, "push_subscriptions");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Endpoint).HasColumnName("endpoint").HasMaxLength(500).IsRequired();
            entity.Property(item => item.P256DH).HasColumnName("p256dh").HasMaxLength(200).IsRequired();
            entity.Property(item => item.Auth).HasColumnName("auth").HasMaxLength(200).IsRequired();
            entity.Property(item => item.UserAgent).HasColumnName("user_agent").HasMaxLength(1_000);
            entity.Property(item => item.IsActive).HasColumnName("is_active");
            entity.HasIndex(item => item.Endpoint).IsUnique();
            entity.HasIndex(item => item.UserId);
        });
        modelBuilder.Entity<LegacyAnnouncement>(entity =>
        {
            ConfigureNotificationEntity(entity, "announcements");
            entity.Property(item => item.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(item => item.Content).HasColumnName("content").IsRequired();
            entity.Property(item => item.Type).HasColumnName("type").HasMaxLength(20).IsRequired();
            entity.Property(item => item.Priority).HasColumnName("priority");
            entity.Property(item => item.TargetAudience).HasColumnName("target_audience").HasMaxLength(50).IsRequired();
            entity.Property(item => item.TargetInstitutionId).HasColumnName("target_institution_id");
            entity.Property(item => item.TargetRoles).HasColumnName("target_roles").HasMaxLength(2_000);
            entity.Property(item => item.StartDate).HasColumnName("start_date");
            entity.Property(item => item.EndDate).HasColumnName("end_date");
            entity.Property(item => item.IsPinned).HasColumnName("is_pinned");
            entity.Property(item => item.IsActive).HasColumnName("is_active");
            entity.Property(item => item.ActionUrl).HasColumnName("action_url").HasMaxLength(500);
            entity.Property(item => item.ImageUrl).HasColumnName("image_url").HasMaxLength(1_000);
            entity.Property(item => item.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(item => item.PlainTextContent).HasColumnName("plain_text_content");
            entity.Property(item => item.ExpiresAt).HasColumnName("expires_at");
            entity.Property(item => item.DisplayType).HasColumnName("display_type");
            entity.Property(item => item.Icon).HasColumnName("icon").HasMaxLength(200);
            entity.Property(item => item.ColorTheme).HasColumnName("color_theme").HasMaxLength(50);
            entity.Property(item => item.ActionText).HasColumnName("action_text").HasMaxLength(100);
            entity.Property(item => item.SendEmailNotification).HasColumnName("send_email_notification");
            entity.Property(item => item.CreateInAppNotification).HasColumnName("create_in_app_notification");
            entity.Property(item => item.EmailCampaignId).HasColumnName("email_campaign_id");
            entity.HasIndex(item => item.IsActive);
            entity.HasIndex(item => item.CreatedAt);
        });
        modelBuilder.Entity<LegacyAnnouncementUserInteraction>(entity =>
        {
            ConfigureNotificationEntity(entity, "announcement_user_interactions");
            entity.Property(item => item.AnnouncementId).HasColumnName("announcement_id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.ViewedAt).HasColumnName("viewed_at");
            entity.Property(item => item.ClickedAt).HasColumnName("clicked_at");
            entity.Property(item => item.DismissedAt).HasColumnName("dismissed_at");
            entity.HasIndex(item => new { item.AnnouncementId, item.UserId }).IsUnique();
        });
        modelBuilder.Entity<LegacyEmailTemplate>(entity =>
        {
            ConfigureNotificationEntity(entity, "email_templates");
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(item => item.Subject).HasColumnName("subject").HasMaxLength(500).IsRequired();
            entity.Property(item => item.Body).HasColumnName("body").IsRequired();
            entity.Property(item => item.Variables).HasColumnName("variables");
            entity.Property(item => item.IsSystem).HasColumnName("is_system");
            entity.Property(item => item.IsActive).HasColumnName("is_active");
            entity.Property(item => item.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(item => item.Code).HasColumnName("code").HasMaxLength(100);
            entity.Property(item => item.AvailableVariables).HasColumnName("available_variables");
            entity.HasIndex(item => item.Code).IsUnique().HasFilter("code IS NOT NULL");
        });
        modelBuilder.Entity<LegacyEmailCampaign>(entity =>
        {
            ConfigureNotificationEntity(entity, "email_campaigns");
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(item => item.Subject).HasColumnName("subject").HasMaxLength(500).IsRequired();
            entity.Property(item => item.Body).HasColumnName("body").IsRequired();
            entity.Property(item => item.TargetRoles).HasColumnName("target_roles").HasMaxLength(2_000);
            entity.Property(item => item.TargetInstitutionId).HasColumnName("target_institution_id");
            entity.Property(item => item.TemplateId).HasColumnName("template_id");
            entity.Property(item => item.ScheduledFor).HasColumnName("scheduled_for");
            entity.Property(item => item.SentAt).HasColumnName("sent_at");
            entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(item => item.TotalRecipients).HasColumnName("total_recipients");
            entity.Property(item => item.SentCount).HasColumnName("sent_count");
            entity.Property(item => item.FailedCount).HasColumnName("failed_count");
            entity.Property(item => item.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(item => item.PlainTextBody).HasColumnName("plain_text_body");
            entity.Property(item => item.IncludeAllUsers).HasColumnName("include_all_users");
            entity.Property(item => item.IncludeSubscribers).HasColumnName("include_subscribers");
            entity.Property(item => item.OpenedCount).HasColumnName("opened_count");
            entity.Property(item => item.ClickedCount).HasColumnName("clicked_count");
            entity.HasIndex(item => item.TemplateId);
            entity.HasIndex(item => item.Status);
        });
        modelBuilder.Entity<LegacyEmailCampaignLog>(entity =>
        {
            ConfigureNotificationEntity(entity, "email_campaign_logs");
            entity.Property(item => item.CampaignId).HasColumnName("campaign_id");
            entity.Property(item => item.RecipientEmail).HasColumnName("recipient_email").HasMaxLength(256).IsRequired();
            entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(item => item.SentAt).HasColumnName("sent_at");
            entity.Property(item => item.ErrorMessage).HasColumnName("error_message").HasMaxLength(2_000);
            entity.HasIndex(item => item.CampaignId);
        });
        modelBuilder.Entity<LegacyRsvpSession>(entity =>
        {
            ConfigureLegacyEntity(entity, "rsvp_sessions");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.TextId).HasColumnName("text_id");
            entity.Property(item => item.TextContent).HasColumnName("text_content");
            entity.Property(item => item.WordsPerMinute).HasColumnName("words_per_minute");
            entity.Property(item => item.FontFamily).HasColumnName("font_family").HasMaxLength(100).IsRequired();
            entity.Property(item => item.FontSize).HasColumnName("font_size");
            entity.Property(item => item.BackgroundColor).HasColumnName("background_color").HasMaxLength(20).IsRequired();
            entity.Property(item => item.TextColor).HasColumnName("text_color").HasMaxLength(20).IsRequired();
            entity.Property(item => item.TotalWords).HasColumnName("total_words");
            entity.Property(item => item.CompletedWords).HasColumnName("completed_words");
            entity.Property(item => item.SessionDuration).HasColumnName("session_duration");
            entity.Property(item => item.Completed).HasColumnName("completed");
            entity.Property(item => item.CompletedAt).HasColumnName("completed_at");
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => new { item.UserId, item.CreatedAt });
        });
        modelBuilder.Entity<OwnedIdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_records");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Scope).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Key).HasMaxLength(128).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.HasIndex(item => new { item.Scope, item.Key }).IsUnique();
            entity.HasIndex(item => item.CreatedAt);
        });

        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.ToTable("exercises");
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2_000).IsRequired();
            entity.Property(item => item.TypeCode).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ConfigurationJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.TypeCode, item.IsActive });
            entity.HasIndex(item => item.CreatorId);
            entity.HasOne<ExerciseType>()
                .WithMany()
                .HasForeignKey(item => item.ExerciseTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExerciseTypeCategory>(entity =>
        {
            entity.ToTable("exercise_type_categories");
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2_000).IsRequired();
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<ExerciseType>(entity =>
        {
            entity.ToTable("exercise_types");
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2_000).IsRequired();
            entity.Property(item => item.IconName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ColorCode).HasMaxLength(30).IsRequired();
            entity.Property(item => item.EngineType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasIndex(item => new { item.CategoryId, item.IsActive });
            entity.HasOne<ExerciseTypeCategory>()
                .WithMany()
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReadingText>(entity =>
        {
            entity.ToTable("reading_texts");
            entity.Property(item => item.Title).HasMaxLength(300).IsRequired();
            entity.Property(item => item.Content).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Language).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Tags).HasMaxLength(1_000).IsRequired();
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.Property(item => item.AverageComprehensionScore).HasPrecision(5, 2);
            entity.HasIndex(item => new { item.ExerciseId, item.IsActive });
            entity.HasIndex(item => new { item.Language, item.IsActive });
            entity.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(item => item.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReadingQuestion>(entity =>
        {
            entity.ToTable("reading_questions");
            entity.Property(item => item.QuestionText).IsRequired();
            entity.Property(item => item.Explanation).HasMaxLength(2_000);
            entity.Property(item => item.OptionA).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OptionB).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OptionC).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OptionD).HasMaxLength(500).IsRequired();
            entity.Property(item => item.CorrectAnswer).HasMaxLength(500).IsRequired();
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.ReadingTextId, item.OrderIndex }).IsUnique();
            entity.HasOne<ReadingText>()
                .WithMany()
                .HasForeignKey(item => item.ReadingTextId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExerciseSession>(entity =>
        {
            entity.ToTable("exercise_sessions");
            entity.Property(item => item.Status).HasConversion<int>().IsRequired();
            entity.Property(item => item.SessionDataJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.CustomDataJson).HasColumnType("jsonb");
            entity.Property(item => item.ProcessedActionsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => new { item.StudentId, item.Status });
            entity.HasIndex(item => new { item.StudentId, item.ExerciseId, item.Status });
            entity.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(item => item.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReadingText>()
                .WithMany()
                .HasForeignKey(item => item.ReadingTextId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.Answers)
                .WithOne()
                .HasForeignKey(item => item.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExerciseSessionAnswer>(entity =>
        {
            entity.ToTable("exercise_session_answers");
            entity.Property(item => item.Answer).HasMaxLength(2_000).IsRequired();
            entity.HasIndex(item => new { item.SessionId, item.QuestionId }).IsUnique();
        });

        modelBuilder.Entity<ExerciseSessionResult>(entity =>
        {
            entity.ToTable("exercise_session_results");
            entity.Property(item => item.RawWpm).HasPrecision(10, 2);
            entity.Property(item => item.ComprehensionScore).HasPrecision(5, 2);
            entity.Property(item => item.WeightedKdp).HasPrecision(10, 2);
            entity.Property(item => item.Score).HasPrecision(5, 2);
            entity.Property(item => item.QuestionAnswersJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.ReadingMovementsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => item.SessionId).IsUnique();
            entity.HasIndex(item => item.LegacySessionId);
            entity.HasIndex(item => new { item.StudentId, item.CompletedAt });
            entity.HasOne<ExerciseSession>()
                .WithOne()
                .HasForeignKey<ExerciseSessionResult>(item => item.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ReadingText>()
                .WithMany()
                .HasForeignKey(item => item.ReadingTextId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReadingSession>(entity =>
        {
            entity.ToTable("reading_sessions");
            entity.Property(item => item.ComprehensionRate).HasPrecision(5, 2);
            entity.Property(item => item.EfficiencyScore).HasPrecision(5, 2);
            entity.HasIndex(item => new { item.UserId, item.CompletedAt });
            entity.HasIndex(item => item.ReadingTextId);
            entity.HasOne<ReadingText>()
                .WithMany()
                .HasForeignKey(item => item.ReadingTextId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.ToTable("assignments");
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2_000).IsRequired();
            entity.HasIndex(item => new { item.TeacherId, item.CreatedAt });
            entity.HasIndex(item => new { item.ExerciseId, item.IsActive });
            entity.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(item => item.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReadingText>()
                .WithMany()
                .HasForeignKey(item => item.ReadingTextId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentAssignment>(entity =>
        {
            entity.ToTable("student_assignments");
            entity.Property(item => item.Score).HasPrecision(5, 2);
            entity.Property(item => item.KeyPerformanceMetric).HasPrecision(10, 2);
            entity.HasIndex(item => new { item.AssignmentId, item.StudentId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE");
            entity.HasIndex(item => new { item.StudentId, item.IsActive, item.CreatedAt });
            entity.HasIndex(item => item.ResultId);
            entity.HasOne<Assignment>()
                .WithMany()
                .HasForeignKey(item => item.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExerciseSession>()
            .HasIndex(item => item.StudentAssignmentId);

        modelBuilder.Entity<AgeGroupConfiguration>(entity =>
        {
            entity.ToTable("age_group_configurations");
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2_000);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasIndex(item => new { item.IsActive, item.MinAge, item.MaxAge });
        });

        modelBuilder.Entity<ProgramTemplate>(entity =>
        {
            entity.ToTable("program_templates");
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(5_000).IsRequired();
            entity.Property(item => item.WeeklyPatternJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.ExamType).HasMaxLength(100);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.IsActive, item.DisplayOrder });
        });

        modelBuilder.Entity<StudentProgramProgress>(entity =>
        {
            entity.ToTable("student_program_progress");
            entity.Property(item => item.AverageSuccessRate).HasPrecision(5, 2);
            entity.HasIndex(item => new { item.UserId, item.IsActive, item.AssignedDate });
            entity.HasIndex(item => item.ProgramTemplateId);
            entity.HasOne<ProgramTemplate>()
                .WithMany()
                .HasForeignKey(item => item.ProgramTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DailyExerciseLog>(entity =>
        {
            entity.ToTable("daily_exercise_logs");
            entity.Property(item => item.ResultDataJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.SuccessRate).HasPrecision(5, 2);
            entity.Property(item => item.AverageWPM).HasPrecision(10, 2);
            entity.Property(item => item.AverageComprehension).HasPrecision(5, 2);
            entity.Property(item => item.AverageResponseTimeMs).HasPrecision(10, 2);
            entity.Property(item => item.MedianResponseTimeMs).HasPrecision(10, 2);
            entity.Property(item => item.StdDevResponseTimeMs).HasPrecision(10, 2);
            entity.Property(item => item.PerformanceTrend).HasPrecision(10, 2);
            entity.Property(item => item.PreviousAverageScore).HasPrecision(5, 2);
            entity.Property(item => item.EngagementScore).HasPrecision(10, 2);
            entity.Property(item => item.FrustrationScore).HasPrecision(10, 2);
            entity.Property(item => item.LearningRate).HasPrecision(10, 2);
            entity.Property(item => item.ConsistencyScore).HasPrecision(10, 2);
            entity.Property(item => item.DevicePlatform).HasMaxLength(50).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.CompletedDate });
            entity.HasIndex(item => new { item.StudentProgramProgressId, item.WeekNumber, item.DayNumber });
            entity.HasIndex(item => item.ExerciseId);
            entity.HasOne<StudentProgramProgress>()
                .WithMany()
                .HasForeignKey(item => item.StudentProgramProgressId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(item => item.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ExerciseType>()
                .WithMany()
                .HasForeignKey(item => item.ExerciseTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SpeedReadingUserProfile>(entity =>
        {
            entity.ToTable("user_profiles");
            entity.Property(item => item.TargetComprehension).HasPrecision(5, 2);
            entity.HasIndex(item => item.UserId).IsUnique();
            entity.HasIndex(item => item.AgeGroupConfigurationId);
            entity.HasOne<AgeGroupConfiguration>()
                .WithMany()
                .HasForeignKey(item => item.AgeGroupConfigurationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LearningPathTemplate>(entity =>
        {
            entity.ToTable("learning_path_templates");
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(5_000);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => item.Name);
            entity.HasIndex(item => new { item.IsActive, item.IsDeleted });
            entity.HasOne<AgeGroupConfiguration>()
                .WithMany()
                .HasForeignKey(item => item.TargetAgeGroupConfigurationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LearningPathNode>(entity =>
        {
            entity.ToTable("learning_path_nodes");
            entity.Property(item => item.NodeType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(300).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(100);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.TemplateId, item.IsDeleted, item.Order });
            entity.HasIndex(item => item.ParentNodeId);
            entity.HasOne<LearningPathTemplate>()
                .WithMany()
                .HasForeignKey(item => item.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LearningPathNode>()
                .WithMany()
                .HasForeignKey(item => item.ParentNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LearningPathNodeContent>(entity =>
        {
            entity.ToTable("learning_path_node_contents");
            entity.Property(item => item.Description).HasMaxLength(2_000);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.NodeId, item.IsDeleted });
            entity.HasOne<LearningPathNode>()
                .WithMany()
                .HasForeignKey(item => item.NodeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(item => item.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReadingText>()
                .WithMany()
                .HasForeignKey(item => item.ReadingTextId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LearningPathPrerequisite>(entity =>
        {
            entity.ToTable("learning_path_prerequisites");
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.NodeId, item.PrerequisiteNodeId, item.IsDeleted }).IsUnique();
            entity.HasOne<LearningPathNode>()
                .WithMany()
                .HasForeignKey(item => item.NodeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LearningPathNode>()
                .WithMany()
                .HasForeignKey(item => item.PrerequisiteNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentLearningPathProgress>(entity =>
        {
            entity.ToTable("student_learning_path_progress");
            entity.Property(item => item.Progress).HasPrecision(5, 1);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.StudentId, item.TemplateId, item.IsDeleted });
            entity.HasIndex(item => new { item.StudentId, item.CreatedAt });
            entity.HasOne<LearningPathTemplate>()
                .WithMany()
                .HasForeignKey(item => item.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentLearningNodeProgress>(entity =>
        {
            entity.ToTable("student_learning_node_progress");
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Score).HasPrecision(5, 2);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.StudentId, item.NodeId, item.IsDeleted }).IsUnique();
            entity.HasIndex(item => new { item.StudentId, item.Status });
            entity.HasOne<LearningPathNode>()
                .WithMany()
                .HasForeignKey(item => item.NodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PersonalizedLearningPathItem>(entity =>
        {
            entity.ToTable("personalized_learning_path_items");
            entity.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ContentTitle).HasMaxLength(500).IsRequired();
            entity.Property(item => item.AchievedScore).HasPrecision(5, 2);
            entity.Property(item => item.RecommendationReason).HasMaxLength(2_000);
            entity.Property(item => item.DeletedBy).HasMaxLength(100);
            entity.HasIndex(item => new { item.StudentId, item.PathIndex, item.IsDeleted }).IsUnique();
            entity.HasIndex(item => new { item.StudentId, item.IsCompleted, item.IsUnlocked });
            entity.HasOne<LearningPathTemplate>()
                .WithMany()
                .HasForeignKey(item => item.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureEntity<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : Entity<Guid>
    {
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        entity.Property(item => item.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        entity.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        if (typeof(AggregateRoot).IsAssignableFrom(typeof(TEntity)))
        {
            entity.Property("Version").HasColumnName("version").IsRequired();
        }
    }

    private static void ConfigureLegacyEntity<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : LegacyBaseEntity
    {
        entity.ToTable(tableName);
        entity.HasKey(item => item.Id);
        entity.Property(item => item.CreatedAt).IsRequired();
        entity.Property(item => item.CreatedBy).IsRequired();
        entity.Property(item => item.UpdatedAt);
        entity.Property(item => item.UpdatedBy);
        entity.Property(item => item.IsDeleted).IsRequired();
        entity.Property(item => item.DeletedAt);
        entity.Property(item => item.DeletedBy);
    }

    private static void ConfigureNotificationEntity<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : LegacyNotificationBase
    {
        entity.ToTable(tableName);
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        entity.Property(item => item.IsDeleted).HasColumnName("is_deleted").IsRequired();
    }
}
