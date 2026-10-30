using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
/// Learning path review (30.10.2026, H18): the manual "check recommendations" saves what it creates; old adaptive offsets
/// cannot make the level jump; the shown level is the level the exercises are chosen at; a support path is built only
/// from new comprehension evidence, never empty when content exists and never from warm-ups; a new program does not lock
/// the path again; a reset returns to the program's own level.
/// </summary>
public sealed class SpeedReadingLearningPathFixesTests
{
    private static readonly Assembly Infrastructure = typeof(OwnedSpeedReadingDbContext).Assembly;

    // Finding 1 (high)
    [Fact]
    public async Task Checking_recommendations_saves_the_new_path()
    {
        await using var db = Context();
        var (student, age) = await Student(db, initial: 3, max: 4);
        await AddText(db, "Destek metni", 3, age, bloom: 2);
        await WeakEvidence(db, student, DateTime.UtcNow.AddHours(-1), bloom: 2);

        var created = await Paths(db).GeneratePersonalizedPathAsync(student);

        created.Should().BeGreaterThan(0);
        db.ChangeTracker.Clear();
        (await db.PersonalizedLearningPathItems.CountAsync()).Should().Be(created);
    }

    // Finding 2 (high)
    [Theory]
    [InlineData(1, 5, -2, 1, 1, 2, 1)]   // initial 1, stored offset −2 (pre-H15), good week → level 2, not 4
    [InlineData(3, 5, -4, 1, -1, 1, -2)] // initial 3, stored offset −4, weak week → stays at level 1, never rises
    [InlineData(3, 4, 5, 3, 1, 4, 1)]    // stored offset above the range, good week → level 4
    public void Old_offsets_outside_the_range_cannot_make_the_level_jump(
        int initial, int max, int storedOffset, int shownLevel, int step, int expectedLevel, int expectedOffset)
    {
        var at = DateTime.UtcNow;
        var actor = Guid.NewGuid();
        var template = Template(initial, max, weeksPerIncrease: 1);
        var progress = StudentProgramProgress.Import(Guid.NewGuid(), Guid.NewGuid(), template.Id, at, 1, 2, shownLevel, 7, 40,
            at, true, null, 70, 1, 1, at, null, null, null, storedOffset);

        progress.ApplyAdaptiveDifficultyAdjustment(step, template, actor, at);

        (progress.CurrentDifficultyLevel, progress.AdaptiveDifficultyOffset).Should().Be((expectedLevel, expectedOffset));
    }

