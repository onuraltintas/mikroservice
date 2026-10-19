using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed record TargetAdmissionFilter(string ExamCode="YKS",int Year=2026,string? Search=null,string? City=null,
    string? District=null,string? ScoreType=null,string? Institution=null,string? InstitutionType=null,string? Degree=null,
    string? Language=null,string? Scholarship=null,string? EducationType=null,string? Field=null,
    decimal? MinimumScore=null,decimal? MaximumScore=null,int? MaximumRank=null,decimal? MaximumPercentile=null,
    int PageNumber=1,int PageSize=20,string Sort="name");
public sealed class TargetAdmissionView
{
    public Guid Id {get;init;} public Guid TargetId {get;init;}
    public string Name {get;init;}="";public string Institution {get;init;}="";public string ExternalCode {get;init;}="";public string ScoreType {get;init;}="";
    public string? City {get;init;} public string? District {get;init;} public string? InstitutionType {get;init;} public string? Degree {get;init;}
    public string? Language {get;init;} public string? Scholarship {get;init;} public string? EducationType {get;init;} public string? Field {get;init;}
    public int? Quota {get;init;}public int? Placed {get;init;}public decimal? MinimumScore {get;init;}public decimal? MaximumScore {get;init;}
    public int? Rank {get;init;}public decimal? Percentile {get;init;}public int Year {get;init;}public string Stage {get;init;}="";public string SourceUrl {get;init;}="";
}
public sealed class TargetAdmissionSearchService(CoachingDbContext db)
{
    public static void Validate(TargetAdmissionFilter f)
    {
        if(f.ExamCode is not ("LGS" or "YKS") || f.Year<2026 || f.Year>2100 || f.PageNumber is <1 or >10000
            || f.PageSize is <1 or >50 || f.MinimumScore<0 || f.MaximumScore<0 || f.MinimumScore>f.MaximumScore
            || f.MaximumRank<=0 || f.MaximumPercentile is <0 or >100 || f.Sort is not ("name" or "score-desc" or "rank" or "percentile"))
            throw new ArgumentException("Invalid admission search filters.");
        foreach(var s in new[]{f.Search,f.City,f.District,f.ScoreType,f.Institution,f.InstitutionType,f.Degree,f.Language,f.Scholarship,f.EducationType,f.Field})
            if(s?.Length>300)throw new ArgumentException("Search value too long.");
    }
    private IQueryable<TargetAdmissionView> Base(TargetAdmissionFilter f)=>
        from r in db.Set<TargetAdmissionRecord>().AsNoTracking()
        join b in db.Set<TargetAdmissionBatch>().AsNoTracking() on r.BatchId equals b.Id
        where b.ExamCode==f.ExamCode && b.Year==f.Year && b.PublishedAt!=null && r.MatchStatus=="Verified"
            && ((r.ProgramId!=null && db.TargetUniversityPrograms.Any(p=>p.Id==r.ProgramId && p.IsActive))
                || (r.SchoolId!=null && db.TargetSchools.Any(s=>s.Id==r.SchoolId && s.IsActive)))
        select new TargetAdmissionView {Id=r.Id,TargetId=r.ProgramId??r.SchoolId!.Value,Name=r.Name,Institution=r.Institution,
            ExternalCode=r.ExternalCode,ScoreType=r.ScoreType,City=r.City,District=r.District,InstitutionType=r.InstitutionType,
            Degree=r.Degree,Language=r.Language,Scholarship=r.Scholarship,EducationType=r.EducationType,Field=r.Field,
            Quota=r.Quota,Placed=r.Placed,MinimumScore=r.MinimumScore,MaximumScore=r.MaximumScore,Rank=r.Rank,
            Percentile=r.Percentile,Year=b.Year,Stage=b.Stage,SourceUrl=b.SourceUrl};
    public async Task<TargetSearchPage<TargetAdmissionView>> SearchAsync(TargetAdmissionFilter f,CancellationToken ct=default)
    {
        Validate(f);var q=Base(f);
        if(!string.IsNullOrWhiteSpace(f.Search)){var s=f.Search.Trim().ToUpper();q=q.Where(x=>x.Name.ToUpper().Contains(s)||x.Institution.ToUpper().Contains(s)||x.ExternalCode.Contains(s));}
        if(!string.IsNullOrWhiteSpace(f.City))q=q.Where(x=>x.City==f.City);
        if(!string.IsNullOrWhiteSpace(f.District))q=q.Where(x=>x.District==f.District);
        if(!string.IsNullOrWhiteSpace(f.ScoreType))q=q.Where(x=>x.ScoreType==f.ScoreType);
        if(!string.IsNullOrWhiteSpace(f.Institution))q=q.Where(x=>x.Institution==f.Institution);
        if(!string.IsNullOrWhiteSpace(f.InstitutionType))q=q.Where(x=>x.InstitutionType==f.InstitutionType);
        if(!string.IsNullOrWhiteSpace(f.Degree))q=q.Where(x=>x.Degree==f.Degree);
        if(!string.IsNullOrWhiteSpace(f.Language))q=q.Where(x=>x.Language==f.Language);
        if(!string.IsNullOrWhiteSpace(f.Scholarship))q=q.Where(x=>x.Scholarship==f.Scholarship);
        if(!string.IsNullOrWhiteSpace(f.EducationType))q=q.Where(x=>x.EducationType==f.EducationType);
        if(!string.IsNullOrWhiteSpace(f.Field))q=q.Where(x=>x.Field==f.Field);
        if(f.MinimumScore.HasValue)q=q.Where(x=>x.MinimumScore>=f.MinimumScore);
        if(f.MaximumScore.HasValue)q=q.Where(x=>x.MinimumScore<=f.MaximumScore);
        if(f.MaximumRank.HasValue)q=q.Where(x=>x.Rank<=f.MaximumRank);
        if(f.MaximumPercentile.HasValue)q=q.Where(x=>x.Percentile<=f.MaximumPercentile);
        var count=await q.CountAsync(ct);
        q=f.Sort switch {"score-desc"=>q.OrderByDescending(x=>x.MinimumScore.HasValue).ThenByDescending(x=>x.MinimumScore),
            "rank"=>q.OrderByDescending(x=>x.Rank.HasValue).ThenBy(x=>x.Rank),
            "percentile"=>q.OrderByDescending(x=>x.Percentile.HasValue).ThenBy(x=>x.Percentile),
            _=>q.OrderBy(x=>x.Institution).ThenBy(x=>x.Name)};
        return new(await ((IOrderedQueryable<TargetAdmissionView>)q).ThenBy(x=>x.Id).Skip((f.PageNumber-1)*f.PageSize).Take(f.PageSize).ToListAsync(ct),count,f.PageNumber,f.PageSize);
    }
    public async Task<Dictionary<string,string[]>> FacetsAsync(string exam,int year,CancellationToken ct=default)
    {
        var f=new TargetAdmissionFilter(exam,year);Validate(f);
        var rows=await Base(f).Select(x=>new{x.City,x.District,x.ScoreType,x.Institution,x.InstitutionType,x.Degree,x.Language,x.Scholarship,x.EducationType,x.Field}).Distinct().ToListAsync(ct);
        return new(){["city"]=rows.Select(x=>x.City).OfType<string>().Distinct().Order().ToArray(),
            ["district"]=rows.Select(x=>x.District).OfType<string>().Distinct().Order().ToArray(),
            ["scoreType"]=rows.Select(x=>x.ScoreType).Distinct().Order().ToArray(),
            ["institution"]=rows.Select(x=>x.Institution).Distinct().Order().ToArray(),
            ["institutionType"]=rows.Select(x=>x.InstitutionType).OfType<string>().Distinct().Order().ToArray(),
            ["degree"]=rows.Select(x=>x.Degree).OfType<string>().Distinct().Order().ToArray(),
            ["language"]=rows.Select(x=>x.Language).OfType<string>().Distinct().Order().ToArray(),
            ["scholarship"]=rows.Select(x=>x.Scholarship).OfType<string>().Distinct().Order().ToArray(),
            ["educationType"]=rows.Select(x=>x.EducationType).OfType<string>().Distinct().Order().ToArray(),
            ["field"]=rows.Select(x=>x.Field).OfType<string>().Distinct().Order().ToArray()};
    }
}
