using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Infrastructure.Middleware;
using SpeedReading.Infrastructure.Legacy;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure;

/// <summary>
/// Access boundary for the existing Hızlı Okuma database.
/// The existing schema remains the source of truth. The service only applies
/// idempotent additive compatibility scripts before replicas start.
/// </summary>
public sealed class SpeedReadingDbContext(DbContextOptions<SpeedReadingDbContext> options) : DbContext(options), ISpeedReadingDataContext
{
    internal DbSet<LegacyExerciseTypeCategory> ExerciseTypeCategories => Set<LegacyExerciseTypeCategory>();
    internal DbSet<LegacyContentBlock> ContentBlocks => Set<LegacyContentBlock>();
    internal DbSet<LegacyPage> Pages => Set<LegacyPage>();
    internal DbSet<LegacyBlogPost> BlogPosts => Set<LegacyBlogPost>();
    internal DbSet<LegacyContactMessage> ContactMessages => Set<LegacyContactMessage>();
    internal DbSet<LegacyNewsletterSubscriber> NewsletterSubscribers => Set<LegacyNewsletterSubscriber>();
    internal DbSet<LegacyCmsMediaAsset> CmsMediaAssets => Set<LegacyCmsMediaAsset>();
    internal DbSet<LegacyProduct> Products => Set<LegacyProduct>();
    internal DbSet<LegacySubscriptionPlan> SubscriptionPlans => Set<LegacySubscriptionPlan>();
    internal DbSet<LegacyUserSubscription> UserSubscriptions => Set<LegacyUserSubscription>();
    internal DbSet<LegacyPayment> Payments => Set<LegacyPayment>();
    internal DbSet<LegacyExerciseType> ExerciseTypes => Set<LegacyExerciseType>();
    internal DbSet<LegacyExercise> Exercises => Set<LegacyExercise>();
    internal DbSet<LegacyReadingText> ReadingTexts => Set<LegacyReadingText>();
    internal DbSet<LegacyReadingQuestion> ReadingQuestions => Set<LegacyReadingQuestion>();
    internal DbSet<LegacyExerciseSession> ExerciseSessions => Set<LegacyExerciseSession>();
    internal DbSet<LegacyStudentExerciseResult> StudentExerciseResults => Set<LegacyStudentExerciseResult>();
    internal DbSet<LegacyAssignment> Assignments => Set<LegacyAssignment>();
    internal DbSet<LegacyStudentAssignment> StudentAssignments => Set<LegacyStudentAssignment>();
    internal DbSet<LegacyReadingSession> ReadingSessions => Set<LegacyReadingSession>();
    internal DbSet<LegacyExerciseProgramTemplate> ExerciseProgramTemplates => Set<LegacyExerciseProgramTemplate>();
    internal DbSet<LegacyStudentProgramProgress> StudentProgramProgresses => Set<LegacyStudentProgramProgress>();
    internal DbSet<LegacyDailyExerciseLog> DailyExerciseLogs => Set<LegacyDailyExerciseLog>();
    internal DbSet<LegacyLearningPathTemplate> LearningPathTemplates => Set<LegacyLearningPathTemplate>();
    internal DbSet<LegacyLearningPathNode> LearningPathNodes => Set<LegacyLearningPathNode>();
    internal DbSet<LegacyNodeContent> NodeContents => Set<LegacyNodeContent>();
    internal DbSet<LegacyNodePrerequisite> NodePrerequisites => Set<LegacyNodePrerequisite>();
    internal DbSet<LegacyStudentPathProgress> StudentPathProgresses => Set<LegacyStudentPathProgress>();
    internal DbSet<LegacyStudentNodeProgress> StudentNodeProgresses => Set<LegacyStudentNodeProgress>();
    internal DbSet<LegacyPersonalizedLearningPath> PersonalizedLearningPaths => Set<LegacyPersonalizedLearningPath>();
    internal DbSet<LegacyIdempotencyRecord> IdempotencyRecords => Set<LegacyIdempotencyRecord>();
    public DbSet<AdminAuditRecord> AdminAuditRecords => Set<AdminAuditRecord>();
    internal DbSet<LegacyAchievement> Achievements => Set<LegacyAchievement>();
    internal DbSet<LegacyUserAchievement> UserAchievements => Set<LegacyUserAchievement>();
    internal DbSet<LegacyUserGamification> UserGamifications => Set<LegacyUserGamification>();
    internal DbSet<LegacyUser> Users => Set<LegacyUser>();
    internal DbSet<LegacyReportTemplate> ReportTemplates => Set<LegacyReportTemplate>();
    internal DbSet<LegacyReportSnapshot> ReportSnapshots => Set<LegacyReportSnapshot>();
    internal DbSet<LegacyScheduledReport> ScheduledReports => Set<LegacyScheduledReport>();
    internal DbSet<LegacyStudentLearningProfile> StudentLearningProfiles => Set<LegacyStudentLearningProfile>();
    internal DbSet<LegacyContentRecommendation> ContentRecommendations => Set<LegacyContentRecommendation>();
    internal DbSet<LegacyDailyGoal> DailyGoals => Set<LegacyDailyGoal>();
    internal DbSet<LegacyStudentReadingProfile> StudentReadingProfiles => Set<LegacyStudentReadingProfile>();
    internal DbSet<LegacyTextRecommendationHistory> TextRecommendationHistories => Set<LegacyTextRecommendationHistory>();
    internal DbSet<LegacyUserContentFeedback> UserContentFeedbacks => Set<LegacyUserContentFeedback>();
    internal DbSet<LegacyVisualizationScene> VisualizationScenes => Set<LegacyVisualizationScene>();
    internal DbSet<LegacyVisualizationQuestion> VisualizationQuestions => Set<LegacyVisualizationQuestion>();
    internal DbSet<LegacyVocabularyItem> VocabularyItems => Set<LegacyVocabularyItem>();
    internal DbSet<LegacyUserVocabularyProgress> UserVocabularyProgresses => Set<LegacyUserVocabularyProgress>();
    internal DbSet<LegacyAgeGroupConfiguration> AgeGroupConfigurations => Set<LegacyAgeGroupConfiguration>();
    internal DbSet<LegacyExamQuestion> ExamQuestions => Set<LegacyExamQuestion>();
    internal DbSet<LegacyRsvpSession> RsvpSessions => Set<LegacyRsvpSession>();
    internal DbSet<LegacyExerciseReviewItem> ExerciseReviewItems => Set<LegacyExerciseReviewItem>();
    internal DbSet<LegacyUserNotification> Notifications => Set<LegacyUserNotification>();
    internal DbSet<LegacyNotificationPreference> NotificationPreferences => Set<LegacyNotificationPreference>();
    internal DbSet<LegacyNotificationTypePreference> NotificationTypePreferences => Set<LegacyNotificationTypePreference>();
    internal DbSet<LegacyPushSubscription> PushSubscriptions => Set<LegacyPushSubscription>();
    internal DbSet<LegacyAnnouncement> Announcements => Set<LegacyAnnouncement>();
    internal DbSet<LegacyAnnouncementUserInteraction> AnnouncementUserInteractions => Set<LegacyAnnouncementUserInteraction>();
    internal DbSet<LegacyEmailTemplate> EmailTemplates => Set<LegacyEmailTemplate>();
    internal DbSet<LegacyEmailCampaign> EmailCampaigns => Set<LegacyEmailCampaign>();
    internal DbSet<LegacyEmailCampaignLog> EmailCampaignLogs => Set<LegacyEmailCampaignLog>();
    internal DbSet<LegacyUserRoleLink> UserRoleLinks => Set<LegacyUserRoleLink>();
    internal DbSet<LegacyRoleLookup> Roles => Set<LegacyRoleLookup>();
    DbSet<LegacyProduct> ISpeedReadingDataContext.Products => Products;
    DbSet<LegacyContentBlock> ISpeedReadingDataContext.ContentBlocks => ContentBlocks;
    DbSet<LegacyPage> ISpeedReadingDataContext.Pages => Pages;
    DbSet<LegacyBlogPost> ISpeedReadingDataContext.BlogPosts => BlogPosts;
    DbSet<LegacyContactMessage> ISpeedReadingDataContext.ContactMessages => ContactMessages;
    DbSet<LegacyNewsletterSubscriber> ISpeedReadingDataContext.NewsletterSubscribers => NewsletterSubscribers;
    DbSet<LegacyCmsMediaAsset> ISpeedReadingDataContext.CmsMediaAssets => CmsMediaAssets;
    DbSet<LegacySubscriptionPlan> ISpeedReadingDataContext.SubscriptionPlans => SubscriptionPlans;
    DbSet<LegacyUserSubscription> ISpeedReadingDataContext.UserSubscriptions => UserSubscriptions;
    DbSet<LegacyPayment> ISpeedReadingDataContext.Payments => Payments;
    DbSet<LegacyUserNotification> ISpeedReadingDataContext.Notifications => Notifications;
    DbSet<LegacyNotificationPreference> ISpeedReadingDataContext.NotificationPreferences => NotificationPreferences;
    DbSet<LegacyNotificationTypePreference> ISpeedReadingDataContext.NotificationTypePreferences => NotificationTypePreferences;
    DbSet<LegacyPushSubscription> ISpeedReadingDataContext.PushSubscriptions => PushSubscriptions;
    DbSet<LegacyAnnouncement> ISpeedReadingDataContext.Announcements => Announcements;
    DbSet<LegacyAnnouncementUserInteraction> ISpeedReadingDataContext.AnnouncementUserInteractions => AnnouncementUserInteractions;
    DbSet<LegacyEmailTemplate> ISpeedReadingDataContext.EmailTemplates => EmailTemplates;
    DbSet<LegacyEmailCampaign> ISpeedReadingDataContext.EmailCampaigns => EmailCampaigns;
    DbSet<LegacyEmailCampaignLog> ISpeedReadingDataContext.EmailCampaignLogs => EmailCampaignLogs;
    DbSet<LegacyRsvpSession> ISpeedReadingDataContext.RsvpSessions => RsvpSessions;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // This context is a compatibility reader for the existing database. It
        // deliberately has no EF migrations and never calls EnsureCreated.
        modelBuilder.Entity<LegacyContentBlock>(entity =>
        {
            entity.ToTable("ContentBlocks");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Key).HasColumnName("Key").HasMaxLength(200).IsRequired();
            entity.Property(item => item.Group).HasColumnName("Group").HasMaxLength(100).IsRequired();
            entity.Property(item => item.Label).HasMaxLength(200);
            entity.Property(item => item.Value).HasColumnName("Value").IsRequired();
            entity.HasIndex(item => item.Group).HasDatabaseName("IX_ContentBlocks_Group");
            entity.HasIndex(item => item.Key).HasDatabaseName("IX_ContentBlocks_Key");
        });

        modelBuilder.Entity<LegacyPage>(entity =>
        {
            entity.ToTable("Pages");
            entity.HasKey(item => item.Id);
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
            entity.HasIndex(item => item.Slug).HasDatabaseName("IX_Pages_Slug");
        });

        modelBuilder.Entity<LegacyBlogPost>(entity =>
        {
            entity.ToTable("BlogPosts");
            entity.HasKey(item => item.Id);
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
            entity.HasIndex(item => item.PublishedAt).HasDatabaseName("IX_BlogPosts_PublishedAt");
            entity.HasIndex(item => item.Slug).HasDatabaseName("IX_BlogPosts_Slug");
        });

        modelBuilder.Entity<LegacyContactMessage>(entity =>
        {
            entity.ToTable("ContactMessages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Message).IsRequired();
            entity.Property(item => item.ReplyContent);
            entity.HasIndex(item => item.CreatedAt).HasDatabaseName("IX_ContactMessages_CreatedAt");
            entity.HasIndex(item => item.IsRead).HasDatabaseName("IX_ContactMessages_IsRead");
        });

        modelBuilder.Entity<LegacyNewsletterSubscriber>(entity =>
        {
            entity.ToTable("NewsletterSubscribers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Email).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(50);
            entity.HasIndex(item => item.Email).HasDatabaseName("IX_NewsletterSubscribers_Email");
        });

        modelBuilder.Entity<LegacyCmsMediaAsset>(entity =>
        {
            entity.ToTable("CmsMediaAssets");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FileName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.StorageKey).HasMaxLength(500).IsRequired();
            entity.Property(item => item.AltText).HasMaxLength(500);
            entity.HasIndex(item => item.CreatedAt).HasDatabaseName("IX_CmsMediaAssets_CreatedAt");
        });

        modelBuilder.Entity<LegacyProduct>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Slug).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.IncludedProductSlugsJson).HasColumnName("IncludedProductSlugs").IsRequired();
            entity.HasIndex(item => item.Slug).IsUnique();
        });

        modelBuilder.Entity<LegacySubscriptionPlan>(entity =>
        {
            entity.ToTable("SubscriptionPlans");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Price).HasPrecision(10, 2);
            entity.Property(item => item.BillingPeriod).IsRequired();
            entity.HasIndex(item => item.ProductId);
            entity.HasIndex(item => item.Slug).IsUnique();
        });

        modelBuilder.Entity<LegacyUserSubscription>(entity =>
        {
            entity.ToTable("UserSubscriptions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Status).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.Status });
            entity.HasIndex(item => new { item.UserId, item.PlanId });
            entity.HasIndex(item => new { item.UserId, item.ProductId });
        });

        modelBuilder.Entity<LegacyPayment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserEmail).HasMaxLength(255).IsRequired();
            entity.Property(item => item.UserName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Amount).HasPrecision(10, 2);
            entity.Property(item => item.Provider).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Status).IsRequired();
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.ProviderToken);
            entity.HasIndex(item => item.ProviderToken)
                .IsUnique()
                .HasDatabaseName("IX_Payments_ProviderToken_Unique")
                .HasFilter("\"ProviderToken\" IS NOT NULL");
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.PlanId);
        });

        modelBuilder.Entity<LegacyExerciseTypeCategory>(entity =>
        {
            entity.ToTable("ExerciseTypeCategories");
            entity.HasKey(item => item.Id);
        });

        modelBuilder.Entity<LegacyExerciseType>(entity =>
        {
            entity.ToTable("ExerciseTypes");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.CategoryId);
        });

        modelBuilder.Entity<LegacyExercise>(entity =>
        {
            entity.ToTable("Exercises");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ExerciseTypeId);
            entity.HasIndex(item => item.TargetAgeGroupConfigurationId);
            entity.Property(item => item.TargetAgeGroupConfigurationId)
                .HasColumnName("TargetAgeGroupId");
            // The legacy table has no CreatorId column; CreatedBy is the
            // available author identifier and is used during owned backfill.
            entity.Ignore(item => item.CreatorId);
        });

        modelBuilder.Entity<LegacyVisualizationScene>(entity =>
        {
            entity.ToTable("VisualizationScenes");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ExerciseId);
            entity.HasIndex(item => item.TargetAgeGroupConfigurationId);
        });

        modelBuilder.Entity<LegacyVisualizationQuestion>(entity =>
        {
            entity.ToTable("VisualizationQuestions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.SceneId);
        });

        modelBuilder.Entity<LegacyVocabularyItem>(entity =>
        {
            entity.ToTable("VocabularyItems");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TargetAgeGroupConfigurationId);
        });

        modelBuilder.Entity<LegacyUserVocabularyProgress>(entity =>
        {
            entity.ToTable("UserVocabularyProgresses");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.VocabularyItemId);
        });

        modelBuilder.Entity<LegacyAgeGroupConfiguration>(entity =>
        {
            entity.ToTable("AgeGroupConfigurations");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name);
        });

        modelBuilder.Entity<LegacyExamQuestion>(entity =>
        {
            entity.ToTable("ExamQuestions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TargetAgeGroupConfigurationId);
            entity.HasIndex(item => item.ExamType);
            entity.HasIndex(item => item.Category);
        });

        modelBuilder.Entity<LegacyRsvpSession>(entity =>
        {
            entity.ToTable("RSVPSessions");
            entity.HasKey(item => item.Id);
            entity.Ignore(item => item.TextId);
            entity.Property(item => item.TextContent).HasColumnName("Text");
            entity.Ignore(item => item.WordsPerMinute);
            entity.Property(item => item.SourceAverageWpm).HasColumnName("AverageWPM");
            entity.Ignore(item => item.FontFamily);
            entity.Ignore(item => item.FontSize);
            entity.Ignore(item => item.BackgroundColor);
            entity.Ignore(item => item.TextColor);
            entity.Ignore(item => item.CompletedWords);
            entity.Property(item => item.CompletionPercentage).HasColumnName("CompletionPercentage");
            entity.Property(item => item.SessionDuration).HasColumnName("DurationSeconds");
            entity.Ignore(item => item.Completed);
            entity.Property(item => item.CompletedAt).HasColumnName("EndTime");
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => new { item.UserId, item.CreatedAt });
        });

        modelBuilder.Entity<LegacyExerciseReviewItem>(entity =>
        {
            entity.ToTable("ExerciseReviewItems");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.ExerciseId);
            entity.HasIndex(item => item.ProgramTemplateId);
            entity.Property(item => item.EasinessFactor).HasColumnType("double precision");
            entity.Property(item => item.LastScore).HasColumnType("double precision");
        });

        modelBuilder.Entity<LegacyUserNotification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(item => item.Id);
            entity.Ignore(item => item.Channel);
            entity.Ignore(item => item.Status);
            entity.Property(item => item.IsRead).HasColumnName("IsRead");
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.Data).HasColumnName("MetadataJson");
            entity.Ignore(item => item.IconUrl);
            entity.Ignore(item => item.SentAt);
            entity.Ignore(item => item.UserName);
            entity.Ignore(item => item.UserEmail);
            entity.Ignore(item => item.UserRole);
            entity.Ignore(item => item.ErrorMessage);
            entity.Property(item => item.EmailSent).HasColumnName("EmailSent");
            entity.Property(item => item.EmailSentAt).HasColumnName("EmailSentAt");
            entity.Property(item => item.PushSent).HasColumnName("PushSent");
            entity.Property(item => item.PushSentAt).HasColumnName("PushSentAt");
            entity.Property(item => item.ExpiresAt).HasColumnName("ExpiresAt");
            entity.Property(item => item.RelatedEntityId).HasColumnName("RelatedEntityId");
            entity.Property(item => item.RelatedEntityType).HasColumnName("RelatedEntityType");
            entity.HasIndex(item => item.UserId);
        });

        modelBuilder.Entity<LegacyNotificationPreference>(entity =>
        {
            entity.ToTable("NotificationPreferences");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.NotificationType).HasColumnName("NotificationType");
            entity.Property(item => item.EmailEnabled).HasColumnName("EnableEmail");
            entity.Property(item => item.PushEnabled).HasColumnName("EnablePush");
            entity.Property(item => item.InAppEnabled).HasColumnName("EnableInApp");
            entity.Property(item => item.EnableInstant).HasColumnName("EnableInstant");
            entity.Property(item => item.EnableDaily).HasColumnName("EnableDaily");
            entity.Property(item => item.EnableWeekly).HasColumnName("EnableWeekly");
            entity.Property(item => item.PreferredTime).HasColumnName("PreferredTime").HasMaxLength(10);
            entity.Ignore(item => item.SmsEnabled);
            entity.Ignore(item => item.AchievementsEnabled);
            entity.Ignore(item => item.LevelUpEnabled);
            entity.Ignore(item => item.DailyReminderEnabled);
            entity.Ignore(item => item.StreakMilestoneEnabled);
            entity.Ignore(item => item.Email);
            entity.Ignore(item => item.PhoneNumber);
            entity.HasIndex(item => new { item.UserId, item.NotificationType });
        });

        modelBuilder.Entity<LegacyNotificationTypePreference>(entity =>
        {
            entity.ToTable("NotificationTypePreferences");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PreferredTime).HasMaxLength(10);
            entity.HasIndex(item => new { item.UserId, item.NotificationType }).IsUnique();
        });

        modelBuilder.Entity<LegacyPushSubscription>(entity =>
        {
            entity.ToTable("PushSubscriptions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Endpoint).HasMaxLength(500).IsRequired();
            entity.Property(item => item.P256DH).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Auth).HasMaxLength(200).IsRequired();
            entity.Ignore(item => item.IsActive);
            entity.HasIndex(item => item.Endpoint);
            entity.HasIndex(item => item.UserId);
        });

        modelBuilder.Entity<LegacyAnnouncement>(entity =>
        {
            entity.ToTable("Announcements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Content).IsRequired();
            entity.Ignore(item => item.Type);
            entity.Property(item => item.TargetAudience).HasMaxLength(50).IsRequired();
            entity.Property(item => item.TargetRoles).HasMaxLength(200);
            entity.Property(item => item.StartDate).HasColumnName("StartDate");
            entity.Ignore(item => item.EndDate);
            entity.Ignore(item => item.ImageUrl);
            entity.Ignore(item => item.CreatedByUserId);
            entity.Property(item => item.ColorTheme).HasMaxLength(50);
            entity.Property(item => item.ActionText).HasMaxLength(100);
            entity.HasIndex(item => item.IsActive);
        });

        modelBuilder.Entity<LegacyAnnouncementUserInteraction>(entity =>
        {
            entity.ToTable("AnnouncementUserInteractions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ViewedAt).HasColumnName("LastViewedAt");
            entity.HasIndex(item => new { item.AnnouncementId, item.UserId }).IsUnique();
        });

        modelBuilder.Entity<LegacyEmailTemplate>(entity =>
        {
            entity.ToTable("EmailTemplates");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Ignore(item => item.Variables);
            entity.Ignore(item => item.IsSystem);
        });

        modelBuilder.Entity<LegacyEmailCampaign>(entity =>
        {
            entity.ToTable("EmailCampaigns");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.TargetRoles).HasMaxLength(200);
            entity.Ignore(item => item.TemplateId);
            entity.Ignore(item => item.CreatedByUserId);
        });

        modelBuilder.Entity<LegacyEmailCampaignLog>(entity =>
        {
            entity.ToTable("EmailCampaignLogs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RecipientEmail).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(item => item.CampaignId);
        });

        modelBuilder.Entity<LegacyUserRoleLink>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(item => new { item.UserId, item.RoleId });
        });

        modelBuilder.Entity<LegacyRoleLookup>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(256);
        });

        modelBuilder.Entity<LegacyReadingText>(entity =>
        {
            entity.ToTable("ReadingTexts");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ExerciseId);
            entity.HasIndex(item => item.TargetAgeGroupConfigurationId);
            entity.Property(item => item.TargetAgeGroupConfigurationId)
                .HasColumnName("TargetAgeGroupId");
        });

        modelBuilder.Entity<LegacyReadingQuestion>(entity =>
        {
            entity.ToTable("ReadingQuestions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ReadingTextId);
        });

        modelBuilder.Entity<LegacyExerciseSession>(entity =>
        {
            entity.ToTable("ExerciseSessions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.StudentId);
            entity.HasIndex(item => item.ExerciseId);
            entity.HasIndex(item => item.ReadingTextId);
            entity.HasIndex(item => item.StudentAssignmentId);
            // These fields were introduced after the legacy database schema;
            // owned sessions persist them in the new schema.
            entity.Ignore(item => item.PausedAt);
            entity.Ignore(item => item.TimeLimitSeconds);
            // Added only in the owned schema; legacy rows predate this field.
            entity.Ignore(item => item.ProcessedActionsJson);
        });

        modelBuilder.Entity<LegacyStudentExerciseResult>(entity =>
        {
            entity.ToTable("StudentExerciseResults");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.StudentId);
            entity.HasIndex(item => item.ExerciseId);
            entity.HasIndex(item => item.ReadingTextId);
            // StudentExerciseResults in the legacy database does not retain
            // a session link; owned backfill keeps those results unlinked.
            entity.Ignore(item => item.SessionId);
            entity.Property(item => item.RawWPM).HasPrecision(18, 2);
            entity.Property(item => item.ComprehensionScore).HasPrecision(18, 2);
        });

        modelBuilder.Entity<LegacyAssignment>(entity =>
        {
            entity.ToTable("Assignments");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TeacherId);
            entity.HasIndex(item => item.ExerciseId);
            entity.HasIndex(item => item.ReadingTextId);
            entity.HasIndex(item => new { item.IsDeleted, item.TeacherId, item.CreatedAt });
        });

        modelBuilder.Entity<LegacyStudentAssignment>(entity =>
        {
            entity.ToTable("StudentAssignments");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.AssignmentId);
            entity.HasIndex(item => item.StudentId);
            entity.HasIndex(item => item.ResultId);
            entity.HasIndex(item => new { item.IsDeleted, item.StudentId, item.CreatedAt });
            entity.Property(item => item.Score).HasPrecision(18, 2);
            entity.Property(item => item.KeyPerformanceMetric).HasPrecision(18, 2);
        });

        modelBuilder.Entity<LegacyReadingSession>(entity =>
        {
            entity.ToTable("ReadingSessions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.ReadingTextId);
            entity.HasIndex(item => new { item.IsDeleted, item.CompletedAt, item.UserId });
            entity.HasIndex(item => new { item.IsDeleted, item.UserId, item.CompletedAt });
        });

        modelBuilder.Entity<LegacyExerciseProgramTemplate>(entity =>
        {
            entity.ToTable("ExerciseProgramTemplates");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TargetAgeGroupConfigurationId);
            entity.Property(item => item.TargetAgeGroupConfigurationId)
                .HasColumnName("TargetAgeGroupId");
        });

        modelBuilder.Entity<LegacyStudentProgramProgress>(entity =>
        {
            entity.ToTable("StudentProgramProgresses");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.ProgramTemplateId);
            entity.HasIndex(item => new { item.IsDeleted, item.UserId, item.AssignedDate });
        });

        modelBuilder.Entity<LegacyDailyExerciseLog>(entity =>
        {
            entity.ToTable("DailyExerciseLogs");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.StudentProgramProgressId);
            entity.HasIndex(item => item.ExerciseId);
            entity.HasIndex(item => item.ExerciseTypeId);
            entity.HasIndex(item => new { item.IsDeleted, item.UserId, item.CompletedDate });
            entity.Property(item => item.ResultDataJson).IsRequired();
            entity.Property(item => item.DevicePlatform).HasMaxLength(50).IsRequired();
            // These derived fields exist in the newer model but not in the
            // live legacy table; the owned log schema stores them separately.
            entity.Ignore(item => item.AverageWPM);
            entity.Ignore(item => item.AverageComprehension);
            entity.Property(item => item.AverageResponseTimeMs).HasPrecision(18, 2);
            entity.Property(item => item.MedianResponseTimeMs).HasPrecision(18, 2);
            entity.Property(item => item.StdDevResponseTimeMs).HasPrecision(18, 2);
            entity.Property(item => item.PerformanceTrend).HasPrecision(18, 2);
            entity.Property(item => item.PreviousAverageScore).HasPrecision(18, 2);
            entity.Property(item => item.EngagementScore).HasPrecision(18, 2);
            entity.Property(item => item.FrustrationScore).HasPrecision(18, 2);
            entity.Property(item => item.LearningRate).HasPrecision(18, 2);
            entity.Property(item => item.ConsistencyScore).HasPrecision(18, 2);
        });

        modelBuilder.Entity<LegacyLearningPathTemplate>(entity =>
        {
            entity.ToTable("LearningPathTemplates");
            entity.HasKey(item => item.Id);
        });

        modelBuilder.Entity<LegacyLearningPathNode>(entity =>
        {
            entity.ToTable("LearningPathNodes");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TemplateId);
            entity.Property(item => item.Order).HasColumnName("OrderIndex");
            entity.Ignore(item => item.ParentNodeId);
            entity.Ignore(item => item.ContentType);
            entity.Ignore(item => item.ContentId);
        });

        modelBuilder.Entity<LegacyNodeContent>(entity =>
        {
            entity.ToTable("NodeContents");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.NodeId);
            entity.Ignore(item => item.ExerciseId);
            entity.Ignore(item => item.ReadingTextId);
            entity.Property(item => item.SourceContentId).HasColumnName("ContentId");
            entity.Property(item => item.SourceContentType).HasColumnName("ContentType");
            entity.Property(item => item.Description).HasColumnName("ContentDescription");
        });

        modelBuilder.Entity<LegacyNodePrerequisite>(entity =>
        {
            entity.ToTable("NodePrerequisites");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.NodeId);
            entity.HasIndex(item => item.PrerequisiteNodeId);
            entity.Property(item => item.NodeId).HasColumnName("DependentNodeId");
        });

        modelBuilder.Entity<LegacyStudentPathProgress>(entity =>
        {
            entity.ToTable("StudentPathProgresses");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.StudentId);
            entity.HasIndex(item => item.TemplateId);
            entity.Ignore(item => item.IsCompleted);
            entity.Property(item => item.Progress).HasColumnName("CompletionPercentage");
        });

        modelBuilder.Entity<LegacyStudentNodeProgress>(entity =>
        {
            entity.ToTable("StudentNodeProgresses");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.StudentId);
            entity.HasIndex(item => item.NodeId);
        });

        modelBuilder.Entity<LegacyPersonalizedLearningPath>(entity =>
        {
            entity.ToTable("PersonalizedLearningPaths");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.StudentId);
            entity.Ignore(item => item.TemplateId);
            entity.Ignore(item => item.IsUnlocked);
        });

        modelBuilder.Entity<LegacyIdempotencyRecord>(entity =>
        {
            entity.ToTable("SpeedReadingIdempotencyRecords");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Scope).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Key).HasMaxLength(128).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => new { item.Scope, item.Key })
                .HasDatabaseName("UX_SpeedReadingIdempotencyRecords_Scope_Key")
                .IsUnique();
            entity.HasIndex(item => item.CreatedAt)
                .HasDatabaseName("IX_SpeedReadingIdempotencyRecords_CreatedAt");
        });

        modelBuilder.Entity<AdminAuditRecord>(entity =>
        {
            entity.ToTable("SpeedReadingAdminAuditRecords");
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

        modelBuilder.Entity<LegacyAchievement>(entity =>
        {
            entity.ToTable("Achievements");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Category);
            entity.HasIndex(item => item.Tier);
            entity.HasIndex(item => item.IsActive);
            entity.HasIndex(item => item.SortOrder);
        });

        modelBuilder.Entity<LegacyUserAchievement>(entity =>
        {
            entity.ToTable("UserAchievements");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.AchievementId }).IsUnique();
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.UnlockedAt);
            entity.HasIndex(item => new { item.UserId, item.IsShowcased });
        });

        modelBuilder.Entity<LegacyUserGamification>(entity =>
        {
            entity.ToTable("UserGameifications");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.UserId).IsUnique();
            entity.HasIndex(item => item.TotalXP);
            entity.HasIndex(item => item.CurrentLevel);
            entity.HasIndex(item => item.CurrentStreak);
            entity.Property(item => item.MaxComprehensionScore).HasPrecision(18, 2);
            entity.Property(item => item.MaxRSVPComprehension).HasPrecision(18, 2);
        });

        modelBuilder.Entity<LegacyUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(item => item.Id);
            // The legacy Users table has Email but no separate UserName column.
            entity.Ignore(item => item.UserName);
            entity.HasIndex(item => new { item.IsDeleted, item.InstitutionId, item.Id });
        });

        modelBuilder.Entity<LegacyStudentLearningProfile>(entity =>
        {
            entity.ToTable("StudentLearningProfiles");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.StudentId);
        });

        modelBuilder.Entity<LegacyContentRecommendation>(entity =>
        {
            entity.ToTable("ContentRecommendations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ConfidenceScore).HasPrecision(18, 2);
            entity.HasIndex(item => item.StudentId);
            entity.HasIndex(item => item.ReadingTextId);
        });

        modelBuilder.Entity<LegacyDailyGoal>(entity =>
        {
            entity.ToTable("DailyGoals");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.StudentId, item.Date });
        });

        modelBuilder.Entity<LegacyStudentReadingProfile>(entity =>
        {
            entity.ToTable("StudentReadingProfiles");
            entity.HasKey(item => item.Id);
            entity.Ignore(item => item.PreferredCategories);
            entity.Ignore(item => item.DifficultCategories);
            entity.Property(item => item.PreferredCategoriesSource).HasColumnName("PreferredCategories");
            entity.Property(item => item.DifficultCategoriesSource).HasColumnName("DifficultCategories");
            entity.HasIndex(item => item.StudentId);
        });

        modelBuilder.Entity<LegacyTextRecommendationHistory>(entity =>
        {
            entity.ToTable("TextRecommendationHistories");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ReadingTextId);
            entity.HasIndex(item => item.StudentId);
        });

        modelBuilder.Entity<LegacyUserContentFeedback>(entity =>
        {
            entity.ToTable("UserContentFeedbacks");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ContentType).HasMaxLength(50).IsRequired();
            entity.Property(item => item.DeviceType).HasMaxLength(50).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.SessionDate });
            entity.HasIndex(item => new { item.UserId, item.ContentType, item.ContentId });
        });

        modelBuilder.Entity<LegacyReportTemplate>(entity =>
        {
            entity.ToTable("ReportTemplates");
            entity.HasKey(item => item.Id);
            // The legacy entity hides BaseEntity.CreatedBy behind a nullable
            // User navigation. Keep the nullable CreatedById column explicit
            // and do not map the non-nullable audit base property here.
            entity.Ignore(item => item.CreatedBy);
            entity.Property(item => item.CreatedById).HasColumnName("CreatedById");
            entity.HasIndex(item => item.CreatedAt);
            entity.HasIndex(item => new { item.Type, item.IsActive });
        });

        modelBuilder.Entity<LegacyReportSnapshot>(entity =>
        {
            entity.ToTable("ReportSnapshots");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.GeneratedForUserId, item.GeneratedAt });
            entity.HasIndex(item => item.ReportTemplateId);
        });

        modelBuilder.Entity<LegacyScheduledReport>(entity =>
        {
            entity.ToTable("ScheduledReports");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.IsActive });
            entity.HasIndex(item => item.ReportTemplateId);
        });
    }
}
