using FluentAssertions;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingAdminProgressSearchTests
{
    [Fact]
    public void Parses_a_guid_search_term_for_progress_id_matching()
    {
        var id = Guid.NewGuid();

        SpeedReadingAdminProgressSearch.TryParseId($"  {id:D} ").Should().Be(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ada")]
    public void Ignores_non_guid_search_terms(string? searchTerm)
    {
        SpeedReadingAdminProgressSearch.TryParseId(searchTerm).Should().BeNull();
    }
}
