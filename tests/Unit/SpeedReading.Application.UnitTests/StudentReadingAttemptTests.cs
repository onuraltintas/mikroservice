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

    [Fact]
    public void SetQuestionSnapshot_stores_a_trimmed_snapshot_and_audit_timestamp()
    {
        var userId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddMinutes(-1);
        var updatedAt = DateTime.UtcNow;
        var attempt = StudentReadingAttempt.Start(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            startedAt);

        attempt.SetQuestionSnapshot("  []  ", updatedAt);

        attempt.QuestionSnapshotJson.Should().Be("[]");
        attempt.UpdatedAt.Should().Be(updatedAt);
        attempt.UpdatedBy.Should().Be(userId.ToString());
    }

    [Fact]
    public void SetQuestionSnapshot_requires_json_content()
    {
        var attempt = StudentReadingAttempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        var act = () => attempt.SetQuestionSnapshot(" ", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
