using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coaching.Domain.Entities;

namespace Coaching.Application.Finance;

public sealed record CoachingFeeCreate([property: JsonRequired] Guid RequestId, [Required, MaxLength(200)] string Title,
    [property: JsonRequired] decimal TotalAmount, [Required] IReadOnlyList<CoachingFeeInstallmentInput> Installments);
public sealed record CoachingFeePaymentInput([property: JsonRequired] Guid RequestId,
    [property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion, [property: JsonRequired] decimal Amount,
    [property: JsonRequired] CoachingFeePaymentMethod Method, [property: JsonRequired] DateOnly ReceivedOn, [MaxLength(300)] string? Reference);
public sealed record CoachingFeeReversalInput([property: JsonRequired] Guid RequestId,
    [property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion, [property: JsonRequired] DateOnly ReceivedOn, [Required, MaxLength(300)] string Reason);
public sealed record CoachingFeeCancelInput([property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion, [Required, MaxLength(300)] string Reason);
public sealed record CoachingFeeInstallmentView(Guid Id, int Sequence, DateOnly DueOn, decimal Amount, decimal Balance, bool IsOverdue);
public sealed record CoachingFeeView(Guid Id, Guid StudentId, Guid? InstitutionId, Guid? CoachId, int Version, string Title,
    string Currency, decimal TotalAmount, decimal PaidAmount, decimal Balance, decimal OverdueAmount, bool IsCancelled,
    string? CancellationReason, DateTime? CancelledAt, IReadOnlyList<CoachingFeeInstallmentView> Installments);
public sealed record CoachingFeeAccountView(decimal TotalAmount, decimal PaidAmount, decimal Balance, decimal OverdueAmount,
    bool CanManage, bool CanCreate, int TotalCount, int PageNumber, int PageSize, IReadOnlyList<CoachingFeeView> Agreements);
public sealed record CoachingFeePaymentView(Guid Id, Guid AgreementId, Guid StudentId, Guid RecordedByUserId,
    Guid? ReversalOfPaymentId, CoachingFeePaymentMethod Method, decimal Amount, DateOnly ReceivedOn, string? Reference, DateTime CreatedAt);
public sealed record CoachingFeePaymentPage(IReadOnlyList<CoachingFeePaymentView> Items, int TotalCount, int PageNumber, int PageSize);
public sealed record CoachingCashStatement(decimal CashTotal, decimal BankTransferTotal,
    IReadOnlyList<CoachingFeePaymentView> Items, int TotalCount, int PageNumber, int PageSize);
public sealed record CoachingReceivablesView(decimal Balance, decimal OverdueAmount,
    IReadOnlyList<CoachingFeeView> Items, int TotalCount, int PageNumber, int PageSize);
