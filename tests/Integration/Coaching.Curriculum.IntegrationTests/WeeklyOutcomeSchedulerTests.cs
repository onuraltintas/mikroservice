using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyOutcomeSchedulerTests
{
    private static readonly Guid Math=Guid.NewGuid(),Turkish=Guid.NewGuid();
    private static readonly DateOnly Start=new(2027,5,24);
    private static WeeklyDraftSlot Slot(Guid lesson,DayOfWeek day,int start=1080,int minutes=30)=>new(Guid.NewGuid(),lesson,lesson==Math?"Matematik":"Türkçe","LGS",day,start,minutes);
    private static WeeklyLessonWork Work(Guid lesson,int minutes)=>new(lesson,lesson==Math?"Matematik":"Türkçe",[new(Guid.NewGuid(),"Konu",Guid.NewGuid(),"K.1","Kazanım",Guid.NewGuid(),1,minutes)]);

    [Fact]
    public void SplitsOutcomeAcrossOwnLessonHours_AndReportsExactFinishAndDeficit()
    {
        var result=WeeklyOutcomeScheduler.Build(Start,new(2027,6,1),[Slot(Math,DayOfWeek.Monday)], [Work(Math,80)],false,[]);
        Assert.Equal(new[]{30,30},result.Tasks.Select(x=>x.Minutes));
        Assert.All(result.Tasks,x=>Assert.Equal(1080,x.StartMinute));
        var summary=Assert.Single(result.Lessons);Assert.Equal(20,summary.RemainingMinutes);Assert.Equal(new DateOnly(2027,6,7),summary.FinishDate);Assert.Equal(30,summary.ExtraWeeklyMinutes);
        Assert.Equal(80,summary.RequiredMinutes);Assert.Equal(60,summary.ScheduledMinutes);
    }
    [Fact]
    public void ReusesOnlyFreedCapacityWhenChosen_AndPreservesTotalMinutes()
    {
        var slots=new[]{Slot(Math,DayOfWeek.Monday),Slot(Turkish,DayOfWeek.Monday,1110)};
        var work=new[]{Work(Math,90),Work(Turkish,30)};
        var normal=WeeklyOutcomeScheduler.Build(Start,new(2027,6,1),slots,work,false,[]);
        var reused=WeeklyOutcomeScheduler.Build(Start,new(2027,6,1),slots,work,true,[]);
        Assert.Equal(30,normal.Lessons.Single(x=>x.LessonId==Math).RemainingMinutes);
        Assert.All(reused.Lessons,x=>Assert.Equal(0,x.RemainingMinutes));Assert.Equal(120,reused.Tasks.Sum(x=>x.Minutes));
        Assert.Contains(reused.Tasks,x=>x.LessonId==Math&&x.StartMinute==1110&&x.ReusedSlot);
        Assert.Equal(new DateOnly(2027,5,31),reused.RecommendedEndDate);
    }
    [Fact]
    public void PacksMultipleOutcomesWithoutRoundingAwayMinutes_AndIncludesDeadline()
    {
        var a=Work(Math,20);var b=Work(Math,40).Outcomes[0];
        var result=WeeklyOutcomeScheduler.Build(Start,Start,[Slot(Math,DayOfWeek.Monday,1080,60)],[a with {Outcomes=[..a.Outcomes,b]}],false,[]);
        Assert.Equal(new[]{1080,1100},result.Tasks.Select(x=>x.StartMinute));Assert.Equal(60,result.Tasks.Sum(x=>x.Minutes));Assert.Equal(Start,Assert.Single(result.Lessons).FinishDate);
    }
    [Fact]
    public void ReservesTimedAndUntimedProtectedWorkWithoutOverlaps()
    {
        var result=WeeklyOutcomeScheduler.Build(Start,Start,[Slot(Math,DayOfWeek.Monday,1080,60)],[Work(Math,90)],false,[new(Start,1095,15),new(Start,null,15)]);
        Assert.Equal(30,result.Tasks.Sum(x=>x.Minutes));Assert.Equal(1110,Assert.Single(result.Tasks).StartMinute);
        Assert.Equal(60,Assert.Single(result.Lessons).RemainingMinutes);
    }
    [Fact]
    public void NoOwnHoursDoesNotPretendCompletion_AndInvalidRangeIsRejected()
    {
        var result=WeeklyOutcomeScheduler.Build(Start,new(2027,6,1),[],[Work(Math,30)],false,[]);
        Assert.Null(result.Lessons[0].FinishDate);Assert.Equal(30,result.Lessons[0].RemainingMinutes);
        Assert.Throws<ArgumentException>(()=>WeeklyOutcomeScheduler.Build(Start,Start.AddDays(-1),[],[],false,[]));
        Assert.Throws<ArgumentException>(()=>WeeklyOutcomeScheduler.Build(Start,Start.AddDays(731),[],[],false,[]));
    }
    [Theory]
    [InlineData(2026,10,19,2027)] [InlineData(2027,5,31,2027)] [InlineData(2027,6,1,2027)] [InlineData(2027,6,2,2028)]
    public void DefaultDeadlineIsNextJuneFirst(int y,int m,int d,int targetYear)=>Assert.Equal(new DateOnly(targetYear,6,1),WeeklyOutcomeScheduler.DefaultEnd(new(y,m,d)));
}
