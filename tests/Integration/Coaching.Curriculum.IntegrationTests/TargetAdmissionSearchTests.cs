using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;
public sealed class TargetAdmissionSearchTests
{
    [Fact]
    public async Task FiltersOnlyReturnPublishedVerifiedMatchingRows_AndPreserveMissingRanks()
    {
        using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var batch=TargetAdmissionBatch.Create("YKS",2026,"Main","https://cdn.osym.gov.tr/results.xlsx",new string('e',64));batch.Publish();db.Add(batch);
        var data=new TargetAdmissionData("1","University","Engineering","SAY","ANKARA",null,"DEVLET","Bachelor","İngilizce","Burslu",null,null,20,20,400,450,null,null);
        var target=TargetUniversityProgram.Create("test","1","University","Engineering","1","SAY",null);target.SetActive(true);db.Add(target);
        var a=TargetAdmissionRecord.Create(batch.Id,data);a.Match(null,target.Id,"Verified");db.Add(a);
        var b=TargetAdmissionRecord.Create(batch.Id,data with{ExternalCode="2"});b.MarkIssue("Ambiguous","Review candidates");db.Add(b);await db.SaveChangesAsync();
        var service=new TargetAdmissionSearchService(db);
        var result=await service.SearchAsync(new("YKS",2026,City:"ANKARA",ScoreType:"SAY",InstitutionType:"DEVLET",Degree:"Bachelor",Language:"İngilizce",Scholarship:"Burslu"));
        Assert.Single(result.Items);Assert.Equal(target.Id,result.Items[0].TargetId);Assert.Null(result.Items[0].Rank);
        Assert.Empty((await service.SearchAsync(new("YKS",2026,MaximumRank:10000))).Items);
        Assert.Empty((await service.SearchAsync(new("YKS",2026,City:"İZMİR"))).Items);
        Assert.Throws<ArgumentException>(()=>TargetAdmissionSearchService.Validate(new("YKS",2025)));
        Assert.Throws<ArgumentException>(()=>TargetAdmissionSearchService.Validate(new("YKS",2026,MinimumScore:500,MaximumScore:400)));
    }
}
