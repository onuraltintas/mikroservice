using EduPlatform.Shared.Contracts.Reporting;

namespace SpeedReading.Application.Analytics;

public sealed record TeacherStudentPerformance(
    string StudentIdentifier,
    decimal AverageWpm,
    decimal AverageComprehension,
    int ActivitiesCompleted,
    int TotalMinutes,
    string PerformanceLevel);

public sealed record TeacherClassOverviewAnalytics(
    DateTime DateFrom,
    DateTime DateTo,
    int TotalStudents,
    int ActiveStudents,
    bool ActiveStudentsDataAvailable,
    bool ClassAverageWpmDataAvailable,
    bool ClassAverageComprehensionDataAvailable,
    decimal ClassAverageWpm,
    decimal ClassAverageComprehension,
    int TotalActivitiesCompleted,
    int StudentsAboveAverage,
    int StudentsAtAverage,
    int StudentsBelowAverage,
    IReadOnlyList<TeacherStudentPerformance> TopPerformers,
    IReadOnlyList<TeacherStudentPerformance> StudentsNeedingSupport);

public sealed record TeacherAssignmentInfo(
    string AssignmentId,
    string Title,
    string Description,
    DateTime DueDate,
    DateTime AssignedDate);

public sealed record TeacherAssignmentCompletionStats(
    int TotalStudents,
    int Completed,
    int InProgress,
    int NotStarted,
    decimal CompletionRate);

public sealed record TeacherAssignmentPerformanceStats(
    decimal AverageScore,
    decimal MedianScore,
    decimal HighestScore,
    decimal LowestScore,
    decimal StandardDeviation);

public sealed record TeacherAssignmentStudentBreakdown(
    Guid StudentId,
    string StudentName,
    string Status,
    decimal? Score,
    int? CompletionTime,
    DateTime? SubmittedAt);

public sealed record TeacherAssignmentTimeStats(
    decimal AverageCompletionTime,
    decimal MedianCompletionTime,
    decimal FastestCompletion,
    decimal SlowestCompletion);

public sealed record TeacherAssignmentAnalytics(
    DateTime DateFrom,
    DateTime DateTo,
    bool DataAvailable,
    string? UnavailableReason,
    TeacherAssignmentInfo? AssignmentInfo,
    TeacherAssignmentCompletionStats? CompletionStats,
    TeacherAssignmentPerformanceStats? PerformanceStats,
    IReadOnlyList<AdminAnalyticsChartData> ScoreDistribution,
    IReadOnlyList<TeacherAssignmentStudentBreakdown> StudentBreakdown,
    TeacherAssignmentTimeStats? TimeStats);

public sealed record TeacherContentAnalysisAnalytics(
    DateTime DateFrom,
    DateTime DateTo,
    IReadOnlyList<AdminExerciseTypeAnalysis> ExerciseAnalysis,
    IReadOnlyList<AdminAnalyticsChartData> ExerciseFrequencyChart,
    IReadOnlyList<AdminReadingLevelAnalysis> ReadingAnalysis,
    IReadOnlyList<AdminAnalyticsChartData> ReadingPerformanceChart);

public sealed record TeacherTimeProgressAnalytics(
    DateTime DateFrom,
    DateTime DateTo,
    IReadOnlyList<AdminAnalyticsChartData> WeeklyProgressChart,
    IReadOnlyList<AdminAnalyticsChartData> MonthlyProgressChart,
    IReadOnlyList<AdminAnalyticsChartData> ActivityIntensityChart,
    IReadOnlyList<TeacherProgressStudent> ImprovingStudents,
    IReadOnlyList<TeacherProgressStudent> DecliningStudents);

public sealed record TeacherProgressStudent(
    Guid StudentId,
    string StudentName,
    decimal PreviousScore,
    decimal CurrentScore,
    decimal Improvement,
    string Trend);

public interface ILegacySpeedReadingTeacherReports
{
    Task<TeacherClassOverviewAnalytics> GetClassOverviewAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<TeacherAssignmentAnalytics> GetAssignmentsAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<TeacherContentAnalysisAnalytics> GetContentAnalysisAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<TeacherTimeProgressAnalytics> GetTimeProgressAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);
}
