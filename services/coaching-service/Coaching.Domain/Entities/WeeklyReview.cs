using System.Text.Json;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

public sealed class WeeklyReflection : AggregateRoot
{
    public Guid StudentId { get; private set; }
    public DateOnly WeekStart { get; private set; }
    public string Successes { get; private set; } = "";
    public string Obstacles { get; private set; } = "";
    public string NextFocus { get; private set; } = "";
    public int? Rating { get; private set; }
    public string? ResultsJson { get; private set; }
    public string ReflectionHistoryJson { get; private set; } = "[]";
    public IReadOnlyList<WeeklyReflectionChange> ReflectionHistory => JsonSerializer.Deserialize<WeeklyReflectionChange[]>(ReflectionHistoryJson)!;
    private WeeklyReflection() { }
    public static WeeklyReflection Create(Guid student, DateOnly week) => new() { StudentId = student, WeekStart = week };
    public void Edit(string successes, string obstacles, string nextFocus, int? rating)
    {
        if (Successes.Length > 0 || NextFocus.Length > 0)
            ReflectionHistoryJson = JsonSerializer.Serialize(ReflectionHistory.Append(new WeeklyReflectionChange(
                Version, UpdatedAt ?? CreatedAt, Successes, Obstacles, NextFocus, Rating, ResultsJson)));
        Successes = successes; Obstacles = obstacles; NextFocus = nextFocus; Rating = rating; UpdatedAt = DateTime.UtcNow;
    }
    public void EditWithResults(string successes, string obstacles, string nextFocus, int? rating, string resultsJson)
    {
        Edit(successes, obstacles, nextFocus, rating);
        ResultsJson = resultsJson;
    }
}
public sealed record WeeklyReflectionChange(int Version, DateTime SavedAt, string Successes, string Obstacles,
    string NextFocus, int? Rating, string? ResultsJson);

public sealed record WeeklyDecisionChange(DateTime At, string Title, string Note, DateOnly DueOn, string Status,
    Guid? StudyTaskId = null, Guid? AssignmentId = null, string? Action = null, Guid? ActorUserId = null, Guid? PreviousTeacherId = null,
    Guid? ResponsibleTeacherId = null, DateOnly? PreviousDueOn = null, string? Reason = null, Guid? PreviousStudyTaskId = null);

public sealed class WeeklyDecision : AggregateRoot
{
    public Guid StudentId { get; private set; }
    public Guid TeacherId { get; private set; }
    public Guid ResponsibleTeacherId { get; private set; }
    public Guid? InstitutionId { get; private set; }
    public Guid? SourceReflectionId { get; private set; }
    public int? SourceReflectionVersion { get; private set; }
    public string? SourceReflectionSnapshotJson { get; private set; }
    public Guid? PreviousDecisionId { get; private set; }
    public string? PreviousDecisionSnapshotJson { get; private set; }
    public bool HasAssignmentScope { get; private set; } = true;
    public DateOnly InitialDueOn { get; private set; }
    public DateOnly ReviewWeekStart { get; private set; }
    public string Title { get; private set; } = "";
    public string Note { get; private set; } = "";
    public DateOnly DueOn { get; private set; }
    public string Status { get; private set; } = "Planned";
    public DateTime? CompletedAt { get; private set; }
    public Guid? StudyTaskId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public string HistoryJson { get; private set; } = "[]";
    public IReadOnlyList<WeeklyDecisionChange> History => JsonSerializer.Deserialize<WeeklyDecisionChange[]>(HistoryJson)!;
    private WeeklyDecision() { }
    public static WeeklyDecision Create(Guid id, Guid student, Guid teacher, DateOnly week, Guid? institutionId = null) =>
        new() { Id = id, StudentId = student, TeacherId = teacher, ResponsibleTeacherId = teacher, InstitutionId = institutionId, ReviewWeekStart = week };
    public void CaptureOrigin(Guid reflectionId,int version,string snapshot,Guid? previousDecisionId,string? previousSnapshot)
    {
        if(SourceReflectionId.HasValue || History.Count>0 || reflectionId==Guid.Empty || version<0 || string.IsNullOrWhiteSpace(snapshot)
            || previousDecisionId==Guid.Empty || previousDecisionId.HasValue != (previousSnapshot is not null))
            throw new ArgumentException("Kararın değerlendirme kaynağı yalnız oluşturulurken kaydedilebilir.");
        SourceReflectionId=reflectionId;SourceReflectionVersion=version;SourceReflectionSnapshotJson=snapshot;
        PreviousDecisionId=previousDecisionId;PreviousDecisionSnapshotJson=previousSnapshot;
    }
    public void Edit(string title, string note, DateOnly dueOn, string status, DateTime now)
    {
        if (History.Count > 0 && Matches(title, note, dueOn, status)) return;
        if (InitialDueOn == default) InitialDueOn = dueOn;
        Title = title; Note = note; DueOn = dueOn; Status = status;
        CompletedAt = status == "Completed" ? CompletedAt ?? now : null;
        RecordChange(now);
        UpdatedAt = now;
    }
    public bool IsOpen => Status is "Planned" or "InProgress";
    public void TransferResponsibility(Guid teacher, Guid actor, DateTime now)
    {
        if (!IsOpen || teacher == ResponsibleTeacherId) return;
        if (teacher == Guid.Empty || actor == Guid.Empty) throw new ArgumentException("Geçerli sorumlu ve işlem yapan kişi gereklidir.");
        var previous = ResponsibleTeacherId; ResponsibleTeacherId = teacher; UpdatedAt = now;
        Append(new(now,Title,Note,DueOn,Status,StudyTaskId,AssignmentId,"ResponsibilityTransfer",actor,previous,teacher,
            Reason:"Öğretmen ataması değişti."));
    }
    public void Postpone(DateOnly dueOn, string reason, Guid actor, DateOnly today, DateTime now)
    {
        reason = reason?.Trim() ?? "";
        if (!IsOpen || DueOn >= today || dueOn <= today || dueOn <= DueOn || reason.Length is < 5 or > 1000 || actor != ResponsibleTeacherId)
            throw new ArgumentException("Yalnız gecikmiş açık kararlar, gerekçeyle gelecekteki bir tarihe ertelenebilir.");
        var previous = DueOn; DueOn = dueOn; UpdatedAt = now;
        Append(new(now,Title,Note,DueOn,Status,StudyTaskId,AssignmentId,"Postponement",actor,
            ResponsibleTeacherId:ResponsibleTeacherId,PreviousDueOn:previous,Reason:reason));
    }
    public void ChangeDeadline(DateOnly dueOn,string reason,Guid actor,DateOnly today,DateTime now)
    {
        if(DueOn<today){Postpone(dueOn,reason,actor,today,now);return;}
        reason=reason?.Trim()??"";
        if(!IsOpen||dueOn<=today||dueOn<ReviewWeekStart.AddDays(7)||reason.Length is <5 or >1000||actor!=ResponsibleTeacherId)
            throw new ArgumentException("Açık kararın yeni son tarihi gelecekte ve kaynak haftadan sonra olmalı; değişiklik için 5–1000 karakterlik gerekçe girin.");
        if(dueOn==DueOn)return;
        var previous=DueOn;DueOn=dueOn;UpdatedAt=now;
        Append(new(now,Title,Note,DueOn,Status,StudyTaskId,AssignmentId,"DeadlineChange",actor,
            ResponsibleTeacherId:ResponsibleTeacherId,PreviousDueOn:previous,Reason:reason));
    }
    private void Append(WeeklyDecisionChange change) => HistoryJson = JsonSerializer.Serialize(History.Append(change));

