using FluentAssertions;
using Identity.Infrastructure.Seed;

namespace Identity.API.IntegrationTests;

public sealed class TurkeyLocationSeedDataTests
{
    [Fact]
    public void Dataset_ContainsAllTurkishProvincesAndDistricts()
    {
        TurkeyLocationSeedData.Provinces.Should().HaveCount(81);
        TurkeyLocationSeedData.Districts.Should().HaveCount(973);
        TurkeyLocationSeedData.Districts.Should().OnlyContain(district =>
            TurkeyLocationSeedData.Provinces.Any(province => province.Id == district.ProvinceId));
    }

    [Fact]
    public void Dataset_MapsCankayaToAnkara()
    {
        var ankara = TurkeyLocationSeedData.Provinces
            .Single(province => province.Name == "Ankara");

        TurkeyLocationSeedData.Districts.Should().Contain(district =>
            district.ProvinceId == ankara.Id && district.Name == "Çankaya");
    }
}
