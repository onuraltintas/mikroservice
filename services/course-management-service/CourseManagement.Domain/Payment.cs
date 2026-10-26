namespace CourseManagement.Domain;

public enum PaymentMethod { Cash = 1, BankTransfer = 2 }

public sealed class Payment
{
    private Payment() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public Guid? ReversalOfPaymentId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public string? Note { get; private set; }

    public static Payment Create(Guid id, Guid workspaceId, Guid studentId, Guid recordedByUserId, PaymentMethod method,
        decimal amount, DateTimeOffset receivedAt, string? note)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || studentId == Guid.Empty || recordedByUserId == Guid.Empty
            || !Enum.IsDefined(method)) throw new ArgumentException("Payment identifiers and method are required.");
        if (amount is <= 0 or > 9_999_999_999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive and use at most two decimal places.");
        return new Payment { Id = id, WorkspaceId = workspaceId, StudentId = studentId, RecordedByUserId = recordedByUserId,
            Method = method, Amount = amount, ReceivedAt = receivedAt.ToUniversalTime(), Note = Optional(note) };
    }

    public static Payment CreateReversal(Guid id, Payment original, Guid recordedByUserId, DateTimeOffset receivedAt, string? reason) => new()
    {
        Id = id, WorkspaceId = original.WorkspaceId, StudentId = original.StudentId, RecordedByUserId = recordedByUserId,
        ReversalOfPaymentId = original.Id, Method = original.Method, Amount = -original.Amount,
        ReceivedAt = receivedAt.ToUniversalTime(), Note = Optional(reason)
    };

    private static string? Optional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > 300) throw new ArgumentException("Payment note is too long.", nameof(value));
        return normalized;
    }
}
