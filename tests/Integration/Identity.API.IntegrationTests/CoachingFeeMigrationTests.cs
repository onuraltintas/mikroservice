using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingFeeMigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task FeeSchemaMatchesModelAndRoundTripsWithoutLosingExistingReminders()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try {
            await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
            var now = DateTime.UtcNow; var student = Guid.NewGuid(); var actor = Guid.NewGuid();
            var study = CoachingReminderReceipt.Create(student, Guid.NewGuid(), "StudyDay", "2026-10-13", now, now);
            var overdue = CoachingReminderReceipt.Create(student, student, "FeeOverdue", "2026-10-13", now, now);
            var fee = CoachingFeeAgreement.Create(Guid.NewGuid(), student, Guid.NewGuid(), null, actor, "Koçluk", 1000m,
                [new(DateOnly.FromDateTime(now), 1000m)], now);
            var payment = fee.AddPayment(Guid.NewGuid(), actor, 100m, CoachingFeePaymentMethod.Cash, DateOnly.FromDateTime(now), now, null);
            fee.ReversePayment(Guid.NewGuid(), payment.Id, actor, DateOnly.FromDateTime(now), "Düzeltme", now);
            db.AddRange(study, overdue, fee); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(2, await db.CoachingFeePayments.CountAsync());
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261013130901_AddCoachingReminderReceipts");
            Assert.Equal(study.Id, (await db.CoachingReminderReceipts.SingleAsync()).Id);
            await migrator.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.CoachingFeeAgreements.ToListAsync());
            Assert.Equal(study.Id, (await db.CoachingReminderReceipts.SingleAsync()).Id);
            db.Add(CoachingReminderReceipt.Create(student, student, "FeeOverdue", "2026-10-14", now, now));
            await db.SaveChangesAsync(); Assert.Equal(2, await db.CoachingReminderReceipts.CountAsync());
        } finally { await db.Database.EnsureDeletedAsync(); }
    }
}
