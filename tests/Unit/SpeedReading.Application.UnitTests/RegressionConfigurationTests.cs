using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

public sealed class RegressionConfigurationTests
{
    [Theory]
    [InlineData("contingent")]
    [InlineData("ior")]
    public void RejectsUnimplementedMaskingModes(string mode)
    {
        var configuration = $$$"""{"engineType":"regression_reduction","engineConfig":{"maskingType":"{{{mode}}}"}}""";
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(configuration, "regression_reduction"));
    }
}
