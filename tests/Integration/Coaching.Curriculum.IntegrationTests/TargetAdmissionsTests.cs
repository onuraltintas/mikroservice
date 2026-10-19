using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class TargetAdmissionsTests
{
    private static TargetAdmissionData Data() => new("106510077", "University", "Computer Engineering", "SAY",
        "Ankara", null, "State", "Bachelor", "English", null, "Onsite", null, 80, 80,
        443.22731m, 492.64166m, null, null);

    [Theory]
    [InlineData(2025, "https://cdn.osym.gov.tr/results.xlsx")]
    [InlineData(2026, "https://osym.gov.tr.example.com/results.xlsx")]
    [InlineData(2026, "http://cdn.osym.gov.tr/results.xlsx")]
    public void BatchRejectsOldYearAndUnofficialOrInsecureSources(int year, string url) =>
        Assert.Throws<ArgumentException>(() => TargetAdmissionBatch.Create("YKS", year, "Main", url, new string('a',64)));

    [Fact]
    public void PendingRowCannotBecomeSelectableWithoutExactTarget()
    {
        var batch = TargetAdmissionBatch.Create("YKS", 2026, "Main", "https://cdn.osym.gov.tr/results.xlsx", new string('a',64));
        var row = TargetAdmissionRecord.Create(batch.Id, Data());
        Assert.Equal("Pending", row.MatchStatus);
        Assert.Null(row.ProgramId);
        Assert.Throws<ArgumentException>(()=>row.Match(null,null,"Verified"));
        Assert.Throws<ArgumentException>(()=>row.Match(Guid.NewGuid(),Guid.NewGuid(),"Verified"));
        var id=Guid.NewGuid(); row.Match(null,id,"Verified");
        Assert.Equal(id,row.ProgramId);
        Assert.Equal(443.22731m,row.MinimumScore);
    }

    [Fact]
    public void InvalidRangesAndMissingIdentityAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>TargetAdmissionRecord.Create(Guid.NewGuid(), Data() with {ExternalCode=""}));
        Assert.Throws<ArgumentException>(()=>TargetAdmissionRecord.Create(Guid.NewGuid(), Data() with {Percentile=101}));
        Assert.Throws<ArgumentException>(()=>TargetAdmissionRecord.Create(Guid.NewGuid(), Data() with {MinimumScore=500,MaximumScore=400}));
        Assert.Throws<ArgumentException>(()=>TargetAdmissionRecord.Create(Guid.NewGuid(), Data() with {Rank=0}));
    }

    [Fact]
    public void AdditiveModelKeepsLegacyIdentityAndSeparatesAmbiguousRows()
    {
        using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var batch=TargetAdmissionBatch.Create("YKS",2026,"Main","https://cdn.osym.gov.tr/results.xlsx",new string('b',64));
        var program=TargetUniversityProgram.Create("legacy","old-id","University","Program","106510077","SAY",440);
        db.TargetUniversityPrograms.Add(program);
        var row=TargetAdmissionRecord.Create(batch.Id,Data()); row.Match(null,program.Id,"Verified");
        var ambiguous=TargetAdmissionRecord.Create(batch.Id,Data() with {ExternalCode="other"});
        ambiguous.MarkIssue("Ambiguous","Multiple candidates; do not merge");
        db.Set<TargetAdmissionBatch>().Add(batch); db.Set<TargetAdmissionRecord>().AddRange(row,ambiguous); db.SaveChanges();
        Assert.Single(db.TargetUniversityPrograms);
        Assert.Equal("old-id",db.TargetUniversityPrograms.Single().SourceId);
        Assert.Single(db.Set<TargetAdmissionRecord>().Where(x=>x.MatchStatus=="Verified"));
        Assert.Equal(440,db.TargetUniversityPrograms.Single().MinimumScore);
    }
}
