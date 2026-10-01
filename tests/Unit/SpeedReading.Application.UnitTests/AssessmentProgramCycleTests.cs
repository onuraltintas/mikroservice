using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Programs;
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
}
