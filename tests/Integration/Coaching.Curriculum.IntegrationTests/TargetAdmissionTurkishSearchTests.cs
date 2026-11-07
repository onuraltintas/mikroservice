using System.Globalization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>
/// E7 (#30, live test 06.11.2026): the catalog writes names in Turkish capitals ("ANKARA ÜNİVERSİTESİ"); a student typing
/// "Ankara Üniversitesi" found nothing because the server upper-cased "i" to "I". Both sides now fold dotted and dotless i
/// the way the first-login wizard does, whatever the culture of the process or the collation of the database.
/// </summary>
public sealed class TargetAdmissionTurkishSearchTests
{
    [Theory]
    [InlineData("Ankara Üniversitesi", "Hukuk Fakültesi", "Hukuk")]
    [InlineData("ankara üniversitesi", "hukuk fakültesi", "hukuk")]
    [InlineData("ANKARA ÜNİVERSİTESİ", "HUKUK FAKÜLTESİ", "HUKUK")]
    [InlineData("ANKARA ÜNIVERSITESI", "HUKUK FAKÜLTESI", "Hukuk")]
    public async Task UniversityFacultyAndProgramMatchWhateverTheTurkishCase(string university, string faculty, string program)
    {
        using var culture = new InvariantCultureScope();
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var law = await Seed(db);
        var result = await new TargetAdmissionSearchService(db).SearchAsync(
            new("YKS", 2026, ProgramType: "Bachelor", University: university, Faculty: faculty, ProgramName: program, ScoreType: "EA"));
        Assert.Equal(law.Id, Assert.Single(result.Items).TargetId);
    }

    [Theory]
    [InlineData("fen lisesi")]
    [InlineData("Işıklar")]
    [InlineData("ışıklar fen")]
    public async Task SchoolNamesMatchWhateverTheTurkishCase(string search)
    {
        using var culture = new InvariantCultureScope();
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await Seed(db);
        var result = await new TargetAdmissionSearchService(db).SearchAsync(new("LGS", Search: search));
        Assert.Equal("IŞIKLAR FEN LİSESİ", Assert.Single(result.Items).Name);
    }

    [Theory]
    [InlineData("afyonkarahisar")]
    [InlineData("AFYONKARAHİSAR")]
    [InlineData("Afyonkarahisar Sosyal")]
    // The shared Catalogs.TurkishSearch folding: a name written with a combining dot ("i̇") still matches.
    public async Task NamesWithACombiningDotMatch(string search)
    {
        using var culture = new InvariantCultureScope();
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var school = TargetSchool.Create("mebi-lgs", Guid.NewGuid().ToString(), "Afyonkarahi\u0307sar Sosyal Bilimler Lisesi", "AFYONKARAHİSAR", "MERKEZ", 430m);
        school.SetActive(true); db.Add(school); await db.SaveChangesAsync();
        Assert.Single((await new TargetAdmissionSearchService(db).SearchAsync(new("LGS", Search: search))).Items);
    }

    // PR #128 review M1: the server folds ş ç ğ ö ü like the client, so a search typed without Turkish letters still matches.
    [Theory]
    [InlineData("isiklar fen")]
    [InlineData("ISIKLAR")]
    [InlineData("Işıklar Fen Lİsesi")]
    public async Task SearchesWithoutTurkishLettersMatch(string search)
    {
        using var culture = new InvariantCultureScope();
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var law = await Seed(db);
        Assert.Equal("IŞIKLAR FEN LİSESİ", Assert.Single((await new TargetAdmissionSearchService(db).SearchAsync(new("LGS", Search: search))).Items).Name);
        var result = await new TargetAdmissionSearchService(db).SearchAsync(new("YKS", 2026, ProgramType: "Bachelor", University: "ankara universitesi",
            Faculty: "hukuk fakultesi", ScoreType: "EA"));
        Assert.Equal(law.Id, Assert.Single(result.Items).TargetId);
    }

