using System.Text.RegularExpressions;
using FluentAssertions;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// With C# 14 on .NET 10, <c>array.Contains(x)</c> inside an EF query binds to the ReadOnlySpan overload and EF fails
/// to evaluate it ("…ReadOnlySpan`1… violates the constraint…") on every provider — the request returns 500. Found
/// while working on review H4; e.g. the active-session list broke as soon as a student had one. Collections used in
/// queries are kept as List/HashSet.
/// </summary>
public sealed partial class EfArrayContainsGuardTests
{
    [Fact]
    public void Arrays_are_not_used_for_contains_in_queries()
    {
        var root = FindRepositoryRoot();
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "services", "speed-reading-service", "SpeedReading.Infrastructure"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
                continue;
            var source = File.ReadAllText(file);
            var arrays = ArrayVariable().Matches(source).Select(match => match.Groups[1].Value).ToHashSet();
            foreach (var name in arrays)
            {
                foreach (Match use in new Regex(@"(?<![\w.])" + Regex.Escape(name) + @"\.Contains\(\w+(?:\.\w+)+\)").Matches(source))
                    offenders.Add($"{Path.GetFileName(file)}:{source[..use.Index].Count(ch => ch == '\n') + 1} {use.Value}");
            }
        }

        offenders.Should().BeEmpty();
    }

    [GeneratedRegex(@"var (\w+) = [^;]*\.ToArray(?:Async)?\([^()]*\)\s*;", RegexOptions.Singleline)]
    private static partial Regex ArrayVariable();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EduPlatform.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
