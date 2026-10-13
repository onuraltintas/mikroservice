using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

public enum CoachingFeePaymentMethod { Cash = 1, BankTransfer = 2 }
public sealed record CoachingFeeInstallmentInput(DateOnly DueOn, decimal Amount);

public sealed class CoachingFeeAgreement : AggregateRoot
{
    public Guid StudentId { get; private set; }
    public Guid? InstitutionId { get; private set; }
    public Guid? CoachId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public bool IsCancelled { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    private readonly List<CoachingFeeInstallment> _installments = [];
    private readonly List<CoachingFeePayment> _payments = [];
    public IReadOnlyCollection<CoachingFeeInstallment> Installments => _installments.AsReadOnly();
    public IReadOnlyCollection<CoachingFeePayment> Payments => _payments.AsReadOnly();
    public decimal PaidAmount { get; private set; }
    public decimal Balance => IsCancelled ? 0 : TotalAmount - PaidAmount;
    private CoachingFeeAgreement() { }

    public static CoachingFeeAgreement Create(Guid id, Guid studentId, Guid? institutionId, Guid? coachId,
        Guid actorId, string title, decimal totalAmount, IReadOnlyCollection<CoachingFeeInstallmentInput> installments, DateTime now)
    {
        if (id == Guid.Empty || studentId == Guid.Empty || actorId == Guid.Empty || institutionId == Guid.Empty || coachId == Guid.Empty
            || institutionId.HasValue == coachId.HasValue) throw new ArgumentException("A fee requires a student, operator and exactly one institution or coach.");
        RequireMoney(totalAmount); RequireUtc(now);
        ArgumentNullException.ThrowIfNull(installments);
        if (installments.Count is < 1 or > 24) throw new ArgumentException("A fee requires 1–24 installments.", nameof(installments));
        foreach (var installment in installments) {
            if (installment is null) throw new ArgumentException("An installment row is required.", nameof(installments));
            RequireMoney(installment.Amount);
            if (installment.DueOn == default) throw new ArgumentException("An installment requires a due date.");
        }
        if (installments.Sum(x => x.Amount) != totalAmount) throw new ArgumentException("Installments must equal the total fee.");
        var agreement = new CoachingFeeAgreement { Id = id, StudentId = studentId, InstitutionId = institutionId, CoachId = coachId,
            CreatedByUserId = actorId, Title = StudyCatalogText.Require(title, 200), TotalAmount = totalAmount, CreatedAt = now };
        foreach (var installment in installments.OrderBy(x => x.DueOn))
            agreement._installments.Add(CoachingFeeInstallment.Create(id, agreement._installments.Count + 1, installment, now));
        return agreement;
    }

    public CoachingFeePayment AddPayment(Guid id, Guid actorId, decimal amount, CoachingFeePaymentMethod method,
        DateOnly receivedOn, DateTime now, string? reference)
    {
        RequirePayment(id, actorId, receivedOn, now); RequireMoney(amount);
        if (!Enum.IsDefined(method)) throw new ArgumentOutOfRangeException(nameof(method));
        var note = string.IsNullOrWhiteSpace(reference) ? null : StudyCatalogText.Require(reference, 300);
        var existing = _payments.SingleOrDefault(x => x.Id == id);
        if (existing is not null) {
            if (existing.ReversalOfPaymentId is null && existing.RecordedByUserId == actorId && existing.Amount == amount
                && existing.Method == method && existing.ReceivedOn == receivedOn && existing.Reference == note) return existing;
            throw new InvalidOperationException("This receipt identity belongs to another payment.");
        }
        if (IsCancelled || amount > Balance) throw new InvalidOperationException("An active fee with sufficient outstanding balance is required.");
        var payment = CoachingFeePayment.Create(id, Id, actorId, amount, method, receivedOn, now, note);
        _payments.Add(payment); PaidAmount += amount; UpdatedAt = now; return payment;
    }

    public CoachingFeePayment ReversePayment(Guid id, Guid originalId, Guid actorId, DateOnly receivedOn, string reason, DateTime now)
    {
        RequirePayment(id, actorId, receivedOn, now);
        var note = StudyCatalogText.Require(reason, 300);
        var existing = _payments.SingleOrDefault(x => x.Id == id);
        if (existing is not null) {
            if (existing.ReversalOfPaymentId == originalId && existing.RecordedByUserId == actorId
                && existing.ReceivedOn == receivedOn && existing.Reference == note) return existing;
            throw new InvalidOperationException("This receipt identity belongs to another payment.");
        }
        var original = _payments.SingleOrDefault(x => x.Id == originalId);
        if (original is null || original.ReversalOfPaymentId.HasValue || _payments.Any(x => x.ReversalOfPaymentId == originalId))
            throw new InvalidOperationException("Only an unreversed receipt belonging to this fee can be reversed.");
        var reversal = CoachingFeePayment.Create(id, Id, actorId, -original.Amount, original.Method, receivedOn, now, note, originalId);
        _payments.Add(reversal); PaidAmount -= original.Amount; UpdatedAt = now; return reversal;
    }

    public void Cancel(Guid actorId, string reason, DateTime now)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("An operator is required.");
        RequireUtc(now); var note = StudyCatalogText.Require(reason, 300);
        if (PaidAmount != 0) throw new InvalidOperationException("Reverse recorded payments before cancelling the fee.");
        if (IsCancelled) return;
        IsCancelled = true; CancelledByUserId = actorId; CancellationReason = note; CancelledAt = now; UpdatedAt = now;
    }

    private static void RequireMoney(decimal value) {
        if (value is <= 0 or > 999_999_999.99m || decimal.Round(value, 2) != value)
            throw new ArgumentOutOfRangeException(nameof(value), "Money must be positive with at most two decimal places.");
    }
    private static void RequireUtc(DateTime now) {
        if (now.Kind != DateTimeKind.Utc) throw new ArgumentException("A UTC timestamp is required.");
    }
    private static void RequirePayment(Guid id, Guid actorId, DateOnly receivedOn, DateTime now) {
        if (id == Guid.Empty || actorId == Guid.Empty || receivedOn == default) throw new ArgumentException("A receipt, operator and received date are required.");
        RequireUtc(now);
    }
}

public sealed class CoachingFeeInstallment : Entity
{
    public Guid AgreementId { get; private set; }
    public int Sequence { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly DueOn { get; private set; }
    private CoachingFeeInstallment() { }
    internal static CoachingFeeInstallment Create(Guid agreementId, int sequence, CoachingFeeInstallmentInput input, DateTime now) =>
        new() { AgreementId = agreementId, Sequence = sequence, Amount = input.Amount, DueOn = input.DueOn, CreatedAt = now };
}

public sealed class CoachingFeePayment : Entity
{
    public Guid AgreementId { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public Guid? ReversalOfPaymentId { get; private set; }
    public CoachingFeePaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly ReceivedOn { get; private set; }
    public string? Reference { get; private set; }
    private CoachingFeePayment() { }
    internal static CoachingFeePayment Create(Guid id, Guid agreementId, Guid actorId, decimal amount,
        CoachingFeePaymentMethod method, DateOnly receivedOn, DateTime now, string? reference, Guid? reversalOf = null) =>
        new() { Id = id, AgreementId = agreementId, RecordedByUserId = actorId, Amount = amount, Method = method,
            ReceivedOn = receivedOn, Reference = reference, ReversalOfPaymentId = reversalOf, CreatedAt = now };
}
