using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class BaselineTrainingGateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Active_training_blocks_new_and_resumed_baseline_without_changing_history(bool completedBaseline)
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var (service, template, request) = await SeedAsync(db, user);
        var baseline = await service.StartAttemptAsync(user, request, CancellationToken.None);
        if (completedBaseline)
            (await db.AssessmentAttempts.SingleAsync()).Complete(DateTime.UtcNow);
        db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var start = () => service.StartAttemptAsync(user, request, CancellationToken.None);

        var error = await start.Should().ThrowAsync<BusinessRuleException>();
        error.Which.Code.Should().Be("Assessment.BaselineDuringTraining");
        (await db.AssessmentAttempts.SingleAsync()).Id.Should().Be(baseline.Id);
        (await db.StudentProgramProgresses.SingleAsync()).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Active_training_locks_missing_baseline_in_phase_plan()
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var (service, template, _) = await SeedAsync(db, user);
        db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var plan = await service.GetPhasePlanAsync(user, CancellationToken.None);

        plan.Phases.Single(item => item.Phase == AssessmentAttemptPhase.Baseline).Status.Should().Be(AssessmentPhasePlanStatus.Locked);
        plan.NextPhase.Should().BeNull();
    }

    [Fact]
    public async Task Another_students_training_does_not_block_first_baseline()
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var (service, template, request) = await SeedAsync(db, user);
        var other = Guid.NewGuid();
        db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), other, template, 0, 0, other, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var started = await service.StartAttemptAsync(user, request, CancellationToken.None);

        started.Phase.Should().Be(AssessmentAttemptPhase.Baseline);
        (await service.GetPhasePlanAsync(user, CancellationToken.None)).NextPhase.Should().Be(AssessmentAttemptPhase.Baseline);
    }

    [Fact]
    public async Task Old_baseline_form_cannot_start_an_exercise_during_training()
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var (assessment, template, request) = await SeedAsync(db, user);
        var baseline = await assessment.StartAttemptAsync(user, request, CancellationToken.None);
        db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var exercise = await db.AssessmentAttemptExercises.OrderBy(item => item.OrderIndex).FirstAsync();
        var sessions = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, db)!;

        var start = () => sessions.StartAsync(user, new() { ExerciseId = exercise.ExerciseId, AssessmentAttemptId = baseline.Id });

        var error = await start.Should().ThrowAsync<BusinessRuleException>();
        error.Which.Code.Should().Be("Assessment.BaselineDuringTraining");
        (await db.ExerciseSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Completed_training_still_opens_post_training_measurement()
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var (service, template, request) = await SeedAsync(db, user);
        await service.StartAttemptAsync(user, request, CancellationToken.None);
        var now = DateTime.UtcNow;
        (await db.AssessmentAttempts.SingleAsync()).Complete(now.AddSeconds(-1));
        var program = StudentProgramProgress.Import(Guid.NewGuid(), user, template.Id, now,
            1, 1, 1, 1, 1, now, false, now, 80, 1, 1, now, null, null, null);
        db.StudentProgramProgresses.Add(program);
        await db.SaveChangesAsync();

        var plan = await service.GetPhasePlanAsync(user, CancellationToken.None);
        var post = await service.StartAttemptAsync(user, new() { Phase = AssessmentAttemptPhase.PostTraining }, CancellationToken.None);

        plan.NextPhase.Should().Be(AssessmentAttemptPhase.PostTraining);
        post.ProgramProgressId.Should().Be(program.Id);
        (await db.AssessmentAttempts.CountAsync()).Should().Be(2);
    }

    private static OwnedSpeedReadingDbContext CreateDb() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingAssessment Service, ProgramTemplate Template, StartAssessmentAttemptRequest Request)>
        SeedAsync(OwnedSpeedReadingDbContext db, Guid user)
    {
        var now = DateTime.UtcNow;
        var age = AgeGroupConfiguration.Create(Guid.NewGuid(), "teen", "Genç", 13, 16, 100, 200, 300, 70, 15, 1, 1, true, null, user, now);
        db.AgeGroupConfigurations.Add(age);
        var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Sabitleme", "motion_path");
        db.ExerciseTypes.Add(type);
        const string configuration = """{"engineType":"motion_path","engineConfig":{"mode":"fixation","content":{"points":2,"peripheralCount":2},"timing":{"holdMs":100}}}""";
        var exercises = Enumerable.Range(0, 3).Select(index => Exercise.Create($"Sabitleme {index}", "attention", configuration,
            1, user, type.Id, isAssessmentOnly: true)).ToArray();
        db.Exercises.AddRange(exercises);
        db.ProgramTemplates.Add(ProgramTemplate.Import(Guid.NewGuid(), "Assessment", "", age.Id, 0, 100,
            JsonSerializer.Serialize(new { week1 = exercises.Select(item => new { exerciseId = item.Id }) }),
            1, 1, 5, 1, 1, true, 1, 0, null, true, now, null, null, null));
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Training", "", age.Id, 0, 100, "{}",
            1, 1, 5, 1, 1, true, 1, 0, null, false, now, null, null, null);
        db.ProgramTemplates.Add(template);
        await db.SaveChangesAsync();
        var assembly = typeof(OwnedSpeedReadingDbContext).Assembly;
        var catalog = Activator.CreateInstance(assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingLevelCatalog")!, db);
        var service = (ISpeedReadingAssessment)Activator.CreateInstance(assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!, db, catalog)!;
        return (service, template, new() { Phase = AssessmentAttemptPhase.Baseline, AgeGroupConfigurationId = age.Id });
    }
}
