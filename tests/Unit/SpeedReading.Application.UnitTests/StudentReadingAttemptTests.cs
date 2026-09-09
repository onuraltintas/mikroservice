using FluentAssertions;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Application.UnitTests;

public sealed class StudentReadingAttemptTests
{
    [Fact]
    public void Start_requires_authenticated_user_and_active_text()
    {
        var act = () => StudentReadingAttempt.Start(
            Guid.NewGuid(),
            Guid.Empty,
            Guid.NewGuid(),
            DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Complete_is_idempotent_and_preserves_the_first_completion_time()
    {
        var startedAt = DateTime.UtcNow.AddMinutes(-5);
        var attempt = StudentReadingAttempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            startedAt);
        var completedAt = DateTime.UtcNow;

        attempt.Complete(completedAt);
        attempt.Complete(completedAt.AddMinutes(1));

        attempt.CompletedAt.Should().Be(completedAt);
    }
}
