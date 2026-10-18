using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class OutcomeScopeAllocationTests
{
    [Fact]
    public void SharedOutcomeRetainsBothTopicPortionsWithoutDoubleCounting()
    {
        var outcome=Guid.NewGuid();var estimate=Guid.NewGuid();var first=Guid.NewGuid();var second=Guid.NewGuid();
        var portions=StudyOutcomeAllocation.Deduplicate([
            new(first,outcome,estimate,20,"Catalog",IsTopicShare:true),
            new(second,outcome,estimate,40,"Catalog",IsTopicShare:true)]);
        Assert.Equal(2,portions.Count);Assert.Equal(60,portions.Sum(x=>x.Minutes));
        Assert.Equal(new[]{first,second},portions.Select(x=>x.TopicId));
    }

    [Fact]
    public void LegacyWholeOutcomeStillDeduplicates_AndConflictingVersionsAreRejected()
    {
        var item=new ResolvedStudyOutcome(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),60,"Catalog");
        Assert.Single(StudyOutcomeAllocation.Deduplicate([item,item with {TopicId=Guid.NewGuid()}]));
        Assert.Throws<ArgumentException>(()=>StudyOutcomeAllocation.Deduplicate([item,item with {Minutes=30}]));
        Assert.Throws<ArgumentException>(()=>StudyOutcomeAllocation.Deduplicate([
            item with {IsTopicShare=true},item with {TopicId=Guid.NewGuid(),EstimateId=Guid.NewGuid(),IsTopicShare=true}]));
        Assert.Throws<ArgumentException>(()=>StudyOutcomeAllocation.Deduplicate([item,item with {TopicId=Guid.NewGuid(),IsTopicShare=true}]));
    }

    [Fact]
    public void VersionedPortionsMustExactlyCoverApprovedTopicsAndEveryMinuteComponent()
    {
        var estimate=OutcomeDurationEstimate.Create(Guid.NewGuid(),2,20,30,10,"ReviewedStudy","Low","İncelenen çalışma süresi",Guid.NewGuid());
        var a=StudyCatalogTopicOutcomeShare.Create(Guid.NewGuid(),estimate,5,10,5,"Birinci bölüm kapsamı");
        var b=StudyCatalogTopicOutcomeShare.Create(Guid.NewGuid(),estimate,15,20,5,"İkinci bölüm kapsamı");
        OutcomeScopeBudget.Validate(estimate,[a,b],[a.TopicId,b.TopicId]);
        Assert.Equal(60,a.TotalMinutes+b.TotalMinutes);
        Assert.Throws<ArgumentException>(()=>OutcomeScopeBudget.Validate(estimate,[a],[a.TopicId,b.TopicId]));
        Assert.Throws<ArgumentException>(()=>OutcomeScopeBudget.Validate(estimate,[a,b],[a.TopicId]));
        var excessive=StudyCatalogTopicOutcomeShare.Create(b.TopicId,estimate,20,20,5,"Fazla öğrenme süresi");
        Assert.Throws<ArgumentException>(()=>OutcomeScopeBudget.Validate(estimate,[a,excessive],[a.TopicId,b.TopicId]));
        Assert.Throws<ArgumentException>(()=>StudyCatalogTopicOutcomeShare.Create(a.TopicId,estimate,0,0,0,"Boş süre kabul edilmez"));
        Assert.Throws<ArgumentException>(()=>StudyCatalogTopicOutcomeShare.Create(a.TopicId,estimate,-1,10,0,"Negatif süre kabul edilmez"));
    }

    [Fact]
    public void SharedOutcomeSchedulesBothSectionsWithTheirOwnTopicNames()
    {
        var lesson=Guid.NewGuid();var outcome=Guid.NewGuid();var estimate=Guid.NewGuid();var a=Guid.NewGuid();var b=Guid.NewGuid();
        var date=new DateOnly(2027,5,24);
        var result=WeeklyOutcomeScheduler.Build(date,date,
            [new(Guid.NewGuid(),lesson,"Türkçe","LGS",DayOfWeek.Monday,1080,60)],
            [new(lesson,"Türkçe",[new(a,"Paragrafta Anlam 1",outcome,"T.8.3.17","Ana fikir",estimate,2,20),
                new(b,"Paragrafta Anlam 2",outcome,"T.8.3.17","Ana fikir",estimate,2,40)])],false,[]);
        Assert.Equal(60,result.Tasks.Sum(x=>x.Minutes));
        Assert.Contains(result.Tasks,x=>x.Outcome.TopicId==a);
        Assert.Contains(result.Tasks,x=>x.Outcome.TopicId==b);
    }
}
