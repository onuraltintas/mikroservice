using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

public sealed class SkimmingConfigurationTests
{
    [Theory]
    [InlineData(3000, 2000)]
    [InlineData(3000, 3000)]
    [InlineData(0, 0)]
    public void RejectsInvalidInspectionWindow(int minimum, int maximum)
    {
        var configuration = $$$"""{"engineType":"skimming","timing":{"minReadingTimeMs":{{{minimum}}},"maxReadingTimeMs":{{{maximum}}}}}""";
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(configuration, "skimming"));
    }

    [Fact]
    public void AcceptsMainIdeaInspectionWithoutSearchTargets()
    {
        ExerciseConfigurationRules.ValidateActiveConfiguration("""{"engineType":"skimming","timing":{"minReadingTimeMs":3000,"maxReadingTimeMs":90000},"visuals":{"fontSize":"24px"}}""", "skimming");
    }
}
