using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Management;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Paket B (user decision 05.11.2026): score estimates for every exam the coaching product supports.
// YDS / YÖKDİL: the official rule itself (correct answers × 100 / 80) and the YDS level. MEB-AGS: ÖSYM's standard score method
// with the 2026 test statistics, the guide's weights, the 1-net rule and the 0–100 formula with assumed X / S / B
// (content-packs/coaching-score-models/2026-meb-ags). LGS: MEB's method with full net = 500 and a calibrated slope
// (content-packs/coaching-score-models/2026-lgs).
public sealed class CoachingScoreEstimatesAllExamsTests
{
    // ÖSYM, 2026 MEB-AGS Sayısal Bilgiler: (questions, mean, sd) of the AGS tests and two ÖABT fields.
    private static readonly Dictionary<string, (int N, decimal Mean, decimal Sd)> Ags = new()
    {
        ["SOZ"] = (15, 8.3m, 3.2m), ["SAY"] = (15, 3.4m, 3.9m), ["TAR"] = (6, 2.7m, 2.1m), ["COG"] = (6, 2.2m, 1.7m),
        ["EGT"] = (30, 11.2m, 6.3m), ["MEV"] = (8, 2.5m, 2.4m)
    };
    private static readonly Dictionary<string, decimal> P1Weights = new() { ["SOZ"] = 20, ["SAY"] = 20, ["TAR"] = 15, ["COG"] = 10, ["EGT"] = 25, ["MEV"] = 10 };

    // ---------- Practice nets: each exam's own rule ----------

    [Theory]
    [InlineData("LGS", 9, 3, 8)]        // MEB: doğru − yanlış/3
    [InlineData("AGS", 9, 4, 8)]        // ÖSYM MEB-AGS: doğru − yanlış/4
    [InlineData("OABT_IMAT", 9, 4, 8)]
    [InlineData("TYT", 9, 4, 8)]
    [InlineData("YDS_EN", 9, 4, 9)]     // ÖSYM YDS / YÖKDİL: only correct answers count
    [InlineData("YOKDIL_SAG", 9, 4, 9)]
    public void PracticeNetsUseEachExamsOwnRule(string code, int correct, int wrong, decimal net)
    {
        var profile = PracticeExamRules.Profiles.Single(x => x.Code == code);
        var first = profile.Sections[0];
        var summary = PracticeExamRules.Summarize(new(code, profile.Sections.Select(x => x == first
            ? new PracticeSectionAnswers(x.Code, correct, wrong, x.QuestionCount - correct - wrong)
            : new PracticeSectionAnswers(x.Code, 0, 0, x.QuestionCount)).ToArray()));
        summary.Sections[0].Net.Should().Be(net);
    }

    // ---------- YDS and YÖKDİL: exact ----------

    [Theory]
    [InlineData(80, 0, 100)]
    [InlineData(64, 16, 80)]
    [InlineData(64, 0, 80)]
    [InlineData(1, 79, 1.25)]
    [InlineData(0, 80, 0)]
    public void YdsAndYokdilScoreIsTheCorrectCountOutOfAHundredWhateverTheWrongAnswers(int correct, int wrong, decimal score)
    {
        foreach (var code in new[] { "YDS_EN", "YOKDIL_FEN", "YOKDIL_SAG", "YOKDIL_SOS" })
            LanguageScoreRules.Score(PracticeExamRules.Summarize(new(code, [new("ENG", correct, wrong, 80 - correct - wrong)]))).Should().Be(score, code);
    }

    [Theory]
    [InlineData(100, "A")] [InlineData(90, "A")] [InlineData(89.99, "B")] [InlineData(80, "B")] [InlineData(79.99, "C")] [InlineData(70, "C")]
    [InlineData(60, "D")] [InlineData(50, "E")] [InlineData(49.99, null)] [InlineData(0, null)]
    public void TheYdsLevelFollowsOsymsScale(double score, string? level) => LanguageScoreRules.YdsLevel((decimal)score).Should().Be(level);

