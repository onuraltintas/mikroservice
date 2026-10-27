using Coaching.API.Controllers;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap O5: LGS and YKS score and rank estimates from a student's practice exams, using the score models
// (coefficients and score-to-rank tables) that the platform administrator uploads every year.
public sealed class CoachingScoreEstimateTests
{
    private const string Header = "tur;puan_turu;sinav;ders;deger;siralama;yuzdelik";

    /// <summary>TYT = 100 + 3 × each net; LGS = 200 + 4 × (Türkçe, Matematik, Fen) + 2 × the others.</summary>
    private static string Models(string tytRanks = "siralama;TYT;;;400;10000;\nsiralama;TYT;;;300;100000;\nsiralama;TYT;;;200;800000;") =>
        Header + "\n" +
        "taban;TYT;;;100;;\nobp;TYT;;;0,12;;\n" +
        string.Join("\n", new[] { "TUR", "MAT", "TAR", "COG", "FEL", "DIN", "FIZ", "KIM", "BIO" }.Select(x => $"katsayi;TYT;TYT;{x};3;;")) + "\n" +
        tytRanks + "\n" +
        "taban;LGS;;;200;;\n" +
        string.Join("\n", new[] { ("TUR", 4), ("MAT", 4), ("FEN", 4), ("INK", 2), ("DIN", 2), ("ENG", 2) }.Select(x => $"katsayi;LGS;LGS;{x.Item1};{x.Item2};;")) + "\n" +
        "siralama;LGS;;;500;1;0,01\nsiralama;LGS;;;450;20000;2\nsiralama;LGS;;;400;80000;8\nsiralama;LGS;;;300;400000;40";

