using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Coaching.Domain.Entities;

namespace Coaching.Infrastructure.Configurations;

/// <summary>
/// CoachingSession entity configuration
/// </summary>
public class CoachingSessionConfiguration : IEntityTypeConfiguration<CoachingSession>
{
    public void Configure(EntityTypeBuilder<CoachingSession> builder)
    {
        builder.ToTable("coaching_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.TeacherId)
            .HasColumnName("teacher_id")
            .IsRequired();

        builder.Property(x => x.InstitutionId)
            .HasColumnName("institution_id");

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.SessionType)
            .HasColumnName("session_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ScheduledDate)
            .HasColumnName("scheduled_date")
            .IsRequired();

        builder.Property(x => x.DurationMinutes)
            .HasColumnName("duration_minutes")
            .IsRequired();

        builder.Property(x => x.MeetingLink)
            .HasColumnName("meeting_link")
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.TeacherNotes)
            .HasColumnName("teacher_notes")
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Relationships
        builder.HasMany(x => x.Attendances)
            .WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.TeacherId)
            .HasDatabaseName("ix_coaching_sessions_teacher_id");

        builder.HasIndex(x => x.ScheduledDate)
            .HasDatabaseName("ix_coaching_sessions_scheduled_date");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_coaching_sessions_status");
    }
}

/// <summary>
/// SessionAttendance entity configuration
/// </summary>
public class SessionAttendanceConfiguration : IEntityTypeConfiguration<SessionAttendance>
{
    public void Configure(EntityTypeBuilder<SessionAttendance> builder)
    {
        builder.ToTable("session_attendances");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(x => x.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(x => x.AttendanceStatus)
            .HasColumnName("attendance_status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.JoinedAt)
            .HasColumnName("joined_at");

        builder.Property(x => x.LeftAt)
            .HasColumnName("left_at");

        builder.Property(x => x.StudentNote)
            .HasColumnName("student_note")
            .HasColumnType("text");

        builder.Property(x => x.TeacherNote)
            .HasColumnName("teacher_note")
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("ix_session_attendances_student_id");

        builder.HasIndex(x => new { x.SessionId, x.StudentId })
            .HasDatabaseName("ix_session_attendances_session_student")
            .IsUnique();
    }
}
