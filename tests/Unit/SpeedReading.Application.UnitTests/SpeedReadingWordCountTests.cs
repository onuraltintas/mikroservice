using FluentAssertions;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H8 (decision 29.10.2026): a token is a word only when it carries a letter or digit, so stand-alone dashes,
/// ellipses and quote marks no longer inflate the word count (and with it the measured reading speed).
/// </summary>
public sealed class SpeedReadingWordCountTests
{
    [Theory]
    [InlineData("Merhaba – dünya — !", 2)]
    [InlineData("Bir  iki\nüç", 3)]
    [InlineData("« Evet » … dedi .", 2)]
    [InlineData("1923 yılında - Cumhuriyet ilan edildi.", 5)]
    [InlineData("   ", 0)]
    public void Only_tokens_with_a_letter_or_digit_count_as_words(string content, int expected) =>
        ReadingText.CalculateWordCount(content).Should().Be(expected);

    [Fact]
    public void An_existing_text_is_recounted_with_the_new_rule()
    {
        var text = ReadingText.Create(Guid.NewGuid(), "Başlık", "Merhaba – dünya — !");
        // A count stored under the old rule, which counted every whitespace-separated token.
        typeof(ReadingText).GetProperty(nameof(ReadingText.WordCount))!.SetValue(text, 5);

        text.RecalculateWordCount().Should().BeTrue();
        text.WordCount.Should().Be(2);
    }
}
