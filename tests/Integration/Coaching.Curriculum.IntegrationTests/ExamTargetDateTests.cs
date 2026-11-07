using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;

namespace Coaching.Curriculum.IntegrationTests;

// G2 (#40) and its review: the exam date of a year-only target and the first year whose exam has not happened yet.
public sealed class ExamTargetDateTests
{
    [Fact]
    public void TheEstimateCarriesTheReferenceDayIntoTheYear()
    {
        Assert.Equal(new DateOnly(2027, 2, 28), CoachingExamSessions.EstimatedDate(new DateOnly(2028, 2, 29), 2027));
        Assert.Equal(new DateOnly(2032, 2, 29), CoachingExamSessions.EstimatedDate(new DateOnly(2028, 2, 29), 2032));
        Assert.Equal(new DateOnly(2028, 6, 15), CoachingExamSessions.EstimatedDate(null, 2028));
    }

    [Fact]
    public void TheReferenceIsTheFirstSessionOfTheLatestKnownYear()
    {
        Assert.Equal(new DateOnly(2027, 3, 28), CoachingExamSessions.EstimateReference(
            [new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 21), new DateOnly(2027, 3, 28)]));
        Assert.Null(CoachingExamSessions.EstimateReference([]));
    }

    [Fact]
    // 10 July 2027: the 2027 YKS and LGS sessions have passed, so 2028 is the first year that can still be chosen.
    public void APassedYearIsNotOpen()
    {
        var july = new DateOnly(2027, 7, 10);
        Assert.Equal(2028, CoachingExamSessions.FirstOpenYear([new DateOnly(2027, 6, 19)], july));
        Assert.Equal(2028, CoachingExamSessions.FirstOpenYear([new DateOnly(2027, 6, 13)], july));
        Assert.Equal(2027, CoachingExamSessions.FirstOpenYear([new DateOnly(2027, 6, 19)], new DateOnly(2027, 6, 1)));
        // No session known: the 15 June guess has passed too.
        Assert.Equal(2028, CoachingExamSessions.FirstOpenYear([], july));
        // YDS: a later session of the same year keeps the year open.
        Assert.Equal(2027, CoachingExamSessions.FirstOpenYear([new DateOnly(2027, 3, 28), new DateOnly(2027, 11, 21)], july));
    }

    [Fact]
    public async Task APassedEstimateFallsBackToTheNextSessionFromItsYear()
    {
        await using var f = new CurriculumManagementTests.Fixture();
        var student = Guid.NewGuid();
        var yds = StudyCatalogExam.Create("YDS", "YDS");
        yds.ReplaceSessions([new(null, new DateOnly(2026, 3, 22), "2026/1"), new(null, new DateOnly(2028, 3, 26), "2028/1")]);
        f.Db.AddRange(yds, StudentLearningContext.Create(student, 2026, LearningPurpose.Yds, 2027, null, "English"));
        await f.Db.SaveChangesAsync();
        var july = new DateOnly(2027, 7, 10);
        // The 2027 estimate (22 March) has passed: the next session from 2027 on is used, never an earlier one.
        Assert.Null(await CoachingExamSessions.ForYearAsync(f.Db, "YDS", 2027, july, default));
        var target = await CoachingExamSessions.ForStudentAsync(f.Db, student, ["YDS"], july, default);
        Assert.Equal((new DateOnly(2028, 3, 26), "Catalog"), (target!.Date, target.Source));
        // Before it, the same year counts down to its estimate (the first session of the latest known year, 26 March 2028).
        var march = await CoachingExamSessions.ForStudentAsync(f.Db, student, ["YDS"], new DateOnly(2027, 1, 5), default);
        Assert.Equal((new DateOnly(2027, 3, 26), "Estimated", false), (march!.Date, march.Source, march.Guessed));
        // An inactive exam's sessions are not used: the guess is 15 June.
        yds.SetActive(false); await f.Db.SaveChangesAsync();
        var guess = await CoachingExamSessions.ForYearAsync(f.Db, "YDS", 2027, new DateOnly(2027, 1, 5), default);
        Assert.Equal((new DateOnly(2027, 6, 15), true), (guess!.Date, guess.Guessed));
    }
}
