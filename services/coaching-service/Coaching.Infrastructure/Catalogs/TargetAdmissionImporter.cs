using System.Globalization;
using System.Text;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

public sealed record TargetAdmissionPackage(string ExamCode,int Year,string Stage,string SourceUrl,string DocumentHash,
    IReadOnlyList<TargetAdmissionData> Rows,IReadOnlyDictionary<string,string>? Issues=null,
    IReadOnlyDictionary<string,string[]>? SchoolAliases=null);
public sealed record TargetAdmissionImportResult(Guid BatchId,int Verified,int Ambiguous,int Conflicts,bool AlreadyImported);

public sealed class TargetAdmissionImporter(CoachingDbContext db)
{
    public async Task<TargetAdmissionImportResult> ImportAsync(TargetAdmissionPackage package,CancellationToken ct=default)
    {
        var batch=TargetAdmissionBatch.Create(package.ExamCode,package.Year,package.Stage,package.SourceUrl,package.DocumentHash);
        if(package.Rows.Count==0 || package.Rows.Select(x=>x.ExternalCode).Distinct(StringComparer.Ordinal).Count()!=package.Rows.Count)
            throw new ArgumentException("An import needs nonempty rows with unique source codes.");
        if(package.ExamCode=="LGS" && package.Stage is not ("Main" or "Transfer1" or "Transfer2")
            && package.Rows.Any(x=>x.MinimumScore.HasValue||x.MaximumScore.HasValue||x.Percentile.HasValue||x.Rank.HasValue||x.Placed.HasValue))
            throw new ArgumentException("LGS results require a verified placement stage; metadata must not carry scores.");
        var rows=package.Rows.Select(x=>TargetAdmissionRecord.Create(batch.Id,x)).ToList();
        if(rows.Any(x=>package.ExamCode=="LGS" ? x.ScoreType!="LGS" : x.ScoreType is not ("TYT" or "SAY" or "EA" or "SOZ" or "DIL")))
            throw new ArgumentException("The score type does not match the package scope.");
        var prior=await db.Set<TargetAdmissionBatch>().SingleOrDefaultAsync(x=>x.ExamCode==batch.ExamCode && x.Year==batch.Year
            && x.Stage==batch.Stage && x.DocumentHash==batch.DocumentHash,ct);
        if(prior is not null)
        {
            var saved=await db.Set<TargetAdmissionRecord>().Where(x=>x.BatchId==prior.Id).ToListAsync(ct);
            if(prior.SourceUrl!=batch.SourceUrl || !saved.OrderBy(x=>x.ExternalCode,StringComparer.Ordinal).Select(x=>x.ToData())
                .SequenceEqual(rows.OrderBy(x=>x.ExternalCode,StringComparer.Ordinal).Select(x=>x.ToData())))
                throw new ArgumentException("An existing source hash cannot be reused for changed data.");
            if(package.Issues?.Any(issue=>!saved.Any(x=>x.ExternalCode==issue.Key && x.MatchStatus=="Conflict" && x.Issue==issue.Value))==true)
                throw new ArgumentException("Changed review findings require a new reviewed import; existing matches are preserved.");
            return Result(prior.Id,saved,true);
        }
        var programs=await db.TargetUniversityPrograms.ToListAsync(ct);
        var schools=await db.TargetSchools.ToListAsync(ct);
        var programLookup=programs.Where(x=>x.ProgramCode is not null).ToLookup(x=>x.ProgramCode!);
        var schoolLookup=schools.GroupBy(x=>SchoolKey(x.Name,x.City,x.District)).ToDictionary(x=>x.Key,x=>x.ToList());
        // Different source documents for the same cycle need an explicit review, never last-writer-wins.
        var cycleBatches=await db.Set<TargetAdmissionBatch>().Where(x=>x.ExamCode==batch.ExamCode && x.Year==batch.Year && x.Stage==batch.Stage).ToListAsync(ct);
        var otherCycle=cycleBatches.Any(x=>x.SourceUrl==batch.SourceUrl);
        var cycleIds=cycleBatches.Select(x=>x.Id).ToList();
        var previous=await db.Set<TargetAdmissionRecord>().Where(x=>cycleIds.Contains(x.BatchId)).ToListAsync(ct);
        foreach(var row in rows)
        {
            if(package.Issues?.TryGetValue(row.ExternalCode,out var issue)==true)
            {row.MarkIssue("Conflict",issue);continue;}
            if(otherCycle){row.MarkIssue("Conflict","Another document exists for this cycle; review source correction.");continue;}
            if(previous.Any(x=>x.ExternalCode==row.ExternalCode || (package.ExamCode=="LGS" && OfferingKey(x)==OfferingKey(row))))
            {row.MarkIssue("Conflict","An overlapping offering exists in another source; review before linking.");continue;}
            if(package.ExamCode=="YKS")
            {
                var candidates=programLookup[row.ExternalCode].ToList();
                if(candidates.Count>1){row.MarkIssue("Ambiguous","Multiple legacy programs have this source code.");continue;}
                var target=candidates.SingleOrDefault();
                if(target is not null && NormalizeScore(target.ScoreType)!=row.ScoreType)
                {row.MarkIssue("Conflict","Program code exists with a different score type.");continue;}
                if(target is null)
                {
                    target=TargetUniversityProgram.Create("osym-2026",row.ExternalCode,row.Institution,row.Name,row.ExternalCode,row.ScoreType,null);
                    target.SetActive(true);db.Add(target);
                }
                row.Match(null,target.Id,"Verified");
            }
            else
            {
                if(string.IsNullOrWhiteSpace(row.City)||string.IsNullOrWhiteSpace(row.District))
                {row.MarkIssue("Pending","Official province and district are required.");continue;}
                var key=SchoolKey(row.Institution,row.City,row.District);
                var names=package.SchoolAliases?.GetValueOrDefault(row.ExternalCode)??[];
                var candidates=names.Append(row.Institution).SelectMany(name=>schoolLookup.GetValueOrDefault(SchoolKey(name,row.City,row.District))??[]).DistinctBy(x=>x.Id).ToList();
                if(candidates.Count>1){row.MarkIssue("Ambiguous","Multiple legacy school rows; offering evidence needs review.");continue;}
                var target=candidates.SingleOrDefault();
                if(target is null)
                {
                    target=TargetSchool.Create("meb-2026",row.ExternalCode,row.Institution,row.City,row.District,null);
                    target.SetActive(true);db.Add(target);
                    schoolLookup[key]=[target];
                }
                row.Match(target.Id,null,"Verified");
            }
        }
        batch.Publish();db.Add(batch);db.AddRange(rows);
        // One SaveChanges transaction keeps targets, evidence and matches atomic. Legacy rows are never updated.
        await db.SaveChangesAsync(ct);
        return Result(batch.Id,rows,false);
    }
    private static TargetAdmissionImportResult Result(Guid id,IReadOnlyList<TargetAdmissionRecord> rows,bool prior)=>
        new(id,rows.Count(x=>x.MatchStatus=="Verified"),rows.Count(x=>x.MatchStatus=="Ambiguous"),rows.Count(x=>x.MatchStatus is "Conflict" or "Pending"),prior);
    private static string NormalizeScore(string? s)=>s switch {"SÖZ"=>"SOZ","DİL"=>"DIL",_=>s??""};
    private static string SchoolKey(string name,string city,string district)=>Key(name,city,district.Trim().EndsWith(" (MERKEZ)",StringComparison.OrdinalIgnoreCase)?district.Trim()[..^9]:district);
    private static string OfferingKey(TargetAdmissionRecord row)=>Key(row.Institution,row.City??"",row.District??"",row.Field??"",row.EducationType??"",row.Language??"",row.ScoreType);
    private static string Key(params string[] values)=>string.Join('|',values.Select(x=>string.Join(' ',x.Trim().ToUpper(CultureInfo.GetCultureInfo("tr-TR")).Normalize(NormalizationForm.FormC).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries))));
}
