using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class TargetAdmissionBatchConfiguration : IEntityTypeConfiguration<TargetAdmissionBatch>
{
    public void Configure(EntityTypeBuilder<TargetAdmissionBatch> b)
    {
        b.ToTable("target_admission_batches",t=>t.HasCheckConstraint("ck_admission_year","\"Year\" >= 2026"));
        b.HasKey(x=>x.Id);
        b.Property(x=>x.ExamCode).HasMaxLength(10);b.Property(x=>x.Stage).HasMaxLength(40);
        b.Property(x=>x.SourceUrl).HasMaxLength(2000);b.Property(x=>x.DocumentHash).HasMaxLength(64);
        b.HasIndex(x=>new{x.ExamCode,x.Year,x.Stage,x.DocumentHash}).IsUnique();
    }
}
public sealed class TargetAdmissionRecordConfiguration : IEntityTypeConfiguration<TargetAdmissionRecord>
{
    public void Configure(EntityTypeBuilder<TargetAdmissionRecord> b)
    {
        b.ToTable("target_admission_records",t=>
        {
            t.HasCheckConstraint("ck_admission_target","NOT (\"SchoolId\" IS NOT NULL AND \"ProgramId\" IS NOT NULL)");
            t.HasCheckConstraint("ck_admission_values","(\"Quota\" IS NULL OR \"Quota\" >= 0) AND (\"Placed\" IS NULL OR \"Placed\" >= 0) AND (\"Rank\" IS NULL OR \"Rank\" > 0) AND (\"Percentile\" IS NULL OR \"Percentile\" BETWEEN 0 AND 100) AND (\"MinimumScore\" IS NULL OR \"MinimumScore\" >= 0) AND (\"MaximumScore\" IS NULL OR \"MaximumScore\" >= 0) AND (\"MinimumScore\" IS NULL OR \"MaximumScore\" IS NULL OR \"MinimumScore\" <= \"MaximumScore\")");
            t.HasCheckConstraint("ck_admission_verified","\"MatchStatus\" IN ('Pending','Ambiguous','Conflict','Verified') AND (\"MatchStatus\" <> 'Verified' OR (\"SchoolId\" IS NOT NULL OR \"ProgramId\" IS NOT NULL))");
        });
        b.HasKey(x=>x.Id);b.HasIndex(x=>new{x.BatchId,x.ExternalCode}).IsUnique();
        b.HasOne<TargetAdmissionBatch>().WithMany().HasForeignKey(x=>x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TargetSchool>().WithMany().HasForeignKey(x=>x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TargetUniversityProgram>().WithMany().HasForeignKey(x=>x.ProgramId).OnDelete(DeleteBehavior.Restrict);
        foreach(var name in new[]{"Institution","Name","City","District","InstitutionType","Degree","Language","Scholarship","EducationType","Field"}) b.Property<string>(name).HasMaxLength(300);
        b.Property(x=>x.ExternalCode).HasMaxLength(100);b.Property(x=>x.ScoreType).HasMaxLength(30);
        b.Property(x=>x.MatchStatus).HasMaxLength(20);b.Property(x=>x.Issue).HasMaxLength(2000);
        b.Property(x=>x.MinimumScore).HasPrecision(12,5);b.Property(x=>x.MaximumScore).HasPrecision(12,5);
        b.Property(x=>x.Percentile).HasPrecision(7,4);
        b.HasIndex(x=>new{x.BatchId,x.MatchStatus,x.ScoreType});
        b.HasIndex(x=>new{x.City,x.InstitutionType,x.Degree});
    }
}
