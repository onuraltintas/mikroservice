using FluentAssertions;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.UnitTests;

public sealed class CmsContactReplyEmailTests
{
    [Fact]
    public void Builds_a_safe_html_reply_with_the_original_message_context()
    {
        var email = CmsContactReplyEmailFormatter.Create(
            "Ada <script>",
            "Yardım & bilgi",
            "Merhaba <b>ekip</b>",
            "Yanıtım: 2 > 1");

        email.Subject.Should().Be("RE: Yardım & bilgi - Hızlı Okuma destek talebi yanıtı");
        email.Body.Should().Contain("Ada &lt;script&gt;");
        email.Body.Should().Contain("Yard&#x131;m &amp; bilgi");
        email.Body.Should().Contain("Merhaba &lt;b&gt;ekip&lt;/b&gt;");
        email.Body.Should().Contain("Yan&#x131;t&#x131;m: 2 &gt; 1");
        email.Body.Should().NotContain("<script>");
    }
}
