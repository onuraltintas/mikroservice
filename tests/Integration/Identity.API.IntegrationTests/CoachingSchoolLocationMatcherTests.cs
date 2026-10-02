using Coaching.Application.CatalogAdministration;

namespace Identity.API.IntegrationTests;

public sealed class CoachingSchoolLocationMatcherTests
{
    private static readonly LocationProvince[] Provinces = [new("34", "İstanbul"), new("06", "Ankara")];
    private static readonly LocationDistrict[] Districts =
        [new("a", "34", "Şişli"), new("b", "06", "Çankaya")];

    [Theory]
    [InlineData("İSTANBUL", "ŞİŞLİ")]
    [InlineData(" istanbul ", " şişli ")]
    public void ExactTurkishNamesResolveWithoutChangingSourceText(string city, string district)
    {
        var result = SchoolLocationMatcher.Match(city, district, Provinces, Districts);
        Assert.Equal("Matched", result.Status);
        Assert.Equal("34", result.ProvinceId);
        Assert.Equal("a", result.DistrictId);
    }

    [Theory]
    [InlineData("Istanbul", "Sisli", "ProvinceNotFound")]
    [InlineData("İstanbul", "Çankaya", "DistrictNotFound")]
    [InlineData("İstanbul", "Şiş", "DistrictNotFound")]
    [InlineData("", "Şişli", "ProvinceNotFound")]
    public void NoFuzzyMatchingOrCrossProvinceDistricts(string city, string district, string status)
    {
        var result = SchoolLocationMatcher.Match(city, district, Provinces, Districts);
        Assert.Equal(status, result.Status);
        Assert.Null(result.ProvinceId);
        Assert.Null(result.DistrictId);
    }

    [Fact]
    public void AmbiguousProvinceIsNotAutomaticallyChosen()
    {
        var result = SchoolLocationMatcher.Match("İstanbul", "Şişli",
            [.. Provinces, new("99", "İstanbul")], Districts);
        Assert.Equal("ProvinceAmbiguous", result.Status);
        Assert.Null(result.DistrictId);
    }

    [Fact]
    public void AmbiguousDistrictIsNotAutomaticallyChosen()
    {
        var result = SchoolLocationMatcher.Match("İstanbul", "Şişli", Provinces,
            [.. Districts, new("c", "34", "Şişli")]);
        Assert.Equal("DistrictAmbiguous", result.Status);
        Assert.Null(result.DistrictId);
    }
}
