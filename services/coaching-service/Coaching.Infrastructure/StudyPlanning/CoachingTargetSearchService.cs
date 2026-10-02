using Coaching.Application.StudyPlanning;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingTargetSearchService(CoachingDbContext db) : ITargetSearchService
{
    public async Task<TargetSearchPage<SchoolTargetView>> SearchSchoolsAsync(string? search, string? city,
        string? district, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        ValidatePage(pageNumber, pageSize);
        search = Filter(search, 200); city = Filter(city, 100); district = Filter(district, 100);
        var query = db.TargetSchools.AsNoTracking().Where(x => x.IsActive);
        if (search is not null)
        {
            var pattern = Pattern(search);
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\"));
        }
        if (city is not null) query = query.Where(x => x.City == city);
        if (district is not null) query = query.Where(x => x.District == district);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((pageNumber - 1) * pageSize)
            .Take(pageSize).Select(x => new SchoolTargetView(x.Id, x.Name, x.City, x.District, x.MinimumScore, x.ScoreYear))
            .ToListAsync(cancellationToken);
        return new(items, count, pageNumber, pageSize);
    }

    public async Task<TargetSearchPage<UniversityTargetView>> SearchProgramsAsync(string? search, string? scoreType,
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        ValidatePage(pageNumber, pageSize);
        search = Filter(search, 200); scoreType = Filter(scoreType, 30);
        var query = db.TargetUniversityPrograms.AsNoTracking().Where(x => x.IsActive);
        if (search is not null)
        {
            var pattern = Pattern(search);
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\")
                || EF.Functions.ILike(x.UniversityName, pattern, "\\")
                || (x.ProgramCode != null && EF.Functions.ILike(x.ProgramCode, pattern, "\\")));
        }
        if (scoreType is not null) query = query.Where(x => x.ScoreType == scoreType);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.UniversityName).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new UniversityTargetView(x.Id, x.UniversityName, x.Name, x.ProgramCode, x.ScoreType, x.MinimumScore, x.ScoreYear))
            .ToListAsync(cancellationToken);
        return new(items, count, pageNumber, pageSize);
    }

    private static void ValidatePage(int pageNumber, int pageSize)
    {
        if (pageNumber is < 1 or > 10000 || pageSize is < 1 or > 50)
            throw new ArgumentException("Sayfalama değerleri geçersiz.");
    }
    private static string? Filter(string? value, int maximum)
    {
        value = value?.Trim();
        if (value?.Length > maximum) throw new ArgumentException("Arama filtresi çok uzun.");
        return string.IsNullOrEmpty(value) ? null : value;
    }
    private static string Pattern(string value) => "%" + value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
