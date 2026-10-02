namespace Coaching.Domain.Entities;

public sealed class TargetUniversityProgram
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Source { get; private set; } = string.Empty;
    public string SourceId { get; private set; } = string.Empty;
    public string UniversityName { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? ProgramCode { get; private set; }
    public string? ScoreType { get; private set; }
    public decimal? MinimumScore { get; private set; }
    // Unknown score year is preserved; these values are not placement predictions.
    public int? ScoreYear { get; private set; }
    public bool IsActive { get; private set; }
    private TargetUniversityProgram() { }

    public static TargetUniversityProgram Create(string source, string sourceId, string universityName,
        string name, string? programCode, string? scoreType, decimal? minimumScore, int? scoreYear = null)
    {
        if (minimumScore < 0) throw new ArgumentOutOfRangeException(nameof(minimumScore));
        return new TargetUniversityProgram
        {
            Source = StudyCatalogText.Require(source, 100), SourceId = StudyCatalogText.Require(sourceId, 100),
            UniversityName = StudyCatalogText.Require(universityName, 300), Name = StudyCatalogText.Require(name, 300),
            ProgramCode = string.IsNullOrWhiteSpace(programCode) ? null : StudyCatalogText.Require(programCode, 50),
            ScoreType = string.IsNullOrWhiteSpace(scoreType) ? null : StudyCatalogText.Require(scoreType, 30),
            MinimumScore = minimumScore, ScoreYear = scoreYear
        };
    }
}

public sealed class TargetSchool
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Source { get; private set; } = string.Empty;
    public string SourceId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string District { get; private set; } = string.Empty;
    public string? ProvinceId { get; private set; }
    public string? DistrictId { get; private set; }
    public decimal? MinimumScore { get; private set; }
    public int? ScoreYear { get; private set; }
    public bool IsActive { get; private set; }
    private TargetSchool() { }

    // Call only after the shared Identity directory has verified this exact pair.
    public void SetVerifiedLocation(string provinceId, string districtId)
    {
        var province = StudyCatalogText.Require(provinceId, 20);
        var district = StudyCatalogText.Require(districtId, 20);
        ProvinceId = province;
        DistrictId = district;
    }

    public static TargetSchool Create(string source, string sourceId, string name, string city,
        string district, decimal? minimumScore, int? scoreYear = null)
    {
        if (minimumScore is < 0 or > 500) throw new ArgumentOutOfRangeException(nameof(minimumScore));
        return new TargetSchool
        {
            Source = StudyCatalogText.Require(source, 100), SourceId = StudyCatalogText.Require(sourceId, 100),
            Name = StudyCatalogText.Require(name, 300), City = StudyCatalogText.Require(city, 100),
            District = StudyCatalogText.Require(district, 100), MinimumScore = minimumScore, ScoreYear = scoreYear
        };
    }
}
