using FluentAssertions;
using SpeedReading.Application.Subscription;

namespace SpeedReading.Application.UnitTests;

public sealed class BankTransferPaymentRulesTests
{
    [Theory]
    [InlineData(true, "Onal Yazılım ve Otomasyon", "Örnek Banka", "TR12 0006 1005 1978 6457 8413 26", true)]
    [InlineData(false, "Onal Yazılım ve Otomasyon", "Örnek Banka", "TR120006100519786457841326", false)]
    [InlineData(true, "", "Örnek Banka", "TR120006100519786457841326", false)]
    [InlineData(true, "Onal Yazılım ve Otomasyon", "", "TR120006100519786457841326", false)]
    [InlineData(true, "Onal Yazılım ve Otomasyon", "Örnek Banka", "TR12000610051978645784132", false)]
    public void Exposes_bank_transfer_details_only_when_the_configuration_is_complete(
        bool enabled,
        string accountHolder,
        string bankName,
        string iban,
        bool expected)
    {
        BankTransferPaymentRules.HasCompletePublicSettings(enabled, accountHolder, bankName, iban)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(750, "OneTime", 180, true)]
    [InlineData(1500, "Annual", 365, true)]
    [InlineData(-1, "OneTime", 180, false)]
    [InlineData(750, "Weekly", 180, false)]
    [InlineData(750, "OneTime", 0, false)]
    public void Validates_plan_prices_periods_and_access_durations(
        int price,
        string billingPeriod,
        int durationDays,
        bool expected)
    {
        BankTransferPaymentRules.IsValidPlanDefinition(price, billingPeriod, durationDays)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(499, true, false)]
    [InlineData(499, false, true)]
    public void Allows_contact_only_plans_only_without_a_list_price(
        int price,
        bool isContactOnly,
        bool expected)
    {
        BankTransferPaymentRules.IsValidPlanDefinition(price, "Annual", 365, isContactOnly)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(499, false, true)]
    [InlineData(0, false, false)]
    [InlineData(499, true, false)]
    public void Excludes_contact_only_plans_from_student_bank_transfer_requests(
        int price,
        bool isContactOnly,
        bool expected)
    {
        BankTransferPaymentRules.CanRequestBankTransfer(price, isContactOnly)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true, true, true, true)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    public void Allows_a_student_to_request_only_a_public_active_plan_of_a_public_active_product(
        bool planActive,
        bool planPublic,
        bool productActive,
        bool productPublic,
        bool expected)
    {
        BankTransferPaymentRules.IsPubliclyPurchasable(planActive, planPublic, productActive, productPublic)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, false, 365, true)]
    [InlineData(true, true, true, 365, true)]
    [InlineData(true, true, false, 365, false)]
    [InlineData(false, false, false, 365, false)]
    [InlineData(true, false, false, 180, false)]
    public void Allows_active_one_year_institution_plans_when_hidden_or_contact_only(
        bool planActive,
        bool planPublic,
        bool isContactOnly,
        int durationDays,
        bool expected)
    {
        BankTransferPaymentRules.IsInstitutionAccessPlan(planActive, planPublic, isContactOnly, durationDays)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(" eft-2026/000123 ", "EFT-2026-000123")]
    [InlineData("TRX_987", "TRX_987")]
    public void Normalizes_the_student_payment_reference_without_changing_its_identity(
        string reference,
        string expected)
    {
        BankTransferPaymentRules.NormalizePaymentReference(reference).Should().Be(expected);
    }

    [Theory]
    [InlineData("Pending", "Approved", true)]
    [InlineData("Pending", "Rejected", true)]
    [InlineData("Approved", "Approved", false)]
    [InlineData("Rejected", "Approved", false)]
    public void Allows_a_payment_request_to_be_reviewed_once(
        string currentStatus,
        string nextStatus,
        bool expected)
    {
        BankTransferPaymentRules.CanTransition(currentStatus, nextStatus).Should().Be(expected);
    }
}
