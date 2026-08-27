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
}
