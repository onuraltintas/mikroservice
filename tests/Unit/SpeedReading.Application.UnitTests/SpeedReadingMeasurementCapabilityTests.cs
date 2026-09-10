using FluentAssertions;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingMeasurementCapabilityTests
{
    [Theory]
    [InlineData("VisualExpansion")]
    [InlineData("Focus")]
    [InlineData("Schulte")]
    [InlineData("ReadingComprehension")]
    public void Server_validated_engines_are_eligible_for_assessment(string typeName)
    {
        SpeedReadingMeasurementCapabilities.IsAssessmentEligible(typeName).Should().BeTrue();
    }

    [Theory]
    [InlineData("MotionPath")]
    [InlineData("EyeTracking")]
    [InlineData("Unknown")]
    public void Observation_only_engines_are_not_eligible_for_assessment(string typeName)
    {
        SpeedReadingMeasurementCapabilities.IsAssessmentEligible(typeName).Should().BeFalse();
    }

    [Fact]
    public void Catalog_explains_visual_expansion_and_observation_only_status()
    {
        SpeedReadingMeasurementCapabilities.Definitions
            .Should().Contain(item => item.Code == "visual_expansion" && item.IsAssessmentEligible);
        SpeedReadingMeasurementCapabilities.Definitions
            .Should().Contain(item => item.Code == "motion_path" && !item.IsAssessmentEligible);
    }
}
