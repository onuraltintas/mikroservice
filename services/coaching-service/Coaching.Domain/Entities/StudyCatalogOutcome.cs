namespace Coaching.Domain.Entities;

public sealed class OfficialCurriculumEdition
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = "";
    public string ExamCode { get; private set; } = "";
    public int GradeNumber { get; private set; }
    public string SubjectName { get; private set; } = "";
    public int AcademicYearStart { get; private set; }
    public int AcademicYearEnd { get; private set; }
    public string SourceUrl { get; private set; } = "";
    public string DocumentSha256 { get; private set; } = "";
    public string ScopeEvidenceUrl { get; private set; } = "";
    public string PackageSha256 { get; private set; } = "";
    private OfficialCurriculumEdition() { }
    public static OfficialCurriculumEdition Create(string code, string exam, int grade, string subject, int from, int to,
        string url, string hash, string scopeUrl, string? packageHash = null)
    {
        exam = StudyCatalogExam.NormalizeCode(exam);
        if (grade is < 1 or > 12 || from is < 2000 or > 2200 || to < from || to > 2200)
            throw new ArgumentException("Programın sınıfını ve geçerlilik yıllarını kontrol edin.");
        foreach (var link in new[] { url, scopeUrl })
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != "https" || link.Length > 1000)
                throw new ArgumentException("Resmi kaynak ve kapsam adresleri HTTPS olmalıdır.");
        if (hash is null || hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new ArgumentException("Belge SHA256 özeti gereklidir.");
        return new() { Code = StudyCatalogText.Require(code, 100), ExamCode = exam, GradeNumber = grade,
            SubjectName = StudyCatalogText.Require(subject, 300), AcademicYearStart = from, AcademicYearEnd = to,
            SourceUrl = url, DocumentSha256 = hash.ToLowerInvariant(), ScopeEvidenceUrl = scopeUrl, PackageSha256 = packageHash ?? hash.ToLowerInvariant() };
    }
    public void Edit(string code, string exam, int grade, string subject, int from, int to, string url, string hash, string scopeUrl)
    {
        var valid = Create(code, exam, grade, subject, from, to, url, hash, scopeUrl);
        Code=valid.Code; ExamCode=valid.ExamCode; GradeNumber=valid.GradeNumber; SubjectName=valid.SubjectName;
        AcademicYearStart=from; AcademicYearEnd=to; SourceUrl=valid.SourceUrl; DocumentSha256=valid.DocumentSha256; ScopeEvidenceUrl=valid.ScopeEvidenceUrl;
    }
}

public sealed class OfficialCurriculumTopic
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EditionId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public int OutcomeCount { get; private set; }
    public int LessonPeriods { get; private set; }
    private OfficialCurriculumTopic() { }
    public void Edit(string code, string name, int count, int periods)
    {
        var valid = Create(EditionId, code, name, count, periods);
        Code=valid.Code; Name=valid.Name; OutcomeCount=count; LessonPeriods=periods;
    }
    public static OfficialCurriculumTopic Create(Guid edition, string code, string name, int count, int periods)
    {
        if (edition == Guid.Empty || count is < 1 or > 500 || periods is < 1 or > 500) throw new ArgumentException("Resmi konu bütçesi geçersiz.");
        return new() { EditionId = edition, Code = StudyCatalogText.Require(code, 100), Name = StudyCatalogText.Require(name, 300), OutcomeCount = count, LessonPeriods = periods };
    }
}

public sealed class StudyCatalogOutcome
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EditionId { get; private set; }
    public Guid OfficialTopicId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Notes { get; private set; } = "";
    public int SourcePage { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsArchived { get; private set; }
    private StudyCatalogOutcome() { }
    public void Edit(string code, string name, string notes, int page, int order)
    {
        var valid = Create(EditionId, OfficialTopicId, code, name, notes, page, order);
        Code = valid.Code; Name = valid.Name; Notes = valid.Notes; SourcePage = valid.SourcePage; DisplayOrder = valid.DisplayOrder;
    }
    public void Archive(bool archived) => IsArchived = archived;
    public static StudyCatalogOutcome Create(Guid edition, Guid group, string code, string name, string notes, int page, int order)
    {
        if (edition == Guid.Empty || group == Guid.Empty || page < 1 || order < 1 || notes is null || notes.Length > 8000)
            throw new ArgumentException("Kazanım kaynağı ve sırasını kontrol edin.");
        return new() { EditionId = edition, OfficialTopicId = group, Code = StudyCatalogText.Require(code, 100),
            Name = StudyCatalogText.Require(name, 2000), Notes = notes, SourcePage = page, DisplayOrder = order };
    }
}

