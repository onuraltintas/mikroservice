using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingFeePrivacyTests(PostgresFixture postgres)
{
    [Fact]
    public async Task OwnFinancialHistoryIsExportedAssessedAndErasedWithoutDeletingOtherStudentsFees()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try {
            var student = Guid.NewGuid(); var other = Guid.NewGuid(); var actor = Guid.NewGuid(); var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now); var institution = Guid.NewGuid();
            CoachingFeeAgreement Fee(Guid subject) => CoachingFeeAgreement.Create(Guid.NewGuid(), subject, institution, null,
                actor, "Koçluk paketi", 1000m, [new(today, 400m), new(today.AddMonths(1), 600m)], now);
            var own = Fee(student); var foreign = Fee(other); var receiptId = Guid.NewGuid();
            own.AddPayment(receiptId, actor, 100m, CoachingFeePaymentMethod.Cash, today, now, "Makbuz");
            own.ReversePayment(Guid.NewGuid(), receiptId, actor, today, "Düzeltme", now);
            db.AddRange(own, foreign); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var export = await new CoachingDataExportRepository(db).ExportStudentDataAsync(student, new(now), default);
            var fee = Assert.Single(export.Fees!); Assert.Equal(own.Id, fee.Id); Assert.Equal("TRY", fee.Currency);
            Assert.Equal(2, fee.Installments.Count); Assert.Equal(2, fee.Payments.Count); Assert.Equal(0m, fee.PaidAmount);
            Assert.Contains(fee.Payments, x => x.ReversalOfPaymentId == receiptId && x.Amount == -100m);
            var requestId = Guid.NewGuid(); var eventId = Guid.NewGuid();
            var assessment = await new CoachingErasureAssessmentService(db, TimeProvider.System)
                .AssessAsync(new(eventId, requestId, student, now, true, PersonalDataScope.Coaching), default);
            Assert.Equal(5, assessment.FinanceRecordCount);
            var execution = await new CoachingErasureExecutionService(db, new CoachingStudyPlanningPrivacyTests.NoAttachments(), TimeProvider.System)
                .ExecuteAsync(new(eventId, requestId, student, now, PersonalDataScope.Coaching), default);
            Assert.Equal(5, execution.DeletedRecordCount);
            Assert.False(await db.CoachingFeeAgreements.AnyAsync(x => x.StudentId == student));
            Assert.False(await db.CoachingFeePayments.AnyAsync()); Assert.Equal(2, await db.CoachingFeeInstallments.CountAsync());
            Assert.Equal(foreign.Id, (await db.CoachingFeeAgreements.SingleAsync()).Id);
        } finally { await db.Database.EnsureDeletedAsync(); }
    }
}
