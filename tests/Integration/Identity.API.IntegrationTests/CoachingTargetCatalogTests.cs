using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingTargetCatalogTests
{
    [Theory]
    [InlineData("DİL","lisans","YDT","Bachelor")]
    [InlineData("dil","LİSANS","YDT","Bachelor")]
    [InlineData("SÖZ","önlisans","SOZ","Associate")]
    public void TurkishCatalogLabelsUseValidUnicode(string score,string degree,string expectedScore,string expectedDegree)
    {
        var program=TargetUniversityProgram.Create("code","University","Program",null,degree,score);
        Assert.Equal(expectedScore,program.ScoreType);Assert.Equal(expectedDegree,program.ProgramType);
    }
    [Fact]
    public void UniversityProgramStoresIdentityOnlyAndNormalizesLanguageProgramsToYdt()
    {
        var program = TargetUniversityProgram.Create("106510077", "University", "Program", "Engineering Faculty", "Bachelor", "DİL");
        Assert.False(program.IsActive);
        Assert.Equal("106510077", program.ProgramCode);
        Assert.Equal("YDT", program.ScoreType);
        Assert.Equal("Engineering Faculty", program.FacultyName);
        Assert.Equal("Bachelor", program.ProgramType);
    }

    [Fact]
    public void School_PreservesLocationAndUnknownScore()
    {
        var school = TargetSchool.Create("catalog", "s1", "School", "ADANA", "CEYHAN", null);
        Assert.False(school.IsActive);
        Assert.Null(school.MinimumScore);
        Assert.Null(school.ScoreYear);
        Assert.Equal("CEYHAN", school.District);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(501)]
    public void School_RejectsInvalidScore(int score) => Assert.Throws<ArgumentOutOfRangeException>(
        () => TargetSchool.Create("catalog", "s1", "School", "City", "District", score));

    [Fact]
    public void Program_RejectsMissingIdentityAndUnknownProgramType()
    {
        Assert.Throws<ArgumentException>(() => TargetUniversityProgram.Create("", "University", "Program", null, "Bachelor", "SAY"));
        Assert.Throws<ArgumentException>(() => TargetUniversityProgram.Create("code", " ", "Program", null, "Bachelor", "SAY"));
        Assert.Throws<ArgumentException>(() => TargetUniversityProgram.Create("code", "University", "Program", null, "Certificate", "SAY"));
    }

    [Fact]
    public void Catalogs_UseCoachingSchemaAndUniqueActiveProgramCode()
    {
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        foreach (var type in new[] { typeof(TargetUniversityProgram), typeof(TargetSchool) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.Equal("coaching", entity.GetSchema());
            if (type == typeof(TargetUniversityProgram))
                Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(new[] { "ProgramCode" })
                    && index.GetFilter() == "\"ProgramCode\" IS NOT NULL AND \"IsActive\" = TRUE");
            else
                Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name)
                    .SequenceEqual(new[] { "Source", "SourceId" }));
        }
    }
}
