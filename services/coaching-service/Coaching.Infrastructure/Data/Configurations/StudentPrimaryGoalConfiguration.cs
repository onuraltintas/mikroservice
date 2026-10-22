using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coaching.Infrastructure.Data.Configurations;
public sealed class StudentPrimaryGoalConfiguration : IEntityTypeConfiguration<StudentPrimaryGoal>
{
    public void Configure(EntityTypeBuilder<StudentPrimaryGoal> b)
    {
        b.ToTable("student_primary_goals"); b.HasKey(x => x.Id);
        b.HasIndex(x => x.StudentId).IsUnique();
        b.HasOne<AcademicGoal>().WithMany().HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Version).IsConcurrencyToken();
    }
}
