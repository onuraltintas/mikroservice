using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class CoachingFeeAgreementConfiguration : IEntityTypeConfiguration<CoachingFeeAgreement>
{
    public void Configure(EntityTypeBuilder<CoachingFeeAgreement> b)
    {
        b.ToTable("fee_agreements", table => {
            table.HasCheckConstraint("ck_fee_owner", "(\"InstitutionId\" IS NOT NULL) <> (\"CoachId\" IS NOT NULL)");
            table.HasCheckConstraint("ck_fee_amounts", "\"TotalAmount\" > 0 AND \"TotalAmount\" <= 999999999.99 AND \"PaidAmount\" BETWEEN 0 AND \"TotalAmount\"");
            table.HasCheckConstraint("ck_fee_cancelled", "NOT \"IsCancelled\" OR (\"PaidAmount\" = 0 AND \"CancelledAt\" IS NOT NULL AND \"CancelledByUserId\" IS NOT NULL AND \"CancellationReason\" IS NOT NULL)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.CancellationReason).HasMaxLength(300);
        b.Property(x => x.TotalAmount).HasPrecision(11, 2); b.Property(x => x.PaidAmount).HasPrecision(11, 2); b.Ignore(x => x.Balance);
        b.HasIndex(x => new { x.InstitutionId, x.StudentId }); b.HasIndex(x => new { x.CoachId, x.StudentId }); b.HasIndex(x => x.StudentId);
        b.HasMany(x => x.Installments).WithOne().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class CoachingFeeInstallmentConfiguration : IEntityTypeConfiguration<CoachingFeeInstallment>
{
    public void Configure(EntityTypeBuilder<CoachingFeeInstallment> b)
    {
        b.ToTable("fee_installments", table => table.HasCheckConstraint("ck_fee_installment", "\"Amount\" > 0 AND \"Amount\" <= 999999999.99 AND \"Sequence\" BETWEEN 1 AND 24"));
        b.HasKey(x => x.Id); b.Property(x => x.Amount).HasPrecision(11, 2);
        b.HasIndex(x => new { x.AgreementId, x.Sequence }).IsUnique(); b.HasIndex(x => x.DueOn);
    }
}
public sealed class CoachingFeePaymentConfiguration : IEntityTypeConfiguration<CoachingFeePayment>
{
    public void Configure(EntityTypeBuilder<CoachingFeePayment> b)
    {
        b.ToTable("fee_payments", table => {
            table.HasCheckConstraint("ck_fee_payment_amount", "ABS(\"Amount\") BETWEEN 0.01 AND 999999999.99 AND ((\"ReversalOfPaymentId\" IS NULL AND \"Amount\" > 0) OR (\"ReversalOfPaymentId\" IS NOT NULL AND \"Amount\" < 0))");
            table.HasCheckConstraint("ck_fee_payment_method", "\"Method\" IN (1, 2)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.AgreementId, x.Id });
        b.Property(x => x.Amount).HasPrecision(11, 2); b.Property(x => x.Reference).HasMaxLength(300);
        b.HasIndex(x => x.ReversalOfPaymentId).IsUnique().HasFilter("\"ReversalOfPaymentId\" IS NOT NULL");
        b.HasIndex(x => new { x.AgreementId, x.ReceivedOn });
        b.HasOne<CoachingFeePayment>().WithMany().HasForeignKey(x => new { x.AgreementId, x.ReversalOfPaymentId })
            .HasPrincipalKey(x => new { x.AgreementId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
}
