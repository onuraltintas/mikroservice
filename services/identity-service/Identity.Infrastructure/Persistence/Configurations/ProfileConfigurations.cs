using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.ToTable("student_profiles");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.UserId)
            .IsUnique();

        builder.Property(s => s.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Gender)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.LearningStyle)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.SchoolName)
            .HasMaxLength(255);

        builder.Property(s => s.SchoolCity)
            .HasMaxLength(100);

        builder.Property(s => s.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(s => s.Preferences)
            .HasColumnType("jsonb");

        builder.Property(s => s.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(s => s.InstitutionId);
        builder.HasIndex(s => s.ParentId);
        builder.HasIndex(s => s.GradeLevel);

        // Parent relationship
        builder.HasOne(s => s.Parent)
            .WithMany()
            .HasForeignKey(s => s.ParentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Teacher assignments
        builder.HasMany(s => s.TeacherAssignments)
            .WithOne(a => a.Student)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TeacherProfileConfiguration : IEntityTypeConfiguration<TeacherProfile>
{
    public void Configure(EntityTypeBuilder<TeacherProfile> builder)
    {
        builder.ToTable("teacher_profiles");

        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.UserId)
            .IsUnique();

        builder.Property(t => t.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Title)
            .HasMaxLength(50);

        builder.Property(t => t.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(t => t.Certifications)
            .HasColumnType("jsonb");

        // Subjects as array
        // Subjects stored as JSON array
        builder.Property(t => t.Subjects)
            .HasField("_subjects")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("subjects")
            .HasColumnType("jsonb");

        builder.Property(t => t.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(t => t.InstitutionId);

        // Student assignments
        builder.HasMany(t => t.StudentAssignments)
            .WithOne(a => a.Teacher)
            .HasForeignKey(a => a.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ParentProfileConfiguration : IEntityTypeConfiguration<ParentProfile>
{
    public void Configure(EntityTypeBuilder<ParentProfile> builder)
    {
        builder.ToTable("parent_profiles");

        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.UserId)
            .IsUnique();

        builder.Property(p => p.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(p => p.Relationship)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.NotificationPreferences)
            .HasColumnType("jsonb");

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("NOW()");
    }
}

public class TeacherStudentAssignmentConfiguration : IEntityTypeConfiguration<TeacherStudentAssignment>
{
    public void Configure(EntityTypeBuilder<TeacherStudentAssignment> builder)
    {
        builder.ToTable("teacher_student_assignments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Subject)
            .HasMaxLength(100);

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(a => a.TeacherId);
        builder.HasIndex(a => a.StudentId);
        builder.HasIndex(a => a.InstitutionId);

        builder.HasIndex(a => new { a.TeacherId, a.StudentId, a.Subject })
            .IsUnique();

        // Institution relationship
        builder.HasOne(a => a.Institution)
            .WithMany()
            .HasForeignKey(a => a.InstitutionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
