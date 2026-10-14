namespace CourseManagement.Domain;

public sealed class Installment
{
    private Installment() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid FeeAgreementId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid? SourceLessonId { get; private set; }
    public Guid? ReversalOfInstallmentId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateOnly DueOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Installment Create(Guid id, Guid workspaceId, Guid feeAgreementId, Guid studentId, string label,
        decimal amount, DateOnly dueOn, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || feeAgreementId == Guid.Empty || studentId == Guid.Empty)
            throw new ArgumentException("Installment identifiers are required.");
        if (amount is <= 0 or > 9_999_999_999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive and use at most two decimal places.");
        return new Installment
        {
            Id = id, WorkspaceId = workspaceId, FeeAgreementId = feeAgreementId, StudentId = studentId,
            Label = Required(label), Amount = amount, DueOn = dueOn, CreatedAt = now.ToUniversalTime()
        };
    }

    public static Installment CreateLessonCharge(Guid id, FeeAgreement agreement, Guid lessonId, decimal amount,
        DateOnly dueOn, DateTimeOffset now) => new()
    {
        Id = id, WorkspaceId = agreement.WorkspaceId, FeeAgreementId = agreement.Id, StudentId = agreement.StudentId,
        SourceLessonId = lessonId, Label = "Tamamlanan ders", Amount = amount, DueOn = dueOn, CreatedAt = now.ToUniversalTime()
    };

    public static Installment CreateReversal(Guid id, Installment original, DateTimeOffset now) => new()
    {
        Id = id, WorkspaceId = original.WorkspaceId, FeeAgreementId = original.FeeAgreementId, StudentId = original.StudentId,
        ReversalOfInstallmentId = original.Id, Label = $"İptal: {original.Label}", Amount = -original.Amount,
        DueOn = DateOnly.FromDateTime(now.UtcDateTime), CreatedAt = now.ToUniversalTime()
    };

    private static string Required(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Trim();
        if (normalized.Length is < 1 or > 120) throw new ArgumentException("Installment label is invalid.", nameof(value));
        return normalized;
    }
}
