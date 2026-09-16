namespace SpeedReading.Application.Analytics;

/// <summary>
/// Keeps teacher analytics calculations independent from the persistence query shape.
/// Reading measurements are the source of speed and comprehension metrics; exercises
/// contribute to activity volume and use their own success metric.
/// </summary>
public sealed record TeacherMetricSample(
    Guid StudentId,
    int ActivityCount,
    decimal AverageWpm,
    decimal AverageComprehension,
    int TotalSeconds,
    bool IsReading,
    int? ComprehensionActivityCount = null,
    int? MeasuredActivityCount = null);

public sealed record TeacherStudentMetricSummary(
    Guid StudentId,
    int ReadingActivities,
    int ExerciseActivities,
    int ComprehensionActivities,
    int MeasuredReadingActivities,
    decimal AverageWpm,
    decimal AverageComprehension,
    int TotalSeconds)
{
    public int TotalActivities => ReadingActivities + ExerciseActivities;
}

public sealed record TeacherClassMetricSummary(
    IReadOnlyList<Guid> ActiveStudentIds,
    IReadOnlyList<TeacherStudentMetricSummary> ReadingStudents,
    int TotalActivitiesCompleted,
    decimal ClassAverageWpm,
    decimal ClassAverageComprehension,
    int StudentsAboveAverage,
    int StudentsAtAverage,
    int StudentsBelowAverage);

public sealed record TeacherProgressSample(
    Guid StudentId,
    DateTime Date,
    decimal Score,
    bool IsReading);

public sealed record TeacherProgressMetric(
    Guid StudentId,
    decimal PreviousScore,
    decimal CurrentScore,
    decimal Improvement,
    string Metric);

public static class TeacherAnalyticsRules
{
    public static TeacherClassMetricSummary Summarize(IEnumerable<TeacherMetricSample> samples)
    {
        var rows = samples
            .Where(item => item.StudentId != Guid.Empty && item.ActivityCount > 0)
            .GroupBy(item => item.StudentId)
            .Select(group =>
            {
                var reading = group.Where(item => item.IsReading).ToArray();
                var exercises = group.Where(item => !item.IsReading).ToArray();
                var readingActivities = reading.Sum(item => item.ActivityCount);
                var comprehensionActivities = reading.Sum(item => item.ComprehensionActivityCount ?? item.ActivityCount);
                var measuredReadingActivities = reading.Sum(item => item.MeasuredActivityCount ?? item.ActivityCount);
                return new TeacherStudentMetricSummary(
                    group.Key,
                    readingActivities,
                    exercises.Sum(item => item.ActivityCount),
                    comprehensionActivities,
                    measuredReadingActivities,
                    readingActivities > 0
                        ? measuredReadingActivities > 0
                            ? reading.Sum(item => item.AverageWpm * (item.MeasuredActivityCount ?? item.ActivityCount)) / measuredReadingActivities
                            : 0
                        : 0,
                    comprehensionActivities > 0
                        ? reading.Sum(item => item.AverageComprehension * (item.ComprehensionActivityCount ?? item.ActivityCount)) / comprehensionActivities
                        : 0,
                    group.Sum(item => item.TotalSeconds));
            })
            .ToList();

        var readingActivitiesTotal = rows.Sum(item => item.MeasuredReadingActivities);
        var classAverageWpm = readingActivitiesTotal > 0
            ? rows.Sum(item => item.AverageWpm * item.MeasuredReadingActivities) / readingActivitiesTotal
            : 0;
        var comprehensionActivitiesTotal = rows.Sum(item => item.ComprehensionActivities);
        var classAverageComprehension = comprehensionActivitiesTotal > 0
            ? rows.Sum(item => item.AverageComprehension * item.ComprehensionActivities) / comprehensionActivitiesTotal
            : 0;
        var readingStudents = rows.Where(item => item.ReadingActivities > 0).ToList();
        var measuredReadingStudents = readingStudents.Where(item => item.MeasuredReadingActivities > 0).ToList();

        return new TeacherClassMetricSummary(
            rows.Select(item => item.StudentId).ToArray(),
            readingStudents,
            rows.Sum(item => item.TotalActivities),
            Math.Round(classAverageWpm, 2),
            Math.Round(classAverageComprehension, 2),
            measuredReadingStudents.Count(item => item.AverageWpm > classAverageWpm),
            measuredReadingStudents.Count(item => item.AverageWpm == classAverageWpm),
            measuredReadingStudents.Count(item => item.AverageWpm < classAverageWpm));
    }

    public static IReadOnlyList<TeacherProgressMetric> CalculateProgress(
        IEnumerable<TeacherProgressSample> samples,
        DateTime midpoint)
    {
        var result = new List<TeacherProgressMetric>();
        foreach (var group in samples
                     .Where(item => item.StudentId != Guid.Empty)
                     .GroupBy(item => item.StudentId))
        {
            var reading = group.Where(item => item.IsReading).ToArray();
            var source = reading.Length > 0 ? reading : group.ToArray();
            var previous = source.Where(item => item.Date < midpoint).Select(item => item.Score).ToArray();
            var current = source.Where(item => item.Date >= midpoint).Select(item => item.Score).ToArray();
            if (previous.Length == 0 || current.Length == 0)
                continue;

            var previousScore = previous.Average();
            var currentScore = current.Average();
            result.Add(new TeacherProgressMetric(
                group.Key,
                previousScore,
                currentScore,
                Math.Round(currentScore - previousScore, 2),
                reading.Length > 0 ? "comprehension" : "exercise_success"));
        }

        return result;
    }
}
