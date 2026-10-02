using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingTargetSearchTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Search_ReturnsOnlyActiveFilteredTargetsAndPreservesUnknownScoreYear()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var school = TargetSchool.Create("test", "1", "Science School", "Ankara", "Center", 450);
            var hidden = TargetSchool.Create("test", "2", "Science Hidden", "Ankara", "Center", null);
            var program = TargetUniversityProgram.Create("test", "3", "Example University", "Software", "100", "SAY", null);
            db.AddRange(school, hidden, program);
            db.Entry(school).Property(x => x.IsActive).CurrentValue = true;
            db.Entry(program).Property(x => x.IsActive).CurrentValue = true;
            await db.SaveChangesAsync();
            var service = new CoachingTargetSearchService(db);
            var schools = await service.SearchSchoolsAsync("science", "Ankara", "Center", 1, 1);
            Assert.Equal(1, schools.TotalCount);
            Assert.Equal(school.Id, Assert.Single(schools.Items).Id);
            Assert.Null(schools.Items[0].ScoreYear);
            Assert.Empty((await service.SearchSchoolsAsync("%", null, null, 1, 20)).Items);
            Assert.Empty((await service.SearchSchoolsAsync(null, "Izmir", null, 1, 20)).Items);
            Assert.Empty((await service.SearchSchoolsAsync(null, null, null, 2, 1)).Items);
            var programs = await service.SearchProgramsAsync("example", "SAY", 1, 20);
            Assert.Equal(program.Id, Assert.Single(programs.Items).Id);
            Assert.Empty((await service.SearchProgramsAsync(null, "EA", 1, 20)).Items);
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchSchoolsAsync(null, null, null, 0, 20));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchProgramsAsync(new string('a', 201), null, 1, 20));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchProgramsAsync(null, null, 1, 51));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
