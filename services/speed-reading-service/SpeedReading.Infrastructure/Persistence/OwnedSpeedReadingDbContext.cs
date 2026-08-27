using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Kernel.Primitives;
using EduPlatform.Shared.Infrastructure.Middleware;
using SpeedReading.Domain.Assignments;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.LearningPaths;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Profiles;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// EF Core context for data owned by the Speed Reading bounded context.
/// It intentionally has no legacy entity sets.
/// </summary>
public sealed class OwnedSpeedReadingDbContext(
    DbContextOptions<OwnedSpeedReadingDbContext> options) : DbContext(options)
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
}
