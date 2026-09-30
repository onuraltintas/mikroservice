using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class PostTrainingProgramGateTests
{
    [Fact]
    public async Task Previous_completion_does_not_unlock_post_training_for_a_new_unfinished_program()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var baseline = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.Baseline,
            "tr-baseline-v1", "tr", null, 3, now.AddDays(-30), null);
        baseline.Complete(now.AddDays(-30));
        db.AssessmentAttempts.Add(baseline);
        db.StudentProgramProgresses.Add(StudentProgramProgress.Import(Guid.NewGuid(), user, Guid.NewGuid(),
            now.AddDays(-29), 1, 1, 1, 1, 1, now.AddDays(-2), false, now.AddDays(-2), 80, 1, 1,
            now.AddDays(-29), null, null, null));
        db.StudentProgramProgresses.Add(StudentProgramProgress.Import(Guid.NewGuid(), user, Guid.NewGuid(),
            now.AddDays(-1), 1, 1, 1, 0, 0, null, true, null, 0, 0, 0,
            now.AddDays(-1), null, null, null));
        await db.SaveChangesAsync();
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!;
        var service = (ISpeedReadingAssessment)Activator.CreateInstance(type, db, null)!;

        var plan = await service.GetPhasePlanAsync(user, CancellationToken.None);
        plan.Phases.Single(item => item.Phase == AssessmentAttemptPhase.PostTraining).Status
            .Should().Be(AssessmentPhasePlanStatus.Locked);
        var start = () => service.StartAttemptAsync(user, new StartAssessmentAttemptRequest
        {
            Phase = AssessmentAttemptPhase.PostTraining
        }, CancellationToken.None);
        await start.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>();
    }

    [Fact]
    public async Task Post_training_phase_is_locked_until_a_program_has_completed()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = Guid.NewGuid();
        var attempt = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.Baseline,
            "tr-baseline-v1", "tr", null, 3, DateTime.UtcNow.AddDays(-1), null);
        attempt.Complete(DateTime.UtcNow);
        db.AssessmentAttempts.Add(attempt);
        await db.SaveChangesAsync();
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!;
        var service = (ISpeedReadingAssessment)Activator.CreateInstance(type, db, null)!;

        var plan = await service.GetPhasePlanAsync(user, CancellationToken.None);

        plan.Phases.Single(item => item.Phase == AssessmentAttemptPhase.PostTraining).Status
            .Should().Be(AssessmentPhasePlanStatus.Locked);
        plan.NextPhase.Should().NotBe(AssessmentAttemptPhase.PostTraining);
        var start = () => service.StartAttemptAsync(user, new StartAssessmentAttemptRequest
        {
            Phase = AssessmentAttemptPhase.PostTraining
        }, CancellationToken.None);
        await start.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>();
    }
}