    [Fact]
    public async Task AYdsStudentsGoalIsTheExactScoreWithItsLevel()
    {
        await using var s = await Setup.CreateAsync();
        s.Context(LearningPurpose.Yds, null);
        s.Exam("YDS_EN", new() { ["ENG"] = (64, 16) });
        s.Exam("YOKDIL_SOS", new() { ["ENG"] = (40, 0) });
        await s.F.Db.SaveChangesAsync();

        var estimate = await s.Service.EstimateAsync(s.Student, s.Student, null, default);

        var yds = estimate.LanguageScores.Single(x => x.ExamCode == "YDS_EN");
        (yds.Score, yds.Level, yds.Exact, yds.Label).Should().Be((80m, "B", true, "Deneme sonucuna göre"));
        estimate.LanguageScores.Single(x => x.ExamCode == "YOKDIL_SOS").Level.Should().BeNull("YÖKDİL has no level scale");
        var goal = estimate.Goal!;
        (goal.ScoreType, goal.Score, goal.Level, goal.Exact, goal.Margin, goal.ScaleMax).Should().Be(("YDS_EN", 80m, "B", true, (decimal?)null, 100m));
    }

    // ---------- MEB-AGS: approximate ----------

    [Fact]
    public async Task TheMebAgsPackPreviewsWithoutErrorsAndAppliesWithoutARankTable()
    {
        await using var s = await Setup.CreateAsync();
        var csv = await File.ReadAllTextAsync(Pack("2026-meb-ags", "puan-modelleri-2026-meb-ags.csv"));

        var preview = await s.Service.PreviewAsync(s.Admin, new(2026, csv, null), default);

        preview.Errors.Should().BeEmpty();
        preview.Models.Should().HaveCount(19);
        preview.Models.Single(x => x.ScoreType == "P1").Coefficients.Should().Be(6);
        preview.Models.Where(x => x.ScoreType != "P1").Should().OnlyContain(x => x.Coefficients == 7 && x.ScoreType.StartsWith("P2_"));
        preview.Models.Should().OnlyContain(x => x.RankRows == 0);
        (await s.Service.ApplyAsync(s.Admin, new(2026, csv, preview.Fingerprint), default)).Should().Be(19);
        (await s.F.Db.Set<CoachingScoreModel>().AsNoTracking().ToListAsync()).Should().OnlyContain(x => x.UncertaintyShare > 0 && !x.HasRanks);
    }

    [Fact]
    public async Task P1FollowsOsymsFormulaWithTheAssumedXSAndB()
    {
        await using var s = await Setup.CreateAsync(agsPack: true);
        s.Context(LearningPurpose.Ags, null);
        var nets = new Dictionary<string, int> { ["SOZ"] = 10, ["SAY"] = 5, ["TAR"] = 3, ["COG"] = 3, ["EGT"] = 15, ["MEV"] = 4 };
        s.Exam("AGS", nets.ToDictionary(x => x.Key, x => (x.Value, 0)));
        await s.F.Db.SaveChangesAsync();

        var estimate = await s.Service.EstimateAsync(s.Student, s.Student, null, default);

        var p1 = estimate.Estimates.Single(x => x.ScoreType == "P1");
        var expected = OsymScore(P1Weights, nets.ToDictionary(x => x.Key, x => (decimal)x.Value), rho: 0.5);
        p1.Score.Should().BeApproximately(expected, 0.02m);
        (p1.Group, p1.Label, p1.ScaleMax, p1.Rank, p1.Percentile).Should().Be(("MEB-AGS", "Yaklaşık tahmin", 100m, (int?)null, (decimal?)null));
        p1.Margin.Should().BeApproximately(0.077m * (100 - p1.Score), 0.01m, "the ± range spans every inter-test correlation (S from 0 to 1)");
        // The ends of the range are the same formula with S at ρ = 0 and ρ = 1.
        var lowest = Math.Min(OsymScore(P1Weights, nets.ToDictionary(x => x.Key, x => (decimal)x.Value), 0), OsymScore(P1Weights, nets.ToDictionary(x => x.Key, x => (decimal)x.Value), 1));
        p1.ScoreLow.Should().BeLessThanOrEqualTo(lowest + 0.02m);
        (estimate.Goal!.ScoreType, estimate.Goal.Score, estimate.Goal.Margin).Should().Be(("P1", p1.Score, p1.Margin), "a student without an ÖABT field aims at P1");
        estimate.Notice.Should().BeNull();
    }

