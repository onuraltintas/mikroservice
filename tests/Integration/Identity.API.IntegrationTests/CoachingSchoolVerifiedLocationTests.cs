using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingSchoolVerifiedLocationTests
{
    private static TargetSchool School() => TargetSchool.Create("source", "school", "School", "ANKARA", "ÇANKAYA (MERKEZ)", null);

    [Fact]
    public void LegacySchoolDoesNotInferLocationIdentifiers()
    {
        var school = School();
        Assert.Null(school.ProvinceId);
        Assert.Null(school.DistrictId);
        Assert.Equal("ÇANKAYA (MERKEZ)", school.District);
    }

    [Fact]
    public void VerifiedIdentifiersAreStoredTogetherWithoutChangingSourceNames()
    {
        var school = School();
        school.SetVerifiedLocation("TUR006", "TUR006007");
        Assert.Equal("TUR006", school.ProvinceId);
        Assert.Equal("TUR006007", school.DistrictId);
        Assert.Equal("ANKARA", school.City);
        Assert.Equal("ÇANKAYA (MERKEZ)", school.District);
    }

    [Theory]
    [InlineData("", "district")]
    [InlineData("province", " ")]
    public void InvalidIdentifiersCannotPartiallyChangeLocation(string province, string district)
    {
        var school = School();
        Assert.Throws<ArgumentException>(() => school.SetVerifiedLocation(province, district));
        Assert.Null(school.ProvinceId);
        Assert.Null(school.DistrictId);
    }

    [Fact]
    public void IdentifiersHaveBoundedStorageWithoutCrossServiceForeignKeys()
    {
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var entity = db.Model.FindEntityType(typeof(TargetSchool))!;
        Assert.Equal(20, entity.FindProperty(nameof(TargetSchool.ProvinceId))!.GetMaxLength());
        Assert.Equal(20, entity.FindProperty(nameof(TargetSchool.DistrictId))!.GetMaxLength());
        Assert.Empty(entity.GetForeignKeys());
    }
}
