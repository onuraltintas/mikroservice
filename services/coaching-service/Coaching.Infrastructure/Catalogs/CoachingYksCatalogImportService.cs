using System.Security.Cryptography;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

public sealed record YksDataPackage(string Version,int AcademicYearStart,int AcademicYearEnd,string SourceUrl,string DocumentSha256,
    string ScopeEvidenceUrl,IReadOnlyList<YksSubjectImport> Subjects,IReadOnlyList<YksOutcomeImport> Outcomes,
    IReadOnlyList<YksTopicImport> Topics,IReadOnlyList<YksEquivalentTopicImport> EquivalentTopics);
public sealed record YksSubjectImport(string Tag,string Code,string Subject,string ExamCode,IReadOnlyList<string> LessonSourceIds);
public sealed record YksOfficialReference(string Subject,string Code,string Name,int Page);
public sealed record YksOutcomeImport(string Key,string Owner,string Code,string Name,string SourceKind,int? SourcePage,string SourceUrl,
    string Notes,IReadOnlyList<string> SourceIds,IReadOnlyList<YksOfficialReference> OfficialReferences,int LearningMinutes,int PracticeMinutes,int CheckMinutes,
    string DurationEvidence,IReadOnlyList<YksShareImport> Shares);
public sealed record YksShareImport(string TopicSourceId,int LearningMinutes,int PracticeMinutes,int CheckMinutes,string ScopeDescription);
public sealed record YksTopicImport(string SourceId,string LessonSourceId,string Name,string Exam,IReadOnlyList<string> OutcomeKeys,IReadOnlyList<YksScopeImport> Scopes);
public sealed record YksScopeImport(string ExamCode,string ScoreType,string TestCode,string? Language,IReadOnlyList<string> ExcludedOutcomeKeys);
public sealed record YksEquivalentTopicImport(string TopicSourceId,string CanonicalTopicSourceId,string LessonSourceId,string Name,string Reason);
public sealed record YksImportResult(string Fingerprint,int Programs,int Outcomes,int Topics,int Scopes,int Shares);

