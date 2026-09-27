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

    [Fact]
    public void UpdateSettings_persists_speed_reading_personal_profile_data()
    {
        var userId = Guid.NewGuid();
        var birthDate = new DateTime(2000, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var profile = SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), userId, DateTime.UtcNow);

        profile.UpdateSettings(
            currentLevel: 1,
            targetWpm: 250,
            targetComprehension: 75,
            dailyGoalMinutes: 20,
            ageGroupConfigurationId: Guid.NewGuid(),
            actorId: userId,
            at: DateTime.UtcNow,
            dateOfBirth: birthDate,
            learningStyle: "visual");

        profile.DateOfBirth.Should().Be(birthDate);
        profile.LearningStyle.Should().Be("visual");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void SetGradeLevel_accepts_school_grades_and_keeps_reading_level_separate(int gradeLevel)
    {
        var userId = Guid.NewGuid();
        var profile = SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), userId, DateTime.UtcNow);
        profile.ApplyAdaptiveLevel(4, userId, DateTime.UtcNow);

        profile.SetGradeLevel(gradeLevel, userId, DateTime.UtcNow);

        profile.GradeLevel.Should().Be(gradeLevel);
        profile.CurrentLevel.Should().Be(4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void SetGradeLevel_rejects_values_outside_school_grade_range(int gradeLevel)
    {
        var userId = Guid.NewGuid();
        var profile = SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), userId, DateTime.UtcNow);

        var act = () => profile.SetGradeLevel(gradeLevel, userId, DateTime.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
        profile.GradeLevel.Should().BeNull();
    }

    [Theory]
    [InlineData("mystical")]
    [InlineData("")]
    public void UpdateSettings_rejects_unsupported_learning_style(string learningStyle)
    {
        var userId = Guid.NewGuid();
        var profile = SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), userId, DateTime.UtcNow);

        var act = () => profile.UpdateSettings(
            currentLevel: 1,
            targetWpm: 250,
            targetComprehension: 75,
            dailyGoalMinutes: 20,
            ageGroupConfigurationId: null,
            actorId: userId,
            at: DateTime.UtcNow,
            learningStyle: learningStyle);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateSettings_rejects_a_birth_date_in_the_future()
    {
        var userId = Guid.NewGuid();
        var profile = SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), userId, DateTime.UtcNow);

        var act = () => profile.UpdateSettings(
            currentLevel: 1,
            targetWpm: 250,
            targetComprehension: 75,
            dailyGoalMinutes: 20,
            ageGroupConfigurationId: null,
            actorId: userId,
            at: DateTime.UtcNow,
            dateOfBirth: DateTime.UtcNow.AddDays(1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