public sealed class StudyCatalogOutcomeSource
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OutcomeId { get; private set; }
    public string Source { get; private set; } = "";
    public string SourceId { get; private set; } = "";
    private StudyCatalogOutcomeSource() { }
    public static StudyCatalogOutcomeSource Create(Guid outcome, string source, string id)
    {
        if (outcome == Guid.Empty) throw new ArgumentException("Kazanım gereklidir.");
        return new() { OutcomeId = outcome, Source = StudyCatalogText.Require(source, 100), SourceId = StudyCatalogText.Require(id, 100) };
    }
}

public sealed class StudyCatalogTopicOutcome
{
    public Guid TopicId { get; private set; }
    public Guid OutcomeId { get; private set; }
    public bool IsApproved { get; private set; }
    public string Evidence { get; private set; } = "";
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    private StudyCatalogTopicOutcome() { }
    public static StudyCatalogTopicOutcome Create(Guid topic, Guid outcome, string evidence)
    {
        if (topic == Guid.Empty || outcome == Guid.Empty) throw new ArgumentException("Konu ve kazanım gereklidir.");
        return new() { TopicId = topic, OutcomeId = outcome, Evidence = StudyCatalogText.Require(evidence, 2000) };
    }
    public void Review(bool approved, string evidence, Guid actor)
    {
        if (actor == Guid.Empty) throw new ArgumentException("İnceleyen kullanıcı gereklidir.");
        Evidence = StudyCatalogText.Require(evidence, 2000); IsApproved = approved; ReviewedBy = actor; ReviewedAt = DateTime.UtcNow;
    }
}

public sealed class OutcomeDurationEstimate
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OutcomeId { get; private set; }
    public int Number { get; private set; }
    public int LearningMinutes { get; private set; }
    public int PracticeMinutes { get; private set; }
    public int CheckMinutes { get; private set; }
    public int TotalMinutes => LearningMinutes + PracticeMinutes + CheckMinutes;
    public string Method { get; private set; } = "";
    public string Confidence { get; private set; } = "";
    public string Evidence { get; private set; } = "";
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public bool IsPublished { get; private set; }
    private OutcomeDurationEstimate() { }
    public static OutcomeDurationEstimate Create(Guid outcome, int number, int learning, int practice, int check,
        string method, string confidence, string evidence, Guid? reviewer)
    {
        if (outcome == Guid.Empty || number < 1 || learning is < 0 or > 1440 || practice is < 0 or > 1440 || check is < 0 or > 1440
            || (long)learning + practice + check is < 1 or > 1440
            || method is not ("OfficialBudgetDraft" or "ReviewedStudy") || confidence is not ("Low" or "Medium" or "High")
            || (method == "ReviewedStudy" && (!reviewer.HasValue || reviewer == Guid.Empty)))
            throw new ArgumentException("Kazanım süresi 1–1440 dakika olmalı; yöntem ve inceleme bilgilerini kontrol edin.");
        return new() { OutcomeId = outcome, Number = number, LearningMinutes = learning, PracticeMinutes = practice, CheckMinutes = check,
            Method = method, Confidence = confidence, Evidence = StudyCatalogText.Require(evidence, 2000),
            ReviewedBy = reviewer, ReviewedAt = reviewer.HasValue ? DateTime.UtcNow : null };
    }
    public void Publish()
    {
        if (Method != "ReviewedStudy" || !ReviewedBy.HasValue) throw new ArgumentException("Taslak bütçe tahminini önce inceleyip yeni süre sürümü oluşturun.");
        IsPublished = true;
    }
    public void Withdraw() => IsPublished = false;
}

public sealed class StudyPlanTaskOutcome
{
    public Guid TaskId { get; private set; }
    public Guid OutcomeId { get; private set; }
    public Guid EstimateId { get; private set; }
    public int AllocatedMinutes { get; private set; }
    public string Origin { get; private set; } = "";
    public string? CodeSnapshot { get; private set; }
    public string? NameSnapshot { get; private set; }
    public void CaptureText(string code, string name)
    {
        if (CodeSnapshot is not null && NameSnapshot is not null) return;
        var validCode = StudyCatalogText.Require(code, 100); var validName = StudyCatalogText.Require(name, 2000);
        CodeSnapshot = validCode; NameSnapshot = validName;
    }
    private StudyPlanTaskOutcome() { }
    public static StudyPlanTaskOutcome Create(Guid task, Guid outcome, Guid estimate, int minutes, string origin)
    {
        if (task == Guid.Empty || outcome == Guid.Empty || estimate == Guid.Empty || minutes is < 1 or > 1440
            || origin is not ("Catalog" or "StudentOverride")) throw new ArgumentException("Plan kazanım süresi geçersiz.");
        return new() { TaskId = task, OutcomeId = outcome, EstimateId = estimate, AllocatedMinutes = minutes, Origin = origin };
    }
}
