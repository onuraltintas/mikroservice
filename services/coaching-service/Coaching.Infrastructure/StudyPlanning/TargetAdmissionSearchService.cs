using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed record TargetAdmissionFilter(string ExamCode = "YKS", int Year = 2026,
    string? ProgramType = null, string? University = null, string? Faculty = null, string? ProgramName = null,
    string? ScoreType = null, string? Search = null, string? City = null, string? District = null,
    string? InstitutionType = null, decimal? MinimumScore = null, decimal? MaximumScore = null,
    decimal? MaximumPercentile = null, int PageNumber = 1, int PageSize = 20, string Sort = "name",
    string? Language = null, string? Field = null);

public sealed class TargetAdmissionView
{
    public Guid Id { get; init; }
    public Guid TargetId { get; init; }
    public string Name { get; init; } = "";
    public string Institution { get; init; } = "";
    public string ExternalCode { get; init; } = "";
    public string ScoreType { get; init; } = "";
    public string? City { get; init; }
    public string? District { get; init; }
    public string? InstitutionType { get; init; }
    public string? Degree { get; init; }
    public string? Language { get; init; }
    public string? Scholarship { get; init; }
    public string? EducationType { get; init; }
    public string? Field { get; init; }
    public int? Quota { get; init; }
    public int? Placed { get; init; }
    public decimal? MinimumScore { get; init; }
    public decimal? MaximumScore { get; init; }
    public int? Rank { get; init; }
    public decimal? Percentile { get; init; }
    public int? Year { get; init; }
    public string? Stage { get; init; }
    public string SourceUrl { get; init; } = "";
}

public sealed class TargetAdmissionSearchService(CoachingDbContext db)
{
    public static void Validate(TargetAdmissionFilter f)
    {
        if (f.ExamCode is not ("LGS" or "YKS") || f.Year < 2026 || f.Year > 2100
            || f.PageNumber is < 1 or > 10000 || f.PageSize is < 1 or > 50
            || f.MinimumScore < 0 || f.MaximumScore < 0 || f.MinimumScore > f.MaximumScore
            || f.MaximumPercentile is < 0 or > 100
            || f.Sort is not ("name" or "score-desc" or "percentile"))
            throw new ArgumentException("Invalid admission search filters.");

        foreach (var value in new[] { f.ProgramType, f.University, f.Faculty, f.ProgramName, f.ScoreType,
                     f.Search, f.City, f.District, f.InstitutionType, f.Language, f.Field })
            if (value?.Length > 300) throw new ArgumentException("Search value too long.");

        if (f.ExamCode == "YKS")
        {
            if (f.ProgramType is not (null or "Associate" or "Bachelor")
                || (f.ScoreType is not null && NormalizeScoreType(f.ScoreType) is not ("TYT" or "SAY" or "EA" or "SOZ" or "YDT"))
                || f.Search is not null || f.City is not null || f.District is not null || f.InstitutionType is not null
                || f.MinimumScore.HasValue || f.MaximumScore.HasValue || f.MaximumPercentile.HasValue
                || f.Sort == "percentile" || f.Language is not null || f.Field is not null)
                throw new ArgumentException("Invalid university program filters.");
        }
        else if (f.ProgramType is not null || f.University is not null || f.Faculty is not null || f.ProgramName is not null
                 || f.ScoreType is not (null or "LGS"))
            throw new ArgumentException("Invalid school filters.");
    }

    private IQueryable<TargetAdmissionView> Base(TargetAdmissionFilter f)
    {
        if (f.ExamCode == "YKS")
            return from p in db.TargetUniversityPrograms.AsNoTracking()
                   where p.IsActive && (p.CatalogYear == null || p.CatalogYear == f.Year)
                   select new TargetAdmissionView
                   {
                       Id = p.Id, TargetId = p.Id, Name = p.Name, Institution = p.UniversityName,
                       ExternalCode = p.ProgramCode ?? p.SourceId, ScoreType = p.ScoreType ?? "",
                       Degree = p.ProgramType, Field = p.FacultyName,
                       City = p.City, District = p.District, InstitutionType = p.InstitutionType,
                       Language = p.Language, Scholarship = p.Scholarship, EducationType = p.EducationType,
                       Quota = p.Quota, Placed = p.Placed, MinimumScore = p.MinimumScore,
                       MaximumScore = p.MaximumScore, Rank = p.Rank, Percentile = p.Percentile,
                       Year = p.ScoreYear, Stage = p.PlacementStage, SourceUrl = p.SourceUrl ?? ""
                   };

        return from s in db.TargetSchools.AsNoTracking()
               where s.IsActive
               select new TargetAdmissionView
               {
                   Id = s.Id, TargetId = s.Id, Name = s.Name, Institution = s.Name,
                   ExternalCode = s.SourceId, ScoreType = "LGS", City = s.City, District = s.District,
                   InstitutionType = s.InstitutionType, Language = s.Language, Field = s.Field,
                   Quota = s.Quota, Placed = s.Placed, MinimumScore = s.MinimumScore, MaximumScore = s.MaximumScore,
                   Rank = s.Rank, Percentile = s.Percentile, Year = s.ScoreYear, Stage = s.PlacementStage,
                   SourceUrl = s.SourceUrl ?? "https://ogmmateryal.eba.gov.tr/mebi/lgs-hazirlik/hedef-belirle"
               };
    }

