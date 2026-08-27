using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Kernel.Primitives;
using SpeedReading.Domain.Catalog;
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

        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.ToTable("exercises");
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2_000).IsRequired();
            entity.Property(item => item.TypeCode).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ConfigurationJson).HasColumnType("jsonb").IsRequired();
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
