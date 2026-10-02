using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingTargetCatalogTests
{
    [Fact]
    public void UniversityProgram_PreservesUnknownYearAndStartsInactive()
    {
        var program = TargetUniversityProgram.Create("catalog", "p1", "University", "Program", "106510077", "SAY", 442.53m);
        Assert.False(program.IsActive);
        Assert.Null(program.ScoreYear);
        Assert.Equal(442.53m, program.MinimumScore);
        Assert.Equal("106510077", program.ProgramCode);
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
    public void Program_RejectsMissingIdentityAndNegativeScore()
    {
        Assert.Throws<ArgumentException>(() => TargetUniversityProgram.Create("", "p", "University", "Program", null, null, null));
        Assert.Throws<ArgumentException>(() => TargetUniversityProgram.Create("source", "p", " ", "Program", null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => TargetUniversityProgram.Create("source", "p", "University", "Program", null, null, -1));
    }

    [Fact]
    public void Catalogs_UseCoachingSchemaAndUniqueSourceIdentity()
    {
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        foreach (var type in new[] { typeof(TargetUniversityProgram), typeof(TargetSchool) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.Equal("coaching", entity.GetSchema());
            Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name)
                .SequenceEqual(new[] { "Source", "SourceId" }));
        }
    }
}