    // Finding 7 (medium)
    [Fact]
    public void The_shown_level_is_the_level_exercises_are_chosen_at_even_with_a_calendar_increase_setting()
    {
        var at = DateTime.UtcNow;
        var actor = Guid.NewGuid();
        var template = Template(initial: 2, max: 5, weeksPerIncrease: 1);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), Guid.NewGuid(), template, 0, 0, actor, at);

        StudentProgramCompletionResult last = null!;
        for (var day = 1; day <= 7; day++)
            last = progress.ApplyExerciseCompletion(90, false, 1, 1, template, actor, at.AddDays(day));

        last.WeekChanged.Should().BeTrue();
        last.DifficultyIncreased.Should().BeFalse("the schedule never served calendar increases; only the weekly result moves the level");
        progress.CurrentDifficultyLevel.Should().Be(2);
    }

    [Fact]
    public void A_drifted_shown_level_is_brought_back_to_the_real_level_on_the_next_completion()
    {
        var at = DateTime.UtcNow;
        var template = Template(initial: 3, max: 5, weeksPerIncrease: 1);
        var progress = StudentProgramProgress.Import(Guid.NewGuid(), Guid.NewGuid(), template.Id, at, 2, 3, 5, 15, 80,
            at, true, null, 70, 1, 1, at, null, null, null, 0);

        progress.ApplyExerciseCompletion(90, false, 0, 1, template, Guid.NewGuid(), at.AddMinutes(1));

        progress.CurrentDifficultyLevel.Should().Be(3);
    }

    // Finding 3 (medium)
    [Fact]
    public async Task A_finished_support_path_is_not_rebuilt_from_the_same_old_results()
    {
        await using var db = Context();
        var (student, age) = await Student(db, initial: 3, max: 4);
        for (var index = 0; index < 4; index++)
            await AddText(db, $"Destek {index}", 3, age, bloom: 2);
        var at = await WeakEvidence(db, student, DateTime.UtcNow.AddHours(-3), bloom: 2);
        var paths = Paths(db);

        (await paths.RefreshAdaptiveProgressionAsync(student)).Should().BeTrue();
        await db.SaveChangesAsync();
        foreach (var item in await db.PersonalizedLearningPathItems.ToListAsync())
            item.Complete(50, student, DateTime.UtcNow.AddHours(-2));
        await db.SaveChangesAsync();

        // A warm-up or Schulte task triggers the check again: still the same three results, so no new path.
        (await paths.RefreshAdaptiveProgressionAsync(student)).Should().BeFalse();

        // Three new weak comprehension results are new evidence.
        await WeakEvidence(db, student, DateTime.UtcNow.AddMinutes(-30), bloom: 2);
        (await paths.RefreshAdaptiveProgressionAsync(student)).Should().BeTrue();
    }

    // Finding 5 (medium)
    [Fact]
    public async Task Starting_a_new_program_keeps_the_learning_path_open()
    {
        await using var db = Context();
        var (student, age) = await Student(db, initial: 3, max: 4, daysCompleted: 28, active: false);
        var next = Template(initial: 4, max: 5, weeksPerIncrease: 1, age: age);
        db.ProgramTemplates.Add(next);
        db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), student, next, 0, 0, student, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var availability = await Paths(db).GetPersonalizedAvailabilityAsync(student);

        availability.IsAvailable.Should().BeTrue();
        availability.HasProgram.Should().BeTrue();
    }

    // Finding 11 (low)
    [Fact]
    public async Task A_student_without_a_program_is_told_so_not_to_finish_a_first_week()
    {
        await using var db = Context();
        var availability = await Paths(db).GetPersonalizedAvailabilityAsync(Guid.NewGuid());
        availability.IsAvailable.Should().BeFalse();
        availability.HasProgram.Should().BeFalse();
    }

    // Finding 6 (medium)
    [Fact]
    public async Task A_support_decision_with_weak_bloom_levels_still_builds_a_path_when_no_text_matches_them()
    {
        await using var db = Context();
        var (student, age) = await Student(db, initial: 3, max: 4);
        await AddText(db, "Başka düzey sorulu metin", 3, age, bloom: 5);
        await WeakEvidence(db, student, DateTime.UtcNow.AddHours(-1), bloom: 2);

        (await Paths(db).GeneratePersonalizedPathAsync(student)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task The_weak_exercise_comes_from_sessions_that_asked_questions_not_from_warm_ups()
    {
        await using var db = Context();
        var (student, age) = await Student(db, initial: 3, max: 4);
        var schulte = await AddExercise(db, "SchulteTable", "schulte", 3, age, "Schulte");
        await AddExercise(db, "SchulteTable", "schulte", 3, age, "Schulte 2");
        var comprehension = await AddExercise(db, "Comprehension", "comprehension", 3, age, "Anlama");
        await AddExercise(db, "Comprehension", "comprehension", 3, age, "Anlama 2");
        var at = DateTime.UtcNow.AddHours(-1);
        for (var index = 0; index < 3; index++)
        {
            db.ExerciseSessionResults.Add(Result(student, Guid.NewGuid(), 40, Answers(false), at = at.AddMinutes(1), comprehension.Id, 40));
            db.ExerciseSessionResults.Add(Result(student, null, 0, "[]", at = at.AddMinutes(1), schulte.Id, 10));
        }
        await db.SaveChangesAsync();

        await Paths(db).GeneratePersonalizedPathAsync(student);

        (await db.PersonalizedLearningPathItems.Select(item => item.ContentTitle).ToListAsync())
            .Should().NotBeEmpty().And.OnlyContain(title => title.StartsWith("Anlama"));
    }

    // Finding 10 (low)
    [Fact]
    public void Reset_returns_to_the_program_level()
    {
        var at = DateTime.UtcNow;
        var actor = Guid.NewGuid();
        var template = Template(initial: 3, max: 5, weeksPerIncrease: 1);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), Guid.NewGuid(), template, 0, 0, actor, at);
        progress.ApplyAdaptiveDifficultyAdjustment(1, template, actor, at);
        progress.CurrentDifficultyLevel.Should().Be(4);

        progress.Reset(template, actor, at);

        (progress.CurrentDifficultyLevel, progress.AdaptiveDifficultyOffset).Should().Be((3, 0));
    }

    // Finding 8 (low)
    [Theory]
    [InlineData(false, 3, 4, false, "Egzersiz tamamlandı. Geçen haftaki başarına göre seviyen 4 oldu.")]
    [InlineData(false, 3, 2, false, "Egzersiz tamamlandı. Geçen haftaki sonuçlarına göre seviyen 2 olarak ayarlandı.")]
    [InlineData(false, 3, 3, true, "Egzersiz tamamlandı. Son anlama sonuçlarına göre sana destek çalışmaları hazırlandı.")]
    [InlineData(false, 3, 3, false, "Egzersiz başarıyla tamamlandı.")]
    [InlineData(true, 3, 4, true, "Program başarıyla tamamlandı.")]
    public void Completion_message_says_what_actually_changed(bool programCompleted, int oldLevel, int newLevel, bool pathCreated, string expected) =>
        SpeedReadingDailyProgressRules.CompletionMessage(programCompleted, oldLevel, newLevel, pathCreated).Should().Be(expected);

    // Finding 9 (low)
    [Fact]
    public void A_concurrent_learning_path_insert_is_recognised()
    {
        var helper = Infrastructure.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDailyProgress")!
            .GetMethod("IsLearningPathConflict", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        static DbUpdateException Conflict(string constraint) => new("conflict", new PostgresException(
            "duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: constraint));

        ((bool)helper.Invoke(null, [Conflict("ix_personalized_learning_path_items_student_id_path_index_is_deleted")])!).Should().BeTrue();
        ((bool)helper.Invoke(null, [Conflict("ix_idempotency_records_scope_key")])!).Should().BeFalse();
    }

    private static ProgramTemplate Template(int initial, int max, int weeksPerIncrease, Guid? age = null) =>
        ProgramTemplate.Import(Guid.NewGuid(), $"Seviye {initial}", "", age ?? Guid.NewGuid(), 0, 100, "{}", initial, weeksPerIncrease,
            max, 4, 28, true, 1, 1, null, false, DateTime.UtcNow, null, null, null);

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ILegacySpeedReadingLearningPaths Paths(OwnedSpeedReadingDbContext db) =>
        (ILegacySpeedReadingLearningPaths)Activator.CreateInstance(
            Infrastructure.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingLearningPaths")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;

    private static async Task<(Guid Student, Guid Age)> Student(
        OwnedSpeedReadingDbContext db, int initial, int max, int daysCompleted = 7, bool active = true)
    {
        var student = Guid.NewGuid();
        var age = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.UserProfiles.Add(SpeedReadingUserProfile.Import(Guid.NewGuid(), student, initial, 250, 70m, 20, age, null, true,
            now, null, null, null));
        var template = Template(initial, max, 1, age);
        db.ProgramTemplates.Add(template);
        var progress = StudentProgramProgress.Import(Guid.NewGuid(), student, template.Id, now.AddDays(-30), 1, 2, initial,
            daysCompleted, daysCompleted * 6, now, active, active ? null : now.AddDays(-1), 70, 1, 1, now.AddDays(-30), null, null, null);
        db.StudentProgramProgresses.Add(progress);
        await db.SaveChangesAsync();
        return (student, age);
    }

    private static async Task<DateTime> WeakEvidence(OwnedSpeedReadingDbContext db, Guid student, DateTime at, int bloom)
    {
        for (var index = 0; index < 3; index++)
            db.ExerciseSessionResults.Add(Result(student, Guid.NewGuid(), 40, Answers(false, bloom), at = at.AddMinutes(1)));
        await db.SaveChangesAsync();
        return at;
    }

    private static async Task<Exercise> AddExercise(OwnedSpeedReadingDbContext db, string typeName, string code, int level, Guid age, string title)
    {
        var type = await db.ExerciseTypes.FirstOrDefaultAsync(item => item.Name == typeName);
        if (type is null)
        {
            type = ExerciseType.Create(Guid.NewGuid(), typeName, typeName, code);
            db.ExerciseTypes.Add(type);
        }
        var exercise = Exercise.Create(title, code, "{}", level, Guid.NewGuid(), type.Id, targetAgeGroupId: age);
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        return exercise;
    }

    private static async Task AddText(OwnedSpeedReadingDbContext db, string title, int level, Guid age, int bloom)
    {
        var text = ReadingText.Import(Guid.NewGuid(), title, $"{title} içeriği burada.", "tr", null, 120, "Genel", level, age, true,
            null, 1, 10, 70m, 3, 60, DateTime.UtcNow, null, null, null);
        db.ReadingTexts.Add(text);
        db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Soru?", "A", 1, bloomLevel: bloom));
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
