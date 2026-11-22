using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

/// <summary>
/// KocApp uyumu (22.11.2026): the student's books and sets ("Kaynaklarım"). The student keeps her own; her teacher adds and edits
/// them for her; an institution administrator in scope and a linked parent read them. Progress is what was done before the
/// resource was added plus the ranges of completed study tasks; "planned" counts the ranges of the active plan's open tasks.
/// </summary>
public sealed class CoachingStudentResourceService(CoachingDbContext db, ICoachingAccessPolicy access, CoachingStudyPlanAccess staff,
    TimeProvider clock)
{
    public const int MaxResourcesPerStudent = 100;

    // ---- Who ------------------------------------------------------------------------------------------------------

    /// <summary>The signed-in student herself; for her own list and changes.</summary>
    public Guid RequireStudent()
    {
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student))
            throw new BusinessRuleException("Authorization.Forbidden", "Öğrenci çalışma alanına erişiminiz yok.");
        return student;
    }

    /// <summary>A teacher of the student, an institution administrator in scope or a system administrator: read.</summary>
    public Task<Guid> RequireStaffReadAsync(Guid studentId, CancellationToken ct) => staff.RequireAsync(studentId, ct);

    /// <summary>Only the student's teacher writes for her.</summary>
    public Task<Guid> RequireTeacherWriteAsync(Guid studentId, CancellationToken ct) => staff.RequireTeacherWriteAsync(studentId, ct);

    // ---- Read -----------------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<StudentResourceView>> ListAsync(Guid student, bool includeArchived, CancellationToken ct)
    {
        var rows = await db.Set<StudentResource>().AsNoTracking().Where(x => x.StudentId == student && (includeArchived || !x.IsArchived))
            .OrderBy(x => x.IsArchived).ThenBy(x => x.LessonName).ThenBy(x => x.Name).ThenBy(x => x.Id).ToArrayAsync(ct);
        return await ViewsAsync(student, rows, ct);
    }

    // ---- Write ----------------------------------------------------------------------------------------------------

    public async Task<StudentResourceView> CreateAsync(Guid student, StudentResourceInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (await db.Set<StudentResource>().CountAsync(x => x.StudentId == student, ct) >= MaxResourcesPerStudent)
            throw Conflict($"Bir öğrencinin en çok {MaxResourcesPerStudent} kaynağı olabilir. Biten kaynaklar silinebilir ya da arşivlenebilir.");
        var actor = access.CurrentUserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum bulunamadı.");
        await RequireTopicsAsync(input, ct);
        var resource = StudentResource.Create(student, actor, Details(input), clock.GetUtcNow().UtcDateTime);
        db.Add(resource);
        await db.SaveChangesAsync(ct);
        return (await ViewsAsync(student, [resource], ct))[0];
    }

    public async Task<StudentResourceView> UpdateAsync(Guid student, Guid id, StudentResourceInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var resource = await OwnedAsync(student, id, input.ExpectedVersion, ct);
        await RequireTopicsAsync(input, ct);
        // A shorter resource may not cut through a range a task already points at.
        var furthest = await db.StudyPlanTasks.Where(x => x.StudentId == student && x.ResourceId == id).MaxAsync(x => x.ResourceTo, ct);
        if (furthest is { } last && input.TotalUnits < last)
            throw Conflict($"Plandaki bir çalışma bu kaynağın {last}. birimine kadar gidiyor; toplam bundan az olamaz.");
        resource.Update(Details(input), clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return (await ViewsAsync(student, [resource], ct))[0];
    }

    public async Task<StudentResourceView> SetArchivedAsync(Guid student, Guid id, StudentResourceArchiveInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var resource = await OwnedAsync(student, id, input.ExpectedVersion, ct);
        resource.SetArchived(input.IsArchived, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return (await ViewsAsync(student, [resource], ct))[0];
    }

    /// <summary>A resource no task points at is deleted; one that the plan uses is archived instead (its history stays readable).</summary>
    public async Task DeleteAsync(Guid student, Guid id, int expectedVersion, CancellationToken ct)
    {
        var resource = await OwnedAsync(student, id, expectedVersion, ct);
        if (await db.StudyPlanTasks.AnyAsync(x => x.StudentId == student && x.ResourceId == id, ct))
            throw Conflict("Bu kaynağa bağlı çalışmalar var; silinemez, arşivlenebilir.");
        db.Remove(resource);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// KocApp plan, phase 2 (the teacher's book list): copies a resource, its sections included, to other students the teacher
    /// teaches. A student who already has a resource of the same name is skipped; nothing is counted as done in a copy.
    /// </summary>
    public async Task<StudentResourceCopyResult> CopyAsync(Guid student, Guid id, StudentResourceCopyInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var targets = (input.StudentIds ?? []).Where(x => x != Guid.Empty && x != student).Distinct().ToArray();
        if (targets.Length is 0 or > 50) throw new ArgumentException("Kopyalamak için 1–50 öğrenci seçin.");
        var source = await db.Set<StudentResource>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.StudentId == student, ct)
            ?? throw new BusinessRuleException("StudyPlanning.NotFound", "Kaynak bulunamadı.");
        foreach (var target in targets) await staff.RequireTeacherWriteAsync(target, ct);
        var actor = access.CurrentUserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum bulunamadı.");
        var existing = await db.Set<StudentResource>().AsNoTracking().Where(x => Enumerable.Contains(targets, x.StudentId))
            .Select(x => new { x.StudentId, x.Name }).ToArrayAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var details = new StudentResourceDetails(source.Name, source.Publisher, source.LessonName, source.Kind, source.Unit, source.TotalUnits, 0, source.Sections);
        var copied = 0;
        foreach (var target in targets)
        {
            var own = existing.Where(x => x.StudentId == target).ToArray();
            if (own.Any(x => string.Equals(x.Name, source.Name, StringComparison.CurrentCultureIgnoreCase)) || own.Length >= MaxResourcesPerStudent) continue;
            db.Add(StudentResource.Create(target, actor, details, now));
            copied++;
        }
        await db.SaveChangesAsync(ct);
        return new StudentResourceCopyResult(copied, targets.Length - copied);
    }

    // ---- Views ----------------------------------------------------------------------------------------------------

    private async Task<StudentResourceView[]> ViewsAsync(Guid student, IReadOnlyList<StudentResource> resources, CancellationToken ct)
    {
        if (resources.Count == 0) return [];
        var ids = resources.Select(x => x.Id).ToArray();
        var active = await db.StudyPlanRevisions.AsNoTracking().Where(x => x.StudentId == student && x.Status == StudyPlanStatus.Active)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var ranges = await db.StudyPlanTasks.AsNoTracking()
            .Where(x => x.StudentId == student && x.ResourceId != null && Enumerable.Contains(ids, x.ResourceId!.Value))
            .Select(x => new TaskRange(x.ResourceId!.Value, x.ResourceFrom, x.ResourceTo, x.IsCompleted, x.SkippedAt != null, x.RevisionId == active))
            .ToArrayAsync(ct);
        var topicIds = resources.SelectMany(x => x.Sections).Where(x => x.TopicId.HasValue).Select(x => x.TopicId!.Value).Distinct().ToArray();
        var topics = topicIds.Length == 0 ? new Dictionary<Guid, string>()
            : await db.StudyCatalogTopics.AsNoTracking().Where(x => Enumerable.Contains(topicIds, x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return resources.Select(resource => View(resource, ranges.Where(x => x.ResourceId == resource.Id).ToArray(), topics)).ToArray();
    }

    internal sealed record TaskRange(Guid ResourceId, int? From, int? To, bool Completed, bool Skipped, bool InActivePlan);

    internal static StudentResourceView View(StudentResource resource, IReadOnlyList<TaskRange> ranges, IReadOnlyDictionary<Guid, string> topics)
    {
        var done = ranges.Where(x => x.Completed && x.From.HasValue).Select(x => (x.From!.Value, x.To!.Value)).ToArray();
        var planned = ranges.Where(x => !x.Completed && !x.Skipped && x.InActivePlan && x.From.HasValue).Select(x => (x.From!.Value, x.To!.Value)).ToArray();
        var doneUnits = StudentResource.Covered(resource.TotalUnits, resource.DoneBefore, done);
        // Planned units not already done.
        var plannedUnits = StudentResource.Covered(resource.TotalUnits, resource.DoneBefore, [.. done, .. planned]) - doneUnits;
        var sections = resource.Sections.Select(section => new StudentResourceSectionView(section.Title, section.FromUnit, section.ToUnit,
            section.TopicId, section.TopicId is { } topic ? topics.GetValueOrDefault(topic) : null,
            SectionDone(section, resource.DoneBefore, done))).ToArray();
        var percent = (int)Math.Floor(doneUnits * 100m / resource.TotalUnits);
        return new StudentResourceView(resource.Id, resource.Version, resource.Name, resource.Publisher, resource.LessonName, resource.Kind,
            resource.Unit, resource.TotalUnits, resource.DoneBefore, doneUnits, percent, plannedUnits, resource.IsArchived, ranges.Count == 0,
            resource.CreatedAt, resource.UpdatedAt, sections, resource.IsArchived ? null : Next(resource, done, planned));
    }

    private static int SectionDone(StudentResourceSection section, int doneBefore, IReadOnlyList<(int From, int To)> done)
    {
        var clipped = done.Select(x => (From: Math.Max(x.From, section.FromUnit), To: Math.Min(x.To, section.ToUnit))).Where(x => x.From <= x.To)
            .Select(x => (x.From - section.FromUnit + 1, x.To - section.FromUnit + 1));
        var before = Math.Clamp(doneBefore - section.FromUnit + 1, 0, section.ToUnit - section.FromUnit + 1);
        return StudentResource.Covered(section.ToUnit - section.FromUnit + 1, before, clipped);
    }

    /// <summary>
    /// KocApp plan, phase 2: the next part to plan. With sections: the first section with a unit neither done nor planned (the
    /// part from that unit to the section's end). Without sections: the first free units, a usual sitting long (10 pages, 2 tests
    /// or 40 questions), up to the next done or planned unit.
    /// </summary>
    internal static StudentResourceNext? Next(StudentResource resource, IReadOnlyList<(int From, int To)> done, IReadOnlyList<(int From, int To)> planned)
    {
        var taken = new bool[resource.TotalUnits + 1];
        for (var unit = 1; unit <= Math.Min(resource.DoneBefore, resource.TotalUnits); unit++) taken[unit] = true;
        foreach (var (from, to) in done.Concat(planned))
            for (var unit = Math.Max(1, from); unit <= Math.Min(resource.TotalUnits, to); unit++) taken[unit] = true;
        foreach (var section in resource.Sections)
        {
            var free = Enumerable.Range(section.FromUnit, section.ToUnit - section.FromUnit + 1).FirstOrDefault(unit => !taken[unit]);
            if (free == 0) continue;
            var end = free;
            while (end < section.ToUnit && !taken[end + 1]) end++;
            return new StudentResourceNext(free, end, section.Title, section.TopicId);
        }
        if (resource.Sections.Count > 0) return null;
        var first = Enumerable.Range(1, resource.TotalUnits).FirstOrDefault(unit => !taken[unit]);
        if (first == 0) return null;
        var sitting = resource.Unit switch { StudentResourceUnits.Test => 2, StudentResourceUnits.Question => 40, _ => 10 };
        var last = first;
        while (last < resource.TotalUnits && last - first + 1 < sitting && !taken[last + 1]) last++;
        return new StudentResourceNext(first, last, null, null);
    }

    // ---- Helpers --------------------------------------------------------------------------------------------------

    private async Task<StudentResource> OwnedAsync(Guid student, Guid id, int? expectedVersion, CancellationToken ct)
    {
        var resource = await db.Set<StudentResource>().SingleOrDefaultAsync(x => x.Id == id && x.StudentId == student, ct)
            ?? throw new BusinessRuleException("StudyPlanning.NotFound", "Kaynak bulunamadı.");
        if (expectedVersion is { } version && version != resource.Version)
            throw Conflict("Kaynak bu arada değişti. Sayfa yenilenip yeniden denenmeli.");
        return resource;
    }

    private async Task RequireTopicsAsync(StudentResourceInput input, CancellationToken ct)
    {
        var topics = (input.Sections ?? []).Where(x => x?.TopicId is not null).Select(x => x.TopicId!.Value).Distinct().ToArray();
        if (topics.Length > 0 && await db.StudyCatalogTopics.CountAsync(x => Enumerable.Contains(topics, x.Id) && x.IsActive, ct) != topics.Length)
            throw new ArgumentException("Bölümlerde seçilen konulardan biri kullanılamıyor.");
    }

    private static StudentResourceDetails Details(StudentResourceInput input) => new(input.Name, input.Publisher, input.LessonName, input.Kind,
        input.Unit, input.TotalUnits, input.DoneBefore,
        (input.Sections ?? []).Select(x => x is null ? throw new ArgumentException("Boş bölüm gönderilemez.")
            : new StudentResourceSection(x.Title, x.FromUnit, x.ToUnit, x.TopicId)).ToArray());

    private static BusinessRuleException Conflict(string message) => new("StudyPlanning.Conflict", message);
}

/// <summary>KocApp uyumu (22.11.2026): the resource name and range a plan view and the agenda show next to a task.</summary>
public static class StudentResourceNames
{
    public sealed record Name(string Title, string Unit);

    public static async Task<IReadOnlyDictionary<Guid, Name>> ForAsync(CoachingDbContext db, Guid student, IEnumerable<StudyPlanTask> tasks, CancellationToken ct)
    {
        var ids = tasks.Where(x => x.ResourceId.HasValue).Select(x => x.ResourceId!.Value).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, Name>();
        return await db.Set<StudentResource>().AsNoTracking().Where(x => x.StudentId == student && Enumerable.Contains(ids, x.Id))
            .ToDictionaryAsync(x => x.Id, x => new Name(x.Name, x.Unit), ct);
    }

    public static ManualStudyTaskView Attach(ManualStudyTaskView view, StudyPlanTask task, IReadOnlyDictionary<Guid, Name> names) =>
        view with
        {
            ResourceId = task.ResourceId, ResourceFrom = task.ResourceFrom, ResourceTo = task.ResourceTo,
            ResourceName = task.ResourceId is { } id ? names.GetValueOrDefault(id)?.Title : null,
            ResourceUnit = task.ResourceId is { } unitOf ? names.GetValueOrDefault(unitOf)?.Unit : null,
            AdviceKey = task.AdviceKey
        };

    /// <summary>"Limit Soru Bankası, test 12–15"; the name alone without a range.</summary>
    public static string Text(string name, string unit, int? from, int? to)
    {
        if (from is not { } first || to is not { } last) return name;
        var word = unit switch { StudentResourceUnits.Test => "test", StudentResourceUnits.Question => "soru", _ => "sayfa" };
        return first == last ? $"{name}, {word} {first}" : $"{name}, {word} {first}–{last}";
    }
}

/// <summary>
/// KocApp plan, phase 2: when a plan is made, a learning task of a topic that a section of one of the student's resources is tied
/// to gets the next free part of that section (a usual sitting: 10 pages, 2 tests or 40 questions). Done parts and parts given
/// to earlier tasks of the same plan are skipped; a task that already points at a resource keeps it.
/// </summary>
public static class StudentResourcePlanLinks
{
    public static int Sitting(string unit) => unit switch { StudentResourceUnits.Test => 2, StudentResourceUnits.Question => 40, _ => 10 };

    public static async Task LinkAsync(CoachingDbContext db, Guid student, IReadOnlyList<StudyPlanTask> tasks, CancellationToken ct)
    {
        var open = tasks.Where(x => x.TopicId.HasValue && x.ResourceId is null && x.StudentId == student).ToArray();
        if (open.Length == 0) return;
        var topics = open.Select(x => x.TopicId!.Value).ToHashSet();
        var resources = (await db.Set<StudentResource>().AsNoTracking().Where(x => x.StudentId == student && !x.IsArchived)
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct))
            .Where(x => x.Sections.Any(section => section.TopicId is { } topic && topics.Contains(topic))).ToArray();
        if (resources.Length == 0) return;
        var ids = resources.Select(x => x.Id).ToArray();
        var done = await db.StudyPlanTasks.AsNoTracking().Where(x => x.StudentId == student && x.IsCompleted && x.ResourceId != null
                && Enumerable.Contains(ids, x.ResourceId!.Value) && x.ResourceFrom != null)
            .Select(x => new { Id = x.ResourceId!.Value, From = x.ResourceFrom!.Value, To = x.ResourceTo!.Value }).ToArrayAsync(ct);
        var taken = resources.ToDictionary(x => x.Id, resource =>
        {
            var map = new bool[resource.TotalUnits + 1];
            for (var unit = 1; unit <= Math.Min(resource.DoneBefore, resource.TotalUnits); unit++) map[unit] = true;
            foreach (var range in done.Where(x => x.Id == resource.Id))
                for (var unit = Math.Max(1, range.From); unit <= Math.Min(resource.TotalUnits, range.To); unit++) map[unit] = true;
            return map;
        });
        foreach (var task in open.OrderBy(x => x.PlannedDate).ThenBy(x => x.PlannedStartMinute ?? 0))
        {
            foreach (var resource in resources)
            {
                var map = taken[resource.Id];
                var section = resource.Sections.FirstOrDefault(x => x.TopicId == task.TopicId
                    && Enumerable.Range(x.FromUnit, x.ToUnit - x.FromUnit + 1).Any(unit => !map[unit]));
                if (section is null) continue;
                var from = Enumerable.Range(section.FromUnit, section.ToUnit - section.FromUnit + 1).First(unit => !map[unit]);
                var to = from;
                while (to < section.ToUnit && to - from + 1 < Sitting(resource.Unit) && !map[to + 1]) to++;
                for (var unit = from; unit <= to; unit++) map[unit] = true;
                task.LinkResource(resource, from, to);
                break;
            }
        }
    }
}
