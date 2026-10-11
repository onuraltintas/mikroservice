using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

public sealed class SkimmingConfigurationTests
{
    [Theory]
    [InlineData("{\"minWordCount\":300,\"maxWordCount\":200,\"questionCount\":4}")]
    [InlineData("{\"minWordCount\":200,\"maxWordCount\":300,\"questionCount\":0}")]
    [InlineData("{\"minWordCount\":200,\"maxWordCount\":300,\"questionCount\":51}")]
    public void Rejects_invalid_skimming_content_protocol(string content)
    {
        var configuration = "{\"engineType\":\"skimming\",\"engineConfig\":{\"content\":" + content + "}}";
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(configuration, "skimming"));
    }
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

    [Theory]
    [InlineData("{\"engineType\":\"skimming\",\"timing\":{\"minReadingTimeMs\":100000}}")]
    [InlineData("{\"engineType\":\"skimming\",\"timeLimitSeconds\":2}")]
    public void RejectsInvalidEffectiveDefaultWindow(string configuration)
    {
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(configuration, "skimming"));
    }
}
