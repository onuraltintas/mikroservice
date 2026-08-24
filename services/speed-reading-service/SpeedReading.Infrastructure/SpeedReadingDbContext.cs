using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Infrastructure.Middleware;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure;

/// <summary>
/// Access boundary for the existing Hızlı Okuma database.
/// The first integration phase intentionally does not run migrations: the
/// existing schema is treated as production data and remains the source of truth.
/// </summary>
public sealed class SpeedReadingDbContext(DbContextOptions<SpeedReadingDbContext> options) : DbContext(options)
{
    internal DbSet<LegacyExerciseTypeCategory> ExerciseTypeCategories => Set<LegacyExerciseTypeCategory>();
    internal DbSet<LegacyExerciseType> ExerciseTypes => Set<LegacyExerciseType>();
    internal DbSet<LegacyExercise> Exercises => Set<LegacyExercise>();
    internal DbSet<LegacyReadingText> ReadingTexts => Set<LegacyReadingText>();
    internal DbSet<LegacyReadingQuestion> ReadingQuestions => Set<LegacyReadingQuestion>();
    internal DbSet<LegacyExerciseSession> ExerciseSessions => Set<LegacyExerciseSession>();
    internal DbSet<LegacyStudentExerciseResult> StudentExerciseResults => Set<LegacyStudentExerciseResult>();
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
    internal DbSet<AdminAuditRecord> AdminAuditRecords => Set<AdminAuditRecord>();
    internal DbSet<LegacyAchievement> Achievements => Set<LegacyAchievement>();
    internal DbSet<LegacyUserAchievement> UserAchievements => Set<LegacyUserAchievement>();
    internal DbSet<LegacyUserGamification> UserGamifications => Set<LegacyUserGamification>();
    internal DbSet<LegacyUser> Users => Set<LegacyUser>();
    internal DbSet<LegacyReportTemplate> ReportTemplates => Set<LegacyReportTemplate>();
    internal DbSet<LegacyReportSnapshot> ReportSnapshots => Set<LegacyReportSnapshot>();
    internal DbSet<LegacyScheduledReport> ScheduledReports => Set<LegacyScheduledReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // This context is a compatibility reader for the existing database. It
        // deliberately has no EF migrations and never calls EnsureCreated.
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
            entity.HasIndex(item => item.CreatorId);
        });

        modelBuilder.Entity<LegacyReadingText>(entity =>
        {
            entity.ToTable("ReadingTexts");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ExerciseId);
            entity.HasIndex(item => item.TargetAgeGroupConfigurationId);
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
        });

        modelBuilder.Entity<LegacyStudentExerciseResult>(entity =>
        {
            entity.ToTable("StudentExerciseResults");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.StudentId);
            entity.HasIndex(item => item.ExerciseId);
            entity.HasIndex(item => item.ReadingTextId);
            entity.Property(item => item.RawWPM).HasPrecision(18, 2);
            entity.Property(item => item.ComprehensionScore).HasPrecision(18, 2);
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
            entity.HasIndex(item => item.ParentNodeId);
        });

        modelBuilder.Entity<LegacyNodeContent>(entity =>
        {
            entity.ToTable("NodeContents");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.NodeId);
        });

        modelBuilder.Entity<LegacyNodePrerequisite>(entity =>
        {
            entity.ToTable("NodePrerequisites");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.NodeId);
            entity.HasIndex(item => item.PrerequisiteNodeId);
        });

        modelBuilder.Entity<LegacyStudentPathProgress>(entity =>
        {
            entity.ToTable("StudentPathProgresses");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.StudentId);
            entity.HasIndex(item => item.TemplateId);
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
            entity.HasIndex(item => item.TemplateId);
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
        });

        modelBuilder.Entity<LegacyUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.IsDeleted, item.InstitutionId, item.Id });
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
