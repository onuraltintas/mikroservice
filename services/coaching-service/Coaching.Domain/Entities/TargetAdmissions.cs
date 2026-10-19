namespace Coaching.Domain.Entities;

public sealed class TargetAdmissionBatch
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ExamCode { get; private set; } = "";
    public int Year { get; private set; }
    public string Stage { get; private set; } = "";
    public string SourceUrl { get; private set; } = "";
    public string DocumentHash { get; private set; } = "";
    public DateTimeOffset RetrievedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; private set; }
    private TargetAdmissionBatch() { }
    public static TargetAdmissionBatch Create(string exam, int year, string stage, string url, string hash)
    {
        if (exam is not ("LGS" or "YKS") || year < 2026 || year > 2100 || string.IsNullOrWhiteSpace(stage)
            || !Uri.TryCreate(url,UriKind.Absolute,out var uri) || uri.Scheme != "https"
            || !(uri.Host=="meb.gov.tr" || uri.Host.EndsWith(".meb.gov.tr",StringComparison.Ordinal)
                || uri.Host=="osym.gov.tr" || uri.Host.EndsWith(".osym.gov.tr",StringComparison.Ordinal)
                || uri.Host=="yokatlas.yok.gov.tr")
            || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort
            || hash is null || hash.Length!=64 || hash.Any(c=>!Uri.IsHexDigit(c)))
            throw new ArgumentException("A 2026+ year, official HTTPS source and SHA256 are required.");
        return new() {ExamCode=exam,Year=year,Stage=StudyCatalogText.Require(stage,40),SourceUrl=url,DocumentHash=hash.ToLowerInvariant()};
    }
    public void Publish() => PublishedAt ??= DateTimeOffset.UtcNow;
}

public sealed record TargetAdmissionData(string ExternalCode, string Institution, string Name, string ScoreType,
    string? City, string? District, string? InstitutionType, string? Degree, string? Language, string? Scholarship,
    string? EducationType, string? Field, int? Quota, int? Placed, decimal? MinimumScore, decimal? MaximumScore,
    int? Rank, decimal? Percentile);

/// <summary>A source-year snapshot. Uncertain rows retain their evidence without changing legacy identities.</summary>
public sealed class TargetAdmissionRecord
{
    public Guid Id { get; private set; }=Guid.NewGuid();
    public Guid BatchId { get; private set; }
    public Guid? SchoolId { get; private set; }
    public Guid? ProgramId { get; private set; }
    public string ExternalCode { get; private set; }="";
    public string Institution { get; private set; }="";
    public string Name { get; private set; }="";
    public string ScoreType { get; private set; }="";
    public string? City { get; private set; }
    public string? District { get; private set; }
    public string? InstitutionType { get; private set; }
    public string? Degree { get; private set; }
    public string? Language { get; private set; }
    public string? Scholarship { get; private set; }
    public string? EducationType { get; private set; }
    public string? Field { get; private set; }
    public int? Quota { get; private set; }
    public int? Placed { get; private set; }
    public decimal? MinimumScore { get; private set; }
    public decimal? MaximumScore { get; private set; }
    public int? Rank { get; private set; }
    public decimal? Percentile { get; private set; }
    public string MatchStatus { get; private set; }="Pending";
    public string? Issue { get; private set; }
    private TargetAdmissionRecord() { }
    public TargetAdmissionData ToData()=>new(ExternalCode,Institution,Name,ScoreType,City,District,InstitutionType,
        Degree,Language,Scholarship,EducationType,Field,Quota,Placed,MinimumScore,MaximumScore,Rank,Percentile);
    public static TargetAdmissionRecord Create(Guid batchId,TargetAdmissionData d)
    {
        if(batchId==Guid.Empty || d.Quota<0 || d.Placed<0 || d.MinimumScore<0 || d.MaximumScore<0
            || d.MinimumScore>d.MaximumScore || d.Rank<=0 || d.Percentile is <0 or >100)
            throw new ArgumentException("Invalid admission values.");
        return new() {BatchId=batchId,ExternalCode=StudyCatalogText.Require(d.ExternalCode,100),
            Institution=StudyCatalogText.Require(d.Institution,300),Name=StudyCatalogText.Require(d.Name,300),
            ScoreType=StudyCatalogText.Require(d.ScoreType,30),City=d.City,District=d.District,
            InstitutionType=d.InstitutionType,Degree=d.Degree,Language=d.Language,Scholarship=d.Scholarship,
            EducationType=d.EducationType,Field=d.Field,Quota=d.Quota,Placed=d.Placed,MinimumScore=d.MinimumScore,
            MaximumScore=d.MaximumScore,Rank=d.Rank,Percentile=d.Percentile};
    }
    public void Match(Guid? schoolId,Guid? programId,string status)
    {
        if((schoolId.HasValue==programId.HasValue) || schoolId==Guid.Empty || programId==Guid.Empty || status!="Verified")
            throw new ArgumentException("Exactly one verified target identity is required.");
        SchoolId=schoolId;ProgramId=programId;MatchStatus=status;Issue=null;
    }
    public void MarkIssue(string status,string reason)
    {
        if(status is not ("Pending" or "Ambiguous" or "Conflict")) throw new ArgumentException("Invalid review status.");
        MatchStatus=status;Issue=StudyCatalogText.Require(reason,2000);SchoolId=null;ProgramId=null;
    }
}
