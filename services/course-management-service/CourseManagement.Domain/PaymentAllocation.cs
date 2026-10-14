namespace CourseManagement.Domain;

public sealed class PaymentAllocation
{
    private PaymentAllocation() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid InstallmentId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static PaymentAllocation Create(Guid id, Guid workspaceId, Guid paymentId, Guid installmentId, decimal amount, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || paymentId == Guid.Empty || installmentId == Guid.Empty)
            throw new ArgumentException("Payment allocation identifiers are required.");
        if (amount is <= 0 or > 9_999_999_999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Allocation must be positive and use at most two decimal places.");
        return new PaymentAllocation { Id = id, WorkspaceId = workspaceId, PaymentId = paymentId,
            InstallmentId = installmentId, Amount = amount, CreatedAt = now.ToUniversalTime() };
    }

    public static PaymentAllocation CreateReversal(Guid id, PaymentAllocation original, Guid reversalPaymentId, DateTimeOffset now) => new()
    {
        Id = id, WorkspaceId = original.WorkspaceId, PaymentId = reversalPaymentId,
        InstallmentId = original.InstallmentId, Amount = -original.Amount, CreatedAt = now.ToUniversalTime()
    };
}