    [Fact]
    public async Task TheAdministratorPreviewsAndAppliesAYearsScoreModels()
    {
        await using var s = await Setup.CreateAsync();
        var csv = Models();

        var preview = await s.Service.PreviewAsync(s.Admin, new(2025, csv, null), default);

        preview.Errors.Should().BeEmpty();
        preview.Models.Select(x => (x.ScoreType, x.BaseScore, x.Coefficients, x.RankRows, x.Replaces))
            .Should().BeEquivalentTo(new[] { ("TYT", 100m, 9, 3, false), ("LGS", 200m, 6, 4, false) });
        (await s.F.Db.Set<CoachingScoreModel>().AnyAsync()).Should().BeFalse("a preview changes nothing");

        (await s.Service.ApplyAsync(s.Admin, new(2025, csv, preview.Fingerprint), default)).Should().Be(2);
        (await s.Service.ListAsync(default)).Should().Contain(x => x.Year == 2025 && x.ScoreType == "TYT" && x.RankRows == 3);
        (await s.Service.PreviewAsync(s.Admin, new(2025, csv, null), default)).Models.Should().OnlyContain(x => x.Replaces);

        var changed = await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.ApplyAsync(s.Admin, new(2025, csv + "\n", "eski"), default));
        changed.Code.Should().Be("ScoreModel.Changed");
        s.Service.Template().Should().StartWith(Header);
    }

    [Fact]
    public async Task ThePreviewListsProblemsByLineAndABrokenFileIsNotApplied()
    {
        await using var s = await Setup.CreateAsync();
        var csv = Header + "\n" +
            "taban;TYT;;;100;;\n" +
            "katsayi;TYT;TYT;TUR;3;;\n" +                 // the other TYT sections are missing
            "siralama;TYT;;;300;100000;\n" +
            "siralama;TYT;;;400;200000;\n" +              // a higher score cannot have a worse rank
            "katsayi;XYZ;XYZ;TUR;3;;\n" +
            "taban;LGS;;;yüz;;";

        var preview = await s.Service.PreviewAsync(s.Admin, new(2025, csv, null), default);

        preview.Errors.Select(x => x.Line).Should().Contain([6, 7]);
        preview.Errors.Should().Contain(x => x.Message.Contains("Temel Matematik")).And.Contain(x => x.Message.Contains("sıralama"));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.ApplyAsync(s.Admin, new(2025, csv, preview.Fingerprint), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.PreviewAsync(s.Admin, new(1999, Models(), null), default));
    }

    [Fact]
    public async Task TheEstimateUsesTheLastThreeExamsAndGivesAScoreAndRankRange()
    {
        await using var s = await Setup.CreateAsync(models: true);
        s.Exam("TYT", new(2026, 9, 1), turkish: 5, math: 5);
        s.Exam("TYT", new(2026, 9, 15), turkish: 20, math: 20);
        s.Exam("TYT", new(2026, 10, 1), turkish: 25, math: 25);
        s.Exam("TYT", new(2026, 10, 15), turkish: 30, math: 30);
        await s.F.Db.SaveChangesAsync();

        var estimate = await s.Service.EstimateAsync(s.Student, s.Student, null, default);

        var tyt = estimate.Estimates.Should().ContainSingle("only TYT has both a model and practice exams").Subject;
        (tyt.ScoreType, tyt.ModelYear, tyt.ExamsUsed, tyt.LastExamDate).Should().Be(("TYT", 2025, 3, new DateTime(2026, 10, 15)));
        (tyt.Score, tyt.ScoreLow, tyt.ScoreHigh).Should().Be((250m, 220m, 280m), "the average and the weakest and strongest of the last three");
        (tyt.Rank, tyt.RankBest, tyt.RankWorst).Should().Be((450000, 240000, 660000), "ranks are read between the rows of the table");
        tyt.IncludesDiplomaGrade.Should().BeFalse();
        estimate.Notice.Should().BeNull();
    }

    [Fact]
    public async Task OneExamGivesASmallRangeAroundItAndLgsShowsThePercentile()
    {
        await using var s = await Setup.CreateAsync(models: true);
        s.Exam("LGS", new(2026, 10, 1), turkish: 20, math: 20, science: 15, others: 5);
        await s.F.Db.SaveChangesAsync();

        var lgs = (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Estimates.Single();

        lgs.Score.Should().Be(450m, "200 + 4 × 55 + 2 × 15");
        (lgs.ScoreLow, lgs.ScoreHigh).Should().Be((441m, 459m), "a single exam is given ±2%");
        (lgs.Rank, lgs.Percentile).Should().Be((20000, 2m));
    }

    [Fact]
    public async Task TheDiplomaGradeAddsOnlyToYksScores()
    {
        await using var s = await Setup.CreateAsync(models: true);
        s.Exam("TYT", new(2026, 10, 1), turkish: 25, math: 25);
        s.Exam("LGS", new(2026, 10, 1), turkish: 20, math: 20, science: 15, others: 5);
        await s.F.Db.SaveChangesAsync();

        var estimate = await s.Service.EstimateAsync(s.Student, s.Student, 80m, default);

        var tyt = estimate.Estimates.Single(x => x.ScoreType == "TYT");
        (tyt.Score, tyt.IncludesDiplomaGrade).Should().Be((298m, true), "100 + 3 × 50 + 0,12 × (80 × 5)");
        estimate.Estimates.Single(x => x.ScoreType == "LGS").Score.Should().Be(450m);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.EstimateAsync(s.Student, s.Student, 101m, default));
    }

    [Fact]
    public async Task TheGoalShowsHowFarTheStudentIsFromTheTargetSchool()
    {
        await using var s = await Setup.CreateAsync(models: true);
        s.Exam("LGS", new(2026, 10, 1), turkish: 20, math: 16, science: 10, others: 6);
        var school = TargetSchool.Create("meb", "lise-1", "Fen Lisesi", "İstanbul", "Kadıköy", 450m, 2025);
        school.UpdateDetails(new("lise-1", "MEB", "Fen Lisesi", "LGS", "İstanbul", "Kadıköy", null, null, null, null, null, null, 150, 150, 450m, 480m, null, 2m),
            2026, 2025, null, "https://meb.gov.tr/lgs");
        var goal = AcademicGoal.Create(s.Student, "Fen Lisesi", GoalCategory.ExamPreparation);
        goal.SetCatalogTarget(null, school.Id);
        s.F.Db.AddRange(school, goal); await s.F.Db.SaveChangesAsync();
        s.F.Db.Add(StudentPrimaryGoal.Create(s.Student, goal.Id)); await s.F.Db.SaveChangesAsync();

        var target = (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Target!;

        target.Should().BeEquivalentTo(new ScoreTargetGap("Fen Lisesi", "LGS", 450m, null, 2m, 30m, 9m, null, false),
            "the estimate is 420; 30 points are about 9 nets at an average weight of 3,33 points per net");
    }

    [Fact]
    public async Task WithoutAModelOrExamsThereIsNoEstimateButAnExplanation()
    {
        await using var s = await Setup.CreateAsync();
        s.Exam("TYT", new(2026, 10, 1), turkish: 25, math: 25); await s.F.Db.SaveChangesAsync();
        (await s.Service.EstimateAsync(s.Student, s.Student, null, default)).Notice.Should().Contain("henüz yüklenmedi");

        await using var empty = await Setup.CreateAsync(models: true);
        var none = await empty.Service.EstimateAsync(empty.Student, empty.Student, null, default);
        none.Estimates.Should().BeEmpty(); none.Notice.Should().Contain("deneme");
    }

    [Fact]
    public async Task OnlyTheStudentTheirParentsAndTheirTeachersSeeTheEstimate()
    {
        await using var s = await Setup.CreateAsync(models: true);
        var parent = s.F.AddAccount("Parent"); s.F.Accounts.Representatives[(parent, s.Student)] = new(Guid.NewGuid(), "Mother");
        var stranger = s.F.AddAccount("Teacher");

        (await s.Service.EstimateAsync(parent, s.Student, null, default)).Should().NotBeNull();
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.EstimateAsync(stranger, s.Student, null, default));
    }

    [Fact]
    public void UploadingModelsIsForThePlatformAdministrator()
    {
        var controller = typeof(CoachingScoreModelsAdminController);
        controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Should().Contain(x => x.Roles == "SystemAdmin");
        controller.GetMethod(nameof(CoachingScoreModelsAdminController.Apply))!.GetCustomAttributes(typeof(HasPermissionAttribute), true).Should().NotBeEmpty();
        typeof(CoachingScoreEstimateController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Should().NotBeEmpty();
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CoachingOwnedProfilePersistenceTests.Fixture F { get; } = new();
        public Guid Admin, Student;
        public CoachingScoreEstimateService Service => new(F.Db, F.Scope, TimeProvider.System);

        public static async Task<Setup> CreateAsync(bool models = false)
        {
            var s = new Setup();
            s.Admin = s.F.AddAccount("SystemAdmin");
            s.Student = s.F.AddAccount("Student");
            await s.F.Store.ProvisionAsync(s.Student, "Student", default, new DateOnly(2010, 1, 1));
            if (models)
            {
                var csv = Models();
                await s.Service.ApplyAsync(s.Admin, new(2025, csv, (await s.Service.PreviewAsync(s.Admin, new(2025, csv, null), default)).Fingerprint), default);
            }
            return s;
        }

        /// <summary>A practice exam with the given correct answers and no wrong answers (the rest are blank).</summary>
        public void Exam(string code, DateTime date, int turkish = 0, int math = 0, int science = 0, int others = 0)
        {
            var profile = PracticeExamRules.Profiles.Single(x => x.Code == code);
            var sections = profile.Sections.Select(section =>
            {
                var correct = Math.Min(section.QuestionCount, section.Code switch { "TUR" => turkish, "MAT" => math, "FEN" => science, _ => code == "LGS" ? others : 0 });
                return new PracticeSectionAnswers(section.Code, correct, 0, section.QuestionCount - correct);
            }).ToArray();
            var type = code == "LGS" ? ExamType.LGS : ExamType.YKS;
            var exam = Coaching.Domain.Entities.Exam.CreateStudentReported(Student, $"{code} denemesi", type, DateTime.SpecifyKind(date, DateTimeKind.Utc), 500);
            var result = ExamResult.Create(exam.Id, Student, 0);
            result.SetAnswerStatistics(sections.Sum(x => x.Correct), 0, sections.Sum(x => x.Empty));
            result.SetPracticeExam(new(code, sections), type);
            exam.AddResult(result);
            F.Db.Add(exam);
        }

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }
}
