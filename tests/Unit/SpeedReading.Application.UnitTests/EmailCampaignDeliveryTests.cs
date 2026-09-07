using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Notifications;
using SpeedReading.Infrastructure;

namespace SpeedReading.Application.UnitTests;

public sealed class EmailCampaignDeliveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sending_without_a_delivery_worker_is_rejected_before_changing_campaign_state(bool sendNow)
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;
        await using var context = new SpeedReadingDbContext(options);
        var campaignType = typeof(SpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Legacy.LegacySpeedReadingEmailCampaigns", throwOnError: true)!;
        var campaigns = (ISpeedReadingEmailCampaigns)Activator.CreateInstance(campaignType, context)!;

        var send = () => campaigns.SendAsync(Guid.NewGuid(), new SendEmailCampaignRequest(sendNow), default);

        await send.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Campaign delivery is not available. No emails have been sent or scheduled.");
        context.ChangeTracker.HasChanges().Should().BeFalse();
    }
}