/// <summary>Used by the reviewed release tool; it has no public HTTP endpoint.</summary>
public sealed class CoachingYksCatalogImportService(CoachingDbContext db,ICoachingAdminScopeAuthorization scope,ICurrentUserService user)
{
    public async Task<YksImportResult> ImportAsync(YksDataPackage p,string reason,CancellationToken ct)
    {
        if(!user.IsAuthenticated||user.UserId is not {} actor||!(await scope.RequireReadScopeAsync(ct)).IsGlobal)
            throw new UnauthorizedAccessException("Genel yönetici yetkisi gereklidir.");
        if(string.IsNullOrWhiteSpace(reason)||reason.Trim().Length<5)throw new ArgumentException("Aktarım gerekçesi gereklidir.");
        Validate(p);
        var fingerprint=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(p)));
        var codes=p.Subjects.Select(x=>x.Code).ToArray();
        var existing=await db.Set<OfficialCurriculumEdition>().Where(x=>Enumerable.Contains(codes,x.Code)).ToArrayAsync(ct);
        var expected=new YksImportResult(fingerprint,p.Subjects.Count,p.Outcomes.Count,p.Topics.Count,p.Topics.Sum(x=>x.Scopes.Count),p.Outcomes.Sum(x=>x.Shares.Count));
        if(existing.Length>0)
        {
            if(existing.Length!=codes.Length||existing.Any(x=>x.PackageSha256!=fingerprint))throw new ArgumentException("YKS paketi kısmen mevcut veya değişmiş; yeni sürüm ve inceleme gerekir.");
            var editionIds=existing.Select(x=>x.Id).ToArray();
            var outcomeIds=await db.Set<StudyCatalogOutcome>().Where(x=>Enumerable.Contains(editionIds,x.EditionId)).Select(x=>x.Id).ToArrayAsync(ct);
            if(outcomeIds.Length!=p.Outcomes.Count||await db.Set<OutcomeDurationEstimate>().CountAsync(x=>Enumerable.Contains(outcomeIds,x.OutcomeId)&&x.IsPublished,ct)!=p.Outcomes.Count
                ||await db.Set<StudyCatalogTopicOutcomeShare>().CountAsync(x=>Enumerable.Contains(outcomeIds,x.OutcomeId),ct)!=expected.Shares)
                throw new ArgumentException("Önceki YKS aktarımının onaylı kayıtları değişmiş; yeniden inceleyin.");
            return expected;
        }
        var lessonSources=p.Subjects.SelectMany(x=>x.LessonSourceIds).Distinct().ToArray();
        var lessons=await db.StudyCatalogLessons.Where(x=>x.Source=="approved-catalog"&&Enumerable.Contains(lessonSources,x.SourceId)).ToDictionaryAsync(x=>x.SourceId,ct);
        if(lessons.Count!=lessonSources.Length||lessons.Values.Any(x=>!x.IsActive||x.ExamCode is not ("TYT" or "AYT" or "YDT")||x.CurriculumVersionId==null))
            throw new ArgumentException("Aktif ve program sürümüne bağlı YKS dersleri eksik.");
        if(lessons.Values.Any(x=>x.ExamCode=="YDT"&&(!x.Name.Contains("İngilizce",StringComparison.Ordinal)||x.Name.Contains("Arap",StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("Bu paket yalnız İngilizce YDT kapsamındadır.");
        var topicSources=p.Topics.Select(x=>x.SourceId).Concat(p.EquivalentTopics.Select(x=>x.TopicSourceId)).Distinct().ToArray();
        var topics=await db.StudyCatalogTopics.Where(x=>x.Source=="approved-catalog"&&Enumerable.Contains(topicSources,x.SourceId)).ToDictionaryAsync(x=>x.SourceId,ct);
        if(topics.Count!=topicSources.Length)throw new ArgumentException("Kaynak YKS konuları eksik.");
        foreach(var topic in p.Topics)
            if(!topics[topic.SourceId].IsActive||topics[topic.SourceId].LessonId!=lessons[topic.LessonSourceId].Id||lessons[topic.LessonSourceId].ExamCode!=topic.Exam)
                throw new ArgumentException("YKS konu ve ders sahipliği değişmiş.");
        var units=topics.Values.Select(x=>x.UnitId).Distinct().ToArray();
        if(await db.StudyCatalogUnits.CountAsync(x=>Enumerable.Contains(units,x.Id)&&x.IsActive,ct)!=units.Length)
            throw new ArgumentException("YKS konularının etkin üniteleri eksik.");
        var editions=p.Subjects.ToDictionary(x=>x.Tag,x=>OfficialCurriculumEdition.Create(x.Code,x.ExamCode,null,
            x.Subject+" — sınav hazırlık kapsamı",p.AcademicYearStart,p.AcademicYearEnd,p.SourceUrl,p.DocumentSha256,p.ScopeEvidenceUrl,fingerprint));
        var groups=p.Subjects.ToDictionary(x=>x.Tag,x=>OfficialCurriculumTopic.Create(editions[x.Tag].Id,"PREP."+x.Tag,
            "Sınav hazırlık kapsamı; konu ayrımı MEBİ, resmî kaynak ve beceri türleri ayrı",p.Outcomes.Count(o=>o.Owner==x.Tag),null));
        var outcomes=p.Outcomes.ToDictionary(x=>x.Key,x=>StudyCatalogOutcome.Create(editions[x.Owner].Id,groups[x.Owner].Id,x.Code,x.Name,
            x.Notes+"\n"+string.Join("; ",x.OfficialReferences.Select(r=>$"Resmî dayanak: {r.Subject} {r.Code}, s.{r.Page}")),x.SourcePage,
            p.Outcomes.Where(o=>o.Owner==x.Owner).Select((o,i)=>new{o.Key,Order=i+1}).Single(o=>o.Key==x.Key).Order,x.SourceKind,x.SourceUrl));
        var estimates=p.Outcomes.ToDictionary(x=>x.Key,x=>OutcomeDurationEstimate.Create(outcomes[x.Key].Id,1,x.LearningMinutes,x.PracticeMinutes,x.CheckMinutes,
            "ReviewedStudy","Low",x.DurationEvidence,actor));
        var mappings=p.Topics.SelectMany(t=>t.OutcomeKeys.Select(key=>StudyCatalogTopicOutcome.Create(topics[t.SourceId].Id,outcomes[key].Id,
            "MEBİ konu bağlantısı; kaynak türü ve resmî dayanak kayıtta ayrıdır. "+reason))).ToArray();
        foreach(var mapping in mappings)mapping.Review(true,mapping.Evidence,actor);
        var shares=p.Outcomes.SelectMany(o=>o.Shares.Select(s=>StudyCatalogTopicOutcomeShare.Create(topics[s.TopicSourceId].Id,estimates[o.Key],s.LearningMinutes,s.PracticeMinutes,s.CheckMinutes,s.ScopeDescription))).ToArray();
        foreach(var outcome in p.Outcomes)
        {
            OutcomeScopeBudget.Validate(estimates[outcome.Key],shares.Where(x=>x.OutcomeId==outcomes[outcome.Key].Id).ToArray(),
                mappings.Where(x=>x.OutcomeId==outcomes[outcome.Key].Id).Select(x=>x.TopicId).ToArray());
            estimates[outcome.Key].Publish();
        }
        var scopes=p.Topics.SelectMany(t=>t.Scopes.Select(s=>
        {
            var row=StudyCatalogYksScope.Create(topics[t.SourceId].Id,s.ExamCode,s.ScoreType,s.TestCode,s.Language,
                "ÖSYM 2026 kılavuzu, TTKB 2026 kapsamı ve MEBİ konu kataloğu. 2027 resmî kapsam iddiası değildir.");
            row.ExcludeOutcomes(s.ExcludedOutcomeKeys.Select(key=>outcomes[key].Id));return row;
        })).ToArray();
        var aliases=p.Outcomes.SelectMany(o=>o.SourceIds.Select(id=>StudyCatalogOutcomeSource.Create(outcomes[o.Key].Id,"mebi-yks",id))).ToArray();
        var sourceIds=aliases.Select(x=>x.SourceId).ToArray();
        if(await db.Set<StudyCatalogOutcomeSource>().AnyAsync(x=>x.Source=="mebi-yks"&&Enumerable.Contains(sourceIds,x.SourceId),ct))
            throw new ArgumentException("Kaynak kimlikleri başka bir YKS paketine bağlı.");
        // Coaching audit K1: production retries transient failures and refuses a transaction opened outside its execution
        // strategy. The import is not replayed automatically (its tracked edits are gone after a rollback).
        var attempt=0;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
        {
        if(attempt++>0)throw new InvalidOperationException("YKS paketi aktarımı geçici bir veritabanı hatasıyla yarıda kaldı; aktarımı yeniden başlatın.");
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        try
        {
        foreach(var topic in p.Topics)
        {
            var row=topics[topic.SourceId];var budget=shares.Where(x=>x.TopicId==row.Id).Sum(x=>x.TotalMinutes);
            row.Edit(row.Name,row.DisplayOrder,budget);
        }
        foreach(var equivalent in p.EquivalentTopics)
        {
            var row=topics[equivalent.TopicSourceId];var canonical=topics[equivalent.CanonicalTopicSourceId];
            if(lessons.Values.All(x=>x.Id!=row.LessonId))throw new ArgumentException("Eşdeğer konu YKS kapsamı dışında.");
            row.Edit(row.Name,row.DisplayOrder,canonical.EstimatedMinutes);
        }
            db.AddRange(editions.Values);db.AddRange(groups.Values);db.AddRange(outcomes.Values);db.AddRange(estimates.Values);
            db.AddRange(mappings);db.AddRange(shares);db.AddRange(scopes);db.AddRange(aliases);
            db.AddRange(p.Subjects.SelectMany(s=>s.LessonSourceIds.Select(id=>OfficialCurriculumLessonBinding.Create(editions[s.Tag].Id,lessons[id].Id))));
            await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return expected;
        }
        catch{await transaction.RollbackAsync(ct);db.ChangeTracker.Clear();throw;}
        });
    }

    public static void Validate(YksDataPackage p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if(string.IsNullOrWhiteSpace(p.Version)||p.Subjects.Count==0||p.Outcomes.Count==0||p.Topics.Count==0
            ||p.Subjects.Select(x=>x.Tag).Distinct().Count()!=p.Subjects.Count||p.Subjects.Select(x=>x.Code).Distinct().Count()!=p.Subjects.Count
            ||p.Outcomes.Select(x=>x.Key).Distinct().Count()!=p.Outcomes.Count||p.Topics.Select(x=>x.SourceId).Distinct().Count()!=p.Topics.Count)
            throw new ArgumentException("Geçerli ve yinelenmeyen YKS paketi gerekir.");
        var owners=p.Subjects.Select(x=>x.Tag).ToHashSet();var keys=p.Outcomes.Select(x=>x.Key).ToHashSet();
        var topicIds=p.Topics.Select(x=>x.SourceId).ToHashSet();
        if(p.EquivalentTopics.Select(x=>x.TopicSourceId).Distinct().Count()!=p.EquivalentTopics.Count
            ||p.EquivalentTopics.Any(x=>topicIds.Contains(x.TopicSourceId)||!topicIds.Contains(x.CanonicalTopicSourceId)))
            throw new ArgumentException("Eşdeğer konu kayıtları açık bir ana konuya bağlanmalıdır.");
        if(p.Outcomes.Any(x=>!owners.Contains(x.Owner)||x.Shares.Count==0||x.Shares.Any(s=>!topicIds.Contains(s.TopicSourceId)))
            ||p.Topics.Any(t=>t.OutcomeKeys.Count==0||t.OutcomeKeys.Distinct().Count()!=t.OutcomeKeys.Count||t.OutcomeKeys.Any(k=>!keys.Contains(k))
                ||t.Scopes.Any(s=>s.ExcludedOutcomeKeys.Any(k=>!t.OutcomeKeys.Contains(k))))
            ||p.Outcomes.SelectMany(x=>x.SourceIds).Distinct().Count()!=p.Outcomes.Sum(x=>x.SourceIds.Count))
            throw new ArgumentException("YKS kazanım, kaynak, konu veya kapsam bağlantıları geçersiz.");
        foreach(var subject in p.Subjects)
            _=OfficialCurriculumEdition.Create(subject.Code,subject.ExamCode,null,subject.Subject,p.AcademicYearStart,p.AcademicYearEnd,p.SourceUrl,p.DocumentSha256,p.ScopeEvidenceUrl);
    }
}
