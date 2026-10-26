using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class TargetAdmissionSearchTests
{
    [Fact]
    public async Task LgsCatalogKeepsDistinctProgramsAndUnknownSourceFieldsWithoutLegacyAdmissions()
    {
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        TargetSchool School(string source, string city, string language, bool active = true)
        {
            var school = TargetSchool.Create(source, Guid.NewGuid().ToString(), "Fen Lisesi", city, "MERKEZ", 450);
            school.SetProgramDetails("Fen Lisesi", language, null);
            school.SetActive(active);
            db.Add(school);
            return school;
        }
        var english = School("mebi-lgs", "ANKARA", "İngilizce");
        var german = School("mebi-lgs", "ANKARA", "Almanca");
        School("admin-manual", "İSTANBUL", "İngilizce");
        School("mebi-lgs", "ANKARA", "İngilizce", false);
        School("approved-catalog", "ANKARA", "İngilizce", false);
        await db.SaveChangesAsync();

        var service = new TargetAdmissionSearchService(db);
        var all = await service.SearchAsync(new("LGS"));
        Assert.Equal(3, all.TotalCount);
        Assert.Contains(all.Items, x => x.TargetId == english.Id);
        Assert.All(all.Items, x =>
        {
            Assert.Null(x.Year); Assert.Null(x.Stage); Assert.Null(x.Field); Assert.Null(x.Quota);
            Assert.Null(x.Percentile); Assert.Null(x.Rank); Assert.Equal(450, x.MinimumScore);
            Assert.Equal(x.TargetId, x.Id); Assert.Contains("ogmmateryal.eba.gov.tr", x.SourceUrl);
        });
        var filter = new TargetAdmissionFilter("LGS", City: "ANKARA", Language: "Almanca", InstitutionType: "Fen Lisesi");
        Assert.Equal(german.Id, Assert.Single((await service.SearchAsync(filter)).Items).TargetId);
        var facets = await service.FacetsAsync(filter);
        Assert.Equal(new[] { "ANKARA" }, facets["city"]);
        Assert.Contains("İngilizce", facets["language"]);
        Assert.Contains("Almanca", facets["language"]);
        Assert.Empty(facets["field"]);
        Assert.Throws<ArgumentException>(() => TargetAdmissionSearchService.Validate(new("YKS", Language: "Almanca")));
    }

    [Fact]
    public async Task UniversityFiltersMatchMebiFieldsAndOnlyReturnVerifiedActivePrograms()
    {
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var target = TargetUniversityProgram.Create("1001", "Example University", "Computer Science",
            "Engineering Faculty", "Bachelor", "DIL");
        target.SetActive(true);
        var other = TargetUniversityProgram.Create("1002", "Example University", "Mathematics",
            "Science Faculty", "Bachelor", "SAY");
        other.SetActive(true);
        db.AddRange(target, other);

        target.UpdateDetails(Row("1001", "Computer Science", "YDT", "Engineering Faculty"), 2026, 2026, "Main", "https://osym.gov.tr/2026");
        other.UpdateDetails(Row("1002", "Mathematics", "SAY", "Science Faculty"), 2026, null, null, "https://ogmmateryal.eba.gov.tr/mebi");
        await db.SaveChangesAsync();

        var service = new TargetAdmissionSearchService(db);
        var filter = new TargetAdmissionFilter("YKS", 2026, ProgramType: "Bachelor", University: "example",
            Faculty: "engineering", ProgramName: "computer", ScoreType: "YDT");
        var result = await service.SearchAsync(filter);

        var item = Assert.Single(result.Items);
        Assert.Equal(target.Id, item.TargetId);
        Assert.Equal("Engineering Faculty", item.Field);
        Assert.Equal("Bachelor", item.Degree);
        Assert.Equal(1, result.TotalCount);

        var facets = await service.FacetsAsync(filter);
        Assert.Contains("Bachelor", facets["programType"]);
        Assert.Contains("YDT", facets["scoreType"]);
        Assert.Equal(2, facets.Count);
    }

    private static TargetAdmissionData Row(string code, string name, string scoreType, string faculty) =>
        new TargetAdmissionData(code, "Example University", name, scoreType, null, null, "DEVLET", "Bachelor", "İngilizce", null,
            null, null, 20, 20, 400, 450, null, null) with { Field = faculty };
}
