using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Coaching.Curriculum.IntegrationTests;

// E2 (#15, live test 06.11.2026): migration 20261114090000_CorrectHomeLocationNames. The student profile keeps the home city and
// district names it read from the Identity directory; the directory's names are corrected ("Adiyaman" → "Adıyaman"), so the
// stored copies are corrected by id too. A name that is not the old directory spelling is left alone. No model change.
public sealed class HomeLocationNamesMigrationPostgresTests
{
    private const string Previous = "20261113090000_StudentOnboarding", Current = "20261114090000_CorrectHomeLocationNames";

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task StoredHomeNamesFollowTheCorrectedDirectory()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(Previous);
        Guid adiyaman = Guid.NewGuid(), kadikoy = Guid.NewGuid(), afyon = Guid.NewGuid(), typed = Guid.NewGuid(), none = Guid.NewGuid();
        await Profile(f.Db, adiyaman, "TUR002", "Adiyaman", "TUR002001", "Adiyaman");
        await Profile(f.Db, kadikoy, "TUR034", "İstanbul", "TUR034023", "Kadiköy");
        await Profile(f.Db, afyon, "TUR003", "Afyonkarahi̇sar", "TUR003001", "Afyonkarahisar");
        await Profile(f.Db, typed, "TUR006", "ANKARA", "TUR006017", "Keçi̇ören (eski)");
        await Profile(f.Db, none, null, null, null, null);

        await f.Db.Database.MigrateAsync();
        Assert.False(f.Db.Database.HasPendingModelChanges());
        Assert.Contains(Current, await f.Db.Database.GetAppliedMigrationsAsync());
        var rows = await f.Db.CoachingStudentProfiles.AsNoTracking().ToDictionaryAsync(x => x.UserId);
        Assert.Equal(("Adıyaman", "Adıyaman"), (rows[adiyaman].HomeCity, rows[adiyaman].HomeDistrict));
        Assert.Equal(("İstanbul", "Kadıköy"), (rows[kadikoy].HomeCity, rows[kadikoy].HomeDistrict));
        Assert.Equal(("Afyonkarahisar", "Afyonkarahisar"), (rows[afyon].HomeCity, rows[afyon].HomeDistrict));
        Assert.Equal(("ANKARA", "Keçi̇ören (eski)"), (rows[typed].HomeCity, rows[typed].HomeDistrict));
        Assert.Equal((null, null), (rows[none].HomeCity, rows[none].HomeDistrict));
        // Down keeps the corrected names (a data correction); Up again changes nothing.
        await f.Db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(Previous);
        await f.Db.Database.MigrateAsync();
        Assert.Equal("Adıyaman", (await f.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == adiyaman)).HomeCity);
    }

    private static async Task Profile(DbContext db, Guid user, string? province, string? city, string? district, string? districtName)
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO coaching.student_profiles (\"Id\",\"UserId\",\"EducationStatus\",\"ShareProgressWithTeachers\",\"IsActive\",\"CreatedAt\",\"Version\",\"HomeProvinceId\",\"HomeCity\",\"HomeDistrictId\",\"HomeDistrict\") " +
            "VALUES ({0},{1},2,true,true,now(),1,{2},{3},{4},{5})", Guid.NewGuid(), user, province ?? "", city ?? "", district ?? "", districtName ?? "");
        if (province is null)
            await db.Database.ExecuteSqlRawAsync("UPDATE coaching.student_profiles SET \"HomeProvinceId\" = NULL, \"HomeCity\" = NULL, \"HomeDistrictId\" = NULL, \"HomeDistrict\" = NULL WHERE \"UserId\" = {0}", user);
    }
}
