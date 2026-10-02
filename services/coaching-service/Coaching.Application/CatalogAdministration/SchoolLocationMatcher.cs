using System.Globalization;
using System.Text;

namespace Coaching.Application.CatalogAdministration;

public sealed record LocationProvince(string Id, string Name);
public sealed record LocationDistrict(string Id, string ProvinceId, string Name);
public sealed record SchoolLocationMatch(string Status, string? ProvinceId = null, string? DistrictId = null);

public static class SchoolLocationMatcher
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    // Deliberately no accent removal, fuzzy matching or province-independent district lookup.
    public static SchoolLocationMatch Match(string city, string district,
        IReadOnlyList<LocationProvince> provinces, IReadOnlyList<LocationDistrict> districts)
    {
        var matchingProvinces = provinces.Where(x => Normalize(x.Name) == Normalize(city)).Take(2).ToArray();
        if (matchingProvinces.Length != 1)
            return new(matchingProvinces.Length == 0 ? "ProvinceNotFound" : "ProvinceAmbiguous");
        var province = matchingProvinces[0];
        var matchingDistricts = districts.Where(x => x.ProvinceId == province.Id
            && Normalize(x.Name) == Normalize(district)).Take(2).ToArray();
        return matchingDistricts.Length == 1
            ? new("Matched", province.Id, matchingDistricts[0].Id)
            : new(matchingDistricts.Length == 0 ? "DistrictNotFound" : "DistrictAmbiguous");
    }

    private static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormC).ToUpper(Turkish);
}