    public void LinkWork(Guid? studyTaskId, Guid? assignmentId, DateTime now)
    {
        if (studyTaskId == Guid.Empty || assignmentId == Guid.Empty) throw new ArgumentException("Geçerli iş kimliği gereklidir.");
        if (StudyTaskId == studyTaskId && AssignmentId == assignmentId) return;
        StudyTaskId = studyTaskId; AssignmentId = assignmentId; UpdatedAt = now; RecordChange(now,"WorkLinks");
    }
    public void ContinueStudyWork(Guid task, Guid actor, DateTime now)
    {
        if (!IsOpen || StudyTaskId is null || StudyTaskId == task) return;
        if (task == Guid.Empty || actor == Guid.Empty) throw new ArgumentException("Devam çalışması ve işlem yapan kişi gereklidir.");
        var previous = StudyTaskId; StudyTaskId = task; UpdatedAt = now;
        Append(new(now,Title,Note,DueOn,Status,StudyTaskId,AssignmentId,"WorkContinuation",actor,
            Reason:"Plan yenilendi; çalışma bağlantısı devam görevine aktarıldı.",PreviousStudyTaskId:previous));
    }
    public void RecordWorkChange(DateTime now) { UpdatedAt = now; RecordChange(now,"Calendar"); }
    private void RecordChange(DateTime now, string action="Decision") => HistoryJson = JsonSerializer.Serialize(History.Append(
        new WeeklyDecisionChange(now, Title, Note, DueOn, Status, StudyTaskId, AssignmentId, action)));
    public bool Matches(string title, string note, DateOnly dueOn, string status) => Title == title && Note == note && DueOn == dueOn && Status == status;
}

// Each acknowledgement retains the exact reflection that the teacher reviewed.
public sealed class WeeklyReflectionReview : AggregateRoot
{
    public Guid ReflectionId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid TeacherId { get; private set; }
    public int ReflectionVersion { get; private set; }
    public string Note { get; private set; } = "";
    public string ReflectionSnapshotJson { get; private set; } = "";
    public string? EvidenceFingerprint { get; private set; }
    public string? EvidenceSnapshotJson { get; private set; }
    private WeeklyReflectionReview() { }
    public static WeeklyReflectionReview Create(WeeklyReflection reflection, Guid teacher, string note, string? evidenceFingerprint = null, string? evidenceSnapshotJson = null)
    {
        note = note.Trim();
        if (teacher == Guid.Empty || note.Length is < 5 or > 2000) throw new ArgumentException("İnceleme notu 5–2000 karakter olmalıdır.");
        return new() { ReflectionId = reflection.Id, StudentId = reflection.StudentId, TeacherId = teacher,
            ReflectionVersion = reflection.Version, Note = note, EvidenceFingerprint=evidenceFingerprint,EvidenceSnapshotJson=evidenceSnapshotJson,ReflectionSnapshotJson = JsonSerializer.Serialize(new {
                reflection.Successes, reflection.Obstacles, reflection.NextFocus, reflection.Rating, reflection.ResultsJson }) };
    }
}
