using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// H21 (result audit 30.10.2026): comprehension is only a number when the student answered questions about a text;
/// exercises without questions (Schulte, tachistoscope, Hedef Avı…) and assessment results never lower the student's
/// comprehension average. Adaptive fluency accuracy follows the transfer text, and the scanning penalty is capped like
/// the other search exercises.
/// </summary>
public sealed class SpeedReadingResultCalculationTests
{
    private static readonly Assembly Infrastructure = typeof(OwnedSpeedReadingDbContext).Assembly;

    [Fact]
    public async Task Summary_comprehension_ignores_exercises_without_questions_and_assessments()
    {
        await using var db = Context();
        var student = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Program", "", Guid.NewGuid(), 0, 100,
            "{}", 1, 2, 5, 4, 28, true, 1, 0, null, false, now, null, null, null);
        db.ProgramTemplates.Add(template);
        db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), student, template, 0, 0, student, now, true));
        var text = Guid.NewGuid();
        db.ExerciseSessionResults.AddRange(
            Result(student, text, 90, Answers(true)),
            Result(student, null, 0, "[]"),
            Result(student, null, 0, "[]"),
            Result(student, text, 0, "[]"),
            Result(student, text, 10, Answers(false), assessment: true));
        await db.SaveChangesAsync();

        var summary = await DailyProgress(db).GetProgressSummaryAsync(student);

        summary!.AverageSuccessRate.Should().Be(90);
    }

    [Fact]
    public void Daily_log_result_data_has_no_comprehension_without_questions()
    {
        var method = Infrastructure.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDailyProgress")!
            .GetMethod("CreateAuthoritativeResultData", BindingFlags.NonPublic | BindingFlags.Static)!;
        var student = Guid.NewGuid();

        var withoutQuestions = JsonDocument.Parse((string)method.Invoke(null, [Result(student, null, 0, "[]"), true, 85m])!).RootElement;
        var withQuestions = JsonDocument.Parse((string)method.Invoke(null, [Result(student, Guid.NewGuid(), 75, Answers(true)), true, 75m])!).RootElement;

        withoutQuestions.GetProperty("comprehensionScore").ValueKind.Should().Be(JsonValueKind.Null);
        withoutQuestions.GetProperty("score").GetDecimal().Should().Be(85);
        withQuestions.GetProperty("comprehensionScore").GetDecimal().Should().Be(75);
    }

    [Theory]
    [InlineData(3, 1, null, 75)]
    [InlineData(3, 1, 50.0, 50)]
    public void Adaptive_fluency_accuracy_follows_the_transfer_text(int correct, int incorrect, double? transfer, double expected) =>
        SpeedReadingExerciseSessionRules.CalculateResultAccuracy(correct, incorrect, (decimal?)transfer).Should().Be((decimal)expected);

    [Theory]
    [InlineData(100, 0, 100)]
    [InlineData(100, 2, 80)]
    [InlineData(100, 9, 70)]
    [InlineData(20, 9, 0)]
    public void Scanning_penalty_is_capped_at_thirty_points(double accuracy, int incorrect, double expected) =>
        SpeedReadingExerciseSessionRules.CalculateScanningScore((decimal)accuracy, incorrect).Should().Be((decimal)expected);

    private static ISpeedReadingDailyProgress DailyProgress(OwnedSpeedReadingDbContext db)
    {
        var paths = (ILegacySpeedReadingLearningPaths)Activator.CreateInstance(
            Infrastructure.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingLearningPaths")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        return (ISpeedReadingDailyProgress)Activator.CreateInstance(
            Infrastructure.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDailyProgress")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db, paths], null)!;
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static string Answers(bool correct) => JsonSerializer.Serialize(new[] { new { IsCorrect = correct } });

    private static ExerciseSessionResult Result(Guid student, Guid? text, decimal comprehension, string answers, bool assessment = false) =>
        ExerciseSessionResult.Create(Guid.NewGuid(), Guid.NewGuid(), student, Guid.NewGuid(), text, 200, 60, 200,
            comprehension, 0, comprehension, DateTime.UtcNow, answers, "[]", assessment, true);
}
