using System.Globalization;
using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

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
    }

    private static string TitleCase(string capitals)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        return string.Join(' ', capitals.Split(' ').Select(word => word.Length == 0 ? word
            : word[..1] + word[1..].ToLower(tr)));
    }
}
