using FluentAssertions;
using SpeedReading.Domain.Profiles;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingUserProfileTests
{
    [Fact]
    public void UpdateSettings_persists_profile_setup_values()
    {
        var userId = Guid.NewGuid();
        var ageGroupId = Guid.NewGuid();
        var profile = SpeedReadingUserProfile.CreateDefault(
            Guid.NewGuid(),
            userId,
            DateTime.UtcNow,
            userId.ToString());

        profile.UpdateSettings(
            currentLevel: 1,
            targetWpm: 325,
            targetComprehension: 85,
            dailyGoalMinutes: 30,
            ageGroupConfigurationId: ageGroupId,
            actorId: userId,
            at: DateTime.UtcNow);

        profile.CurrentLevel.Should().Be(1);
        profile.TargetWPM.Should().Be(325);
        profile.TargetComprehension.Should().Be(85);
        profile.DailyGoalMinutes.Should().Be(30);
        profile.AgeGroupConfigurationId.Should().Be(ageGroupId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(481)]
    public void UpdateSettings_rejects_daily_goals_outside_domain_limits(int dailyGoalMinutes)
    {
        var userId = Guid.NewGuid();
        var profile = SpeedReadingUserProfile.CreateDefault(
            Guid.NewGuid(),
            userId,
            DateTime.UtcNow,
            userId.ToString());

        var act = () => profile.UpdateSettings(
            currentLevel: 1,
            targetWpm: 250,
            targetComprehension: 75,
            dailyGoalMinutes,
            ageGroupConfigurationId: null,
            actorId: userId,
            at: DateTime.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
