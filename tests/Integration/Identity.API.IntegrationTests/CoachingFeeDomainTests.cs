using Coaching.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class CoachingFeeDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 13);
    private static CoachingFeeAgreement Agreement() => CoachingFeeAgreement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
        Guid.NewGuid(), "Koçluk dönemi", 1000m, [new(Today, 400m), new(Today.AddMonths(1), 600m)], Now);

    [Fact]
    public void ReceiptAndReversalKeepOriginalHistoryAndRestoreOutstandingBalance()
    {
        var agreement = Agreement(); var actor = Guid.NewGuid(); var paymentId = Guid.NewGuid();
        var receipt = agreement.AddPayment(paymentId, actor, 250m, CoachingFeePaymentMethod.Cash, Today, Now, "Makbuz 1");
        Assert.Equal(750m, agreement.Balance); Assert.Equal(250m, agreement.PaidAmount);
        Assert.Same(receipt, agreement.AddPayment(paymentId, actor, 250m, CoachingFeePaymentMethod.Cash, Today, Now.AddMinutes(1), "Makbuz 1"));
        Assert.Single(agreement.Payments);
        var reversalId = Guid.NewGuid();
        var reversal = agreement.ReversePayment(reversalId, paymentId, actor, Today, "Yanlış tahsilat", Now.AddMinutes(2));
        Assert.Equal(-250m, reversal.Amount); Assert.Equal(paymentId, reversal.ReversalOfPaymentId);
        Assert.Equal(250m, receipt.Amount); Assert.Equal(1000m, agreement.Balance); Assert.Equal(0m, agreement.PaidAmount);
        Assert.Same(reversal, agreement.ReversePayment(reversalId, paymentId, actor, Today, "Yanlış tahsilat", Now.AddMinutes(3)));
        Assert.Throws<InvalidOperationException>(() => agreement.ReversePayment(Guid.NewGuid(), paymentId, actor, Today, "İkinci iptal", Now));
        Assert.Throws<InvalidOperationException>(() => agreement.ReversePayment(Guid.NewGuid(), reversalId, actor, Today, "İptal iptali", Now));
        Assert.Equal(2, agreement.Payments.Count);
    }

    [Fact]
    public void OverpaymentAndReusedReceiptIdentityAreRejectedWithoutChangingBalance()
    {
        var agreement = Agreement(); var id = Guid.NewGuid(); var actor = Guid.NewGuid();
        agreement.AddPayment(id, actor, 800m, CoachingFeePaymentMethod.BankTransfer, Today, Now, null);
        Assert.Throws<InvalidOperationException>(() => agreement.AddPayment(Guid.NewGuid(), actor, 201m, CoachingFeePaymentMethod.Cash, Today, Now, null));
        Assert.Throws<InvalidOperationException>(() => agreement.AddPayment(id, actor, 100m, CoachingFeePaymentMethod.BankTransfer, Today, Now, null));
        Assert.Equal(200m, agreement.Balance); Assert.Single(agreement.Payments);
        agreement.AddPayment(Guid.NewGuid(), actor, 200m, CoachingFeePaymentMethod.Cash, Today, Now, null);
        Assert.Equal(0m, agreement.Balance);
    }

    [Fact]
    public void CancellationRequiresNoRemainingPaymentAndPreservesInstallments()
    {
        var agreement = Agreement(); var actor = Guid.NewGuid(); var payment = Guid.NewGuid();
        agreement.AddPayment(payment, actor, 100m, CoachingFeePaymentMethod.Cash, Today, Now, null);
        Assert.Throws<InvalidOperationException>(() => agreement.Cancel(actor, "İptal", Now)); Assert.False(agreement.IsCancelled);
        agreement.ReversePayment(Guid.NewGuid(), payment, actor, Today, "İade kaydı", Now);
        agreement.Cancel(actor, "Öğrenci ayrıldı", Now);
        Assert.True(agreement.IsCancelled); Assert.Equal(0m, agreement.Balance);
        Assert.Equal(2, agreement.Installments.Count); Assert.Equal(2, agreement.Payments.Count);
        Assert.Throws<InvalidOperationException>(() => agreement.AddPayment(Guid.NewGuid(), actor, 10m, CoachingFeePaymentMethod.Cash, Today, Now, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    [InlineData(1000000000)]
    public void AmountRequiresPositiveTwoDecimalMoney(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CoachingFeeAgreement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            Guid.NewGuid(), "Ücret", amount, [new(Today, amount)], Now));
        var agreement = Agreement();
        Assert.Throws<ArgumentOutOfRangeException>(() => agreement.AddPayment(Guid.NewGuid(), Guid.NewGuid(), amount, CoachingFeePaymentMethod.Cash, Today, Now, null));
        Assert.Empty(agreement.Payments);
    }

    [Fact]
    public void InstallmentsMustMatchTotalAndPreserveSortedDueDates()
    {
        Assert.Throws<ArgumentException>(() => CoachingFeeAgreement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            Guid.NewGuid(), "Ücret", 1000m, [new(Today, 400m), new(Today.AddMonths(1), 500m)], Now));
        Assert.Throws<ArgumentException>(() => CoachingFeeAgreement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            Guid.NewGuid(), "Ücret", 1000m, [], Now));
        var agreement = CoachingFeeAgreement.Create(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Bağımsız koçluk",
            1000m, [new(Today.AddMonths(1), 600m), new(Today, 400m)], Now);
        Assert.Equal(new[] { Today, Today.AddMonths(1) }, agreement.Installments.Select(x => x.DueOn));
        Assert.All(agreement.Installments, x => Assert.Equal(agreement.Id, x.AgreementId));
    }

    [Fact]
    public void NullInstallmentIsRejectedAsInvalidInput()
    {
        Assert.Throws<ArgumentException>(() => CoachingFeeAgreement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            Guid.NewGuid(), "Ücret", 1000m, [null!], Now));
    }

    [Fact]
    public void OwnershipMustBeEitherOneInstitutionOrOneIndependentCoach()
    {
        foreach (var owners in new (Guid? Institution, Guid? Coach)[] { (null, null), (Guid.NewGuid(), Guid.NewGuid()), (Guid.Empty, null), (null, Guid.Empty) })
            Assert.Throws<ArgumentException>(() => CoachingFeeAgreement.Create(Guid.NewGuid(), Guid.NewGuid(), owners.Institution, owners.Coach,
                Guid.NewGuid(), "Ücret", 1000m, [new(Today, 1000m)], Now));
    }
}
