using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Configurations;

public sealed class CoachingAgreementDocumentConfiguration
    : IEntityTypeConfiguration<CoachingAgreementDocument>
{
    public void Configure(EntityTypeBuilder<CoachingAgreementDocument> builder)
    {
        builder.ToTable("coaching_agreement_documents");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.InstitutionId).HasColumnName("institution_id");
        builder.Property(item => item.DocumentVersion).HasColumnName("document_version").HasMaxLength(100).IsRequired();
        builder.Property(item => item.Locale).HasColumnName("locale").HasMaxLength(20).IsRequired();
        builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(item => item.DocumentReference).HasColumnName("document_reference").HasMaxLength(1_000).IsRequired();
        builder.Property(item => item.ContentSha256).HasColumnName("content_sha256").HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(item => item.PublishedByUserId).HasColumnName("published_by_user_id").IsRequired();
        builder.Property(item => item.EffectiveAt).HasColumnName("effective_at").IsRequired();
        builder.Property(item => item.PublishedAt).HasColumnName("published_at").IsRequired();
        builder.Property(item => item.SupersededAt).HasColumnName("superseded_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.Version).HasColumnName("row_version").IsConcurrencyToken().IsRequired();
        builder.Ignore(item => item.CreatedBy);
        builder.Ignore(item => item.UpdatedBy);

        builder.HasIndex(item => new { item.DocumentVersion, item.Locale })
            .HasDatabaseName("ux_coaching_agreement_documents_global_version_locale")
            .HasFilter("institution_id IS NULL")
            .IsUnique();
        builder.HasIndex(item => new { item.InstitutionId, item.DocumentVersion, item.Locale })
            .HasDatabaseName("ux_coaching_agreement_documents_tenant_version_locale")
            .HasFilter("institution_id IS NOT NULL")
            .IsUnique();
        builder.HasIndex(item => new { item.InstitutionId, item.Locale, item.EffectiveAt })
            .HasDatabaseName("ix_coaching_agreement_documents_current_lookup");
    }
}

public sealed class CoachingAgreementAcknowledgementConfiguration
    : IEntityTypeConfiguration<CoachingAgreementAcknowledgement>
{
    public void Configure(EntityTypeBuilder<CoachingAgreementAcknowledgement> builder)
    {
        builder.ToTable("coaching_agreement_acknowledgements");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.AgreementDocumentId).HasColumnName("agreement_document_id").IsRequired();
        builder.Property(item => item.SubjectStudentId).HasColumnName("subject_student_id").IsRequired();
        builder.Property(item => item.AcknowledgedByUserId).HasColumnName("acknowledged_by_user_id").IsRequired();
        builder.Property(item => item.PartyRole).HasColumnName("party_role").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.AcknowledgedAt).HasColumnName("acknowledged_at").IsRequired();
        builder.Property(item => item.WithdrawnAt).HasColumnName("withdrawn_at");
        builder.Property(item => item.WithdrawnByUserId).HasColumnName("withdrawn_by_user_id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Ignore(item => item.CreatedBy);
        builder.Ignore(item => item.UpdatedBy);

        builder.HasOne(item => item.AgreementDocument)
            .WithMany()
            .HasForeignKey(item => item.AgreementDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new
            {
                item.AgreementDocumentId,
                item.SubjectStudentId,
                item.AcknowledgedByUserId,
                item.PartyRole
            })
            .HasDatabaseName("ux_coaching_agreement_acknowledgements_active_evidence")
            .HasFilter("withdrawn_at IS NULL")
            .IsUnique();
        builder.HasIndex(item => new { item.SubjectStudentId, item.AcknowledgedAt })
            .HasDatabaseName("ix_coaching_agreement_acknowledgements_student_timeline");
    }
}
