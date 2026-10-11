using FluentAssertions;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

public sealed class GroupingTextRulesTests
{
    [Theory]
    [InlineData("Bir iki. Üç dört beş! Altı yedi?", 3, "2,5,7", "False,False,False")]
    [InlineData("Bir iki üç\n\ndört beş altı yedi", 2, "2,3,5,7", "False,True,False,False")]
    [InlineData("Dr. Deniz 12.04.2026 tarihinde 3.5 litre aldı.", 3, "3,6,7", "False,False,False")]
    [InlineData("“Bir iki!” Üç dört.", 3, "2,4", "False,False")]
    public void Groups_preserve_sentence_and_paragraph_boundaries(string text, int size, string ends, string breaks)
    {
        var groups = GroupingTextRules.Build(text, size);
        string.Join(',', groups.Select(group => group.EndIndex)).Should().Be(ends);
        string.Join(',', groups.Select(group => group.HasNewline)).Should().Be(breaks);
    }

    [Fact]
    public void Rejects_empty_content_and_invalid_group_size()
    {
        var empty = () => GroupingTextRules.Build("  ", 2);
        var invalid = () => GroupingTextRules.Build("bir", 0);
        empty.Should().Throw<ArgumentException>();
        invalid.Should().Throw<ArgumentOutOfRangeException>();
    }
}
