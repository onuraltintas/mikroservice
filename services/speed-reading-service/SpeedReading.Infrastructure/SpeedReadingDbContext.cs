using Microsoft.EntityFrameworkCore;
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
        });

        modelBuilder.Entity<LegacyDailyExerciseLog>(entity =>
        {
            entity.ToTable("DailyExerciseLogs");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.StudentProgramProgressId);
            entity.HasIndex(item => item.ExerciseId);
            entity.HasIndex(item => item.ExerciseTypeId);
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
    }
}
