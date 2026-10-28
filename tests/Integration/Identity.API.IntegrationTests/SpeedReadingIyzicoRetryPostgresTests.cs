using System.Reflection;
using EduPlatform.Shared.Contracts.Reporting;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Payments;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Review H1 (finding 5): production runs the speed-reading context with EnableRetryOnFailure. A successful card
/// payment opens a transaction to attach the subscription; it must run inside the execution strategy, otherwise
/// EF refuses it after the customer has paid and no subscription is created.
/// </summary>
[Collection("Database")]
public sealed class SpeedReadingIyzicoRetryPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_successful_payment_creates_the_subscription_when_the_context_retries_on_failure()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString, builder => builder.EnableRetryOnFailure(2, TimeSpan.Zero, null)).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var product = Guid.NewGuid();
            var plan = Guid.NewGuid();
            var payment = Guid.NewGuid();
            var now = DateTime.UtcNow;
            Add("LegacyProduct", new { Id = product, Slug = "speed-reading", Name = "Speed", IsActive = true, IsPublic = true, CreatedAt = now, UpdatedAt = now });
            Add("LegacySubscriptionPlan", new { Id = plan, ProductId = product, Slug = "monthly", Name = "Monthly", Price = 100m, IsActive = true, IsPublic = true, BillingPeriod = "Monthly", DurationDays = 30, CreatedAt = now });
            Add("LegacyPayment", new { Id = payment, UserId = user, UserEmail = "payer@example.invalid", UserName = "Payer", PlanId = plan, Amount = 100m, Currency = "TRY",
                Status = SpeedReadingPaymentRules.PendingStatus, Provider = "Iyzico", ProviderToken = "checkout-token", CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Legacy.LegacySpeedReadingSubscription")!;
            var service = (ISpeedReadingSubscription)Activator.CreateInstance(serviceType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                [db, db, new PaidProvider(payment, plan), new IyzicoOptions(), new Directory()], null)!;

            var result = await service.VerifyPaymentAsync(user, "checkout-token");

            Assert.True(result.Success);
            Assert.NotNull(result.SubscriptionId);
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM speed_reading.user_subscriptions").SingleAsync());

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

    private sealed class PaidProvider(Guid payment, Guid plan) : ISpeedReadingPaymentProvider
    {
        public bool IsConfigured => true;
        public Task<PaymentProviderInitializationResult> InitializeAsync(PaymentProviderInitializationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<PaymentProviderRetrieveResult> RetrieveAsync(PaymentProviderRetrieveRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentProviderRetrieveResult(true, true, "SUCCESS", 1, "provider-payment-1", "TRY",
                $"speed-reading-{plan:N}", payment.ToString("N"), 100m, 100m, "checkout-token", null, "{}"));
    }

    private sealed class Directory : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse([]));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
