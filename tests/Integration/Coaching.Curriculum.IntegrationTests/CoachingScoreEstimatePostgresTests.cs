using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Management;
using Microsoft.EntityFrameworkCore;

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
        context.SetTargetExamSession(yds.Sessions[1].Id); f.Db.Add(context);
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

    private sealed class Reader(Guid student) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? institution, bool admin, CancellationToken ct) => Task.FromResult(institution);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(viewerUserId == student ? studentIds : []);
    }
}
