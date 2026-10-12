using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class AssessmentProgramCycleTests
{
    [Fact]
    public void Program_link_is_required_for_followup_and_cannot_be_replaced()
    {
        var attempt = AssessmentAttempt.Start(Guid.NewGuid(), Guid.NewGuid(), AssessmentAttemptPhase.PostTraining,
            "tr-posttraining-v1", "tr", null, 3, DateTime.UtcNow, null);
        var program = Guid.NewGuid();
        attempt.BindToProgram(program);
        attempt.ProgramProgressId.Should().Be(program);
        var rebind = () => attempt.BindToProgram(Guid.NewGuid());
        rebind.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Previous_program_measurement_does_not_complete_the_latest_program_cycle()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var first = CompletedProgram(user, now.AddDays(-40), now.AddDays(-30));
        var latest = CompletedProgram(user, now.AddDays(-20), now.AddDays(-1));
        db.StudentProgramProgresses.AddRange(first, latest);
        var baseline = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.Baseline,
            "tr-baseline-v1", "tr", null, 3, now.AddDays(-50), null);
        baseline.Complete(now.AddDays(-50));
        var post = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.PostTraining,
            "tr-posttraining-v1", "tr", null, 3, now.AddDays(-29), null);
        post.BindToProgram(first.Id);
        post.Complete(now.AddDays(-29));
        db.AssessmentAttempts.AddRange(baseline, post);
        await db.SaveChangesAsync();
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!;
        var service = (ISpeedReadingAssessment)Activator.CreateInstance(type, db, null)!;

        var plan = await service.GetPhasePlanAsync(user, CancellationToken.None);

        plan.Phases.Single(item => item.Phase == AssessmentAttemptPhase.PostTraining).Status
            .Should().Be(AssessmentPhasePlanStatus.Available);
        plan.NextPhase.Should().Be(AssessmentAttemptPhase.PostTraining);
        var start = () => service.StartAttemptAsync(user, new StartAssessmentAttemptRequest
        {
            Phase = AssessmentAttemptPhase.Retention
        }, CancellationToken.None);
        await start.Should().ThrowAsync<InvalidOperationException>();
    }

    private static StudentProgramProgress CompletedProgram(Guid user, DateTime assigned, DateTime completed) =>
        StudentProgramProgress.Import(Guid.NewGuid(), user, Guid.NewGuid(), assigned,
            1, 1, 1, 1, 1, completed, false, completed, 80, 1, 1, assigned, null, null, null);

    [Fact]
    public async Task Historical_followup_remains_available_during_a_new_active_program_and_is_owner_scoped()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var first = CompletedProgram(user, now.AddDays(-45), now.AddDays(-15));
        var active = StudentProgramProgress.Import(Guid.NewGuid(), user, Guid.NewGuid(), now.AddDays(-5),
            1, 1, 1, 0, 0, null, true, null, 0, 0, 0, now.AddDays(-5), null, null, null);
        db.StudentProgramProgresses.AddRange(first, active);
        var age = Guid.NewGuid();
        db.AgeGroupConfigurations.Add(SpeedReading.Domain.AgeGroups.AgeGroupConfiguration.Create(age, "teen", "Genç",
            13, 16, 100, 200, 300, 70, 15, 1, 1, true, null, user, now));
        db.ProgramTemplates.AddRange(new[] { first, active }.Select(progress => ProgramTemplate.Import(
            progress.ProgramTemplateId, "Test program", "", age, 0, 100, "{}", 1, 2, 5, 1, 1, true, 1, 0, null,
            false, now, null, null, null)));
        var typeId = Guid.NewGuid();
        db.ExerciseTypes.Add(ExerciseType.Create(typeId, "Fixation", "Fixation", "focus"));
        var exercises = Enumerable.Range(0, 3).Select(index => Exercise.Create($"Fixation {index}", "Fixation", "{}", 1,
            user, typeId, isAssessmentOnly: true)).ToArray();
        db.Exercises.AddRange(exercises);
        db.ProgramTemplates.Add(ProgramTemplate.Import(Guid.NewGuid(), "Assessment", "", age, 0, 100,
            System.Text.Json.JsonSerializer.Serialize(new { week1 = exercises.Select(exercise => new { exerciseId = exercise.Id }) }),
            1, 2, 5, 1, 1, true, 1, 0, null, true, now, null, null, null));
        var baseline = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.Baseline,
            "baseline", "tr", null, 3, now.AddDays(-50), null);
        baseline.Complete(now.AddDays(-50));
        var post = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.PostTraining,
            "post", "tr", null, 3, now.AddDays(-14), null);
        post.BindToProgram(first.Id);
        post.Complete(now.AddDays(-14));
        db.AssessmentAttempts.AddRange(baseline, post);
        await db.SaveChangesAsync();
        var assembly = typeof(OwnedSpeedReadingDbContext).Assembly;
        var catalog = Activator.CreateInstance(assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingLevelCatalog")!, db);
        var service = (ISpeedReadingAssessment)Activator.CreateInstance(assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!, db, catalog)!;
        var plan = await service.GetProgramPhasePlanAsync(user, first.Id, CancellationToken.None);
        plan.NextPhase.Should().Be(AssessmentAttemptPhase.Retention);
        var pending = await service.GetFollowUpPlansAsync(user, CancellationToken.None);
        pending.Should().ContainSingle(item => item.ProgramProgressId == first.Id && item.AdditionalTraining);
        var started = await service.StartAttemptAsync(user, new StartAssessmentAttemptRequest
        {
            Phase = AssessmentAttemptPhase.Retention, ProgramProgressId = first.Id
        }, CancellationToken.None);
        started.ProgramProgressId.Should().Be(first.Id);
        var form = await service.GetAttemptExercisesAsync(user, started.Id, CancellationToken.None);
        form.Exercises.Should().HaveCount(3);
        var foreignPlan = () => service.GetProgramPhasePlanAsync(Guid.NewGuid(), first.Id, CancellationToken.None);
        await foreignPlan.Should().ThrowAsync<KeyNotFoundException>();
        (await db.StudentProgramProgresses.SingleAsync(item => item.Id == active.Id)).IsActive.Should().BeTrue();
    }
}
