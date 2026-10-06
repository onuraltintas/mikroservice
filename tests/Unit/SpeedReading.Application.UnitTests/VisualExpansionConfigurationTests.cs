using SpeedReading.Domain.Catalog;
using Xunit;

namespace SpeedReading.Application.UnitTests;

public sealed class VisualExpansionConfigurationTests
{
    [Theory]
    [InlineData("""{"engineType":"visual_expansion","expansion":{"pattern":"random"}}""")]
    [InlineData("""{"engineType":"visual_expansion","content":{"stimulusType":"html"}}""")]
    public void Rejects_unimplemented_patterns_and_stimulus_types(string configuration)
    {
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(configuration, "visual_expansion"));
    }
}
