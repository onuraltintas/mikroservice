using FluentAssertions;
using SpeedReading.Domain.AgeGroups;

namespace SpeedReading.Application.UnitTests;

public sealed class AgeGroupRangeRulesTests
{
    [Theory]
    [InlineData(6, 9, 10, 13, false)]
    [InlineData(6, 10, 10, 13, true)]
    [InlineData(14, null, 18, null, true)]
    [InlineData(0, 5, 6, null, false)]
    public void Detects_overlapping_age_ranges(
        int firstMin,
        int? firstMax,
        int secondMin,
        int? secondMax,
        bool expected)
    {
        AgeGroupRangeRules.Overlaps(firstMin, firstMax, secondMin, secondMax)
            .Should().Be(expected);
    }
}
