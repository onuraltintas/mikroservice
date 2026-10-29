using System.Reflection;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Review H2a (findings 3, 4 and the review-status case): money flows of Speed Reading.
/// </summary>
[Collection("Database")]
public sealed class SpeedReadingRenewalPostgresTests(PostgresFixture postgres)
{
    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Monthly = Guid.NewGuid();
    private static readonly Guid Institution = Guid.NewGuid();

    [Fact]
    public async Task An_approved_transfer_is_kept_and_an_early_renewal_keeps_the_remaining_days()
    {
        await WithDatabase(async (db, service) =>
        {
            var user = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var currentEnd = now.AddDays(20);
            Add(db, "LegacyUserSubscription", new { Id = Guid.NewGuid(), UserId = user, PlanId = Monthly, ProductId = Product, Status = "Active",
                StartDate = now.AddDays(-10), EndDate = (DateTime?)currentEnd, CreatedAt = now.AddDays(-10) });
            var request = Guid.NewGuid();
            Add(db, "LegacyBankTransferPaymentRequest", new { Id = request, UserId = user, UserName = "Ada", UserEmail = "ada@example.invalid", PlanId = Monthly,
                Amount = 100m, Currency = "TRY", PaymentReference = "EFT-RENEW-1", Status = "Pending", CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var reviewed = await service.ReviewBankTransferPaymentRequestAsync(request, new ReviewBankTransferPaymentRequest("approved", null),
                Guid.NewGuid(), "eft-review-key-0001");

            Assert.NotNull(reviewed);
            Assert.Equal(BankTransferPaymentRules.ApprovedStatus, reviewed.Status);
            db.ChangeTracker.Clear();
            var renewal = await db.Database.SqlQueryRaw<DateTime>(
                "SELECT \"EndDate\" AS \"Value\" FROM speed_reading.user_subscriptions WHERE \"Notes\" LIKE 'Bank transfer payment %'").SingleAsync();
            Assert.Equal(currentEnd.AddDays(30), renewal, TimeSpan.FromSeconds(5));

            var delete = () => service.DeleteBankTransferPaymentRequestAsync(request);
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(delete);
            Assert.Equal("BankTransfer.ApprovedIsPermanent", refused.Code);
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM speed_reading.bank_transfer_payment_requests").SingleAsync());
        });
    }

    [Fact]
    public async Task An_early_institution_renewal_extends_students_who_still_have_access()
    {
        await WithDatabase(async (db, service) =>
        {
            var now = DateTime.UtcNow;
            var annual = Guid.NewGuid();
            Add(db, "LegacySubscriptionPlan", new { Id = annual, ProductId = Product, Slug = "kurum", Name = "Kurum", Price = 1000m, IsActive = true, IsPublic = false,
                BillingPeriod = "Annual", DurationDays = (int?)365, CreatedAt = now });
            var expiring = Guid.NewGuid();
            var covered = Guid.NewGuid();
            var newcomer = Guid.NewGuid();
            var expiringEnd = now.AddDays(20);
            Add(db, "LegacyUserSubscription", new { Id = Guid.NewGuid(), UserId = expiring, PlanId = annual, ProductId = Product, Status = "Active",
                StartDate = now.AddDays(-345), EndDate = (DateTime?)expiringEnd, CreatedAt = now.AddDays(-345) });
            Add(db, "LegacyUserSubscription", new { Id = Guid.NewGuid(), UserId = covered, PlanId = annual, ProductId = Product, Status = "Active",
                StartDate = now, EndDate = (DateTime?)now.AddDays(400), CreatedAt = now });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var result = await service.CreateInstitutionAccessAsync(new CreateInstitutionAccessRequest(Institution, annual, now,
                [new(expiring, "A", null), new(covered, "B", null), new(newcomer, "C", null)], "KURUM-2027", null), Guid.NewGuid());

            Assert.NotNull(result);
            Assert.Equal(2, result.CreatedCount);
            Assert.NotNull(result.License);
            var ends = await db.Database.SqlQueryRaw<DateTime>(
                $"SELECT \"EndDate\" AS \"Value\" FROM speed_reading.user_subscriptions WHERE \"UserId\" = '{expiring}' ORDER BY \"EndDate\"").ToListAsync();
            Assert.Equal(2, ends.Count);
            Assert.Equal(expiringEnd.AddDays(365), ends[1], TimeSpan.FromSeconds(5));
        });
    }

    private async Task WithDatabase(Func<OwnedSpeedReadingDbContext, ISpeedReadingSubscription, Task> test)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString, builder => builder.EnableRetryOnFailure(2, TimeSpan.Zero, null)).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var now = DateTime.UtcNow;
            Add(db, "LegacyProduct", new { Id = Product, Slug = "hizliokuma", Name = "Hızlı Okuma", IsActive = true, IsPublic = true, CreatedAt = now, UpdatedAt = now });
            Add(db, "LegacySubscriptionPlan", new { Id = Monthly, ProductId = Product, Slug = "aylik", Name = "Aylık", Price = 100m, IsActive = true, IsPublic = true,
                BillingPeriod = "Monthly", DurationDays = (int?)30, CreatedAt = now });
            await db.SaveChangesAsync();
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Legacy.LegacySpeedReadingSubscription")!;
            var service = (ISpeedReadingSubscription)Activator.CreateInstance(serviceType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                [db, db, null, null, new Directory()], null)!;
            await test(db, service);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static void Add(OwnedSpeedReadingDbContext db, string name, object values)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType($"SpeedReading.Infrastructure.Legacy.{name}")!;
        var entity = Activator.CreateInstance(type)!;
        foreach (var property in values.GetType().GetProperties())
            type.GetProperty(property.Name)!.SetValue(entity, property.GetValue(values));
        db.Add(entity);
    }

    private sealed class Directory : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse([]));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
