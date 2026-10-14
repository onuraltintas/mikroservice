using System.Reflection;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;
using Xunit;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingSubscriptionRequirementTests
{
    [Fact]
    public async Task Default_requires_subscription_and_switch_can_be_turned_off_and_back_on()
    {
        using var db = Database();
        var service = Service(db); var user = Guid.NewGuid(); var actor = Guid.NewGuid();
        Assert.True((await service.GetSubscriptionRequirementAsync()).RequireActiveSubscription);
        Assert.False((await service.GetMyAccessAsync(user)).HasSpeedReading);
        await service.UpdateSubscriptionRequirementAsync(false, actor, "requirement-off-test-0001");
        db.ChangeTracker.Clear();
        var open = await Service(db).GetMyAccessAsync(user);
        Assert.True(open.HasSpeedReading);
        Assert.False(open.HasCoaching);
        Assert.Contains("hizliokuma", open.Products);
        Assert.Empty(await service.GetMySubscriptionsAsync(user));
        await service.UpdateSubscriptionRequirementAsync(true, actor, "requirement-on-test-00001");
        db.ChangeTracker.Clear();
        Assert.False((await Service(db).GetMyAccessAsync(user)).HasSpeedReading);
    }

    [Fact]
    public async Task Requirement_update_preserves_bank_settings_and_is_idempotent()
    {
        using var db = Database(); var service = Service(db); var actor = Guid.NewGuid();
        await service.UpdateBankTransferSettingsAsync(new("Test", "Bank", "TR330006100519786457841326", "Original instructions", true),
            actor, "bank-settings-test-000001");
        var bank = await service.GetBankTransferSettingsAsync();
        var first = await service.UpdateSubscriptionRequirementAsync(false, actor, "requirement-same-key-0001");
        db.ChangeTracker.Clear();
        var replay = await Service(db).UpdateSubscriptionRequirementAsync(false, actor, "requirement-same-key-0001");
        Assert.Equal(first.UpdatedAt, replay.UpdatedAt);
        var after = await service.GetBankTransferSettingsAsync();
        Assert.Equal(bank!.AccountHolder, after!.AccountHolder);
        Assert.Equal(bank.BankName, after.BankName); Assert.Equal(bank.Iban, after.Iban);
        Assert.Equal(bank.Instructions, after.Instructions); Assert.Equal(bank.IsEnabled, after.IsEnabled);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateSubscriptionRequirementAsync(true, actor, "requirement-same-key-0001"));
        Assert.False((await service.GetSubscriptionRequirementAsync()).RequireActiveSubscription);
    }

    private static OwnedSpeedReadingDbContext Database() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ISpeedReadingSubscription Service(OwnedSpeedReadingDbContext db) =>
        (ISpeedReadingSubscription)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Legacy.LegacySpeedReadingSubscription")!,
            BindingFlags.Instance | BindingFlags.NonPublic, null, [db, db, null, null, null], null)!;
}
