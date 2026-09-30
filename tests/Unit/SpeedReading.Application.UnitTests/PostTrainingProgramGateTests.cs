using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class PostTrainingProgramGateTests
{
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
