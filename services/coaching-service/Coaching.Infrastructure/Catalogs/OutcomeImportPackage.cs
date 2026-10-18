namespace Coaching.Infrastructure.Catalogs;

public sealed record OutcomeImportPackage(string Code, string ExamCode, int? GradeNumber, string SubjectName,
    int AcademicYearStart, int AcademicYearEnd, string SourceUrl, string DocumentSha256, string ScopeEvidenceUrl,
    IReadOnlyList<OfficialTopicImport> OfficialTopics, IReadOnlyList<OutcomeImport> Outcomes, IReadOnlyList<TopicOutcomeImport> Mappings);
public sealed record OfficialTopicImport(string Code, string Name, int OutcomeCount, int LessonPeriods);
public sealed record OutcomeSourceImport(string Source, string SourceId);
public sealed record OutcomeImport(string Code, string Name, string Notes, string OfficialTopicCode, int SourcePage, int DisplayOrder,
    int LearningMinutes, int PracticeMinutes, int CheckMinutes, string DurationEvidence, IReadOnlyList<OutcomeSourceImport> Sources);
public sealed record TopicOutcomeImport(string TopicSourceId, IReadOnlyList<string> OutcomeCodes, string Evidence);
public sealed record OutcomeImportApproval(OutcomeImportPackage Package, string Fingerprint, string Reason);
public sealed record OutcomeImportReview(string Fingerprint, int OutcomeCount, int MatchedTopics, IReadOnlyList<string> UnmatchedTopics, IReadOnlyList<string> Warnings);
public sealed record OutcomeEstimateRequest(int LearningMinutes, int PracticeMinutes, int CheckMinutes, string Evidence, string Confidence, string Reason,
    IReadOnlyList<OutcomeTopicShareRequest>? TopicShares = null);
public sealed record OutcomeTopicShareRequest(Guid TopicId, int LearningMinutes, int PracticeMinutes, int CheckMinutes, string ScopeDescription);
public sealed record OutcomeMappingReview(bool Approved, string Evidence);
public sealed record PublishedStudyOutcome(Guid OutcomeId, string Code, string Name, Guid EstimateId, int EstimateNumber,
    int LearningMinutes, int PracticeMinutes, int CheckMinutes, int TotalMinutes, string EditionCode, int OfficialLessonPeriods,
    bool IsTopicShare = false, string? ScopeDescription = null);
public sealed record OutcomeCatalogView(Coaching.Domain.Entities.OfficialCurriculumEdition Edition,
    IReadOnlyList<Coaching.Domain.Entities.OfficialCurriculumTopic> OfficialTopics, IReadOnlyList<Coaching.Domain.Entities.StudyCatalogOutcome> Outcomes,
    IReadOnlyList<Coaching.Domain.Entities.OutcomeDurationEstimate> Estimates, IReadOnlyList<Coaching.Domain.Entities.StudyCatalogTopicOutcome> Mappings,
    IReadOnlyList<OutcomeMappedTopic> Topics, IReadOnlyList<Coaching.Domain.Entities.StudyCatalogTopicOutcomeShare>? TopicShares = null);
public sealed record OutcomeMappedTopic(Guid Id, string Name);
