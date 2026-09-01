using FluentAssertions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class OwnedSpeedReadingParityTests
{
    [Fact]
    public void Id_checksum_is_order_independent_but_changes_when_a_row_changes()
    {
        var firstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var replacementId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var checksum = OwnedSpeedReadingParityHash.Compute([secondId, firstId]);

        OwnedSpeedReadingParityHash.Compute([firstId, secondId])
            .Should()
            .Be(checksum);
        OwnedSpeedReadingParityHash.Compute([firstId, replacementId])
            .Should()
            .NotBe(checksum);
    }

    [Fact]
    public void Payload_checksum_is_order_independent_but_changes_when_a_field_changes()
    {
        var firstRow = new Dictionary<string, string?>
        {
            ["Title"] = "Hızlı okuma",
            ["Score"] = "72.50"
        };
        var secondRow = new Dictionary<string, string?>
        {
            ["Title"] = "İleri seviye",
            ["Score"] = "91.00"
        };
        var changedRow = new Dictionary<string, string?>
        {
            ["Title"] = "Hızlı okuma",
            ["Score"] = "72.51"
        };

        var checksum = OwnedSpeedReadingParityHash.ComputePayload([secondRow, firstRow]);

        OwnedSpeedReadingParityHash.ComputePayload([firstRow, secondRow])
            .Should()
            .Be(checksum);
        OwnedSpeedReadingParityHash.ComputePayload([changedRow, secondRow])
            .Should()
            .NotBe(checksum);
    }

    [Fact]
    public void Session_score_matches_the_owned_measurement_migration_formula()
    {
        OwnedSpeedReadingParityDerivedFields.CalculateSessionScore(80m, 500m)
            .Should()
            .Be(88m);
        OwnedSpeedReadingParityDerivedFields.CalculateSessionScore(80.01m, 333.33m)
            .Should()
            .Be(74.67m);

        OwnedSpeedReadingParityDerivedFields.CalculateSessionScore(-10m, 1000m)
            .Should()
            .Be(40m);
    }

    [Theory]
    [InlineData("[]", false)]
    [InlineData("[\"answer\"]", true)]
    [InlineData("{}", false)]
    [InlineData("not-json", false)]
    public void Measurement_flag_from_question_answers_matches_the_owned_migration(
        string questionAnswersJson,
        bool expected)
    {
        OwnedSpeedReadingParityDerivedFields.IsMeasuredFromQuestionAnswers(questionAnswersJson)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void Daily_log_measurement_flag_requires_at_least_one_attempt_signal()
    {
        OwnedSpeedReadingParityDerivedFields.IsMeasuredFromDailyLog(0, 0, 0)
            .Should()
            .BeFalse();
        OwnedSpeedReadingParityDerivedFields.IsMeasuredFromDailyLog(0, 1, 0)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Parity_allows_owned_only_rows_created_after_cutover()
    {
        var row = new OwnedSpeedReadingParityRow(
            "Users",
            "user_profiles",
            "Id",
            "user_id",
            SourceCount: 10,
            OwnedCount: 10,
            OwnedOnlyCount: 1,
            SourceChecksum: "same",
            OwnedChecksum: "same",
            SourcePayloadChecksum: "same-payload",
            OwnedPayloadChecksum: "same-payload",
            FieldParityAvailable: true,
            MismatchedFields: []);

        row.IsMatch.Should().BeTrue();
        row.OwnedOnlyCount.Should().Be(1);
    }
}
