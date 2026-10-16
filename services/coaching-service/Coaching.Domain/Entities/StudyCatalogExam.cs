namespace Coaching.Domain.Entities;

public sealed class StudyCatalogExam
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public DateOnly? ExamDate { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    private StudyCatalogExam() { }
    public static StudyCatalogExam Create(string code, string name, int order = 0, DateOnly? examDate = null)
    {
        var exam = new StudyCatalogExam { Code = NormalizeCode(code) };
        exam.Edit(name, order, examDate); return exam;
    }
    public static string NormalizeCode(string code)
    {
        var value = StudyCatalogText.Require(code, 10).ToUpperInvariant();
        if (value.Any(c => !(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')))
            throw new ArgumentException("Sınav kodu 1–10 karakter; A–Z, 0–9 ve alt çizgi içermelidir.");
        return value;
    }
    public void Edit(string name, int order, DateOnly? examDate = null)
    {
        if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
        Name = StudyCatalogText.Require(name, 300); DisplayOrder = order;
        ExamDate = examDate;
    }
    public void SetActive(bool active) => IsActive = active;
}
