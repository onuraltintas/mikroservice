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
    }
}
