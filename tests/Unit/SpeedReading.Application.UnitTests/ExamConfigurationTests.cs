using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

public sealed class ExamConfigurationTests
{
    [Theory]
    [InlineData("{\"content\":{\"minWordCount\":650,\"maxWordCount\":540}}")]
    [InlineData("{\"content\":{\"questionCount\":0}}")]
    [InlineData("{\"content\":{\"questionCount\":51}}")]
    [InlineData("{\"timing\":{\"minReadingTimeMs\":10000,\"maxReadingTimeMs\":10000}}")]
    [InlineData("{\"timing\":{\"maxReadingTimeMs\":0}}")]
    [InlineData("{\"timing\":{\"minReadingTimeMs\":180000}}")]
    public void RejectsInvalidExamProtocol(string settings)
    {
        var configuration = "{\"engineType\":\"exam_simulation\",\"engineConfig\":" + settings + "}";
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(configuration, "exam_simulation"));
    }

    [Fact]
    public void AcceptsBoundedProtocolAndHistoricalQuestionOnlyConfiguration()
    {
        ExerciseConfigurationRules.ValidateActiveConfiguration("""{"engineType":"exam_simulation","engineConfig":{"content":{"minWordCount":540,"maxWordCount":650,"questionCount":8},"timing":{"minReadingTimeMs":10000,"maxReadingTimeMs":180000,"questionTimeSeconds":48}}}""", "exam_simulation");
        ExerciseConfigurationRules.ValidateActiveConfiguration("""{"engineType":"exam_simulation","engineConfig":{"timing":{"questionTimeSeconds":60}}}""", "exam_simulation");
    }
}
