using System.Reflection;
using EduPlatform.Shared.Contracts.Reporting;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingAdultPayerPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task NewRequestsPersistDeclarationAndRetriesPreserveItsTimestamp()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var product = Guid.NewGuid();
            var plan = Guid.NewGuid();
            var now = DateTime.UtcNow;
            Add("LegacyProduct", new { Id = product, Slug = "speed-reading", Name = "Speed", IsActive = true, IsPublic = true, CreatedAt = now, UpdatedAt = now });
            Add("LegacySubscriptionPlan", new { Id = plan, ProductId = product, Slug = "annual", Name = "Annual", Price = 499m, IsActive = true, IsPublic = true, BillingPeriod = "Annual", DurationDays = 365, CreatedAt = now });
            Add("LegacyBankTransferPaymentSettings", new { Id = Guid.Parse("4c18b5a8-96e8-4af6-9195-0d4fdde3dcf2"), AccountHolder = "Test", BankName = "Bank", Iban = "TR330006100519786457841326", IsEnabled = true, CreatedAt = now });
            await db.SaveChangesAsync();
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Legacy.LegacySpeedReadingSubscription")!;
            var service = (ISpeedReadingSubscription)Activator.CreateInstance(serviceType,
                BindingFlags.Instance | BindingFlags.NonPublic, null, [db, db, null, null, new Directory(user)], null)!;
            var request = new CreateBankTransferPaymentRequest(plan, "EFT-ADULT-01", "Adult", null, true);
            Assert.Null(await service.CreateBankTransferPaymentRequestAsync(user, request with { AdultPayerDeclaration = false }, "adult-denied-key-0001"));
            var created = await service.CreateBankTransferPaymentRequestAsync(user, request, "adult-create-key-0001");
            Assert.NotNull(created);
            Assert.Equal(1, created.AdultPayerDeclarationVersion);
            Assert.InRange(created.AdultPayerDeclaredAt!.Value, now, DateTime.UtcNow);
            db.ChangeTracker.Clear();
            var persisted = Assert.Single(await service.GetMyBankTransferPaymentRequestsAsync(user));
            var replay = await service.CreateBankTransferPaymentRequestAsync(user, request, "adult-create-key-0001");
            Assert.Equal(created.Id, replay!.Id);
            Assert.Equal(persisted.AdultPayerDeclaredAt, replay.AdultPayerDeclaredAt);
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM speed_reading.bank_transfer_payment_requests").SingleAsync());

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

    private sealed class Directory(Guid user) : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse([new(user, "Test", "Adult", true) { Email = "adult@example.invalid" }]));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
