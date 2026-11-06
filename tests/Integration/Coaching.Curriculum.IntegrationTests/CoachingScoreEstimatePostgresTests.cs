using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap O5: score models round-trip through jsonb and the estimate query runs on PostgreSQL.
public sealed class CoachingScoreEstimatePostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ScoreModelsAndEstimatesRunOnPostgres()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var admin = Guid.NewGuid();
        var service = new CoachingScoreEstimateService(f.Db, new Reader(student), TimeProvider.System);
        var csv = CoachingScoreEstimateService.Header + "\ntaban;LGS;;;200;;\n" +
            string.Join("\n", new[] { ("TUR", 4), ("MAT", 4), ("FEN", 4), ("INK", 2), ("DIN", 2), ("ENG", 2) }.Select(x => $"katsayi;LGS;LGS;{x.Item1};{x.Item2};;")) +
            "\nsiralama;LGS;;;500;1;0,01\nsiralama;LGS;;;400;80000;8";
        var preview = await service.PreviewAsync(admin, new(2025, csv, null), default);
        Assert.Empty(preview.Errors);
        Assert.Equal(1, await service.ApplyAsync(admin, new(2025, csv, preview.Fingerprint), default));
        Assert.Equal(1, await service.ApplyAsync(admin, new(2025, csv, preview.Fingerprint), default)); // replaced, not duplicated
        Assert.Equal(1, await f.Db.Set<CoachingScoreModel>().CountAsync());

        var profile = PracticeExamRules.Profiles.Single(x => x.Code == "LGS");
        var sections = profile.Sections.Select(x => new PracticeSectionAnswers(x.Code, x.QuestionCount / 2, 0, x.QuestionCount - x.QuestionCount / 2)).ToArray();
        var exam = Exam.CreateStudentReported(student, "LGS denemesi", ExamType.LGS, DateTime.UtcNow.Date, 500);
        var result = ExamResult.Create(exam.Id, student, 0);
        result.SetAnswerStatistics(sections.Sum(x => x.Correct), 0, sections.Sum(x => x.Empty));
        result.SetPracticeExam(new("LGS", sections), ExamType.LGS);
        exam.AddResult(result); f.Db.Add(exam); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var lgs = Assert.Single((await service.EstimateAsync(student, student, null, default)).Estimates);
        Assert.Equal(350m, lgs.Score); // 200 + 4 × 30 + 2 × 15
        Assert.Equal(80000, lgs.Rank);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    // KM1 (01.11.2026): YDS and YÖKDİL scores need no uploaded model: the ÖSYM guides add up the correct answers out of 100
    // (80 questions, so 1.25 points each). AGS has no estimate yet and says so. The countdown uses the session the student chose.
    public async Task LanguageScoresAreTheCorrectAnswersOutOfAHundredAndAgsHasAClearEmptyState()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid();
        var service = new CoachingScoreEstimateService(f.Db, new Reader(student), TimeProvider.System);
        void Add(string code, ExamType type, int correct, int wrong, int daysAgo)
        {
            var exam = Exam.CreateStudentReported(student, code + " denemesi", type, DateTime.UtcNow.Date.AddDays(-daysAgo), 100);
            var result = ExamResult.Create(exam.Id, student, 0);
            result.SetAnswerStatistics(correct, wrong, 80 - correct - wrong);
            result.SetPracticeExam(new(code, [new("ENG", correct, wrong, 80 - correct - wrong)]), type);
            exam.AddResult(result); f.Db.Add(exam);
        }
        Add("YDS_EN", ExamType.YDS, 60, 20, 10); Add("YDS_EN", ExamType.YDS, 64, 10, 5); Add("YOKDIL_SAG", ExamType.YOKDIL, 70, 2, 1);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Coaching.Domain.CoachingTime.Turkey));
        var yds = StudyCatalogExam.Create("YDS", "YDS");
        yds.ReplaceSessions([new(null, today.AddDays(30), "Bahar", null, null), new(null, today.AddDays(200), "Güz", null, null)]);
        f.Db.Add(yds); await f.Db.SaveChangesAsync();
        var context = StudentLearningContext.Create(student, 2026, LearningPurpose.Yds, today.AddDays(200).Year, null, null);
        context.SetTargetExamSession(yds.Sessions[1].Id); f.Db.AddRange(Coaching.Domain.Profiles.CoachingStudentProfile.Create(student), context);
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var estimate = await service.EstimateAsync(student, student, null, default);
        Assert.Empty(estimate.Estimates);
        Assert.Null(estimate.Notice);
        Assert.Equal(new[] { ("YDS_EN", 77.5m, 75m, 80m, 2), ("YOKDIL_SAG", 87.5m, 87.5m, 87.5m, 1) },
            estimate.LanguageScores.Select(x => (x.ExamCode, x.Score, x.ScoreLow, x.ScoreHigh, x.ExamsUsed)).ToArray());
        Assert.Equal(("YDS · Güz", 200), (estimate.TargetExam!.Name, estimate.TargetExam.DaysLeft));

        await f.Db.StudentLearningContexts.ExecuteDeleteAsync(); await f.Db.ExamResults.ExecuteDeleteAsync();
        f.Db.Add(StudentLearningContext.Create(student, 2026, LearningPurpose.Ags, DateTime.UtcNow.Year + 1, "OABT_OZEL", null)); await f.Db.SaveChangesAsync();
        estimate = await service.EstimateAsync(student, student, null, default);
        Assert.Empty(estimate.LanguageScores);
        Assert.Equal("AGS ve ÖABT için puan tahmini henüz yok. Netlerini Denemeler bölümünden izleyebilirsin.", estimate.Notice);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    // 01.11.2026: ÖSYM's 0,5 ham puan condition (2026 YKS Kılavuzu, Tablo 1D) on the stored practice exams: TYT is not computed
    // with 0,25 in Türkçe and 0 in Temel Matematik, whatever the other tests.
    public async Task TytWithoutHalfARawPointInTurkceOrTemelMatematikIsNotComputedOnPostgres()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var admin = Guid.NewGuid();
        var service = new CoachingScoreEstimateService(f.Db, new Reader(student), TimeProvider.System);
        var tyt = PracticeExamRules.Profiles.Single(x => x.Code == "TYT");
        var csv = CoachingScoreEstimateService.Header + "\ntaban;TYT;;;100;;\n" +
            string.Join("\n", tyt.Sections.Select(x => $"katsayi;TYT;TYT;{x.Code};3;;")) + "\nsiralama;TYT;;;400;10000;\nsiralama;TYT;;;200;800000;";
        await service.ApplyAsync(admin, new(2026, csv, (await service.PreviewAsync(admin, new(2026, csv, null), default)).Fingerprint), default);

        var sections = tyt.Sections.Select(x => x.Code switch
        {
            "TUR" => new PracticeSectionAnswers("TUR", 1, 3, 36),
            "MAT" => new PracticeSectionAnswers("MAT", 0, 0, 40),
            _ => new PracticeSectionAnswers(x.Code, x.QuestionCount, 0, 0)
        }).ToArray();
        var exam = Exam.CreateStudentReported(student, "TYT denemesi", ExamType.YKS, DateTime.UtcNow.Date, 500);
        var result = ExamResult.Create(exam.Id, student, 0);
        result.SetAnswerStatistics(sections.Sum(x => x.Correct), sections.Sum(x => x.Wrong), sections.Sum(x => x.Empty));
        result.SetPracticeExam(new("TYT", sections), ExamType.YKS);
        exam.AddResult(result); f.Db.Add(exam); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var estimate = await service.EstimateAsync(student, student, null, default);

        Assert.Empty(estimate.Estimates);
        var item = Assert.Single(estimate.NotComputed);
        Assert.Equal(("TYT", "minRawScore"), (item.ScoreType, item.NotComputedReason));
        Assert.Equal(new[] { ("TUR", 0.25m), ("TMAT", 0m) }, item.FailedConditions.Single().AnyOf.Select(x => (x.Code, x.RawScore)).ToArray());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    // Paket B (05.11.2026): the 2026 MEB-AGS and LGS packs go through the admin import on PostgreSQL (new score types, no rank
    // table, the model's ± range) and an AGS + ÖABT student gets P1 and P2 with ranges; LGS has no rank.
    public async Task MebAgsAndLgsPacksApplyAndEstimateOnPostgres()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        Assert.False(f.Db.Database.HasPendingModelChanges());
        var student = Guid.NewGuid(); var admin = Guid.NewGuid();
        var service = new CoachingScoreEstimateService(f.Db, new Reader(student), TimeProvider.System);
        foreach (var (folder, file, count) in new[] { ("2026-meb-ags", "puan-modelleri-2026-meb-ags.csv", 19), ("2026-lgs", "puan-modelleri-2026-lgs.csv", 1) })
        {
            var csv = await File.ReadAllTextAsync(Pack(folder, file));
            var preview = await service.PreviewAsync(admin, new(2026, csv, null), default);
            Assert.Empty(preview.Errors);
            Assert.Equal(count, await service.ApplyAsync(admin, new(2026, csv, preview.Fingerprint, "2026 modelleri yüklendi."), default));
        }
        Assert.Equal(20, await f.Db.Set<CoachingScoreModelVersion>().CountAsync());
        f.Db.AddRange(Coaching.Domain.Profiles.CoachingStudentProfile.Create(student),
            StudentLearningContext.Create(student, 2026, LearningPurpose.Ags, DateTime.UtcNow.Year + 1, "OABT_SINIF", null));
        void Add(string code, Func<PracticeSection, PracticeSectionAnswers> answers)
        {
            var sections = PracticeExamRules.Profiles.Single(x => x.Code == code).Sections.Select(answers).ToArray();
            var type = PracticeExamRules.ExamTypeFor(code);
            var exam = Exam.CreateStudentReported(student, code + " denemesi", type, DateTime.UtcNow.Date.AddDays(-1), 100);
            var result = ExamResult.Create(exam.Id, student, 0);
            result.SetAnswerStatistics(sections.Sum(x => x.Correct), sections.Sum(x => x.Wrong), sections.Sum(x => x.Empty));
            result.SetPracticeExam(new(code, sections), type);
            exam.AddResult(result); f.Db.Add(exam);
        }
        Add("AGS", x => new(x.Code, x.QuestionCount / 2, 2, x.QuestionCount - x.QuestionCount / 2 - 2));
        Add("OABT_SINIF", x => new(x.Code, 30, 8, 12));
        Add("LGS", x => new(x.Code, x.QuestionCount, 0, 0));
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var estimate = await service.EstimateAsync(student, student, null, default);

        Assert.Equal(new[] { "LGS", "P1", "P2_SINIF" }, estimate.Estimates.Select(x => x.ScoreType).Order().ToArray());
        var p2 = estimate.Estimates.Single(x => x.ScoreType == "P2_SINIF");
        Assert.InRange(p2.Score, 50, 100);
        Assert.True(p2.Margin > 0 && p2.ScoreLow <= p2.Score - p2.Margin && p2.ScoreHigh >= p2.Score + p2.Margin);
        Assert.Null(p2.Rank);
        Assert.Equal(("P2_SINIF", p2.Score), (estimate.Goal!.ScoreType, estimate.Goal.Score));
        var lgs = estimate.Estimates.Single(x => x.ScoreType == "LGS");
        Assert.Equal((500m, (int?)null), (Math.Round(lgs.Score), lgs.Rank));

        // Down refuses while MEB-AGS models exist. EF runs each Down in its own transaction, so later migrations (e.g.
        // 20261113090000_StudentOnboarding) may be reverted first; ScoreModelsAllExams itself stays applied and Up re-applies the rest.
        await Assert.ThrowsAnyAsync<Exception>(() => f.Db.GetService<IMigrator>().MigrateAsync("20261111090000_TopicMinutesCap4320"));
        Assert.Contains("20261112090000_ScoreModelsAllExams", await f.Db.Database.GetAppliedMigrationsAsync());
        await f.Db.Database.MigrateAsync();
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    // Review 06.11.2026: the previous release cannot read an LGS model without a rank table either, so Down refuses then too;
    // without such rows it goes down and up again.
    public async Task DownRefusesWhileAnLgsModelHasNoRankTable()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var admin = Guid.NewGuid();
        var service = new CoachingScoreEstimateService(f.Db, new Reader(Guid.NewGuid()), TimeProvider.System);
        await f.Db.GetService<IMigrator>().MigrateAsync("20261111090000_TopicMinutesCap4320");
        await f.Db.Database.MigrateAsync(); f.Db.ChangeTracker.Clear();
        var csv = await File.ReadAllTextAsync(Pack("2026-lgs", "puan-modelleri-2026-lgs.csv"));
        await service.ApplyAsync(admin, new(2026, csv, (await service.PreviewAsync(admin, new(2026, csv, null), default)).Fingerprint, "LGS 2026 yüklendi."), default);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => f.Db.GetService<IMigrator>().MigrateAsync("20261111090000_TopicMinutesCap4320"));
        Assert.Contains("without a rank table", error.ToString());
        Assert.Contains("20261112090000_ScoreModelsAllExams", await f.Db.Database.GetAppliedMigrationsAsync());
        await f.Db.Database.MigrateAsync();
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
        Assert.Equal(0.2m, (await f.Db.Set<CoachingScoreModel>().AsNoTracking().SingleAsync()).UncertaintyShare);
    }

    private static string Pack(string folder, string file)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        return Path.Combine(root!.FullName, "content-packs", "coaching-score-models", folder, file);
    }

    private sealed class Reader(Guid student) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? institution, bool admin, CancellationToken ct) => Task.FromResult(institution);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(viewerUserId == student ? studentIds : []);
    }
}
