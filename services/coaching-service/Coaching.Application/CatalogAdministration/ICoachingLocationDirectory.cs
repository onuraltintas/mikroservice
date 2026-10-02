namespace Coaching.Application.CatalogAdministration;

public interface ICoachingLocationDirectory
{
    Task<IReadOnlyList<LocationProvince>> GetProvincesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LocationDistrict>> GetDistrictsAsync(string provinceId, CancellationToken cancellationToken);
    Task<bool> VerifyPairAsync(string provinceId, string districtId, CancellationToken cancellationToken);
}
