using System.Text.RegularExpressions;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

/// <summary>Keeps blocking harness calls out of this suite (see <see cref="TestHarnessTimeouts"/>).</summary>
public sealed class TestHarnessUsageTests
{
    [Fact]
    public void Tests_DoNotBlockOnHarnessLists_OrSetTheirOwnShortTimeouts()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "EduPlatform.sln"))) directory = directory.Parent!;
        var tests = Path.Combine(directory.FullName, "tests", "Integration", "Identity.API.IntegrationTests");
        var blocking = new Regex(@"\.(Published|Consumed|Sent)\s*\.Select<", RegexOptions.Compiled);

        foreach (var file in Directory.GetFiles(tests, "*.cs").Where(file => !file.EndsWith(nameof(TestHarnessTimeouts) + ".cs") && !file.EndsWith(nameof(TestHarnessUsageTests) + ".cs")))
        {
            var source = File.ReadAllText(file);
            blocking.IsMatch(source).Should().BeFalse($"{Path.GetFileName(file)} must use PublishedMessagesAsync, not the blocking Select");
            source.Should().NotContain("SetTestTimeouts(", $"{Path.GetFileName(file)} must use UseSuiteTimeouts");
        }
    }
}
