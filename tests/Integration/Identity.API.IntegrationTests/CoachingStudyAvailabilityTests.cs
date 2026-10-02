using Coaching.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudyAvailabilityTests
{
    [Fact]
    public void Availability_AllowsSeparateAndAdjacentWindows()
    {
        var availability = StudyAvailability.Create(Guid.NewGuid(), "Europe/Istanbul");
        availability.ReplaceWindows([new(DayOfWeek.Monday, 480, 540), new(DayOfWeek.Monday, 540, 600), new(DayOfWeek.Tuesday, 480, 540)]);
        Assert.Equal(180, availability.Windows.Sum(x => x.EndMinute - x.StartMinute));
        Assert.Equal("Europe/Istanbul", availability.TimeZoneId);
        availability.ReplaceWindows([]);
        Assert.Empty(availability.Windows);
    }

    [Fact]
    public void Availability_RejectsOverlapWithoutErasingExistingPreferences()
    {
        var availability = StudyAvailability.Create(Guid.NewGuid(), "Europe/Istanbul");
        availability.ReplaceWindows([new(DayOfWeek.Monday, 480, 540)]);
        Assert.Throws<ArgumentException>(() => availability.ReplaceWindows([
            new(DayOfWeek.Monday, 480, 540), new(DayOfWeek.Monday, 530, 600)]));
        Assert.Single(availability.Windows);
    }

    [Theory]
    [InlineData(-1, 60)]
    [InlineData(60, 60)]
    [InlineData(60, 1441)]
    public void Window_RejectsInvalidLocalTimeRange(int start, int end)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StudyAvailabilityWindow(DayOfWeek.Monday, start, end));
    }

    [Fact]
    public void Availability_RequiresValidOwnerAndTimeZone()
    {
        Assert.Throws<ArgumentException>(() => StudyAvailability.Create(Guid.Empty, "Europe/Istanbul"));
        Assert.Throws<TimeZoneNotFoundException>(() => StudyAvailability.Create(Guid.NewGuid(), "Not/AZone"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StudyAvailabilityWindow((DayOfWeek)7, 0, 60));
    }
}
