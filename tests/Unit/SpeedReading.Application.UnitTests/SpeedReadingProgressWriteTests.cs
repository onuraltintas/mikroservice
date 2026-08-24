using FluentAssertions;
using SpeedReading.Application.Progress;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingProgressWriteTests
{
    [Fact]
    public void Request_hash_is_stable_for_the_same_payload()
    {
        var request = CreateRequest();

        SpeedReadingRequestHasher.Create(request)
            .Should().Be(SpeedReadingRequestHasher.Create(request));
    }

    [Fact]
    public void Request_hash_changes_when_a_metric_changes()
    {
        var first = CreateRequest();
        var second = first with { RawWpm = first.RawWpm + 1 };

        SpeedReadingRequestHasher.Create(first)
            .Should().NotBe(SpeedReadingRequestHasher.Create(second));
    }

    [Fact]
    public void Request_hash_changes_when_the_authenticated_student_changes()
    {
        var request = CreateRequest();
        var firstStudent = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondStudent = Guid.Parse("22222222-2222-2222-2222-222222222222");

        SpeedReadingRequestHasher.Create(firstStudent, request)
            .Should().NotBe(SpeedReadingRequestHasher.Create(secondStudent, request));
    }

    private static CreateExerciseResultRequest CreateRequest() => new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        420,
        95,
        264.5m,
        87.25m,
        230.84m,
        "[{\"questionId\":\"q1\",\"isCorrect\":true}]",
        "[]",
        new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc));
}
