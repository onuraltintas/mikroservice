namespace Coaching.Domain.Entities;

public sealed record TargetAdmissionData(string ExternalCode, string Institution, string Name, string ScoreType,
    string? City, string? District, string? InstitutionType, string? Degree, string? Language, string? Scholarship,
    string? EducationType, string? Field, int? Quota, int? Placed, decimal? MinimumScore, decimal? MaximumScore,
    int? Rank, decimal? Percentile);
