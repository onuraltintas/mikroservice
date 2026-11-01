using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Management;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// KM4 (01.11.2026): the 2026 YKS score models in content-packs/coaching-score-models/2026 go through the real admin import
// (preview → apply) without errors, reproduce ÖSYM's published anchors and read ÖSYM's 2026 placement distribution back.
public sealed class CoachingScoreModelPack2026Tests
{
    private static string PackFile()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        return Path.Combine(root!.FullName, "content-packs", "coaching-score-models", "2026", "puan-modelleri-2026.csv");
    }

    [Fact]
    public async Task ThePackPreviewsWithoutErrorsAndApplies()
    {
        await using var s = await Setup.CreateAsync();
        var csv = await File.ReadAllTextAsync(PackFile());

        var preview = await s.Service.PreviewAsync(s.Admin, new(2026, csv, null), default);

        preview.Errors.Should().BeEmpty();
        preview.Models.Select(x => (x.ScoreType, x.Coefficients)).Should().BeEquivalentTo(new[]
            { ("TYT", 9), ("SAY", 13), ("EA", 13), ("SOZ", 16), ("DIL", 10) });
        preview.Models.Should().OnlyContain(x => x.RankRows >= 100);
        (await s.Service.ApplyAsync(s.Admin, new(2026, csv, preview.Fingerprint), default)).Should().Be(5);

        // ÖSYM 2026-YKS Sayısal Bilgiler, cumulative placement-score distribution (candidates at or above the score).
        var models = await s.F.Db.Set<CoachingScoreModel>().AsNoTracking().ToDictionaryAsync(x => x.ScoreType);
        foreach (var (type, score, osym) in new[] { ("TYT", 350m, 412_011), ("SAY", 450m, 63_669), ("EA", 390m, 58_772), ("SOZ", 370m, 45_237), ("DIL", 350m, 43_129) })
        {
            models[type].Year.Should().Be(2026);
            models[type].DiplomaWeight.Should().Be(0.12m);
            models[type].RankOf(score).Rank.Should().BeCloseTo(osym, (uint)(osym / 100), $"{type} {score}");
        }
        models["TYT"].RankOf(560).Rank.Should().Be(1);
        models["TYT"].RankOf(300).Percentile.Should().BeInRange(30, 50);
    }

    [Fact]
    public async Task TheEstimateReproducesOsymsAnchorProfiles()
    {
        await using var s = await Setup.CreateAsync();
        var csv = await File.ReadAllTextAsync(PackFile());
        await s.Service.ApplyAsync(s.Admin, new(2026, csv, (await s.Service.PreviewAsync(s.Admin, new(2026, csv, null), default)).Fingerprint), default);

        // ÖSYM: 5 net in Türkçe and Temel Matematik and 2,5 in the other TYT tests gives 150 TYT points.
        s.Exam("TYT", new() { ["TUR"] = (5, 0), ["MAT"] = (5, 0), ["TAR"] = (2, 0), ["COG"] = (1, 2), ["FIZ"] = (2, 0), ["KIM"] = (1, 2) });
        // ÖSYM: 20% of every test's questions gives 170 SAY points (AYT nets here are a little under 20%: 7,75 / 2,75 / 2,5 / 2,5).
        s.Exam("TYT", new() { ["TUR"] = (8, 0), ["MAT"] = (8, 0), ["TAR"] = (4, 0), ["FIZ"] = (4, 0) }, daysAgo: 0);
        s.Exam("AYT_SAY", new() { ["MAT"] = (8, 1), ["FIZ"] = (3, 1), ["KIM"] = (3, 2), ["BIO"] = (3, 2) });
        await s.F.Db.SaveChangesAsync();

        var estimate = await s.Service.EstimateAsync(s.Student, s.Student, null, default);
        var tyt = estimate.Estimates.Single(x => x.ScoreType == "TYT");
        tyt.ScoreLow.Should().BeApproximately(150m, 0.5m, "the weaker TYT deneme is ÖSYM's 150-point profile");
        var say = estimate.Estimates.Single(x => x.ScoreType == "SAY");
        say.ScoreHigh.Should().BeInRange(168.5m, 170.5m);
        say.ModelYear.Should().Be(2026);

        var withDiploma = await s.Service.EstimateAsync(s.Student, s.Student, 80, default);
        (withDiploma.Estimates.Single(x => x.ScoreType == "TYT").Score - tyt.Score).Should().Be(48m, "OBP 400 × 0,12");
    }

    private sealed class Setup : IAsyncDisposable
    {
        private int order;
        public CoachingOwnedProfilePersistenceTests.Fixture F { get; } = new();
        public Guid Admin, Student;
        public CoachingScoreEstimateService Service => new(F.Db, F.Scope, TimeProvider.System);

        public static async Task<Setup> CreateAsync()
        {
            var s = new Setup();
            s.Admin = s.F.AddAccount("SystemAdmin");
            s.Student = s.F.AddAccount("Student");
            await s.F.Store.ProvisionAsync(s.Student, "Student", default, new DateOnly(2008, 1, 1));
            return s;
        }

        /// <summary>A self-reported practice exam: section → (correct, wrong); the rest of the questions are blank.</summary>
        public void Exam(string code, Dictionary<string, (int Correct, int Wrong)> answers, int? daysAgo = null)
        {
            var profile = PracticeExamRules.Profiles.Single(x => x.Code == code);
            var sections = profile.Sections.Select(section =>
            {
                var (correct, wrong) = answers.GetValueOrDefault(section.Code);
                return new PracticeSectionAnswers(section.Code, correct, wrong, section.QuestionCount - correct - wrong);
            }).ToArray();
            var date = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-(daysAgo ?? 10 + order++)), DateTimeKind.Utc);
            var exam = Coaching.Domain.Entities.Exam.CreateStudentReported(Student, $"{code} denemesi", ExamType.YKS, date, 500);
            var result = ExamResult.Create(exam.Id, Student, 0);
            result.SetAnswerStatistics(sections.Sum(x => x.Correct), sections.Sum(x => x.Wrong), sections.Sum(x => x.Empty));
            result.SetPracticeExam(new(code, sections), ExamType.YKS);
            exam.AddResult(result);
            F.Db.Add(exam);
        }

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }
}
