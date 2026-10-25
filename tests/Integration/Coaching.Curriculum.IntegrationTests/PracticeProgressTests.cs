using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class PracticeProgressTests
{
    [Fact]
    public void SameDayExamsUseRecordedTimeBeforeTheUuidForTheLatestResult()
    {
        var date=new DateTime(2026,10,20);
        var older=new PracticeEvidence(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),"İlk",date,"StudentReported",new("YDT_EN",[new("ENG",50,0,30)]),date.AddHours(9));
        var newer=new PracticeEvidence(Guid.Parse("00000000-0000-0000-0000-000000000001"),"Son",date,"StudentReported",new("YDT_EN",[new("ENG",60,0,20)]),date.AddHours(10));
        var trend=Assert.Single(PracticeProgressCalculator.Calculate([newer,older]));
        Assert.Equal(60,trend.Points[^1].Net);Assert.Equal(10,trend.Change);
    }
    [Fact]
    public void TrendsSeparateScopeAndSourceAndUseOnlyTheLatestSixInChronologicalOrder()
    {
        var evidence = Enumerable.Range(1, 8).Select(i => new PracticeEvidence(Guid.NewGuid(), "Deneme", new DateTime(2026, 10, i), "StudentReported",
            new("YDT_EN", [new("ENG", i * 5, 0, 80 - i * 5)]))).ToList();
        evidence.Add(new(Guid.NewGuid(), "Öğretmen", new(2026,10,9), "TeacherRecorded", new("YDT_EN", [new("ENG", 60, 0, 20)])));
        var groups = PracticeProgressCalculator.Calculate(evidence);
        Assert.Equal(2, groups.Count);
        var own = groups.Single(x => x.Source == "StudentReported");
        Assert.Equal(6, own.Points.Count); Assert.Equal(15, own.Points[0].Net); Assert.Equal(40, own.Points[^1].Net);
        Assert.Equal(25, own.Change); Assert.True(own.Sections.Single().NeedsReview);
    }

    [Fact]
    public void NetTargetIsBoundedByTheSelectedExamAndDoesNotAlterRecordedGoalProgress()
    {
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Net hedefim", Coaching.Domain.Enums.GoalCategory.ExamPreparation); goal.UpdateProgress(20);
        goal.SetNetTarget("LGS", 75);
        Assert.Equal(75, goal.TargetNet); Assert.Equal(20, goal.CurrentProgress);
        Assert.Throws<ArgumentException>(() => goal.SetNetTarget("LGS", 91));
        Assert.Throws<ArgumentException>(() => goal.SetNetTarget("TYT", null));
        Assert.Equal("LGS", goal.TargetPracticeExamCode);
        goal.SetNetTarget(null, null); Assert.Null(goal.TargetNet);
    }
}
