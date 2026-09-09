using FluentAssertions;
using SpeedReading.Application.Assessment;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingAssessmentMeasurementRulesTests
{
    [Fact]
    public void Level_dictionary_is_ordered_and_has_matching_speed_and_comprehension_bands()
    {
        var definitions = SpeedReadingLevelRules.Definitions;

        definitions.Should().HaveCount(8);
        definitions.Select(item => item.Level).Should().Equal(Enumerable.Range(1, 8));
        definitions.Select(item => item.MinimumWpm).Should().BeInAscendingOrder();
        definitions.Select(item => item.MinimumComprehension).Should().BeInAscendingOrder();
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(100, 40, 2)]
    [InlineData(500, 90, 8)]
    public void Shared_level_dictionary_drives_both_placement_dimensions(
        decimal wpm,
        decimal comprehension,
        int expectedLevel)
    {
        SpeedReadingLevelRules.CalculateSpeedLevel(wpm).Should().Be(expectedLevel);
        SpeedReadingLevelRules.CalculateComprehensionLevel(comprehension).Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData(600, 0, 1)]
    [InlineData(600, 50, 2)]
    [InlineData(300, 80, 6)]
    [InlineData(600, 95, 8)]
    public void Level_is_limited_by_comprehension_as_well_as_speed(
        decimal averageWpm,
        decimal averageComprehension,
        int expectedLevel)
    {
        SpeedReadingAssessmentMeasurementRules.CalculateLevel(
            averageWpm,
            averageComprehension).Should().Be(expectedLevel);
    }

    [Fact]
    public void Invalid_measurements_are_bounded_before_level_calculation()
    {
        SpeedReadingAssessmentMeasurementRules.CalculateLevel(-10, 150)
            .Should().Be(1);
        SpeedReadingAssessmentMeasurementRules.CalculateLevel(300, 150)
            .Should().Be(6);
    }
}
