using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

public enum StudyPlanStatus { Draft = 0, Active = 1, Archived = 2 }

public sealed class StudyPlanRevision : AggregateRoot
{
    public Guid StudentId { get; private set; }
    public Guid? CreatedByTeacherId { get; private set; }
    public Guid PlanId { get; private set; }
    public int RevisionNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public StudyPlanStatus Status { get; private set; }
    public bool IsActive { get; private set; }
    public bool WasPublished { get; private set; }
    public int? AutomaticAvailabilityVersion { get; private set; }
    public Guid? AutomaticSourceRevisionId { get; private set; }
    public int? AutomaticSourceRevisionVersion { get; private set; }
    public AutomaticOutcomeEvidence? AutomaticOutcomeEvidence { get; private set; }
    public WeeklyPlanSource? WeeklySource { get; private set; }
    // When the plan stopped being current. Its unfinished work from that day on belongs to the plan that replaced it.
    public DateTime? ArchivedAt { get; private set; }
    private StudyPlanRevision() { }

    public static StudyPlanRevision Create(Guid studentId, Guid planId, int revisionNumber, string title, Guid? createdByTeacherId = null)
    {
        if (studentId == Guid.Empty || planId == Guid.Empty) throw new ArgumentException("Plan ownership is required.");
        if (revisionNumber < 1) throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        if (createdByTeacherId == Guid.Empty) throw new ArgumentException("Öğretmen kimliği geçersiz.");
        return new StudyPlanRevision { StudentId = studentId, PlanId = planId, CreatedByTeacherId = createdByTeacherId,
            RevisionNumber = revisionNumber, Title = StudyCatalogText.Require(title, 200) };
    }

