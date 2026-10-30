using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.LearningPaths;
using SpeedReading.Domain.Profiles;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Program revision decisions (30.10.2026, H15): speed may rise only while comprehension holds — a week moves the
/// difficulty up only when its average and the comprehension of the sessions that asked questions are both ≥80. The
/// learning path judges comprehension only from sessions that asked questions (finding A), picks content at the
/// student's training level (finding B), never recommends assessment-only exercises or exercise-bound texts
/// (finding C), and the weekly adjustment stays inside the program's real range (finding D).
/// </summary>
public sealed class SpeedReadingComprehensionGateTests
{
    private static readonly Assembly Infrastructure = typeof(OwnedSpeedReadingDbContext).Assembly;

    [Theory]
    [InlineData(85.0, 85.0, 1)]
    [InlineData(85.0, 80.0, 1)]
    [InlineData(85.0, 79.9, 0)]
    [InlineData(95.0, null, 0)]
    [InlineData(70.0, 95.0, 0)]
    [InlineData(59.9, 95.0, -1)]
    [InlineData(59.9, null, -1)]
    [InlineData(null, 90.0, 0)]
    public void Moving_up_needs_both_the_week_average_and_comprehension(double? average, double? comprehension, int expected) =>
        SpeedReadingDailyProgressRules.WeeklyDifficultyStep((decimal?)average, (decimal?)comprehension).Should().Be(expected);

    [Fact]
    public async Task Week_comprehension_counts_only_sessions_that_asked_questions()
    {
        await using var db = Context();
        var student = Guid.NewGuid();
        var text = Guid.NewGuid();
        var asked = Result(student, text, 90, Answers(true));
        var asked2 = Result(student, text, 70, Answers(false));
        var noQuestions = Result(student, null, 0, "[]");
        var readingWithoutQuestions = Result(student, text, 0, "[]");
        var otherWeek = Result(student, text, 10, Answers(false));
        db.ExerciseSessionResults.AddRange(asked, asked2, noQuestions, readingWithoutQuestions, otherWeek);
        await db.SaveChangesAsync();

        var average = await WeekComprehension(db, student,
            [asked.SessionId!.Value, asked2.SessionId!.Value, noQuestions.SessionId!.Value, readingWithoutQuestions.SessionId!.Value]);
        average.Should().Be(80);
        (await WeekComprehension(db, student, [noQuestions.SessionId!.Value])).Should().BeNull();
    }

    [Fact]
    public async Task Exercises_without_questions_do_not_open_a_support_path()
    {
        await using var db = Context();
        var (student, age) = await Student(db, currentLevel: 3, initial: 3, max: 4, offset: 0);
        var tachistoscope = await AddExercise(db, "Tachistoscope", "tachistoscope", 3, age);
        await AddExercise(db, "Tachistoscope", "tachistoscope", 3, age);
        var text = Guid.NewGuid();
        var at = DateTime.UtcNow.AddHours(-1);
        for (var index = 0; index < 3; index++)
        {
            db.ExerciseSessionResults.Add(Result(student, text, 90, Answers(true), at = at.AddMinutes(1)));
            db.ExerciseSessionResults.Add(Result(student, null, 0, "[]", at = at.AddMinutes(1), tachistoscope.Id, score: 50));
        }
        await db.SaveChangesAsync();

        (await Paths(db).RefreshAdaptiveProgressionAsync(student)).Should().BeFalse();
        db.PersonalizedLearningPathItems.Should().BeEmpty();
    }

    [Fact]
    public async Task Support_content_follows_the_training_level_not_the_assessment_level()
    {
        await using var db = Context();
        var (student, age) = await Student(db, currentLevel: 8, initial: 3, max: 4, offset: 1);
        for (var level = 1; level <= 5; level++)
            await AddText(db, $"Metin {level}", level, age);
        var at = DateTime.UtcNow.AddHours(-1);
        for (var index = 0; index < 3; index++)
            db.ExerciseSessionResults.Add(Result(student, Guid.NewGuid(), 40, Answers(false, bloom: 2), at = at.AddMinutes(1)));
        await db.SaveChangesAsync();

        (await Paths(db).GeneratePersonalizedPathAsync(student)).Should().BeGreaterThan(0);

        var levels = await db.PersonalizedLearningPathItems.Select(item => item.DifficultyLevel).ToListAsync();
        levels.Should().OnlyContain(level => level == 3 || level == 4);
    }

