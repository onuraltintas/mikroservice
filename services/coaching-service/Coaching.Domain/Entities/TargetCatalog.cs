namespace Coaching.Domain.Entities;

public abstract class TargetCatalogEntry
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public string Source { get; protected set; } = string.Empty;
    public string SourceId { get; protected set; } = string.Empty;
    public string Name { get; protected set; } = string.Empty;
    public string? City { get; protected set; }
    public string? District { get; protected set; }
    public string? ScoreType { get; protected set; }
    public string? InstitutionType { get; protected set; }
    public string? Language { get; protected set; }
    public string? Field { get; protected set; }
    public int? Quota { get; protected set; }
    public int? Placed { get; protected set; }
    public decimal? MinimumScore { get; protected set; }
    public decimal? MaximumScore { get; protected set; }
    public int? Rank { get; protected set; }
    public decimal? Percentile { get; protected set; }
    public string? Scholarship { get; protected set; }
    public string? EducationType { get; protected set; }
    public int? CatalogYear { get; protected set; }
    public int? ScoreYear { get; protected set; }
    public string? PlacementStage { get; protected set; }
    public string? SourceUrl { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public bool IsActive { get; protected set; }

    public void SetActive(bool active) => IsActive = active;

    public void UpdateDetails(TargetAdmissionData data, int? catalogYear, int? placementYear,
        string? placementStage, string sourceUrl)
    {
        if (data.Quota < 0 || data.Placed < 0 || data.Rank <= 0 || data.Percentile is < 0 or > 100
            || data.MinimumScore < 0 || data.MaximumScore < 0 || data.MinimumScore > data.MaximumScore
            || (this is TargetSchool && (data.MinimumScore > 500 || data.MaximumScore > 500))
            || catalogYear is < 2026 or > 2100 || placementYear is < 1900 or > 2100
            || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new ArgumentException("Invalid target catalog details.");
        City = Optional(data.City); District = Optional(data.District);
        InstitutionType = Optional(data.InstitutionType); Language = Optional(data.Language);
        Field = Optional(data.Field); Scholarship = Optional(data.Scholarship);
        EducationType = Optional(data.EducationType); Quota = data.Quota; Placed = data.Placed;
        MinimumScore = data.MinimumScore; MaximumScore = data.MaximumScore;
        Rank = data.Rank; Percentile = data.Percentile; CatalogYear = catalogYear;
        ScoreYear = placementYear; PlacementStage = placementStage is null ? null : StudyCatalogText.Require(placementStage, 40);
        SourceUrl = StudyCatalogText.Require(sourceUrl, 2000); UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value)
        ? null : StudyCatalogText.Require(value, 300);
}

public sealed class TargetUniversityProgram : TargetCatalogEntry
{
    public string UniversityName { get; private set; } = string.Empty;
    public string? ProgramCode { get; private set; }
    public string? FacultyName { get; private set; }
    public string? ProgramType { get; private set; }
    private TargetUniversityProgram() { }


    public void UpdateOfficial(string universityName, string name, string programCode, string? facultyName,
        string? programType, string? scoreType)
    {
        var validated = Create(programCode, universityName, name, facultyName, programType, scoreType);
        UniversityName = validated.UniversityName;
        Name = validated.Name;
        ProgramCode = validated.ProgramCode;
        FacultyName = validated.FacultyName;
        ProgramType = validated.ProgramType;
        ScoreType = validated.ScoreType;
    }

    public static TargetUniversityProgram Create(string programCode, string universityName, string name,
        string? facultyName, string? programType, string? scoreType)
    {
        return new TargetUniversityProgram
        {
            Source = "mebi-yks", SourceId = StudyCatalogText.Require(programCode, 50),
            UniversityName = StudyCatalogText.Require(universityName, 300), Name = StudyCatalogText.Require(name, 300),
            ProgramCode = StudyCatalogText.Require(programCode, 50),
            FacultyName = string.IsNullOrWhiteSpace(facultyName) ? null : StudyCatalogText.Require(facultyName, 300),
            ProgramType = NormalizeProgramType(programType),
            ScoreType = NormalizeScoreType(scoreType)
        };
    }

    private static string? NormalizeProgramType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "ASSOCIATE" or "ÖNLİSANS" or "ÖNLISANS" => "Associate",
        "BACHELOR" or "LİSANS" or "LISANS" => "Bachelor",
        null or "" => null,
        _ => throw new ArgumentException("Program type must be Associate or Bachelor.", nameof(value))
    };

    private static string? NormalizeScoreType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "DIL" or "DİL" or "YDT" => "YDT",
            "SOZ" or "SÖZ" => "SOZ",
            "TYT" or "SAY" or "EA" => normalized,
            _ => throw new ArgumentException("Unsupported university program score type.", nameof(value))
        };
    }
}

public sealed class TargetSchool : TargetCatalogEntry
{
    public string? ProvinceId { get; private set; }
    public string? DistrictId { get; private set; }
    private TargetSchool() { }


    public void SetProgramDetails(string? schoolType, string? language, string? field)
    {
        InstitutionType = string.IsNullOrWhiteSpace(schoolType) ? null : StudyCatalogText.Require(schoolType, 300);
        Language = string.IsNullOrWhiteSpace(language) ? null : StudyCatalogText.Require(language, 300);
        Field = string.IsNullOrWhiteSpace(field) ? null : StudyCatalogText.Require(field, 300);
    }

    public void Edit(string name, decimal? minimumScore, int? scoreYear)
    {
        var validated = Create(Source, SourceId, name, City!, District!, minimumScore, scoreYear);
        Name = validated.Name;
        MinimumScore = validated.MinimumScore;
        ScoreYear = validated.ScoreYear;
    }

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
            ScoreType = "LGS", Source = StudyCatalogText.Require(source, 100), SourceId = StudyCatalogText.Require(sourceId, 100),
            Name = StudyCatalogText.Require(name, 300), City = StudyCatalogText.Require(city, 100),
            District = StudyCatalogText.Require(district, 100), MinimumScore = minimumScore, ScoreYear = scoreYear
        };
    }
}