    public void Activate()
    {
        if (Status == StudyPlanStatus.Archived) throw new InvalidOperationException("Archived revisions cannot be activated.");
        Status = StudyPlanStatus.Active;
        IsActive = true;
        WasPublished = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAutomaticSource(int availabilityVersion, Guid? revisionId, int? revisionVersion)
    {
        if (Status != StudyPlanStatus.Draft || availabilityVersion < 0 || revisionId == Guid.Empty
            || revisionVersion < 0 || revisionId.HasValue != revisionVersion.HasValue)
            throw new ArgumentException("Valid automatic plan source is required.");
        AutomaticAvailabilityVersion = availabilityVersion;
        AutomaticSourceRevisionId = revisionId;
        AutomaticSourceRevisionVersion = revisionVersion;
    }

    public void SetWeeklySource(WeeklyPlanSource source)
    {
        if(Status!=StudyPlanStatus.Draft||source.DraftVersion<0||source.EndDate<source.StartDate||string.IsNullOrEmpty(source.Fingerprint))
            throw new ArgumentException("Geçerli haftalık plan kaynağı gereklidir.");
        WeeklySource=source;
    }

    public void SetAutomaticOutcomeEvidence(Guid[] outcomeIds,string fingerprint)
    {
        if(Status!=StudyPlanStatus.Draft||AutomaticAvailabilityVersion is null||outcomeIds.Any(x=>x==Guid.Empty)||string.IsNullOrWhiteSpace(fingerprint))
            throw new ArgumentException("Geçerli otomatik plan kazanım kanıtı gereklidir.");
        AutomaticOutcomeEvidence=new(outcomeIds.Distinct().Order().ToArray(),fingerprint);
    }

    public void RenameDraft(string title)
    {
        if (Status != StudyPlanStatus.Draft) throw new InvalidOperationException("Only drafts can be renamed.");
        Title = StudyCatalogText.Require(title, 200);
        UpdatedAt = DateTime.UtcNow;
    }

    public void CorrectTitle(string title)
    {
        if (Status == StudyPlanStatus.Archived) throw new InvalidOperationException("Archived revisions cannot be corrected.");
        Title = StudyCatalogText.Require(title, 200);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = StudyPlanStatus.Archived;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        ArchivedAt = UpdatedAt;
    }

    public void RecordRealizationChange() { UpdatedAt = DateTime.UtcNow; }

    public void RecordTaskChange()
    {
        if (Status != StudyPlanStatus.Active) throw new InvalidOperationException("Only active plan tasks can be changed.");
        UpdatedAt = DateTime.UtcNow;
    }

    public void ExtendDeadline(DateOnly date)
    {
        if (Status != StudyPlanStatus.Active || WeeklySource is null || date <= WeeklySource.EndDate)
            throw new ArgumentException("Yalnız aktif yıllık planın bitişi uzatılabilir.");
        WeeklySource = WeeklySource with { EndDate = date };
        UpdatedAt = DateTime.UtcNow;
    }

    public void RestoreArchived()
    {
        if (Status != StudyPlanStatus.Archived) throw new InvalidOperationException("Only archived plans can be restored.");
        Status = StudyPlanStatus.Active;
        IsActive = true;
        WasPublished = true;
        UpdatedAt = DateTime.UtcNow;
        ArchivedAt = null;
    }

    public void RecordScheduleChange()
    {
        if (Status == StudyPlanStatus.Archived) throw new InvalidOperationException("Archived plans cannot be changed.");
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class StudyPlanTask : AggregateRoot
{
    public Guid RevisionId { get; private set; }
    public Guid? OriginalTaskId { get; private set; }
    public Guid? PredecessorTaskId { get; private set; }
    public int? PredecessorVersion { get; private set; }
    public int? RemainingMinutes { get; private set; }
    public int? RemainingQuestions { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid? TopicId { get; private set; }
    public DateOnly PlannedDate { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int PlannedMinutes { get; private set; }
    public int? ActualMinutes { get; private set; }
    public string RealizationHistoryJson { get; private set; } = "[]";
    public IReadOnlyList<StudyRealizationChange> RealizationHistory => System.Text.Json.JsonSerializer.Deserialize<StudyRealizationChange[]>(RealizationHistoryJson)!;
    public string? ActualWorkJson { get; private set; }
    public DateOnly? WorkFromDate { get; private set; }
    public DateOnly? WorkToDate { get; private set; }
    public IReadOnlyList<StudyWorkDay> WorkDays => ActualWorkJson is not null
        ? System.Text.Json.JsonSerializer.Deserialize<StudyWorkDay[]>(ActualWorkJson)!
        : ActualMinutes is {} minutes ? [new(WorkFromDate ?? PlannedDate,minutes,ActualQuestions,true)] : [];
    public string? SourceName { get; private set; }
    public int? PlannedQuestions { get; private set; }
    public int? ActualQuestions { get; private set; }
    public bool IsPinned { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    // Completion can be established by another published task in the same work chain.
    // Its effort stays on that task; it must never be copied into this task's work days.
    public Guid? CompletionSourceTaskId { get; private set; }
    public int? PlannedStartMinute { get; private set; }
    public Guid? WeeklyLessonId { get; private set; }
    public string? LessonNameSnapshot { get; private set; }
    // Missed work the student or teacher decided not to do: it stays in history but is no longer overdue.
    // Moving it to another day or recording work on it reopens it.
    public DateTime? SkippedAt { get; private set; }
    // Roadmap O7: what kind of work the plan engine chose (Learn, Review, Refresh, Spaced) and why.
    public string? PlanKind { get; private set; }
    public string? PlanReason { get; private set; }
    public static readonly IReadOnlySet<string> PlanKinds = new HashSet<string>(StringComparer.Ordinal) { "Learn", "Review", "Refresh", "Spaced" };
    private StudyPlanTask() { }

    public void SetPlanDecision(string kind, string? reason)
    {
        if (!PlanKinds.Contains(kind)) throw new ArgumentException("Geçersiz plan görevi türü.", nameof(kind));
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason is { Length: > 300 }) reason = reason[..300];
        PlanKind = kind; PlanReason = reason;
    }

    public static StudyPlanTask Create(StudyPlanRevision revision, DateOnly plannedDate, string title,
        int plannedMinutes, Guid? topicId = null, bool isPinned = false, string? sourceName = null, int? plannedQuestions = null)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.Status == StudyPlanStatus.Archived) throw new InvalidOperationException("Cannot add tasks to archived revisions.");
        if (plannedMinutes is < 1 or > 1440) throw new ArgumentOutOfRangeException(nameof(plannedMinutes));
        if (topicId == Guid.Empty || plannedDate == default) throw new ArgumentException("Valid task date and topic are required.");
        if (plannedQuestions is < 0 or > 100000) throw new ArgumentOutOfRangeException(nameof(plannedQuestions));
        var source = string.IsNullOrWhiteSpace(sourceName) ? null : StudyCatalogText.Require(sourceName, 300);
        return new StudyPlanTask { RevisionId = revision.Id, StudentId = revision.StudentId,
            PlannedDate = plannedDate, Title = StudyCatalogText.Require(title, 200), PlannedMinutes = plannedMinutes,
            TopicId = topicId, IsPinned = isPinned, SourceName = source, PlannedQuestions = plannedQuestions };
    }

    // Copies retain the root identity without changing or deleting the source record.
    public void CarryFrom(StudyPlanTask source)
    {
        if (source.StudentId != StudentId || source.IsCompleted)
            throw new ArgumentException("Yalnız aynı öğrencinin bekleyen çalışması devredilebilir.");
        OriginalTaskId = source.OriginalTaskId ?? source.Id;
        PredecessorTaskId = source.Id; PredecessorVersion = source.Version;
    }

    public void DeclareRemainingScope(int minutes, int? questions, Guid actor)
    {
        if(IsCompleted || minutes is <1 or >1440 || questions is <0 or >100000 || actor==Guid.Empty)
            throw new ArgumentException("Tamamlanmamış çalışma için kalan süre ve soru sayısını kontrol edin.");
        if(RemainingMinutes==minutes && RemainingQuestions==questions)return;
        var at=DateTime.UtcNow;
        RealizationHistoryJson=System.Text.Json.JsonSerializer.Serialize(RealizationHistory.Append(new StudyRealizationChange(
            at,actor,"RemainingScope",ActualMinutes,ActualQuestions,IsCompleted,CompletedAt,ActualMinutes ?? 0,ActualQuestions,false,
            "Yeni plana devredilecek kalan kapsam belirtildi.",RemainingMinutes,minutes,RemainingQuestions,questions)));
        RemainingMinutes=minutes;RemainingQuestions=questions;UpdatedAt=at;
    }

    public void ReconcileProgress(StudyPlanTask source, Guid actor, string reason, int? remainingMinutes = null, int? remainingQuestions = null)
    {
        if(source.StudentId!=StudentId || (source.OriginalTaskId??source.Id)!=(OriginalTaskId??Id) || actor==Guid.Empty || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("İlerleme yalnız aynı öğrencinin aynı çalışma zincirinden uzlaştırılabilir.");
        if(source.Id==Id)return;
        if(!source.IsCompleted&&source.ActualMinutes.HasValue&&!source.RemainingMinutes.HasValue&&!remainingMinutes.HasValue)
            throw new ArgumentException("Kısmi çalışmanın kalan kapsamı gerçekleşen süreden tahmin edilemez; güncel kalan süreyi belirtin.");
        int? minutes=source.IsCompleted?null:remainingMinutes??source.RemainingMinutes??source.PlannedMinutes;
        var questions=source.IsCompleted?null:remainingMinutes.HasValue?remainingQuestions:source.RemainingMinutes.HasValue?source.RemainingQuestions:source.PlannedQuestions;
        if(minutes is <1 or >1440 || questions is <0 or >100000)throw new ArgumentException("Güncel kalan kapsamı kontrol edin.");
        var completionSource=source.IsCompleted?source.CompletionSourceTaskId??source.Id:(Guid?)null;
        var completedAt=source.IsCompleted?source.CompletedAt:null;
        if(IsCompleted==source.IsCompleted && CompletedAt==completedAt && CompletionSourceTaskId==completionSource && RemainingMinutes==minutes && RemainingQuestions==questions)return;
        var at=DateTime.UtcNow;
        RealizationHistoryJson=System.Text.Json.JsonSerializer.Serialize(RealizationHistory.Append(new StudyRealizationChange(
            at,actor,"ContinuationReconciliation",ActualMinutes,ActualQuestions,IsCompleted,CompletedAt,ActualMinutes??0,ActualQuestions,source.IsCompleted,
            reason,RemainingMinutes,minutes,RemainingQuestions,questions,PreviousWorkDays:WorkDays,WorkDays:WorkDays,
            SourceTaskId:source.Id,SourceTaskVersion:source.Version)));
        RemainingMinutes=minutes;RemainingQuestions=questions;IsCompleted=source.IsCompleted;CompletedAt=completedAt;
        CompletionSourceTaskId=completionSource;UpdatedAt=at;
    }

    // Values are cumulative totals for this task, never an increment that can be applied twice.
    public void RecordRealization(int minutes, int? questions, bool completed, Guid actor, string? reason = null, int? remainingMinutes = null, int? remainingQuestions = null, DateOnly? workedOn = null, DateOnly? reassignFromDate = null)
    {
        reason = reason?.Trim();
        if (minutes is < 0 or > 1440 || questions is < 0 or > 100000 || actor == Guid.Empty
            || minutes == 0 && (reason is null || completed || questions is > 0))
            throw new ArgumentException("Gerçek süre ve soru sayısını kontrol edin. Hatalı kaydı geri almak için gerekçeyle 0 dakika ve tamamlanmadı durumunu kullanın.");
        if (remainingMinutes is < 1 or > 1440 || remainingQuestions is < 0 or > 100000
            || remainingQuestions.HasValue && !remainingMinutes.HasValue)
            throw new ArgumentException("Kalan çalışma süresi 1–1440 dakika; kalan soru sayısı 0–100000 olmalıdır.");
        var nextRemaining = completed || minutes == 0 ? null : remainingMinutes ?? RemainingMinutes;
        var nextQuestions = completed || minutes == 0 ? null : remainingQuestions ?? RemainingQuestions;
        if (minutes == 0) questions = null;
        if (reason is not null && (reason.Length < 5 || reason.Length > 1000))
            throw new ArgumentException("Düzeltme gerekçesi 5–1000 karakter olmalıdır.");
        if (reason is null && (IsCompleted || minutes < (ActualMinutes ?? 0)
            || ActualQuestions.HasValue && (!questions.HasValue || questions < ActualQuestions)))
            throw new ArgumentException("Kayıtlı gerçekleşmeyi azaltmak veya tamamlanan çalışmayı değiştirmek için gerekçe girin.");
        int? recordedMinutes = minutes == 0 ? null : minutes;
        if (ActualMinutes == recordedMinutes && ActualQuestions == questions && IsCompleted == completed
            && RemainingMinutes == nextRemaining && RemainingQuestions == nextQuestions && reassignFromDate is null) return;
        var at = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(at,TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")));
        var previousDays = WorkDays.ToArray();
        var day = workedOn ?? (reason is not null && previousDays.Length > 0 ? previousDays.Max(x=>x.WorkedOn) : today);
        if(day == default || day > today) throw new ArgumentException("Fiili çalışma tarihi bugün veya geçmişte olmalıdır.");
        var days = previousDays.ToList();
        if(reassignFromDate.HasValue) {
            if(reason is null || !workedOn.HasValue || reassignFromDate==workedOn || minutes!=ActualMinutes || questions!=ActualQuestions)
                throw new ArgumentException("Tarih düzeltmesi için gerekçe, kaynak gün ve yeni fiili tarih gereklidir; miktarlar aynı kalmalıdır.");
            var source=days.SingleOrDefault(x=>x.WorkedOn==reassignFromDate) ?? throw new ArgumentException("Kaynak günde gerçekleşme kaydı yok.");
            var target=days.SingleOrDefault(x=>x.WorkedOn==day);
            days.RemoveAll(x=>x.WorkedOn==reassignFromDate || x.WorkedOn==day);
            days.Add(new(day,source.Minutes+(target?.Minutes??0),source.Questions.HasValue||target?.Questions is not null ? (source.Questions??0)+(target?.Questions??0) : null,target?.IsLegacyDate??false));
        }

        if(minutes == 0) days.Clear();
        else if(reassignFromDate is null) {
            var previous = days.SingleOrDefault(x=>x.WorkedOn==day);
            var nextMinutes = (previous?.Minutes ?? 0) + minutes - (ActualMinutes ?? 0);
            int? nextQuestionsForDay = questions is null ? null : (previous?.Questions ?? 0) + questions.Value - (ActualQuestions ?? 0);
            if(nextMinutes < 0 || nextQuestionsForDay < 0)
                throw new ArgumentException("Düzeltilecek miktar seçilen gündeki kaydı aşıyor. Gerçekleşme günlerini kontrol edip ilgili günü seçin.");
            if(questions is null) days = days.Select(x=>x with { Questions=null }).ToList();
            days.RemoveAll(x=>x.WorkedOn==day);
            if(nextMinutes > 0 || nextQuestionsForDay > 0 || previous is not null && nextQuestionsForDay.HasValue) days.Add(new(day,nextMinutes,nextQuestionsForDay,previous?.IsLegacyDate ?? false));
        }
        days = days.OrderBy(x=>x.WorkedOn).ToList();
        var changes = RealizationHistory.ToList();
        changes.Add(new(at, actor, reason is null ? (completed ? "Completion" : "Progress") : "Correction",
            ActualMinutes, ActualQuestions, IsCompleted, CompletedAt, minutes, questions, completed, reason, RemainingMinutes, nextRemaining, RemainingQuestions, nextQuestions,day,previousDays,days,reassignFromDate));
        RealizationHistoryJson = System.Text.Json.JsonSerializer.Serialize(changes);
        ActualWorkJson = System.Text.Json.JsonSerializer.Serialize(days);
        WorkFromDate = days.Count == 0 ? null : days.Min(x=>x.WorkedOn);
        WorkToDate = days.Count == 0 ? null : days.Max(x=>x.WorkedOn);
        RemainingMinutes = nextRemaining; RemainingQuestions = nextQuestions;
        ActualMinutes = recordedMinutes; ActualQuestions = questions;
        CompletedAt = completed ? (IsCompleted ? CompletedAt : at) : null;
        IsCompleted = completed; UpdatedAt = at; SkippedAt = null;
        if(!completed)CompletionSourceTaskId=null;
    }

    public void Complete(int actualMinutes, int? actualQuestions = null, DateOnly? workedOn = null)
    {
        if (actualMinutes is < 1 or > 1440) throw new ArgumentOutOfRangeException(nameof(actualMinutes));
        if (actualQuestions is < 0 or > 100000) throw new ArgumentOutOfRangeException(nameof(actualQuestions));
        if (IsCompleted)
        {
            if (ActualMinutes != actualMinutes || ActualQuestions != actualQuestions) throw new InvalidOperationException("Completed work cannot be silently overwritten.");
            return;
        }
        RecordRealization(actualMinutes, actualQuestions, true, StudentId, workedOn:workedOn);
    }

    private void FreezeLegacyWorkDate()
    {
        if(ActualWorkJson is null && ActualMinutes.HasValue && WorkFromDate is null)
            WorkFromDate = WorkToDate = PlannedDate;
    }

    public void SetWeeklyPlacement(Guid lesson,string lessonName,int startMinute)
    {
        if(lesson==Guid.Empty||startMinute is <0 or >1439||startMinute+PlannedMinutes>1440)
            throw new ArgumentException("Dersin başlangıcı ve süresi günün içinde olmalıdır.");
        WeeklyLessonId=lesson;LessonNameSnapshot=StudyCatalogText.Require(lessonName,300);PlannedStartMinute=startMinute;
    }

    public void Reschedule(DateOnly plannedDate)
    {
        if (IsCompleted) throw new InvalidOperationException("Completed work cannot be rescheduled.");
        if (plannedDate == default) throw new ArgumentException("Valid task date is required.");
        FreezeLegacyWorkDate();
        PlannedDate = plannedDate;
        SkippedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Skip()
    {
        if (IsCompleted) throw new InvalidOperationException("Completed work cannot be skipped.");
        SkippedAt ??= DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Schedule(DateOnly plannedDate, int? startMinute)
    {
        if (IsCompleted) throw new InvalidOperationException("Completed work cannot be rescheduled.");
        if (plannedDate == default || startMinute is <0 or >1439 || startMinute+PlannedMinutes>1440)
            throw new ArgumentException("Çalışmanın tarihi ve saat aralığı geçerli olmalıdır.");
        FreezeLegacyWorkDate();
        PlannedDate=plannedDate; PlannedStartMinute=startMinute; SkippedAt=null; UpdatedAt=DateTime.UtcNow;
    }

    public void CorrectPlanning(string title, DateOnly plannedDate, int plannedMinutes)
    {
        if (IsCompleted) throw new InvalidOperationException("Completed work cannot be corrected.");
        var validatedTitle = StudyCatalogText.Require(title, 200);
        if (plannedDate == default || plannedMinutes is < 1 or > 1440 || PlannedStartMinute+plannedMinutes>1440) throw new ArgumentException("Valid planning date and minutes are required.");
        FreezeLegacyWorkDate();
        Title = validatedTitle; PlannedDate = plannedDate; PlannedMinutes = plannedMinutes;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record WeeklyPlanSource(int DraftVersion,int? LearningContextVersion,DateOnly StartDate,DateOnly EndDate,bool ReuseFreedSlots,
    string Fingerprint,Guid? ActiveRevisionId,int? ActiveRevisionVersion,string TimeZoneId="Europe/Istanbul");
public sealed record AutomaticOutcomeEvidence(Guid[] OutcomeIds,string Fingerprint);

public sealed record StudyRealizationChange(DateTime At, Guid ActorUserId, string Action,
    int? PreviousMinutes, int? PreviousQuestions, bool PreviouslyCompleted, DateTime? PreviousCompletedAt,
    int Minutes, int? Questions, bool Completed, string? Reason, int? PreviousRemainingMinutes = null, int? RemainingMinutes = null,
    int? PreviousRemainingQuestions = null, int? RemainingQuestions = null, DateOnly? WorkedOn = null,
    IReadOnlyList<StudyWorkDay>? PreviousWorkDays = null, IReadOnlyList<StudyWorkDay>? WorkDays = null, DateOnly? ReassignFromDate = null,
    Guid? SourceTaskId = null, int? SourceTaskVersion = null);
public sealed record StudyWorkDay(DateOnly WorkedOn, int Minutes, int? Questions, bool IsLegacyDate = false);