    [Fact]
    public void FoldingRemovesTurkishMarksLikeTheClient()
    {
        Assert.Equal("karsiyaka cagdas gogus ozel umit", Coaching.Infrastructure.Catalogs.TurkishSearch.Fold("KARŞIYAKA Çağdaş Göğüs Özel Ümİt"));
        Assert.Equal("kazim", Coaching.Infrastructure.Catalogs.TurkishSearch.Fold("Kâzım"));
        Assert.Equal("afyonkarahisar", Coaching.Infrastructure.Catalogs.TurkishSearch.Fold("Afyonkarahi\u0307sar"));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task SearchesWithoutTurkishLettersWorkOnPostgres()
    {
        using var culture = new InvariantCultureScope();
        await using var f = await CurriculumPostgresTests.Pg.Create();
        await Seed(f.Db);
        foreach (var (name, district) in new[] { ("Karşıyaka Cihat Kora Anadolu Lisesi", "KARŞIYAKA"), ("Cihat Kora Anadolu Lisesi", "KARŞIYAKA"),
                     ("Bornova Anadolu Lisesi", "BORNOVA"), ("Kâzım Dirik (Kız) Lisesi", "BORNOVA") })
        {
            var school = TargetSchool.Create("mebi-lgs", Guid.NewGuid().ToString(), name, "İZMİR", district, 400m); school.SetActive(true); f.Db.Add(school);
        }
        await f.Db.SaveChangesAsync();
        async Task<string[]> Names(string search) => await Coaching.Infrastructure.Catalogs.TurkishSearch
            .WhereEveryWord(f.Db.TargetSchools.AsNoTracking(), search, x => x.Name, x => x.District).Select(x => x.Name).OrderBy(x => x).ToArrayAsync();
        Assert.Equal(new[] { "Cihat Kora Anadolu Lisesi", "Karşıyaka Cihat Kora Anadolu Lisesi" }, await Names("karsiyaka anadolu"));
        Assert.Equal(new[] { "Cihat Kora Anadolu Lisesi", "Karşıyaka Cihat Kora Anadolu Lisesi" }, await Names("ANADOLU KARŞIYAKA"));
        Assert.Equal(new[] { "Kâzım Dirik (Kız) Lisesi" }, await Names("kazim (kiz)"));
        Assert.Equal(new[] { "IŞIKLAR FEN LİSESİ" }, await Names("isiklar fen lisesi"));
        Assert.Empty(await Names("karsiyaka bornova"));
        Assert.Empty(await Names("50% [a]"));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheSameSearchesWorkOnPostgres()
    {
        using var culture = new InvariantCultureScope();
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var law = await Seed(f.Db);
        var service = new TargetAdmissionSearchService(f.Db);
        foreach (var university in new[] { "Ankara Üniversitesi", "ankara üniversitesi", "ANKARA ÜNİVERSİTESİ" })
        {
            var result = await service.SearchAsync(new("YKS", 2026, ProgramType: "Bachelor", University: university, Faculty: "hukuk fakültesi",
                ProgramName: "Hukuk", ScoreType: "EA"));
            Assert.Equal(law.Id, Assert.Single(result.Items).TargetId);
        }
        Assert.Single((await service.SearchAsync(new("LGS", Search: "ışıklar fen lisesi"))).Items);
        var dotted = TargetSchool.Create("mebi-lgs", Guid.NewGuid().ToString(), "Afyonkarahi\u0307sar Sosyal Bilimler Lisesi", "AFYONKARAHİSAR", "MERKEZ", 430m);
        dotted.SetActive(true); f.Db.Add(dotted); await f.Db.SaveChangesAsync();
        Assert.Single((await service.SearchAsync(new("LGS", Search: "AFYONKARAHİSAR SOSYAL"))).Items);
    }

    private static async Task<TargetUniversityProgram> Seed(CoachingDbContext db)
    {
        var law = TargetUniversityProgram.Create("101110001", "ANKARA ÜNİVERSİTESİ", "Hukuk", "HUKUK FAKÜLTESİ", "Bachelor", "EA");
        law.SetActive(true);
        var other = TargetUniversityProgram.Create("101110002", "ANKARA ÜNİVERSİTESİ", "Matematik", "FEN FAKÜLTESİ", "Bachelor", "SAY");
        other.SetActive(true);
        var school = TargetSchool.Create("mebi-lgs", Guid.NewGuid().ToString(), "IŞIKLAR FEN LİSESİ", "ANKARA", "ÇANKAYA", 475.14m);
        var another = TargetSchool.Create("mebi-lgs", Guid.NewGuid().ToString(), "ATATÜRK ANADOLU LİSESİ", "ANKARA", "ÇANKAYA", 400m);
        school.SetActive(true); another.SetActive(true);
        db.AddRange(law, other, school, another);
        await db.SaveChangesAsync();
        return law;
    }

    /// <summary>The containers run with the invariant culture, where "i".ToUpper() is "I" (never "İ").</summary>
    private sealed class InvariantCultureScope : IDisposable
    {
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        public InvariantCultureScope() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        public void Dispose() => CultureInfo.CurrentCulture = culture;
    }
}