    private static IQueryable<TargetAdmissionView> Apply(TargetAdmissionFilter f,
        IQueryable<TargetAdmissionView> query, string? exceptFacet = null)
    {
        if (f.ExamCode == "YKS")
        {
            if (exceptFacet != "programType" && f.ProgramType is not null) query = query.Where(x => x.Degree == f.ProgramType);
            if (exceptFacet != "university" && !string.IsNullOrWhiteSpace(f.University))
            {
                var university = f.University.Trim().ToUpper();
                query = query.Where(x => x.Institution.ToUpper().Contains(university));
            }
            if (exceptFacet != "faculty" && !string.IsNullOrWhiteSpace(f.Faculty))
            {
                var faculty = f.Faculty.Trim().ToUpper();
                query = query.Where(x => x.Field != null && x.Field.ToUpper().Contains(faculty));
            }
            if (exceptFacet != "programName" && !string.IsNullOrWhiteSpace(f.ProgramName))
            {
                var program = f.ProgramName.Trim().ToUpper();
                query = query.Where(x => x.Name.ToUpper().Contains(program));
            }
            if (exceptFacet != "scoreType" && f.ScoreType is not null)
            {
                var scoreType = NormalizeScoreType(f.ScoreType);
                query = query.Where(x => x.ScoreType == scoreType);
            }
            return query;
        }

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var search = f.Search.Trim().ToUpper();
            query = query.Where(x => x.Name.ToUpper().Contains(search) || x.ExternalCode.Contains(search));
        }
        if (exceptFacet != "city" && f.City is not null) query = query.Where(x => x.City == f.City);
        if (exceptFacet != "district" && f.District is not null) query = query.Where(x => x.District == f.District);
        if (exceptFacet != "institutionType" && f.InstitutionType is not null) query = query.Where(x => x.InstitutionType == f.InstitutionType);
        if (exceptFacet != "language" && f.Language is not null) query = query.Where(x => x.Language == f.Language);
        if (exceptFacet != "field" && f.Field is not null) query = query.Where(x => x.Field == f.Field);
        if (f.MinimumScore.HasValue) query = query.Where(x => x.MinimumScore >= f.MinimumScore);
        if (f.MaximumScore.HasValue) query = query.Where(x => x.MinimumScore <= f.MaximumScore);
        if (f.MaximumPercentile.HasValue) query = query.Where(x => x.Percentile <= f.MaximumPercentile);
        return query;
    }

    public async Task<TargetSearchPage<TargetAdmissionView>> SearchAsync(TargetAdmissionFilter f,
        CancellationToken ct = default)
    {
        Validate(f);
        var query = Apply(f, Base(f));
        var count = await query.CountAsync(ct);
        IOrderedQueryable<TargetAdmissionView> ordered = f.Sort switch
        {
            "score-desc" => query.OrderByDescending(x => x.MinimumScore.HasValue).ThenByDescending(x => x.MinimumScore),
            "percentile" => query.OrderByDescending(x => x.Percentile.HasValue).ThenBy(x => x.Percentile),
            _ => f.ExamCode == "YKS" ? query.OrderBy(x => x.Institution).ThenBy(x => x.Name)
                : query.OrderBy(x => x.Institution).ThenBy(x => x.Name)
        };
        var items = await ordered.ThenBy(x => x.Id).Skip((f.PageNumber - 1) * f.PageSize)
            .Take(f.PageSize).ToListAsync(ct);
        return new(items, count, f.PageNumber, f.PageSize);
    }

    public async Task<Dictionary<string, string[]>> FacetsAsync(TargetAdmissionFilter f, CancellationToken ct = default)
    {
        Validate(f);
        var baseQuery = Base(f);
        if (f.ExamCode == "YKS")
        {
            var programTypes = await Apply(f, baseQuery, "programType").Select(x => x.Degree).Where(x => x != null).Select(x => x!).Distinct().Order().ToArrayAsync(ct);
            var scoreTypes = await Apply(f, baseQuery, "scoreType").Select(x => x.ScoreType).Distinct().Order().ToArrayAsync(ct);
            return new()
            {
                ["programType"] = programTypes, ["scoreType"] = scoreTypes
            };
        }

        return new()
        {
            ["city"] = await Apply(f, baseQuery, "city").Select(x => x.City).Where(x => x != null).Select(x => x!).Distinct().Order().ToArrayAsync(ct),
            ["district"] = await Apply(f, baseQuery, "district").Select(x => x.District).Where(x => x != null).Select(x => x!).Distinct().Order().ToArrayAsync(ct),
            ["institutionType"] = await Apply(f, baseQuery, "institutionType").Select(x => x.InstitutionType).Where(x => x != null).Select(x => x!).Distinct().Order().ToArrayAsync(ct),
            ["language"] = await Apply(f, baseQuery, "language").Select(x => x.Language).Where(x => x != null).Select(x => x!).Distinct().Order().ToArrayAsync(ct),
            ["field"] = await Apply(f, baseQuery, "field").Select(x => x.Field).Where(x => x != null).Select(x => x!).Distinct().Order().ToArrayAsync(ct)
        };
    }

    private static string NormalizeScoreType(string value) => value.Trim().ToUpperInvariant() switch
    {
        "DIL" or "DİL" or "YDT" => "YDT",
        "SOZ" or "SÖZ" => "SOZ",
        var other => other
    };
}
