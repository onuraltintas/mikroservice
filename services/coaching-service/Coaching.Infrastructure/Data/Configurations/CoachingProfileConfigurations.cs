using Coaching.Domain.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class CoachingInvitationConfiguration : IEntityTypeConfiguration<CoachingInvitation>
{
    public void Configure(EntityTypeBuilder<CoachingInvitation> builder)
    {
        builder.ToTable("invitations");
        builder.HasKey(x => x.Id); builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Ignore(x => x.Type);
        builder.Property(x => x.RecipientEmail).HasMaxLength(254);
        builder.Property(x => x.Role).HasMaxLength(20);
        builder.Property(x => x.Message).HasMaxLength(2000);
        builder.HasIndex(x => new { x.RecipientEmail, x.Status, x.ExpiresAt });
        builder.HasIndex(x => new { x.InviterUserId, x.Status });
        builder.HasIndex(x => new { x.RecipientEmail, x.Role, x.InstitutionId, x.TeacherUserId, x.IsInstitutionInvitation })
            .IsUnique().AreNullsDistinct(false).HasFilter("\"Status\" = 0");
    }
}

public sealed class CoachingStudentProfileConfiguration : IEntityTypeConfiguration<CoachingStudentProfile>
{
    public void Configure(EntityTypeBuilder<CoachingStudentProfile> builder)
    {
        builder.ToTable("student_profiles", table =>
        {
            table.HasCheckConstraint("ck_coaching_student_education", "\"EducationStatus\" BETWEEN 0 AND 2 AND ((\"EducationStatus\" = 1 AND \"GradeLevel\" BETWEEN 1 AND 12 AND \"GradeLevel\" IS NOT NULL) OR (\"EducationStatus\" <> 1 AND \"GradeLevel\" IS NULL))");
            table.HasCheckConstraint("ck_coaching_student_graduation", "\"GraduationYear\" IS NULL OR (\"EducationStatus\" = 2 AND \"GraduationYear\" BETWEEN 1900 AND 9999)");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => x.UserId).HasName("ak_coaching_student_profile_user");
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Ignore(x => x.IsEducationComplete);
        builder.Property(x => x.SchoolName).HasMaxLength(255);
        builder.Property(x => x.SchoolCity).HasMaxLength(100);
        builder.Property(x => x.SchoolDistrict).HasMaxLength(100);
        builder.Property(x => x.SchoolType).HasMaxLength(100);
        builder.Property(x => x.ClassBranch).HasMaxLength(30);
        builder.Property(x => x.BirthDate).HasColumnType("date");
        builder.Property(x => x.Bio).HasMaxLength(2000);
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.HasIndex(x => new { x.IsActive, x.GradeLevel });
    }
}

public sealed class CoachingTeacherProfileConfiguration : IEntityTypeConfiguration<CoachingTeacherProfile>
{
    public void Configure(EntityTypeBuilder<CoachingTeacherProfile> builder)
    {
        builder.ToTable("teacher_profiles");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => x.UserId).HasName("ak_coaching_teacher_profile_user");
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.Title).HasMaxLength(100);
        builder.Property(x => x.Bio).HasMaxLength(2000);
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.Ignore(x => x.Subjects);
        builder.Property<string>("SubjectsJson").HasColumnType("jsonb");
    }
}

public sealed class CoachingInstitutionMembershipConfiguration : IEntityTypeConfiguration<CoachingInstitutionMembership>
{
    public void Configure(EntityTypeBuilder<CoachingInstitutionMembership> builder)
    {
        builder.ToTable("institution_memberships", table => table.HasCheckConstraint("ck_coaching_member_role", "\"Role\" IN (1, 2)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.InstitutionId, x.UserId, x.Role }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Role }).IsUnique().HasFilter("\"IsActive\"");
        builder.HasIndex(x => new { x.InstitutionId, x.Role, x.IsActive });
    }
}

public sealed class CoachingTeacherStudentAssignmentConfiguration : IEntityTypeConfiguration<CoachingTeacherStudentAssignment>
{
    public void Configure(EntityTypeBuilder<CoachingTeacherStudentAssignment> builder)
    {
        builder.ToTable("teacher_student_assignments", table =>
        {
            table.HasCheckConstraint("ck_coaching_assignment_users", "\"TeacherUserId\" <> \"StudentUserId\"");
            table.HasCheckConstraint("ck_coaching_assignment_end", "(\"IsActive\" AND \"EndDate\" IS NULL) OR (NOT \"IsActive\" AND \"EndDate\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.Subject).HasMaxLength(100);
        builder.HasOne<CoachingTeacherProfile>().WithMany().HasForeignKey(x => x.TeacherUserId)
            .HasPrincipalKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CoachingStudentProfile>().WithMany().HasForeignKey(x => x.StudentUserId)
            .HasPrincipalKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TeacherUserId, x.StudentUserId, x.InstitutionId, x.Subject }).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(x => new { x.StudentUserId, x.IsActive });
        builder.HasIndex(x => new { x.InstitutionId, x.IsActive });
    }
}