    [Fact]
    public async Task Support_path_skips_exercise_bound_texts_and_assessment_only_exercises()
    {
        await using var db = Context();
        var (student, age) = await Student(db, currentLevel: 3, initial: 3, max: 4, offset: 0);
        var bound = await AddExercise(db, "Scanning", "scanning", 3, age);
        await AddText(db, "Bağımsız", 3, age);
        await AddText(db, "Tarama listesi", 3, age, bound.Id);
        var at = DateTime.UtcNow.AddHours(-1);
        for (var index = 0; index < 3; index++)
            db.ExerciseSessionResults.Add(Result(student, Guid.NewGuid(), 40, Answers(false, bloom: 2), at = at.AddMinutes(1)));
        await db.SaveChangesAsync();

        await Paths(db).GeneratePersonalizedPathAsync(student);

        (await db.PersonalizedLearningPathItems.Select(item => item.Title).ToListAsync()).Should().Equal("Bağımsız");
    }

    [Fact]
    public async Task Support_exercises_never_include_assessment_only_exercises()
    {
        await using var db = Context();
        var (student, age) = await Student(db, currentLevel: 3, initial: 3, max: 4, offset: 0);
        var comprehension = await AddExercise(db, "Comprehension", "comprehension", 3, age, title: "Normal anlama");
        await AddExercise(db, "Comprehension", "comprehension", 3, age, assessmentOnly: true, title: "Ölçme anlama");
        var at = DateTime.UtcNow.AddHours(-1);
        for (var index = 0; index < 3; index++)
            db.ExerciseSessionResults.Add(Result(student, Guid.NewGuid(), 40, Answers(false), at = at.AddMinutes(1), comprehension.Id));
        await db.SaveChangesAsync();

        await Paths(db).GeneratePersonalizedPathAsync(student);

        (await db.PersonalizedLearningPathItems.Select(item => item.Title).ToListAsync()).Should().Equal("Normal anlama");
    }

