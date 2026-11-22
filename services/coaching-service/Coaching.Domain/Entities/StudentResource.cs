using System.Text.Json;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

/// <summary>KocApp uyumu (22.11.2026): what a resource's progress is counted in; chosen per resource (user decision).</summary>
public static class StudentResourceUnits
{
    public const string Page = "Page";
    public const string Test = "Test";
    public const string Question = "Question";
    public static readonly IReadOnlyList<string> All = [Page, Test, Question];
}

/// <summary>KocApp uyumu (22.11.2026): what kind of book or set a resource is.</summary>
public static class StudentResourceKinds
{
    public const string QuestionBank = "QuestionBank";
    public const string TopicBook = "TopicBook";
    public const string PracticeExams = "PracticeExams";
    public const string Other = "Other";
    public static readonly IReadOnlyList<string> All = [QuestionBank, TopicBook, PracticeExams, Other];
}

/// <summary>A part of a resource (a unit, a chapter, a test range), optionally tied to a curriculum topic.</summary>
public sealed record StudentResourceSection(string Title, int FromUnit, int ToUnit, Guid? TopicId = null);

/// <summary>What a resource is: written by the student or a teacher, checked by <see cref="StudentResource"/>.</summary>
public sealed record StudentResourceDetails(string Name, string? Publisher, string? LessonName, string Kind, string Unit, int TotalUnits,
    int DoneBefore, IReadOnlyList<StudentResourceSection>? Sections, Guid? LessonId = null, string? ExamCode = null);

/// <summary>
/// KocApp uyumu (22.11.2026): a book or a set a student works through ("Limit Matematik Soru Bankası, 480 sayfa"). Its progress is
/// what was done before it was added (<see cref="DoneBefore"/>, from the first unit on) together with the ranges of the completed
/// study tasks that point at it; overlapping ranges count once.
/// </summary>
public sealed class StudentResource : AggregateRoot
{
    public const int MaxUnits = 10_000;
    public const int MaxSections = 300;

    public Guid StudentId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Publisher { get; private set; }
    public string? LessonName { get; private set; }
    /// <summary>The catalog lesson the resource belongs to (its name is kept in <see cref="LessonName"/>); null for a free-text lesson.</summary>
    public Guid? LessonId { get; private set; }
    /// <summary>The exam the resource prepares for (LGS, TYT, AYT, AGS, YDS…); the lesson's exam by default.</summary>
    public string? ExamCode { get; private set; }
    public string Kind { get; private set; } = StudentResourceKinds.QuestionBank;
    public string Unit { get; private set; } = StudentResourceUnits.Page;
    public int TotalUnits { get; private set; }
    public int DoneBefore { get; private set; }
    public bool IsArchived { get; private set; }
    public string SectionsJson { get; private set; } = "[]";

    public IReadOnlyList<StudentResourceSection> Sections =>
        JsonSerializer.Deserialize<StudentResourceSection[]>(SectionsJson) ?? [];

    private StudentResource() { }

    public static StudentResource Create(Guid studentId, Guid createdBy, StudentResourceDetails details, DateTime now)
    {
        if (studentId == Guid.Empty || createdBy == Guid.Empty) throw new ArgumentException("Öğrenci ve ekleyen kişi gerekli.");
        var resource = new StudentResource { StudentId = studentId, CreatedByUserId = createdBy, CreatedAt = now };
        resource.Apply(details);
        return resource;
    }

    public void Update(StudentResourceDetails details, DateTime now)
    {
        Apply(details);
        UpdatedAt = now;
    }

    public void SetArchived(bool archived, DateTime now)
    {
        if (IsArchived == archived) return;
        IsArchived = archived;
        UpdatedAt = now;
    }

    /// <summary>A task range is a part of this resource: 1 ≤ from ≤ to ≤ total.</summary>
    public bool Contains(int from, int to) => from >= 1 && to >= from && to <= TotalUnits;

    /// <summary>How many units are done: the first <paramref name="doneBefore"/> units and every completed range, each unit once.</summary>
    public static int Covered(int total, int doneBefore, IEnumerable<(int From, int To)> ranges)
    {
        var done = Math.Clamp(doneBefore, 0, total);
        var count = done;
        var reach = done;
        foreach (var (from, to) in ranges.Select(x => (From: Math.Max(1, x.From), To: Math.Min(total, x.To))).Where(x => x.From <= x.To).OrderBy(x => x.From))
        {
            var start = Math.Max(from, reach + 1);
            if (to >= start) { count += to - start + 1; reach = to; }
        }
        return count;
    }

    private void Apply(StudentResourceDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var name = details.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200) throw new ArgumentException("Kaynağın adı 1–200 karakter olmalı.");
        var publisher = Optional(details.Publisher, 120, "Yayınevi en çok 120 karakter olabilir.");
        var lesson = Optional(details.LessonName, 100, "Ders adı en çok 100 karakter olabilir.");
        if (!StudentResourceKinds.All.Contains(details.Kind)) throw new ArgumentException("Kaynak türü geçersiz.");
        if (!StudentResourceUnits.All.Contains(details.Unit)) throw new ArgumentException("İlerleme birimi sayfa, test ya da soru olmalı.");
        if (details.TotalUnits is < 1 or > MaxUnits) throw new ArgumentException($"Toplam 1 ile {MaxUnits:N0} arasında olmalı.");
        if (details.DoneBefore < 0 || details.DoneBefore > details.TotalUnits) throw new ArgumentException("Önceden bitirilen kısım toplamı aşamaz.");
        var sections = (details.Sections ?? []).ToArray();
        if (sections.Length > MaxSections) throw new ArgumentException($"Bir kaynakta en çok {MaxSections} bölüm olabilir.");
        var clean = sections.Select(section =>
        {
            var title = section.Title?.Trim();
            if (string.IsNullOrEmpty(title) || title.Length > 200) throw new ArgumentException("Bölüm adı 1–200 karakter olmalı.");
            if (section.FromUnit < 1 || section.ToUnit < section.FromUnit || section.ToUnit > details.TotalUnits)
                throw new ArgumentException($"“{title}” bölümünün aralığı kaynağın içinde olmalı (1–{details.TotalUnits}).");
            if (section.TopicId == Guid.Empty) throw new ArgumentException("Bölümün konusu geçersiz.");
            return section with { Title = title };
        }).OrderBy(x => x.FromUnit).ToArray();
        if (details.LessonId == Guid.Empty) throw new ArgumentException("Ders geçersiz.");
        var exam = Optional(details.ExamCode, 30, "Sınav türü en çok 30 karakter olabilir.")?.ToUpperInvariant();
        if (exam is not null && !exam.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-')) throw new ArgumentException("Sınav türü geçersiz.");
        // A shorter book may not cut through ranges the plan already points at; the service checks the tasks.
        Name = name; Publisher = publisher; LessonName = lesson; Kind = details.Kind; Unit = details.Unit;
        LessonId = details.LessonId; ExamCode = exam;
        TotalUnits = details.TotalUnits; DoneBefore = details.DoneBefore;
        SectionsJson = JsonSerializer.Serialize(clean);
    }

    private static string? Optional(string? value, int max, string error)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (text is { Length: var length } && length > max) throw new ArgumentException(error);
        return text;
    }
}
