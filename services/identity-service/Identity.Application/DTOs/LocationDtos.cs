namespace Identity.Application.DTOs;

public sealed record ProvinceDto(string Id, string Name);

public sealed record DistrictDto(string Id, string ProvinceId, string Name);
