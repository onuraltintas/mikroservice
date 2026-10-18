using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.StudyPlanning;
using Coaching.Application.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class OutcomeScopePlanningTests
{
    [Theory]
    [InlineData(false,20,40)]
    [InlineData(true,20,40)]
    [InlineData(false,60,0)]
    [InlineData(true,60,40)]
    public async Task PinnedPortionIsCreditedOnceAndOtherSectionRemains(bool scopedPinnedEstimate,int pinnedMinutes,int expectedNewMinutes)
    {
        await using var f=new CurriculumManagementTests.Fixture();
        await Verify(f,scopedPinnedEstimate,pinnedMinutes,expectedNewMinutes);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PublishedPortionsAndPinnedCreditWorkWithRealPostgresForeignKeys()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();
        await Verify(pg.Data,true,20,40);
    }

    private static async Task Verify(CurriculumManagementTests.Fixture f,bool scopedPinnedEstimate,int pinnedMinutes,int expectedNewMinutes)
    {
        var service=await WeeklyOutcomePlanTests.Seed(f);
        var topic=await f.Db.StudyCatalogTopics.SingleAsync();
        var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var oldEstimate=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();
        var second=StudyCatalogTopic.Create("test","scope-second",topic.LessonId,topic.UnitId,"İkinci bölüm",null,2);second.SetActive(true);
        var mapping=StudyCatalogTopicOutcome.Create(second.Id,outcome.Id,"İkinci bölüm kapsamı");mapping.Review(true,"İkinci bölüm kapsamı",f.User.UserId!.Value);
        var estimate=OutcomeDurationEstimate.Create(outcome.Id,2,20,30,10,"ReviewedStudy","Low","Kapsam payı test kaynağı",f.User.UserId);estimate.Publish();
        var firstShare=StudyCatalogTopicOutcomeShare.Create(topic.Id,estimate,10,10,0,"Birinci bölüm kapsamı");
        var secondShare=StudyCatalogTopicOutcomeShare.Create(second.Id,estimate,10,20,10,"İkinci bölüm kapsamı");
        f.Db.AddRange(second,mapping,estimate,firstShare,secondShare);await f.Db.SaveChangesAsync();
        var firstRead=Assert.Single(await CoachingOutcomeCatalogService.ReadPublishedAsync(f.Db,topic.Id,2026,default));
        Assert.Equal(20,firstRead.TotalMinutes);Assert.True(firstRead.IsTopicShare);
        Assert.Equal(40,Assert.Single(await CoachingOutcomeCatalogService.ReadPublishedAsync(f.Db,second.Id,2026,default)).TotalMinutes);
        var availability=StudyAvailability.Create(f.User.UserId!.Value,"Europe/Istanbul");
        availability.ReplaceWindows([new(DayOfWeek.Monday,1080,1200)]);f.Db.Add(availability);await f.Db.SaveChangesAsync();
        var automatic=new CoachingAutomaticStudyPlanPreviewService(f.Db,new CoachingAccessPolicy(f.User));
        var automaticPreview=await automatic.PreviewAsync(new(new(2027,5,24),7,availability.Version,
            [new(topic.Id,null,[new(outcome.Id,estimate.Id)]),new(second.Id,null,[new(outcome.Id,estimate.Id)])]));
        Assert.Equal(60,automaticPreview.Schedule.ScheduledMinutes);Assert.Equal(2,automaticPreview.Outcomes!.Count);
        Assert.All(automaticPreview.Outcomes,x=>Assert.True(x.IsTopicShare));
        var input=new WeeklyOutcomePlanRequest(new(2027,5,24),new(2027,6,1),0,false);
        var both=await service.PreviewAsync(input,default);
        Assert.Equal(60,both.Schedule.Tasks.Sum(x=>x.Minutes));
        Assert.Contains(both.Schedule.Tasks,x=>x.Outcome.TopicId==topic.Id);
        Assert.Contains(both.Schedule.Tasks,x=>x.Outcome.TopicId==second.Id);
        var old=await f.Db.StudyPlanRevisions.SingleAsync(x=>x.IsActive);
        var pinned=StudyPlanTask.Create(old,input.StartDate,"Sabit bölüm",pinnedMinutes,topic.Id,true);pinned.SetWeeklyPlacement(topic.LessonId,"Matematik",1080);
        f.Db.Add(pinned);f.Db.Add(StudyPlanTaskOutcome.Create(pinned.Id,outcome.Id,scopedPinnedEstimate?estimate.Id:oldEstimate.Id,pinnedMinutes,"Catalog"));
        await f.Db.SaveChangesAsync();
        var preview=await service.PreviewAsync(input,default);
        Assert.Equal(expectedNewMinutes,preview.Schedule.Tasks.Sum(x=>x.Minutes)+preview.Schedule.Lessons.Sum(x=>x.RemainingMinutes));
        Assert.All(preview.Schedule.Tasks,x=>Assert.Equal(second.Id,x.Outcome.TopicId));
        Assert.Equal(60,Assert.Single(preview.Schedule.Lessons).RequiredMinutes);
        Assert.Equal(1,Assert.Single(preview.Schedule.Lessons).OutcomeCount);
        Assert.Equal(pinnedMinutes,(await f.Db.StudyPlanTasks.SingleAsync(x=>x.Id==pinned.Id)).PlannedMinutes);
        Assert.Equal(oldEstimate.Id,(await f.Db.Set<OutcomeDurationEstimate>().SingleAsync(x=>x.Id==oldEstimate.Id)).Id);
    }
}
