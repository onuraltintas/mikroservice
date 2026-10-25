using System.Reflection;
using System.Text.Json;

namespace Identity.Infrastructure.Seed;

public sealed record TurkeyProvinceSeed(string Id, string Name);

public sealed record TurkeyDistrictSeed(string Id, string ProvinceId, string Name);

public static class TurkeyLocationSeedData
{
    private const string ResourceName = "Identity.Infrastructure.Seed.Data.turkey-locations.json";
    private static readonly Lazy<TurkeyLocations> Locations = new(Load);

    public static IReadOnlyList<TurkeyProvinceSeed> Provinces => Locations.Value.Provinces;
    public static IReadOnlyList<TurkeyDistrictSeed> Districts => Locations.Value.Districts;

    private static TurkeyLocations Load()
    {
        var assembly = typeof(TurkeyLocationSeedData).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Konum referans verisi bulunamadı: {ResourceName}");

        return JsonSerializer.Deserialize<TurkeyLocations>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })
            ?? throw new InvalidOperationException("Konum referans verisi okunamadı.");
    }

    private sealed record TurkeyLocations(
        IReadOnlyList<TurkeyProvinceSeed> Provinces,
        IReadOnlyList<TurkeyDistrictSeed> Districts);
}