    [Fact]
    public async Task AFullNetAgsGivesOneHundredAndNegativeNetsStopAtZero()
    {
        await using var s = await Setup.CreateAsync(agsPack: true);
        s.Exam("AGS", Ags.ToDictionary(x => x.Key, x => (x.Value.N, 0)));
        await s.F.Db.SaveChangesAsync();
        var p1 = (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Estimates.Single(x => x.ScoreType == "P1");
        p1.Score.Should().BeApproximately(100m, 0.01m, "B is the AP of the full-net profile");
        p1.Margin.Should().BeApproximately(0m, 0.01m);
    }

    [Fact]
    public async Task TheWeightsFollowTheGuide()
    {
        await using var s = await Setup.CreateAsync(agsPack: true);
        var models = await s.F.Db.Set<CoachingScoreModel>().AsNoTracking().ToDictionaryAsync(x => x.ScoreType);
        decimal C(string type, string exam, string section) => models[type].Coefficients().Single(x => x.Exam == exam && x.Section == section).Coefficient;
        decimal PerNet(decimal weight, decimal sd) => weight / sd;

        // P1: points per net ∝ weight / sd.
        (C("P1", "AGS", "SOZ") / C("P1", "AGS", "TAR")).Should().BeApproximately(PerNet(20, 3.2m) / PerNet(15, 2.1m), 0.001m);
        (C("P1", "AGS", "EGT") / C("P1", "AGS", "MEV")).Should().BeApproximately(PerNet(25, 6.3m) / PerNet(10, 2.4m), 0.001m);
        // P2: the AGS weights halved (same ratios) and ÖABT 50.
        (C("P2_IMAT", "AGS", "SOZ") / C("P2_IMAT", "AGS", "EGT")).Should().BeApproximately(C("P1", "AGS", "SOZ") / C("P1", "AGS", "EGT"), 0.001m);
        (C("P2_IMAT", "OABT_IMAT", "ALAN") / C("P2_IMAT", "AGS", "SOZ")).Should().BeApproximately(PerNet(50, 9.2m) / PerNet(10, 3.2m), 0.001m);
        (C("P2_OKUL", "OABT_OKUL", "ALAN") / C("P2_OKUL", "AGS", "MEV")).Should().BeApproximately(PerNet(50, 7.9m) / PerNet(5, 2.4m), 0.001m);
        // P2-16: Ortak Alan 15, Temel İslam Bilimleri 15, İslam Tarihi Felsefe ve Din Bilimleri 20; P2-17: 15 / 20 / 15. The one
        // ÖABT section's net is split over the sub-tests by question count (10 / 23 / 17).
        decimal Din(decimal oab, decimal tib, decimal itf) => oab * 10 / 50 / 2.3m + tib * 23 / 50 / 5.4m + itf * 17 / 50 / 3.4m;
        (C("P2_16", "OABT_DIN", "ALAN") / C("P2_16", "AGS", "SOZ")).Should().BeApproximately(Din(15, 15, 20) / PerNet(10, 3.2m), 0.001m);
        (C("P2_17", "OABT_DIN", "ALAN") / C("P2_17", "AGS", "SOZ")).Should().BeApproximately(Din(15, 20, 15) / PerNet(10, 3.2m), 0.001m);
    }

    [Fact]
    public async Task ATestUnderOneNetLeavesItsScoreTypesNotComputed()
    {
        await using var first = await Setup.CreateAsync(agsPack: true);
        var s = first;
        s.Context(LearningPurpose.Ags, "OABT_IMAT");
        // Mevzuat 1 doğru 1 yanlış = 0,75 net (< 1); ÖABT 20 net.
        s.Exam("AGS", new() { ["SOZ"] = (10, 0), ["SAY"] = (5, 0), ["TAR"] = (3, 0), ["COG"] = (3, 0), ["EGT"] = (15, 0), ["MEV"] = (1, 1) });
        s.Exam("OABT_IMAT", new() { ["ALAN"] = (20, 0) });
        await s.F.Db.SaveChangesAsync();

        var estimate = await s.Service.EstimateAsync(s.Student, s.Student, null, default);

        estimate.Estimates.Should().NotContain(x => x.Group == "MEB-AGS");
        estimate.NotComputed.Select(x => x.ScoreType).Should().BeEquivalentTo("P1", "P2_IMAT");
        var p1 = estimate.NotComputed.Single(x => x.ScoreType == "P1");
        (p1.NotComputedReason, p1.MinimumRawScore, p1.Group).Should().Be(("minRawScore", 1m, "MEB-AGS"));
        p1.FailedConditions.Select(x => x.AnyOf.Single()).Select(x => (x.Code, x.Name, x.RawScore)).Should().Equal(("MEV", "Mevzuat", 0.75m));
        (estimate.Goal!.ScoreType, estimate.Goal.Score, estimate.Goal.NotComputedReason).Should().Be(("P2_IMAT", (decimal?)null, "minRawScore"));

        // 1 net is enough; an ÖABT under 1 net leaves only P2 out.
        await using var t = await Setup.CreateAsync(agsPack: true);
        s = t;
        s.Exam("AGS", new() { ["SOZ"] = (10, 0), ["SAY"] = (5, 0), ["TAR"] = (3, 0), ["COG"] = (3, 0), ["EGT"] = (15, 0), ["MEV"] = (1, 0) });
        s.Exam("OABT_IMAT", new() { ["ALAN"] = (1, 1) });
        await s.F.Db.SaveChangesAsync();
        estimate = await s.Service.EstimateAsync(s.Student, s.Student, null, default);
        estimate.Estimates.Select(x => x.ScoreType).Should().Equal("P1");
        estimate.NotComputed.Single().FailedConditions.Single().AnyOf.Single().Code.Should().Be("OABT");
    }

    [Fact]
    public async Task DinKulturuGetsP2_16AndP2_17AndAimsAtP2_16()
    {
        await using var s = await Setup.CreateAsync(agsPack: true);
        s.Context(LearningPurpose.Ags, "OABT_DIN");
        s.Exam("AGS", new() { ["SOZ"] = (10, 0), ["SAY"] = (5, 0), ["TAR"] = (3, 0), ["COG"] = (3, 0), ["EGT"] = (15, 0), ["MEV"] = (4, 0) });
        s.Exam("OABT_DIN", new() { ["ALAN"] = (30, 4) });
        await s.F.Db.SaveChangesAsync();

        var estimate = await s.Service.EstimateAsync(s.Student, s.Student, null, default);

        estimate.Estimates.Select(x => x.ScoreType).Should().BeEquivalentTo("P1", "P2_16", "P2_17");
        var p16 = estimate.Estimates.Single(x => x.ScoreType == "P2_16"); var p17 = estimate.Estimates.Single(x => x.ScoreType == "P2_17");
        p16.Score.Should().BeInRange(60, 100); p17.Score.Should().BeInRange(60, 100);
        p16.Score.Should().NotBe(p17.Score, "the sub-test weights differ");
        estimate.Goal!.ScoreType.Should().Be("P2_16");
        p16.Name.Should().Be("MEB-AGS P2-16 · Din Kültürü ve Ahlak Bilgisi");
    }

    // ---------- LGS: approximate ----------

    [Fact]
    public async Task LgsFullNetIsFiveHundredAndEveryNetCounts()
    {
        await using var s = await Setup.CreateAsync(lgsPack: true);
        s.Context(LearningPurpose.Lgs, null);
        var lgs = PracticeExamRules.Profiles.Single(x => x.Code == "LGS");
        s.Exam("LGS", lgs.Sections.ToDictionary(x => x.Code, x => (x.QuestionCount, 0)));
        await s.F.Db.SaveChangesAsync();

        var estimate = await s.Service.EstimateAsync(s.Student, s.Student, null, default);
        var full = estimate.Estimates.Single(x => x.ScoreType == "LGS");
        full.Score.Should().BeApproximately(500m, 0.05m, "452 students answered every question right in 2026 and got 500");
        (full.Margin, full.Rank, full.Group, full.ScaleMin, full.ScaleMax).Should().Be(((decimal?)0m, (int?)null, "LGS", 100m, 500m));
        (estimate.Goal!.ScoreType, estimate.Goal.Label).Should().Be(("LGS", "Yaklaşık tahmin"));

        // Monotone: one more right answer (one fewer blank) in any test raises the score; Türkçe / Matematik / Fen count more.
        var model = await s.F.Db.Set<CoachingScoreModel>().AsNoTracking().SingleAsync(x => x.ScoreType == "LGS");
        var coefficients = model.Coefficients().ToDictionary(x => x.Section, x => x.Coefficient);
        coefficients.Values.Should().OnlyContain(x => x > 0);
        new[] { "TUR", "MAT", "FEN" }.Should().OnlyContain(x => coefficients[x] > 1.5m * new[] { "INK", "DIN", "ENG" }.Max(y => coefficients[y]),
            "weight 4 against 1, divided by each test's standard deviation");
    }

    [Theory]
    [InlineData(1.0, 500, 0)]
    [InlineData(0.9, 466.7, 6.7)]
    [InlineData(0.8, 433.3, 13.3)]
    [InlineData(0.7, 400, 20)]
    [InlineData(0.5, 333.3, 30)]   // ± is capped at 30
    public async Task LgsCalibrationAnchors(double share, double score, double margin)
    {
        await using var s = await Setup.CreateAsync(lgsPack: true);
        var model = await s.F.Db.Set<CoachingScoreModel>().AsNoTracking().SingleAsync(x => x.ScoreType == "LGS");
        var lgs = PracticeExamRules.Profiles.Single(x => x.Code == "LGS");
        var value = model.BaseScore + model.Coefficients().Sum(x => x.Coefficient * lgs.Sections.Single(y => y.Code == x.Section).QuestionCount * (decimal)share);
        value.Should().BeApproximately((decimal)score, 0.1m);
        model.Margin(value).Should().BeApproximately((decimal)margin, 0.1m);
    }

    [Fact]
    public async Task LgsStopsAtOneHundred()
    {
        await using var s = await Setup.CreateAsync(lgsPack: true);
        var lgs = PracticeExamRules.Profiles.Single(x => x.Code == "LGS");
        s.Exam("LGS", lgs.Sections.ToDictionary(x => x.Code, x => (0, x.QuestionCount)));
        await s.F.Db.SaveChangesAsync();
        var estimate = (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Estimates.Single();
        (estimate.Score, estimate.ScoreLow).Should().Be((100m, 100m), "MSP runs from 100 to 500");
    }

    // ---------- Admin file rules ----------

    [Fact]
    public async Task OnlyYksNeedsARankTableAndTheRangeRowsAreChecked()
    {
        await using var s = await Setup.CreateAsync();
        string Lgs(string extra) => CoachingScoreEstimateService.Header + "\ntaban;LGS;;;200;;\n" +
            string.Join("\n", new[] { "TUR", "MAT", "FEN", "INK", "DIN", "ENG" }.Select(x => $"katsayi;LGS;LGS;{x};3;;")) + "\n" + extra;
        (await s.Service.PreviewAsync(s.Admin, new(2026, Lgs("belirsizlik;LGS;;;0,2;;\nbelirsizlik_siniri;LGS;;;30;;"), null), default)).Errors.Should().BeEmpty();
        (await s.Service.PreviewAsync(s.Admin, new(2026, Lgs("siralama;LGS;;;500;1;"), null), default)).Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("ya hiç olmamalı ya da en az iki satır");
        (await s.Service.PreviewAsync(s.Admin, new(2026, Lgs("belirsizlik;LGS;;;1,5;;"), null), default)).Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("0 ile 1");
        (await s.Service.PreviewAsync(s.Admin, new(2026, Lgs("belirsizlik;LGS;;;0,2;;\nbelirsizlik_siniri;LGS;;;600;;"), null), default)).Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("0 ile 500");
        var tyt = CoachingScoreEstimateService.Header + "\ntaban;TYT;;;100;;\n" + string.Join("\n",
            PracticeExamRules.Profiles.Single(x => x.Code == "TYT").Sections.Select(x => $"katsayi;TYT;TYT;{x.Code};3;;"));
        (await s.Service.PreviewAsync(s.Admin, new(2026, tyt, null), default)).Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("en az iki sıralama satırı");
        s.Service.Template().Should().Contain("belirsizlik;P1;;;;;").And.Contain("katsayi;P2_16;OABT_DIN;ALAN;;;");
    }

    [Fact]
    public async Task HistoryAndRestoreKeepTheModelsRange()
    {
        await using var s = await Setup.CreateAsync(lgsPack: true);
        var csv = (await File.ReadAllTextAsync(Pack("2026-lgs", "puan-modelleri-2026-lgs.csv"))).Replace("belirsizlik;LGS;;;0.2;;", "belirsizlik;LGS;;;0.3;;");
        await s.Service.ApplyAsync(s.Admin, new(2026, csv, (await s.Service.PreviewAsync(s.Admin, new(2026, csv, null), default)).Fingerprint, "Aralık genişletildi."), default);
        (await s.F.Db.Set<CoachingScoreModel>().AsNoTracking().SingleAsync()).UncertaintyShare.Should().Be(0.3m);

        var history = await s.Service.HistoryAsync(2026, default);
        history.Should().HaveCount(2); history[0].IsCurrent.Should().BeTrue(); history[1].IsCurrent.Should().BeFalse();
        await s.Service.RestoreAsync(s.Admin, history[1].Id, "İlk sürüme dönülüyor.", default);
        var restored = await s.F.Db.Set<CoachingScoreModel>().AsNoTracking().SingleAsync();
        (restored.UncertaintyShare, restored.UncertaintyCap).Should().Be((0.2m, (decimal?)30m));
    }

    // ---------- Review fixes (06.11.2026) ----------

    [Theory]
    [InlineData(440, "borderline", false)]   // inside the card's range 420–446,6
    [InlineData(415, "reached", true)]       // under the range's low end
    [InlineData(460, "below", false)]
    public async Task AnLgsTargetIsReachedOnlyWhenTheWholeRangeIsAboveIt(int minimum, string status, bool reached)
    {
        await using var s = await Setup.CreateAsync(lgsPack: true);
        s.Context(LearningPurpose.Lgs, null);
        s.Exam("LGS", new() { ["TUR"] = (16, 0), ["MAT"] = (16, 0), ["FEN"] = (16, 0), ["INK"] = (8, 0), ["DIN"] = (8, 0), ["ENG"] = (8, 0) });
        await s.Target(minimum);

        var estimate = await s.Service.EstimateAsync(s.Student, s.Student, null, default);

        var lgs = estimate.Estimates.Single();
        (lgs.Score, lgs.ScoreLow, lgs.ScoreHigh).Should().Be((433.33m, 420m, 446.66m));
        var target = estimate.Target!;
        (target.Status, target.Reached, target.Margin, target.RankGap).Should().Be((status, reached, lgs.Margin, (int?)null));
        target.ScoreGap.Should().Be(minimum - 433.33m);
    }

    [Fact]
    public async Task TheGoalSaysWhyThereIsNoScore()
    {
        await using (var s = await Setup.CreateAsync())
        {
            s.Context(LearningPurpose.Lgs, null); await s.F.Db.SaveChangesAsync();
            var goal = (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Goal!;
            (goal.ScoreType, goal.Score, goal.Reason).Should().Be(("LGS", (decimal?)null, "noModel"));
        }
        await using (var s = await Setup.CreateAsync(agsPack: true))
        {
            s.Context(LearningPurpose.Ags, "OABT_IMAT"); await s.F.Db.SaveChangesAsync();
            (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Goal!.Reason.Should().Be("noExam");
            s.Exam("AGS", new() { ["SOZ"] = (10, 0), ["SAY"] = (5, 0), ["TAR"] = (3, 0), ["COG"] = (3, 0), ["EGT"] = (15, 0), ["MEV"] = (4, 0) });
            await s.F.Db.SaveChangesAsync();
            var goal = (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Goal!;
            (goal.ScoreType, goal.Reason).Should().Be(("P2_IMAT", "missingExam"));
            goal.MissingExams.Should().Equal("ÖABT · İlköğretim Matematik");
        }
        await using (var s = await Setup.CreateAsync(agsPack: true))
        {
            s.Context(LearningPurpose.Ags, null);
            s.Exam("AGS", new() { ["SOZ"] = (10, 0), ["SAY"] = (5, 0), ["TAR"] = (3, 0), ["COG"] = (3, 0), ["EGT"] = (15, 0), ["MEV"] = (0, 0) });
            await s.F.Db.SaveChangesAsync();
            var goal = (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Goal!;
            (goal.Reason, goal.NotComputedReason).Should().Be(("notComputed", "minRawScore"));
        }
        await using (var s = await Setup.CreateAsync())
        {
            s.Context(LearningPurpose.Yds, null); await s.F.Db.SaveChangesAsync();
            (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Goal!.Reason.Should().Be("noExam", "YDS needs no model");
        }
    }

    [Fact]
    public async Task TheRangeRowsAreOnlyForScoreTypesWithoutARequiredRankTableAndThePreviewShowsThem()
    {
        await using var s = await Setup.CreateAsync();
        var tyt = CoachingScoreEstimateService.Header + "\ntaban;TYT;;;100;;\n" + string.Join("\n",
            PracticeExamRules.Profiles.Single(x => x.Code == "TYT").Sections.Select(x => $"katsayi;TYT;TYT;{x.Code};3;;")) +
            "\nsiralama;TYT;;;400;10000;\nsiralama;TYT;;;200;800000;\nbelirsizlik;TYT;;;0,1;;";
        (await s.Service.PreviewAsync(s.Admin, new(2026, tyt, null), default)).Errors.Should().ContainSingle()
            .Which.Message.Should().Be("Belirsizlik payı yalnız LGS ve MEB-AGS puan türlerinde kullanılır.");
        var lgs = await File.ReadAllTextAsync(Pack("2026-lgs", "puan-modelleri-2026-lgs.csv"));
        var item = (await s.Service.PreviewAsync(s.Admin, new(2026, lgs, null), default)).Models.Single();
        (item.UncertaintyShare, item.UncertaintyCap).Should().Be((0.2m, (decimal?)30m));
    }

    [Theory]
    // Mixed profiles: (correct, wrong) per section TUR, MAT, FEN, INK, DIN, ENG.
    [InlineData(15, 3, 10, 6, 12, 4, 8, 1, 9, 0, 6, 3)]
    [InlineData(18, 1, 17, 2, 16, 3, 10, 0, 7, 2, 9, 1)]
    public async Task LgsMatchesMebsFormulaComputedFromTheReportInTheTest(int tc, int tw, int mc, int mw, int fc, int fw, int ic, int iw, int dc, int dw, int ec, int ew)
    {
        await using var s = await Setup.CreateAsync(lgsPack: true);
        var answers = new Dictionary<string, (int, int)> { ["TUR"] = (tc, tw), ["MAT"] = (mc, mw), ["FEN"] = (fc, fw), ["INK"] = (ic, iw), ["DIN"] = (dc, dw), ["ENG"] = (ec, ew) };
        s.Exam("LGS", answers);
        await s.F.Db.SaveChangesAsync();
        var score = (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Estimates.Single().Score;

        // MEB 2026 Merkezî Sınav Raporu: (questions, weight, p̄, reliability); sd from KR-20; TASP_min at net −n/5 in every test.
        var report = new Dictionary<string, (double N, double W, double P, double R)>
        { ["TUR"] = (20, 4, .47, .82), ["MAT"] = (20, 4, .28, .86), ["FEN"] = (20, 4, .45, .87), ["INK"] = (10, 1, .53, .81), ["DIN"] = (10, 1, .63, .72), ["ENG"] = (10, 1, .44, .88) };
        double Sd((double N, double W, double P, double R) t) => Math.Sqrt(t.N * t.P * (1 - t.P) / (1 - t.R * (t.N - 1) / t.N));
        double Tasp(Func<string, double> net) => report.Sum(t => t.Value.W * 10 * net(t.Key) / Sd(t.Value));   // the means cancel
        double Net(string k) => answers[k].Item1 - answers[k].Item2 / 3.0;
        var max = Tasp(k => report[k].N); var min = Tasp(k => -report[k].N / 5);
        var expected = 100 + 400 * (Tasp(Net) - min) / (max - min);
        score.Should().BeApproximately((decimal)expected, 0.05m);
    }

    [Fact]
    public async Task P2FollowsOsymsFormulaComputedInTheTest()
    {
        await using var s = await Setup.CreateAsync(agsPack: true);
        var nets = new Dictionary<string, int> { ["SOZ"] = 9, ["SAY"] = 6, ["TAR"] = 2, ["COG"] = 4, ["EGT"] = 18, ["MEV"] = 3 };
        s.Exam("AGS", nets.ToDictionary(x => x.Key, x => (x.Value, 0)));
        s.Exam("OABT_SINIF", new() { ["ALAN"] = (33, 0) });
        await s.F.Db.SaveChangesAsync();
        var p2 = (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Estimates.Single(x => x.ScoreType == "P2_SINIF");

        // P2: the AGS weights halved plus ÖABT Sınıf Öğretmenliği 50 % (mean 26,5, sd 7,1, 50 questions).
        var tests = Ags.ToDictionary(x => x.Key, x => (N: (double)x.Value.N, Mean: (double)x.Value.Mean, Sd: (double)x.Value.Sd, W: (double)P1Weights[x.Key] / 2));
        tests["OABT"] = (50, 26.5, 7.1, 50);
        var given = nets.ToDictionary(x => x.Key, x => (double)x.Value); given["OABT"] = 33;
        double Ap(Func<string, double> net) => tests.Sum(t => t.Value.W / 100 * (50 + 10 * (net(t.Key) - t.Value.Mean) / t.Value.Sd));
        var sumSq = tests.Sum(t => Math.Pow(t.Value.W / 100, 2));
        var sd = 10 * Math.Sqrt(sumSq + 0.5 * (1 - sumSq));
        var b = Ap(k => tests[k].N);
        var expected = 70 + 30 * (2 * (Ap(k => given[k]) - 50) - sd) / (2 * (b - 50) - sd);
        p2.Score.Should().BeApproximately((decimal)expected, 0.02m);
    }

    [Fact]
    public async Task NegativeAgsNetsGiveNoScoreAndTheRangeNeverGoesBelowZero()
    {
        await using (var s = await Setup.CreateAsync(agsPack: true))
        {
            s.Exam("AGS", Ags.ToDictionary(x => x.Key, x => (0, x.Value.N)));
            await s.F.Db.SaveChangesAsync();
            var estimate = await s.Service.EstimateAsync(s.Student, s.Student, null, default);
            estimate.Estimates.Should().BeEmpty();
            estimate.NotComputed.Single().FailedConditions.Select(x => x.AnyOf.Single().Code).Should().Equal("SOZ", "SAY", "TAR", "COG", "EGT", "MEV");
        }
        await using (var s = await Setup.CreateAsync(agsPack: true))
        {
            s.Exam("AGS", Ags.ToDictionary(x => x.Key, x => (1, 0)));   // the lowest profile that is computed
            await s.F.Db.SaveChangesAsync();
            var p1 = (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Estimates.Single();
            p1.ScoreLow.Should().BeGreaterThanOrEqualTo(0); p1.Score.Should().BeInRange(40, 50);
        }
    }

    /// <summary>ÖSYM's 2026 MEB-AGS method computed directly from the published statistics (independent of the pack).</summary>
    private static decimal OsymScore(Dictionary<string, decimal> weights, Dictionary<string, decimal> nets, double rho)
    {
        double Sp(string k, double net) => 50 + 10 * (net - (double)Ags[k].Mean) / (double)Ags[k].Sd;
        var ap = weights.Sum(w => (double)w.Value / 100 * Sp(w.Key, (double)nets[w.Key]));
        var b = weights.Sum(w => (double)w.Value / 100 * Sp(w.Key, Ags[w.Key].N));
        var sumSq = weights.Sum(w => Math.Pow((double)w.Value / 100, 2));
        var sd = 10 * Math.Sqrt(sumSq + rho * (1 - sumSq));
        const double x = 50;
        return (decimal)(70 + 30 * (2 * (ap - x) - sd) / (2 * (b - x) - sd));
    }

    private static string Pack(string folder, string file)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        return Path.Combine(root!.FullName, "content-packs", "coaching-score-models", folder, file);
    }

    private sealed class Setup : IAsyncDisposable
    {
        private int order;
        public CoachingOwnedProfilePersistenceTests.Fixture F { get; } = new();
        public Guid Admin, Student;
        public CoachingScoreEstimateService Service => new(F.Db, F.Scope, TimeProvider.System);

        public static async Task<Setup> CreateAsync(bool agsPack = false, bool lgsPack = false)
        {
            var s = new Setup();
            s.Admin = s.F.AddAccount("SystemAdmin");
            s.Student = s.F.AddAccount("Student");
            await s.F.Store.ProvisionAsync(s.Student, "Student", default, new DateOnly(2004, 1, 1));
            foreach (var (on, folder, file) in new[] { (agsPack, "2026-meb-ags", "puan-modelleri-2026-meb-ags.csv"), (lgsPack, "2026-lgs", "puan-modelleri-2026-lgs.csv") })
            {
                if (!on) continue;
                var csv = await File.ReadAllTextAsync(Pack(folder, file));
                await s.Service.ApplyAsync(s.Admin, new(2026, csv, (await s.Service.PreviewAsync(s.Admin, new(2026, csv, null), default)).Fingerprint), default);
            }
            return s;
        }

        public async Task Target(int minimumScore)
        {
            var school = TargetSchool.Create("meb", "lise-1", "Fen Lisesi", "İstanbul", "Kadıköy", minimumScore, 2026);
            var goal = AcademicGoal.Create(Student, "Fen Lisesi", GoalCategory.ExamPreparation);
            goal.SetCatalogTarget(null, school.Id);
            F.Db.AddRange(school, goal); await F.Db.SaveChangesAsync();
            F.Db.Add(StudentPrimaryGoal.Create(Student, goal.Id)); await F.Db.SaveChangesAsync();
        }

        public void Context(LearningPurpose purpose, string? scoreType) =>
            F.Db.Add(StudentLearningContext.Create(Student, 2026, purpose, 2027, scoreType, null));

        /// <summary>A self-reported practice exam: section → (correct, wrong); the rest of the questions are blank.</summary>
        public void Exam(string code, Dictionary<string, (int Correct, int Wrong)> answers)
        {
            var profile = PracticeExamRules.Profiles.Single(x => x.Code == code);
            var sections = profile.Sections.Select(section =>
            {
                var (correct, wrong) = answers.GetValueOrDefault(section.Code);
                return new PracticeSectionAnswers(section.Code, correct, wrong, section.QuestionCount - correct - wrong);
            }).ToArray();
            var type = PracticeExamRules.ExamTypeFor(code);
            var date = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-(10 + order++)), DateTimeKind.Utc);
            var exam = Coaching.Domain.Entities.Exam.CreateStudentReported(Student, $"{code} denemesi", type, date, 500);
            var result = ExamResult.Create(exam.Id, Student, 0);
            result.SetAnswerStatistics(sections.Sum(x => x.Correct), sections.Sum(x => x.Wrong), sections.Sum(x => x.Empty));
            result.SetPracticeExam(new(code, sections), type);
            exam.AddResult(result);
            F.Db.Add(exam);
        }

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }
}
