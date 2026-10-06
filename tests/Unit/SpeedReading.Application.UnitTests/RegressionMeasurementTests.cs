using System.Reflection;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class RegressionMeasurementTests
{
    [Fact]
    public void Automatic_regression_pacer_is_not_a_reading_speed_measurement()
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var state = type.GetMethod("DeserializeState", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
            ["""{"engineType":"regression_reduction","exerciseTypeName":"RegressionReduction","wordCount":100,"readingStartTime":"2026-10-06T10:00:00Z","readingEndTime":"2026-10-06T10:00:30Z"}"""])!;
        var measured = (bool)type.GetMethod("SupportsServerReadingMeasurement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [state])!;
        Assert.False(measured);
    }
}
