using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class OutcomeScopeCatalogTests
{
    [Fact]
    public async Task PublicationRequiresCompleteScope_AndFutureDurationEditsPreserveItsDistribution()
    {
        await using var f=new CurriculumManagementTests.Fixture();var topic=await f.Seed();
        var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var original=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();
        var second=StudyCatalogTopic.Create("test","second-scope",topic.LessonId,topic.UnitId,"İkinci kapsam",null,2);second.SetActive(true);
        var mapping=StudyCatalogTopicOutcome.Create(second.Id,outcome.Id,"İkinci kapsam eşleştirmesi");mapping.Review(true,"İkinci kapsam eşleştirmesi",f.User.UserId!.Value);
        f.Db.AddRange(second,mapping);await f.Db.SaveChangesAsync();
        var service=new CoachingOutcomeCatalogService(f.Db,f.Scope,f.User);
        var request=new OutcomeEstimateRequest(20,30,10,"İncelenen konu kapsamı süreleri","Low","Kapsam paylarını yayınla",
            [new(topic.Id,5,10,5,"İlk bölümün kapsamı"),new(second.Id,15,20,5,"İkinci bölümün kapsamı")]);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.EstimateAsync(outcome.Id,request with {TopicShares=[request.TopicShares![0]]},default));
        Assert.Single(await f.Db.Set<OutcomeDurationEstimate>().ToArrayAsync());
        var version=await service.EstimateAsync(outcome.Id,request,default);
        await service.PublishAsync(version.Id,"Konu paylarını onayla",default);
        Assert.Equal(20,Assert.Single(await service.GetPublishedAsync(topic.Id,2026,default)).TotalMinutes);
        Assert.Equal(40,Assert.Single(await service.GetPublishedAsync(second.Id,2026,default)).TotalMinutes);
        var next=await service.EstimateAsync(outcome.Id,new(40,60,20,"Güncellenen kapsam süreleri","Low","Süreyi yeniden değerlendir"),default);
        await service.PublishAsync(next.Id,"Yeni süre sürümünü onayla",default);
        Assert.Equal(40,Assert.Single(await service.GetPublishedAsync(topic.Id,2026,default)).TotalMinutes);
        Assert.Equal(80,Assert.Single(await service.GetPublishedAsync(second.Id,2026,default)).TotalMinutes);
        Assert.Equal(60,original.TotalMinutes);Assert.True(original.IsPublished);
        Assert.Equal(60,(await f.Db.Set<StudyCatalogTopicOutcomeShare>().Where(x=>x.EstimateId==version.Id).ToArrayAsync()).Sum(x=>x.TotalMinutes));
        await service.ReviewMappingAsync(second.Id,outcome.Id,new(false,"Bu kapsamı yeniden incele"),default);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.EstimateAsync(outcome.Id,new(20,30,10,"Değişen konu kapsamı","Low","Yeni kapsamı incele"),default));
    }
}
