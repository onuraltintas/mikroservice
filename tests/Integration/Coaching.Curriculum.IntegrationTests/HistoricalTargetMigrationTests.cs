using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class HistoricalTargetMigrationTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public Task LegacyUniversityMigrationKeepsNullsAndRejectsDuplicates()=>Verify(true);
    [CurriculumPostgresTests.LocalPostgresFact]
    public Task LegacySchoolMigrationKeepsNullsAndRejectsDuplicates()=>Verify(false);
    private static async Task Verify(bool university)
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("0");var db=pg.Db;var migrator=db.GetService<IMigrator>();
        const string previous="20261002082803_AddCoachingStudyCatalog",current="20261002091400_AddCoachingTargetCatalog";
        await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(previous,current));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.target_university_programs("Id","Source","SourceId","UniversityName","Name","IsActive")
              VALUES({Guid.NewGuid()},'test','program','University','Program',FALSE);
            INSERT INTO coaching.target_schools("Id","Source","SourceId","Name","City","District","IsActive")
              VALUES({Guid.NewGuid()},'test','school','School','City','District',FALSE);
            """);
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT \"MinimumScore\" IS NULL AND \"ScoreYear\" IS NULL AND NOT \"IsActive\" AS \"Value\" FROM coaching.target_university_programs").SingleAsync());
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT \"ScoreYear\" IS NULL AND NOT \"IsActive\" AS \"Value\" FROM coaching.target_schools").SingleAsync());
        if(university) await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.target_university_programs("Id","Source","SourceId","UniversityName","Name","IsActive")
              VALUES({Guid.NewGuid()},'test','program','Other','Program',FALSE);
            """));
        else await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.target_schools("Id","Source","SourceId","Name","City","District","IsActive")
              VALUES({Guid.NewGuid()},'test','school','Other','City','District',FALSE);
            """));
        Assert.Equal(1,await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM coaching.target_university_programs").SingleAsync());
        Assert.Equal(1,await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM coaching.target_schools").SingleAsync());
        await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(current,previous));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM coaching.target_schools").SingleAsync());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM coaching.target_university_programs").SingleAsync());
    }
}
