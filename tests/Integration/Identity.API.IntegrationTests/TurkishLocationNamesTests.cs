using System.Globalization;
using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Identity.API.IntegrationTests;

/// <summary>
/// E2 (#15, live test 06.11.2026): the province and district names are written in Turkish ("Adıyaman", not "Adiyaman" or
/// "Afyonkarahi̇sar" with a combining dot), listed in Turkish alphabetical order (Ç after C, İ after I, Ş after S) and found
/// whether the search is typed with ı/i or İ/I. Existing databases are corrected by a migration.
/// </summary>
public sealed class TurkishLocationNamesTests
{
    private static readonly StringComparer Turkish = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), false);

    [Fact]
    public void TheReferenceDataIsWrittenInTurkish()
    {
        var provinces = TurkeyLocationSeedData.Provinces.Select(x => x.Name).ToArray();
        var districts = TurkeyLocationSeedData.Districts.ToArray();
        provinces.Concat(districts.Select(x => x.Name)).Should().NotContain(x => x.Contains('̇'), "no combining dot above");
        provinces.Should().Contain(["Adıyaman", "Afyonkarahisar", "Ağrı", "Aydın", "Balıkesir", "Diyarbakır", "Eskişehir", "Iğdır", "İstanbul",
            "İzmir", "Kırıkkale", "Şanlıurfa", "Çankırı", "Isparta", "Kırklareli"]);
        string District(string province, string id) => districts.Single(x => x.Id == id && x.ProvinceId == province).Name;
        District("TUR034", "TUR034023").Should().Be("Kadıköy");
        District("TUR006", "TUR006017").Should().Be("Keçiören");
        District("TUR006", "TUR006006").Should().Be("Çamlıdere");
        District("TUR006", "TUR006002").Should().Be("Altındağ");
        District("TUR006", "TUR006010").Should().Be("Etimesgut");
        District("TUR002", "TUR002001").Should().Be("Adıyaman", "a central district has its province's name");
        District("TUR003", "TUR003001").Should().Be("Afyonkarahisar");
        District("TUR058", "TUR058014").Should().Be("Şarkışla");
        District("TUR034", "TUR034021").Should().Be("Gaziosmanpaşa");
        District("TUR046", "TUR046009").Should().Be("Onikişubat");
        District("TUR055", "TUR055001").Should().Be("19 Mayıs");
        // Every name is the Turkish title case of its own capitals (ı/I and i/İ kept apart).
        foreach (var name in provinces.Concat(districts.Select(x => x.Name)))
            name.Should().Be(TitleCase(name.ToUpper(CultureInfo.GetCultureInfo("tr-TR"))), name);
    }

    [Fact]
    public void TheNamesAgreeLetterForLetterWithTheMebCapitals()
    {
        // The MEB LGS export writes provinces and districts in Turkish capitals (I and İ kept apart). A name that matches one of
        // them loosely (ı/i, İ/I and marks ignored) must be exactly its Turkish lower case: a wrong ı or i fails here.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "data", "target-catalog"))) root = root.Parent;
        var file = Directory.GetFiles(Path.Combine(root!.FullName, "data", "target-catalog", "2026", "raw"), "mebi-lgs-full-*.json").Single();
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
        var items = json.RootElement.GetProperty("items").EnumerateArray()
            .Select(x => (City: x.GetProperty("city").GetString()!, Town: x.GetProperty("town").GetString()!)).ToArray();
        var towns = items.Where(x => x.Town != "MERKEZ").GroupBy(x => x.City).ToDictionary(x => x.Key, x => x.Select(y => y.Town).Distinct().ToArray());
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        string Capitals(string name) => NoCircumflex(name.ToUpper(tr));
        var provinces = TurkeyLocationSeedData.Provinces.ToDictionary(x => x.Id, x => x.Name);
        var cities = items.Select(x => x.City).Distinct().ToArray();
        foreach (var province in provinces.Values)
            cities.Should().Contain(Capitals(province), province);
        var checkedDistricts = 0;
        foreach (var district in TurkeyLocationSeedData.Districts)
        {
            var city = Capitals(provinces[district.ProvinceId]);
            var town = towns.GetValueOrDefault(city, []).SingleOrDefault(x => Loose(x) == Loose(district.Name));
            if (town is null) continue;
            Capitals(district.Name).Should().Be(town, $"{provinces[district.ProvinceId]} / {district.Name}");
            checkedDistricts++;
        }
        checkedDistricts.Should().BeGreaterThanOrEqualTo(490, "most districts have a school in the export");
    }

    private static string NoCircumflex(string value) => value.Replace('Â', 'A').Replace('Î', 'I').Replace('Û', 'U');

    private static string Loose(string value) => new(NoCircumflex(value.ToUpperInvariant()).Replace('\u0307'.ToString(), "")
        .Select(c => c switch { 'İ' or 'ı' or 'i' => 'I', 'Ş' => 'S', 'Ç' => 'C', 'Ğ' => 'G', 'Ö' => 'O', 'Ü' => 'U', _ => c }).Where(c => c != ' ').ToArray());

    [Fact]
    public async Task ProvincesAndDistrictsAreListedInTurkishOrderAndFoundWithEitherI()
    {
        await using var database = await IdentityPostgresDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            context.Provinces.AddRange(TurkeyLocationSeedData.Provinces.Select(x => Province.Create(x.Id, x.Name)));
            context.Districts.AddRange(TurkeyLocationSeedData.Districts.Where(x => x.ProvinceId is "TUR006" or "TUR034")
                .Select(x => District.Create(x.Id, x.ProvinceId, x.Name)));
            await context.SaveChangesAsync();
        }

        await using var read = database.CreateContext();
        var locations = new LocationRepository(read);
        var provinces = (await locations.GetProvincesAsync(null, default)).Select(x => x.Name).ToArray();
        provinces.Should().HaveCount(81).And.Equal(provinces.Order(Turkish));
        provinces.Should().ContainInOrder("Bursa", "Çanakkale", "Çankırı", "Çorum", "Denizli");
        provinces.Should().ContainInOrder("Hatay", "Iğdır", "Isparta", "İstanbul", "İzmir", "Kahramanmaraş");
        provinces.Should().ContainInOrder("Sivas", "Şanlıurfa", "Şırnak", "Tekirdağ");
        provinces[0].Should().Be("Adana");

        var ankara = (await locations.GetDistrictsAsync("TUR006", null, default)).Select(x => x.Name).ToArray();
        ankara.Should().Equal(ankara.Order(Turkish));
        ankara.Should().ContainInOrder("Beypazarı", "Çamlıdere", "Çankaya", "Çubuk", "Elmadağ", "Etimesgut");

        foreach (var (search, expected) in new[] { ("adiyaman", "Adıyaman"), ("ADIYAMAN", "Adıyaman"), ("ığdır", "Iğdır"), ("IGDIR", "Iğdır"),
                     ("istanbul", "İstanbul"), ("İSTANBUL", "İstanbul"), ("sanliurfa", "Şanlıurfa"), ("afyon", "Afyonkarahisar") })
            (await locations.GetProvincesAsync(search, default)).Select(x => x.Name).Should().Equal([expected], search);
        (await locations.GetDistrictsAsync("TUR034", "kadikoy", default)).Select(x => x.Name).Should().Equal("Kadıköy");
        (await locations.GetDistrictsAsync("TUR006", "KEÇİÖREN", default)).Select(x => x.Name).Should().Equal("Keçiören");
        (await locations.GetDistrictsAsync("TUR006", "camlidere", default)).Select(x => x.Name).Should().Equal("Çamlıdere");
    }

    [Fact]
    public async Task TheMigrationCorrectsStoredNamesAndInstitutionsThatUseThem()
    {
        await using var database = await IdentityPostgresDatabase.CreateAsync("20261104090000_RemoveLegacyParentFields");
        Guid linked, typed;
        await using (var context = database.CreateContext())
        {
            // Names as the first import wrote them.
            var adiyaman = Province.Create("TUR002", "Adiyaman");
            var afyon = Province.Create("TUR003", "Afyonkarahi̇sar");
            var istanbul = Province.Create("TUR034", "İstanbul");
            var renamed = Province.Create("TUR063", "Urfa (yönetici)");
            context.Provinces.AddRange(adiyaman, afyon, istanbul, renamed);
            var centre = District.Create("TUR002001", "TUR002", "Adiyaman");
            var kadikoy = District.Create("TUR034023", "TUR034", "Kadiköy");
            context.Districts.AddRange(centre, kadikoy, District.Create("TUR003001", "TUR003", "Afyonkarahisar"),
                District.Create("TUR034021", "TUR034", "Gazi̇ Osmanpaşa"));
            var school = Institution.Create("Adıyaman Kurs", InstitutionType.PrivateCourse);
            school.SetLocation(adiyaman, centre);
            var other = Institution.Create("Elle yazılmış", InstitutionType.School, "Adiyaman");
            context.Institutions.AddRange(school, other);
            await context.SaveChangesAsync();
            (linked, typed) = (school.Id, other.Id);
        }

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            context.Database.HasPendingModelChanges().Should().BeFalse();
        }

        await using var read = database.CreateContext();
        (await read.Provinces.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Name).ToListAsync())
            .Should().Equal("Adıyaman", "Afyonkarahisar", "İstanbul", "Urfa (yönetici)");
        (await read.Districts.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Name).ToListAsync())
            .Should().Equal("Adıyaman", "Afyonkarahisar", "Gaziosmanpaşa", "Kadıköy");
        var institution = await read.Institutions.AsNoTracking().SingleAsync(x => x.Id == linked);
        (institution.City, institution.District).Should().Be(("Adıyaman", "Adıyaman"));
        (await read.Institutions.AsNoTracking().SingleAsync(x => x.Id == typed)).City.Should().Be("Adiyaman", "free text without a reference is left alone");

        // Rerunning is harmless: Down keeps the names, Up again (and the update itself) changes nothing.
        await using (var again = database.CreateContext())
        {
            await again.GetService<IMigrator>().MigrateAsync("20261104090000_RemoveLegacyParentFields");
            (await again.Provinces.AsNoTracking().SingleAsync(x => x.Id == "TUR002")).Name.Should().Be("Adıyaman", "Down keeps the correction");
            await again.Database.MigrateAsync();
            (await again.Database.ExecuteSqlRawAsync(
                "UPDATE identity.provinces AS p SET \"Name\" = v.new_name FROM (" + Identity.Infrastructure.Persistence.Migrations.CorrectTurkishLocationNames.Provinces
                + ") AS v(id, old_name, new_name) WHERE p.\"Id\" = v.id AND p.\"Name\" = v.old_name")).Should().Be(0);
        }
        await using var after = database.CreateContext();
        (await after.Provinces.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Name).ToListAsync())
            .Should().Equal("Adıyaman", "Afyonkarahisar", "İstanbul", "Urfa (yönetici)");
        (await after.Districts.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Name).ToListAsync())
            .Should().Equal("Adıyaman", "Afyonkarahisar", "Gaziosmanpaşa", "Kadıköy");
    }

    [Fact]
    public async Task TheSeederLinksOlderInstitutionsWrittenWithoutTurkishLetters()
    {
        await using var database = await IdentityPostgresDatabase.CreateAsync();
        Guid plain, capitals, combined, unknown;
        await using (var context = database.CreateContext())
        {
            var a = Institution.Create("Kurs A", InstitutionType.PrivateCourse, "Adiyaman"); a.UpdateInfo(district: "Adiyaman");
            var b = Institution.Create("Kurs B", InstitutionType.PrivateCourse, "ISTANBUL"); b.UpdateInfo(district: "KADIKOY");
            var c = Institution.Create("Kurs C", InstitutionType.PrivateCourse, "Istanbul, Kadıköy");
            var d = Institution.Create("Kurs D", InstitutionType.PrivateCourse, "Atlantis"); d.UpdateInfo(district: "Merkez");
            context.Institutions.AddRange(a, b, c, d);
            await context.SaveChangesAsync();
            (plain, capitals, combined, unknown) = (a.Id, b.Id, c.Id, d.Id);
        }

        await using (var context = database.CreateContext())
            await IdentitySeeder.EnsureTurkeyLocationsAsync(context, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await using var read = database.CreateContext();
        var rows = await read.Institutions.AsNoTracking().ToDictionaryAsync(x => x.Id);
        (rows[plain].ProvinceId, rows[plain].DistrictId, rows[plain].City, rows[plain].District).Should().Be(("TUR002", "TUR002001", "Adıyaman", "Adıyaman"));
        (rows[capitals].ProvinceId, rows[capitals].DistrictId, rows[capitals].City, rows[capitals].District).Should().Be(("TUR034", "TUR034023", "İstanbul", "Kadıköy"));
        (rows[combined].ProvinceId, rows[combined].DistrictId).Should().Be(("TUR034", "TUR034023"));
        (rows[unknown].ProvinceId, rows[unknown].City).Should().Be((null, "Atlantis"));
    }

    private static string TitleCase(string capitals)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        return string.Join(' ', capitals.Split(' ').Select(word => word.Length == 0 ? word
            : word[..1] + word[1..].ToLower(tr)));
    }
}
