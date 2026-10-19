using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class TargetAdmissionImportTests
{
    [Fact]
    public async Task ReviewedSchoolAliasPreservesExistingIdentityAndDoesNotRenameLegacySchool()
    {
        using var db=Db();var old=TargetSchool.Create("old","alias","School MTAL","ISTANBUL","District (MERKEZ)",300);db.Add(old);await db.SaveChangesAsync();
        var row=Row("alias:1") with {Institution="School Technical High School",City="ISTANBUL",District="District",ScoreType="LGS",MinimumScore=null,MaximumScore=null,Rank=null,Percentile=null,Placed=null};
        var package=Package("LGS",row) with {Stage="Catalog",SchoolAliases=new Dictionary<string,string[]>{{"alias:1",["School MTAL"]}}};
        await new TargetAdmissionImporter(db).ImportAsync(package);
        Assert.Single(db.TargetSchools);Assert.Equal(old.Id,db.Set<TargetAdmissionRecord>().Single().SchoolId);Assert.Equal("School MTAL",old.Name);
    }
    [Fact]
    public async Task IndependentProvincialSourcesAreAllowedButOverlappingOfferingsNeedReview()
    {
        using var db=Db();var importer=new TargetAdmissionImporter(db);
        var row=Row("istanbul:1") with {Institution="School",City="ISTANBUL",District="District",ScoreType="LGS",MinimumScore=null,MaximumScore=null,Rank=null,Percentile=null,Placed=null};
        var first=Package("LGS",row) with {Stage="Catalog",SourceUrl="https://istanbul.meb.gov.tr/catalog"};
        await importer.ImportAsync(first);
        var second=first with {SourceUrl="https://samsun.meb.gov.tr/catalog",DocumentHash=new string('d',64),Rows=[row with {ExternalCode="samsun:1",City="SAMSUN"}]};
        Assert.Equal(1,(await importer.ImportAsync(second)).Verified);
        var overlap=second with {SourceUrl="https://meb.gov.tr/other",DocumentHash=new string('e',64),Rows=[row with {ExternalCode="other-code"}]};
        Assert.Equal(1,(await importer.ImportAsync(overlap)).Conflicts);
        Assert.Equal(2,db.TargetSchools.Count());
    }
    [Fact]
    public async Task MetadataCannotCarryUnstagedScoresAndNewSchoolOfferingsShareOneIdentity()
    {
        using var db=Db();var importer=new TargetAdmissionImporter(db);
        var row=Row("school:one") with {Institution="School",City="ANKARA",District="CANKAYA",ScoreType="LGS"};
        var package=Package("LGS",row) with {Stage="Catalog"};
        await Assert.ThrowsAsync<ArgumentException>(()=>importer.ImportAsync(package));
        Assert.Empty(db.Set<TargetAdmissionBatch>());
        row=row with {MinimumScore=null,MaximumScore=null,Rank=null,Percentile=null,Placed=null};
        await importer.ImportAsync(package with {Rows=[row,row with {ExternalCode="school:two",Field="Other"}]});
        Assert.Single(db.TargetSchools);
        Assert.Single(db.Set<TargetAdmissionRecord>().Select(x=>x.SchoolId).Distinct());
    }
    private static CoachingDbContext Db()=>new(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static TargetAdmissionData Row(string code="123456789")=>new(code,"University","Program","SAY",null,null,"State","Bachelor",null,null,null,null,10,9,400.12345m,450,null,null);
    private static TargetAdmissionPackage Package(string exam,params TargetAdmissionData[] rows)=>new(exam,2026,"Main","https://cdn.osym.gov.tr/results.xlsx",new string('c',64),rows);
    [Fact]
    public async Task ExistingProgramIdentityAndOldScoreArePreservedAndRepeatIsIdempotent()
    {
        using var db=Db();var old=TargetUniversityProgram.Create("old","legacy","University","Program","123456789","SAY",390);db.Add(old);await db.SaveChangesAsync();
        var service=new TargetAdmissionImporter(db);var package=Package("YKS",Row());
        await service.ImportAsync(package);await service.ImportAsync(package);
        Assert.Single(db.TargetUniversityPrograms);Assert.Single(db.Set<TargetAdmissionBatch>());Assert.Single(db.Set<TargetAdmissionRecord>());
        Assert.Equal(old.Id,db.Set<TargetAdmissionRecord>().Single().ProgramId);Assert.Equal(390,old.MinimumScore);Assert.Null(old.ScoreYear);
    }
    [Fact]
    public async Task DuplicateSchoolCandidatesStayUnlinkedAndUnpublishedInSearch()
    {
        using var db=Db(); db.AddRange(TargetSchool.Create("old","1","School","ANKARA","CANKAYA",400),TargetSchool.Create("old","2","School","ANKARA","CANKAYA",410));await db.SaveChangesAsync();
        await new TargetAdmissionImporter(db).ImportAsync(Package("LGS",Row("school-code") with {Institution="School",Name="School - English",City="ANKARA",District="CANKAYA",ScoreType="LGS"}));
        Assert.Equal(2,db.TargetSchools.Count());var row=db.Set<TargetAdmissionRecord>().Single();Assert.Equal("Ambiguous",row.MatchStatus);Assert.Null(row.SchoolId);
    }
    [Fact]
    public async Task SameHashChangedContentAndDuplicateCodesAreRejectedBeforeWrites()
    {
        using var db=Db();var importer=new TargetAdmissionImporter(db);var package=Package("YKS",Row());await importer.ImportAsync(package);
        await Assert.ThrowsAsync<ArgumentException>(()=>importer.ImportAsync(package with {Rows=[Row() with {MinimumScore=410}]}));
        await Assert.ThrowsAsync<ArgumentException>(()=>importer.ImportAsync(package with {Issues=new Dictionary<string,string>{{"123456789","New identity conflict"}}}));
        await Assert.ThrowsAsync<ArgumentException>(()=>importer.ImportAsync(package with {DocumentHash=new string('d',64),Rows=[Row(),Row()]}));
        Assert.Single(db.Set<TargetAdmissionRecord>());
    }
    [Fact]
    public async Task DisagreementBetweenOfficialSourcesIsQuarantined()
    {
        using var db=Db();await new TargetAdmissionImporter(db).ImportAsync(Package("YKS",Row()) with
        {Issues=new Dictionary<string,string>{{"123456789","OSYM and YOK scores disagree"}}});
        Assert.Empty(db.TargetUniversityPrograms);
        Assert.Equal("Conflict",db.Set<TargetAdmissionRecord>().Single().MatchStatus);
    }
}