    [Fact]
    public void Weekly_adjustment_stays_inside_the_program_range_and_shows_the_real_level()
    {
        var at = DateTime.UtcNow;
        var actor = Guid.NewGuid();
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Seviye 3", "", Guid.NewGuid(), 0, 100, "{}", 3, 0, 4, 4, 28,
            true, 1, 1, null, false, at, null, null, null);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), Guid.NewGuid(), template, 0, 0, actor, at);

        progress.ApplyAdaptiveDifficultyAdjustment(1, template, actor, at).Should().BeTrue();
        progress.ApplyAdaptiveDifficultyAdjustment(1, template, actor, at).Should().BeFalse();
        (progress.CurrentDifficultyLevel, progress.AdaptiveDifficultyOffset).Should().Be((4, 1));

        progress.ApplyAdaptiveDifficultyAdjustment(-1, template, actor, at).Should().BeTrue();
        (progress.CurrentDifficultyLevel, progress.AdaptiveDifficultyOffset).Should().Be((3, 0));
        progress.ApplyAdaptiveDifficultyAdjustment(-1, template, actor, at).Should().BeTrue();
        (progress.CurrentDifficultyLevel, progress.AdaptiveDifficultyOffset).Should().Be((2, -1));
        progress.ApplyAdaptiveDifficultyAdjustment(-1, template, actor, at).Should().BeTrue();
        progress.ApplyAdaptiveDifficultyAdjustment(-1, template, actor, at).Should().BeFalse();
        (progress.CurrentDifficultyLevel, progress.AdaptiveDifficultyOffset).Should().Be((1, -2));
    }

    [Fact]
    public async Task Rebuilt_weeks_take_the_daily_task_count_of_the_current_template()
    {
        await using var db = Context();
        var actor = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "RSVP", "RSVP", "text_stream");
        db.ExerciseTypes.Add(type);
        for (var level = 1; level <= 5; level++)
            db.Exercises.Add(Exercise.Create($"Seviye {level}", "rsvp", "{}", level, actor, type.Id));
        var id = Guid.NewGuid();
        var age = Guid.NewGuid();
        var oldTemplate = Template(id, age, Pattern(_ => 1));
        db.ProgramTemplates.Add(oldTemplate);
        await db.SaveChangesAsync();
        var schedule = Infrastructure.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!;
        var build = (Task<string>)schedule.GetMethod("BuildAsync")!.Invoke(null, [db, oldTemplate, null, CancellationToken.None])!;
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), actor, oldTemplate, 0, 0, actor, DateTime.UtcNow);
        progress.SetSchedule(await build, actor, DateTime.UtcNow);
        for (var day = 1; day <= 7; day++)
            progress.ApplyExerciseCompletion(90, false, 1, 1, oldTemplate, actor, DateTime.UtcNow.AddDays(day));
        var newTemplate = Template(id, age, Pattern(week => week == 1 ? 1 : 2));

        var adapt = (Task<bool>)schedule.GetMethod("AdaptAfterWeekAsync")!.Invoke(null,
            [db, progress, newTemplate, (decimal?)90, (decimal?)90, actor, DateTime.UtcNow, CancellationToken.None])!;

        (await adapt).Should().BeTrue();
        using var rebuilt = JsonDocument.Parse(progress.ScheduleJson!);
        var perDay = rebuilt.RootElement.EnumerateArray()
            .GroupBy(slot => (slot.GetProperty("WeekNumber").GetInt32(), slot.GetProperty("DayNumber").GetInt32()))
            .ToDictionary(group => group.Key, group => group.Count());
        perDay.Should().HaveCount(14);
        perDay.Where(item => item.Key.Item1 == 1).Should().OnlyContain(item => item.Value == 1);
        perDay.Where(item => item.Key.Item1 == 2).Should().OnlyContain(item => item.Value == 2);
    }

    private static string Pattern(Func<int, int> countForWeek) =>
        JsonSerializer.Serialize(Enumerable.Range(1, 2).ToDictionary(week => $"week{week}",
            week => Enumerable.Range(1, 7).ToDictionary(day => $"day{day}",
                _ => new[] { new { type = "RSVP", count = countForWeek(week), difficulty = 3 } })));

    private static ProgramTemplate Template(Guid id, Guid age, string pattern) =>
        ProgramTemplate.Import(id, "Uyarlanabilir", "", age, 0, 100, pattern, 3, 0, 5, 2, 14,
            true, 1, 1, null, false, DateTime.UtcNow, null, null, null);

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ILegacySpeedReadingLearningPaths Paths(OwnedSpeedReadingDbContext db) =>
        (ILegacySpeedReadingLearningPaths)Activator.CreateInstance(
            Infrastructure.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingLearningPaths")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;

    private static async Task<decimal?> WeekComprehension(OwnedSpeedReadingDbContext db, Guid student, List<Guid> sessions)
    {
        var evidence = Infrastructure.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingComprehensionEvidence")!;
        return await (Task<decimal?>)evidence.GetMethod("AverageAskedComprehensionAsync")!
            .Invoke(null, [db, student, sessions, CancellationToken.None])!;
    }

    private static async Task<(Guid Student, Guid Age)> Student(
        OwnedSpeedReadingDbContext db, int currentLevel, int initial, int max, int offset)
    {
        var student = Guid.NewGuid();
        var age = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.UserProfiles.Add(SpeedReadingUserProfile.Import(Guid.NewGuid(), student, currentLevel, 250, 70m, 20, age, null, true,
            now, null, null, null));
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Program", "", age, 0, 100, "{}", initial, 0, max, 4, 28,
            true, 1, 1, null, false, now, null, null, null);
        db.ProgramTemplates.Add(template);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), student, template, 0, 0, student, now);
        db.StudentProgramProgresses.Add(progress);
        db.Entry(progress).Property(item => item.DaysCompleted).CurrentValue = 7;
        db.Entry(progress).Property(item => item.AdaptiveDifficultyOffset).CurrentValue = offset;
        db.Entry(progress).Property(item => item.CurrentDifficultyLevel).CurrentValue = initial + offset;
        await db.SaveChangesAsync();
        return (student, age);
    }

    private static async Task<Exercise> AddExercise(OwnedSpeedReadingDbContext db, string typeName, string code, int level, Guid age,
        bool assessmentOnly = false, string? title = null)
    {
        var type = await db.ExerciseTypes.FirstOrDefaultAsync(item => item.Name == typeName);
        if (type is null)
        {
            type = ExerciseType.Create(Guid.NewGuid(), typeName, typeName, code);
            db.ExerciseTypes.Add(type);
        }
        var exercise = Exercise.Create(title ?? $"{typeName} {level}", code, "{}", level, Guid.NewGuid(), type.Id,
            targetAgeGroupId: age, isAssessmentOnly: assessmentOnly);
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        return exercise;
    }

    private static async Task AddText(OwnedSpeedReadingDbContext db, string title, int level, Guid age, Guid? exerciseId = null)
    {
        var text = ReadingText.Import(Guid.NewGuid(), title, $"{title} içeriği burada.", "tr", exerciseId, 120, "Genel", level, age, true,
            null, 1, 10, 70m, 3, 60, DateTime.UtcNow, null, null, null);
        db.ReadingTexts.Add(text);
        db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Soru?", "A", 1, bloomLevel: 2));
        await db.SaveChangesAsync();
    }

    private static string Answers(bool correct, int bloom = 0) => bloom > 0
        ? JsonSerializer.Serialize(new[] { new { BloomLevel = bloom, IsCorrect = correct }, new { BloomLevel = bloom, IsCorrect = correct } })
        : JsonSerializer.Serialize(new[] { new { IsCorrect = correct } });

    private static ExerciseSessionResult Result(Guid student, Guid? text, decimal comprehension, string answers,
        DateTime? at = null, Guid? exerciseId = null, decimal? score = null) =>
        ExerciseSessionResult.Create(Guid.NewGuid(), Guid.NewGuid(), student, exerciseId ?? Guid.NewGuid(), text, 200, 60, 200,
            comprehension, 0, score ?? comprehension, at ?? DateTime.UtcNow, answers);
}
