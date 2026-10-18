using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Coaching.Infrastructure.Catalogs;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingAutomaticStudyPlanPreviewService(CoachingDbContext db, ICoachingAccessPolicy access, ILearningSetupService? setup = null)
    : IAutomaticStudyPlanPreviewService
{
    public static void Validate(AutomaticStudyPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.StartDate == default || request.Days is < 1 or > 90
            || request.StartDate.DayNumber > DateOnly.MaxValue.DayNumber - request.Days + 1
            || request.ExpectedAvailabilityVersion < 0 || request.Topics is null
            || request.Topics.Count is < 1 or > 500
            || request.Topics.Any(x => x is null || x.TopicId == Guid.Empty || x.RequiredMinutes is < 1 or > 1440)
            || request.Topics.Select(x => x.TopicId).Distinct().Count() != request.Topics.Count)
            throw new ArgumentException("Geçerli tarih, konu ve çalışma süreleri seçin.");
        if (request.Topics.Any(t => t.Outcomes is not null && (t.RequiredMinutes.HasValue || t.Outcomes.Count is < 1 or > 100
            || t.Outcomes.Any(o => o is null || o.OutcomeId == Guid.Empty || o.EstimateId == Guid.Empty || o.RequiredMinutes is < 1 or > 1440)
            || t.Outcomes.Select(o => o.OutcomeId).Distinct().Count() != t.Outcomes.Count))
            || request.Topics.Sum(t => t.Outcomes?.Count ?? 0) > 500)
            throw new ArgumentException("Konu süresi veya en fazla 500 sürümlü kazanım seçin; ikisini aynı anda göndermeyin.");
    }

    public async Task<AutomaticStudyPreview> PreviewAsync(AutomaticStudyPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (access.CurrentUserId is not { } studentId || !access.IsCurrentStudent(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Yalnız kendi öğrenci hesabınızla kullanabilirsiniz.");
        Validate(request);
        if (request.ExpectedLearningContextVersion.HasValue)
        {
            var learning = await (setup ?? throw new InvalidOperationException("Learning setup service is required.")).GetAsync(cancellationToken);
            if (!learning.IsReady || learning.Context?.Version != request.ExpectedLearningContextVersion)
                throw new BusinessRuleException("StudyPlanning.Conflict", "Eğitim kurulumu eksik veya değişmiş. Kurulum bilgilerini yeniden yükleyin.");
            var lessons = learning.Context.CoursePreferences.Select(x => x.LessonId).ToArray();
            var selectedTopics = request.Topics.Select(x => x.TopicId).ToArray();
            if (await db.StudyCatalogTopics.CountAsync(x => Enumerable.Contains(selectedTopics, x.Id)
                && Enumerable.Contains(lessons, x.LessonId), cancellationToken) != selectedTopics.Length)
                throw new ArgumentException("Yalnız kurulumda seçtiğiniz derslerin konularını kullanabilirsiniz.");
        }
        var availability = await db.StudyAvailability.AsNoTracking()
            .SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken)
            ?? throw new KeyNotFoundException("Önce çalışma saatlerinizi belirleyin.");
        if (availability.Version != request.ExpectedAvailabilityVersion)
            throw new BusinessRuleException("StudyPlanning.Conflict", "Çalışma saatleri değişti. Yeniden yükleyin.");
        var ids = request.Topics.Select(x => x.TopicId).ToArray();
        var catalog = await (from topic in db.StudyCatalogTopics.AsNoTracking()
                             join unit in db.StudyCatalogUnits on topic.UnitId equals unit.Id
                             join lesson in db.StudyCatalogLessons on topic.LessonId equals lesson.Id
                             where Enumerable.Contains(ids, topic.Id) && topic.IsActive && unit.IsActive
                                 && lesson.IsActive && unit.LessonId == lesson.Id
                                 && !db.StudyCatalogTopics.Any(child => child.ParentId == topic.Id)
                                 && (topic.ParentId == null || db.StudyCatalogTopics.Any(parent => parent.Id == topic.ParentId
                                     && parent.IsActive && parent.UnitId == topic.UnitId && parent.LessonId == topic.LessonId))
                             select topic).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (catalog.Count != ids.Length)
            throw new ArgumentException("Seçilen konulardan biri artık kullanılamıyor.");
        var selectedOutcomes = new List<ResolvedStudyOutcome>();
        var academicYear = request.Topics.Any(x => x.Outcomes is not null)
            ? await CoachingOutcomeCatalogService.AcademicYearAsync(db, studentId, cancellationToken) : 0;
        foreach (var selection in request.Topics.Where(x => x.Outcomes is not null))
        {
            var published = await CoachingOutcomeCatalogService.ReadPublishedAsync(db, selection.TopicId, academicYear, cancellationToken);
            foreach (var chosen in selection.Outcomes!)
            {
                var outcome = published.SingleOrDefault(x => x.OutcomeId == chosen.OutcomeId && x.EstimateId == chosen.EstimateId)
                    ?? throw new ArgumentException("Kazanım eşleştirmesi veya süre sürümü değişti. Kazanımları yeniden yükleyin.");
                selectedOutcomes.Add(new(selection.TopicId, outcome.OutcomeId, outcome.EstimateId, chosen.RequiredMinutes ?? outcome.TotalMinutes,
                    chosen.RequiredMinutes.HasValue && chosen.RequiredMinutes != outcome.TotalMinutes ? "StudentOverride" : "Catalog", outcome.Code, outcome.Name, outcome.IsTopicShare));
            }
        }
        var resolved = StudyOutcomeAllocation.Deduplicate(selectedOutcomes);
        var topics = new List<AutomaticStudyTopic>();
        foreach (var selection in request.Topics)
        {
            var minutes = selection.Outcomes is not null ? resolved.Where(x => x.TopicId == selection.TopicId).Sum(x => x.Minutes)
                : selection.RequiredMinutes ?? catalog[selection.TopicId].EstimatedMinutes
                    ?? throw new ArgumentException("Süresi tanımlanmamış konu için çalışma süresi belirtin.");
            if (minutes > 1440) throw new ArgumentException("Bir konu için toplam süre 1440 dakikayı geçemez; kazanım seçimini azaltın.");
            if (minutes > 0) topics.Add(new(selection.TopicId, minutes));
        }
        var revision = await db.StudyPlanRevisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.StudentId == studentId && x.Status == StudyPlanStatus.Active, cancellationToken);
        var endDate = request.StartDate.AddDays(request.Days - 1);
        var protectedTasks = revision is null ? [] : await db.StudyPlanTasks.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.RevisionId == revision.Id
                && (x.IsPinned || x.IsCompleted) && x.PlannedDate >= request.StartDate && x.PlannedDate <= endDate)
            .OrderBy(x => x.PlannedDate).ThenBy(x => x.Id)
            .Select(x => new ProtectedStudyTask(x.Id, x.PlannedDate, x.Title, x.PlannedMinutes, x.IsPinned, x.IsCompleted))
            .ToArrayAsync(cancellationToken);
        var schedule = StudyPlanDraftScheduler.Generate(request.StartDate, request.Days, availability.Windows,
            topics, protectedTasks.Select(x => new StudyCapacityReservation(x.PlannedDate, x.PlannedMinutes)).ToArray());
        var pinnedCount = revision is null ? 0 : await db.StudyPlanTasks.CountAsync(x => x.StudentId == studentId
            && x.RevisionId == revision.Id && x.IsPinned && !x.IsCompleted, cancellationToken);
        if (pinnedCount + schedule.Tasks.Count > 500)
            throw new ArgumentException("Korunan sabit çalışmalar dahil en fazla 500 çalışma planlanabilir. Konu sayısını azaltın.");
        // Advisory preview only: saving later must revalidate both returned versions.
        return new(availability.Version, availability.TimeZoneId, revision?.Id, revision?.Version, protectedTasks, schedule, resolved);
    }
}
