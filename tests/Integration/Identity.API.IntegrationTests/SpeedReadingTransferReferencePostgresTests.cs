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
/// Decision 29.10.2026: one bank receipt reference cannot be claimed by two accounts (one payment would open two
/// subscriptions). A rejected claim frees the reference.
/// </summary>
[Collection("Database")]
public sealed class SpeedReadingTransferReferencePostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_reference_already_claimed_by_another_account_is_refused_until_that_claim_is_rejected()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var product = Guid.NewGuid();
            var plan = Guid.NewGuid();
            var now = DateTime.UtcNow;
            Add("LegacyProduct", new { Id = product, Slug = "speed-reading", Name = "Speed", IsActive = true, IsPublic = true, CreatedAt = now, UpdatedAt = now });
            Add("LegacySubscriptionPlan", new { Id = plan, ProductId = product, Slug = "annual", Name = "Annual", Price = 499m, IsActive = true, IsPublic = true, BillingPeriod = "Annual", DurationDays = 365, CreatedAt = now });
            Add("LegacyBankTransferPaymentSettings", new { Id = Guid.Parse("4c18b5a8-96e8-4af6-9195-0d4fdde3dcf2"), AccountHolder = "Test", BankName = "Bank", Iban = "TR330006100519786457841326", IsEnabled = true, CreatedAt = now });
            await db.SaveChangesAsync();
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Legacy.LegacySpeedReadingSubscription")!;
            var service = (ISpeedReadingSubscription)Activator.CreateInstance(serviceType,
                BindingFlags.Instance | BindingFlags.NonPublic, null, [db, db, null, null, new Directory()], null)!;

            var claimed = await service.CreateBankTransferPaymentRequestAsync(first,
                new CreateBankTransferPaymentRequest(plan, "eft-shared-01", "Veli", null, true), "reference-first-key-0001");
            Assert.NotNull(claimed);
            db.ChangeTracker.Clear();

            var duplicate = () => service.CreateBankTransferPaymentRequestAsync(second,
                new CreateBankTransferPaymentRequest(plan, "EFT-SHARED-01", "Veli", null, true), "reference-second-key-0001");
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(duplicate);
            Assert.Equal("BankTransfer.ReferenceInUse", refused.Code);

            await service.ReviewBankTransferPaymentRequestAsync(claimed!.Id, new ReviewBankTransferPaymentRequest("rejected", "Yanlış referans"),
                Guid.NewGuid(), "reference-review-key-0001");
            db.ChangeTracker.Clear();
            Assert.NotNull(await service.CreateBankTransferPaymentRequestAsync(second,
                new CreateBankTransferPaymentRequest(plan, "EFT-SHARED-01", "Veli", null, true), "reference-second-key-0002"));

            void Add(string name, object values)
            {
                var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType($"SpeedReading.Infrastructure.Legacy.{name}")!;
                var entity = Activator.CreateInstance(type)!;
                foreach (var property in values.GetType().GetProperties())
                    type.GetProperty(property.Name)!.SetValue(entity, property.GetValue(values));
                db.Add(entity);
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Directory : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse(userIds.Select(id => new SpeedReadingUserDirectoryItem(id, "Test", "Veli", true) { Email = $"{id:N}@example.invalid" }).ToArray()));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
