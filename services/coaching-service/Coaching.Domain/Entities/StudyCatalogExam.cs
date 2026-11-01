namespace Coaching.Domain.Entities;

/// <summary>A session of an exam in an admin's edit: an existing session keeps its id, a new one has none.</summary>
public sealed record StudyCatalogExamSessionInput(Guid? Id, DateOnly Date, string Label, DateOnly? ApplicationStart = null, DateOnly? ApplicationEnd = null);

public sealed class StudyCatalogExam
{
    public const int MaxSessions = 40;
    private readonly List<StudyCatalogExamSession> _sessions = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    /// <summary>
    /// KM1 (01.11.2026): the exam's dated sessions, earliest first. YDS and YÖKDİL have two a year, e-YDS many; LGS and YKS one.
    /// Replaces the single ExamDate the migration moved here.
    /// </summary>
    public IReadOnlyList<StudyCatalogExamSession> Sessions => _sessions.OrderBy(x => x.Date).ThenBy(x => x.Id).ToArray();
    private StudyCatalogExam() { }
    public static StudyCatalogExam Create(string code, string name, int order = 0)
    {
        var exam = new StudyCatalogExam { Code = NormalizeCode(code) };
        exam.Edit(name, order); return exam;
    }
    public static string NormalizeCode(string code)
    {
        var value = StudyCatalogText.Require(code, 10).ToUpperInvariant();
        if (value.Any(c => !(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')))
            throw new ArgumentException("Sınav kodu 1–10 karakter; A–Z, 0–9 ve alt çizgi içermelidir.");
        return value;
    }
    public void Edit(string name, int order)
    {
        if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
        Name = StudyCatalogText.Require(name, 300); DisplayOrder = order;
    }
    public void SetActive(bool active) => IsActive = active;

    /// <summary>The first session on or after <paramref name="after"/>'s next day; else the latest one; null without sessions.</summary>
    public DateOnly? NextSessionDate(DateOnly today) =>
        _sessions.Where(x => x.Date > today).Select(x => (DateOnly?)x.Date).Min() ?? _sessions.Select(x => (DateOnly?)x.Date).Max();

    /// <summary>
    /// Replaces the sessions with the given list. Sessions named by id are edited in place (students who chose them keep the
    /// choice); the others are added. Returns the ids of the sessions that were removed.
    /// </summary>
    public IReadOnlyList<Guid> ReplaceSessions(IReadOnlyList<StudyCatalogExamSessionInput> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        if (sessions.Count > MaxSessions) throw new ArgumentException($"Bir sınavın en çok {MaxSessions} oturumu olabilir.");
        if (sessions.Any(x => x is null)) throw new ArgumentException("Oturum bilgilerini kontrol edin.");
        var labels = sessions.Select(x => StudyCatalogExamSession.NormalizeLabel(x.Label)).ToArray();
        if (sessions.Select(x => x.Date).Distinct().Count() != sessions.Count) throw new ArgumentException("Aynı tarihte iki oturum olamaz.");
        if (labels.Distinct(StringComparer.Ordinal).Count() != labels.Length) throw new ArgumentException("Oturum adları birbirinden farklı olmalıdır.");
        var ids = sessions.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToArray();
        if (ids.Distinct().Count() != ids.Length || ids.Any(id => _sessions.All(x => x.Id != id)))
            throw new ArgumentException("Oturum bu sınava ait değil. Güncel kaydı yeniden açın.");
        foreach (var input in sessions) StudyCatalogExamSession.Validate(input);
        var removed = _sessions.Where(x => !ids.Contains(x.Id)).Select(x => x.Id).ToArray();
        _sessions.RemoveAll(x => removed.Contains(x.Id));
        foreach (var input in sessions)
        {
            if (input.Id is { } id) _sessions.Single(x => x.Id == id).Edit(input);
            else _sessions.Add(StudyCatalogExamSession.Create(Id, input));
        }
        return removed;
    }
}

/// <summary>KM1 (01.11.2026): one dated sitting of an exam, with its optional ÖSYM application window.</summary>
public sealed class StudyCatalogExamSession
{
    private StudyCatalogExamSession() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ExamId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Label { get; private set; } = "";
    public DateOnly? ApplicationStart { get; private set; }
    public DateOnly? ApplicationEnd { get; private set; }

    internal static StudyCatalogExamSession Create(Guid exam, StudyCatalogExamSessionInput input)
    {
        var session = new StudyCatalogExamSession { ExamId = exam }; session.Edit(input); return session;
    }
    internal void Edit(StudyCatalogExamSessionInput input)
    {
        Validate(input);
        Date = input.Date; Label = NormalizeLabel(input.Label); ApplicationStart = input.ApplicationStart; ApplicationEnd = input.ApplicationEnd;
    }
    internal static string NormalizeLabel(string? label) => string.IsNullOrWhiteSpace(label) || label.Trim().Length > 100
        ? throw new ArgumentException("Oturum adı 1–100 karakter olmalıdır (örneğin 2027/1).") : label.Trim();
    internal static void Validate(StudyCatalogExamSessionInput input)
    {
        NormalizeLabel(input.Label);
        if (input.Date.Year is < 2000 or > 2200) throw new ArgumentException("Oturum tarihini kontrol edin.");
        if (input.ApplicationStart.HasValue != input.ApplicationEnd.HasValue)
            throw new ArgumentException("Başvuru dönemi için başlangıç ve bitiş tarihlerini birlikte girin.");
        if (input.ApplicationStart > input.ApplicationEnd || input.ApplicationEnd >= input.Date)
            throw new ArgumentException("Başvuru dönemi sınav tarihinden önce bitmeli ve başlangıcı bitişinden sonra olamaz.");
    }
}
