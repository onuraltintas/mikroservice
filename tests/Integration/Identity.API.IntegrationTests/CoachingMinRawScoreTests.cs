using Coaching.Domain.Entities;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

// 01.11.2026: ÖSYM's 0,5 ham puan condition (2026 YKS Kılavuzu, Tablo 1D). A score type is computed only if one of the tests
// named for it reaches 0,5; SAY, EA, SÖZ and DİL also need TYT's condition.
public sealed class CoachingMinRawScoreTests
{
    /// <summary>Ham puan per ÖSYM test code; tests not listed are 0.</summary>
    private static IReadOnlyList<RawScoreCondition> Failed(string type, params (string Code, decimal Raw)[] raw) =>
        MinRawScoreRules.Failed(type, test => raw.Where(x => x.Code == test.Code).Select(x => x.Raw).FirstOrDefault());

    private static string[][] Codes(IReadOnlyList<RawScoreCondition> failed) => failed.Select(x => x.AnyOf.Select(y => y.Code).ToArray()).ToArray();

    [Theory]
    [InlineData(0.49, 0.49, false)]
    [InlineData(0.5, 0, true)]
    [InlineData(0, 0.5, true)]
    [InlineData(-1.25, 0.5, true)]
    [InlineData(0.25, -0.25, false)]
    public void TytNeedsHalfARawPointInTurkceOrTemelMatematik(double turkish, double math, bool computed)
    {
        var failed = Failed("TYT", ("TUR", (decimal)turkish), ("TMAT", (decimal)math), ("SOS", 40), ("FEN", 20));
        failed.Should().HaveCount(computed ? 0 : 1, "Sosyal Bilimler and Fen Bilimleri do not count for the condition");
        if (!computed) Codes(failed).Should().BeEquivalentTo(new[] { new[] { "TUR", "TMAT" } }, o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData("SAY", "AMAT", "FEN")]
    [InlineData("EA", "AMAT", "TDESB1")]
    [InlineData("SOZ", "TDESB1", "SB2")]
    public void AytScoreTypesNeedHalfARawPointInOneOfTheirTwoTests(string type, string first, string second)
    {
        Codes(Failed(type, ("TUR", 10), (first, 0.49m), (second, 0.49m))).Should().BeEquivalentTo(new[] { new[] { first, second } }, o => o.WithStrictOrdering());
        Failed(type, ("TUR", 10), (first, 0.5m), (second, 0.49m)).Should().BeEmpty();
        Failed(type, ("TUR", 10), (first, 0), (second, 0.5m)).Should().BeEmpty();
    }

    [Fact]
    public void TestsOfOtherScoreTypesDoNotCount()
    {
        Codes(Failed("SAY", ("TUR", 10), ("TDESB1", 20), ("SB2", 20), ("YD", 50))).Should().BeEquivalentTo(new[] { new[] { "AMAT", "FEN" } }, o => o.WithStrictOrdering());
        Codes(Failed("SOZ", ("TUR", 10), ("AMAT", 30))).Should().BeEquivalentTo(new[] { new[] { "TDESB1", "SB2" } }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void DilNeedsHalfARawPointInYabanciDil()
    {
        Codes(Failed("DIL", ("TMAT", 1), ("YD", 0.49m))).Should().BeEquivalentTo(new[] { new[] { "YD" } }, o => o.WithStrictOrdering());
        Failed("DIL", ("TMAT", 1), ("YD", 0.5m)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("SAY")]
    [InlineData("EA")]
    [InlineData("SOZ")]
    [InlineData("DIL")]
    public void WithoutTytsConditionNoOtherYksScoreIsComputed(string type)
    {
        var failed = Failed(type, ("TUR", 0.49m), ("TMAT", 0.25m), ("AMAT", 30), ("FEN", 30), ("TDESB1", 30), ("SB2", 30), ("YD", 60));
        Codes(failed).Should().BeEquivalentTo(new[] { new[] { "TUR", "TMAT" } }, o => o.WithStrictOrdering());
        failed[0].AnyOf.Select(x => (x.Name, x.RawScore)).Should().Equal(("Türkçe", 0.49m), ("Temel Matematik", 0.25m));

        Codes(Failed(type, ("TUR", 0))).Should().HaveCount(2, "both conditions fail and both are named");
    }

    [Fact]
    public void LgsHasNoCondition() => Failed("LGS").Should().BeEmpty();

    [Fact]
    public void TheShownRawScoreIsCutNotRoundedSoAFailingScoreNeverReadsHalf() =>
        Failed("TYT", ("TUR", 5m / 12), ("TMAT", 0.4999m))[0].AnyOf.Select(x => x.RawScore).Should().Equal(0.41m, 0.49m);

    [Fact]
    public void TheTestsAreMadeUpOfThePracticeExamSectionsAsInTheScoreModelPack()
    {
        string[] Sections(string type, string code)
        {
            var test = MinRawScoreRules.ConditionsFor(type).SelectMany(x => x).Single(x => x.Code == code);
            return [test.Exam, .. test.Sections];
        }
        Sections("TYT", "TUR").Should().Equal("TYT", "TUR");
        Sections("TYT", "TMAT").Should().Equal("TYT", "MAT");
        Sections("SAY", "AMAT").Should().Equal("AYT_SAY", "MAT");
        Sections("SAY", "FEN").Should().Equal("AYT_SAY", "FIZ", "KIM", "BIO");
        Sections("EA", "AMAT").Should().Equal("AYT_EA", "MAT");
        Sections("EA", "TDESB1").Should().Equal("AYT_EA", "EDB", "TAR1", "COG1");
        Sections("SOZ", "TDESB1").Should().Equal("AYT_SOZ", "EDB", "TAR1", "COG1");
        Sections("SOZ", "SB2").Should().Equal("AYT_SOZ", "TAR2", "COG2", "FEL", "DIN");
        Sections("DIL", "YD").Should().Equal("YDT_EN", "ENG");
        foreach (var type in ScoreModelRules.Types)
            foreach (var test in MinRawScoreRules.ConditionsFor(type.Code).SelectMany(x => x))
                PracticeExamRules.Profiles.Single(x => x.Code == test.Exam).Sections.Select(x => x.Code).Should().Contain(test.Sections);
    }

    [Fact]
    public void ARawScoreIsTheSumOfItsSectionsCorrectMinusAQuarterOfWrong()
    {
        // Fizik 1 doğru 2 yanlış (0,5), Kimya 0 doğru 1 yanlış (−0,25), Biyoloji 0: Fen Bilimleri 0,25 — below 0,5.
        var summary = PracticeExamRules.Summarize(new("AYT_SAY", [new("MAT", 0, 0, 40), new("FIZ", 1, 2, 11), new("KIM", 0, 1, 12), new("BIO", 0, 0, 13)]));
        var fen = MinRawScoreRules.ConditionsFor("SAY")[1].Single(x => x.Code == "FEN");
        MinRawScoreRules.Raw(fen, summary).Should().Be(0.25m);
        MinRawScoreRules.Raw(MinRawScoreRules.ConditionsFor("SAY")[1].Single(x => x.Code == "AMAT"), summary).Should().Be(0m);
    }
}
