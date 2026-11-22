using Billing.Domain;

namespace Billing.IntegrationTests;

public sealed class BillingRulesTests
{
    [Theory]
    [InlineData("TR33 0006 1005 1978 6457 8413 26", "TR330006100519786457841326")]
    [InlineData("tr330006100519786457841326", "TR330006100519786457841326")]
    [InlineData("TR33000610051978645784132", null)]
    [InlineData("DE89370400440532013000", null)]
    [InlineData("", null)]
    public void Iban_is_stored_without_spaces_and_only_when_turkish(string input, string? expected) =>
        Assert.Equal(expected, BillingRules.NormalizeIban(input));

    [Fact]
    public void Iban_is_shown_in_groups_of_four() =>
        Assert.Equal("TR33 0006 1005 1978 6457 8413 26", BillingRules.FormatIban("TR330006100519786457841326"));

    [Theory]
    [InlineData("coaching.student", "coaching", "coaching.student")]
    [InlineData(" Coaching.Student ", "coaching", "coaching.student")]
    [InlineData("speed-reading.student", "coaching", null)]
    [InlineData("coaching", "coaching", null)]
    [InlineData("coaching.", "coaching", null)]
    public void Entitlement_key_belongs_to_its_product(string key, string productCode, string? expected) =>
        Assert.Equal(expected, BillingRules.NormalizeEntitlementKey(key, productCode));

    [Theory]
    [InlineData(BuyerType.Individual, 100, 30, 0, null, null, false, true)]
    [InlineData(BuyerType.Individual, 100, 30, 7, null, null, false, true)]
    [InlineData(BuyerType.Teacher, 100, 30, 0, 10, null, false, true)]
    [InlineData(BuyerType.Institution, 100, 365, 0, 50, 20, false, true)]
    [InlineData(BuyerType.Institution, 0, 365, 0, 50, null, true, true)]
    // A seat belongs to a teacher or an institution, never to one student.
    [InlineData(BuyerType.Individual, 100, 30, 0, 5, null, false, false)]
    // Institutions do not get a trial.
    [InlineData(BuyerType.Institution, 100, 30, 7, 50, null, false, false)]
    // Extra seats are sold only on teacher and institution plans that have seats (KocApp uyumu, 22.11.2026: teachers too).
    [InlineData(BuyerType.Teacher, 100, 30, 0, 10, 20, false, true)]
    [InlineData(BuyerType.Teacher, 100, 30, 0, null, 20, false, false)]
    [InlineData(BuyerType.Individual, 100, 30, 0, null, 20, false, false)]
    [InlineData(BuyerType.Institution, 100, 30, 0, null, 20, false, false)]
    [InlineData(BuyerType.Individual, -1, 30, 0, null, null, false, false)]
    [InlineData(BuyerType.Individual, 100, 0, 0, null, null, false, false)]
    [InlineData(BuyerType.Individual, 100, 30, 91, null, null, false, false)]
    [InlineData(BuyerType.Institution, 100, 30, 0, 50, null, true, false)]
    public void Plan_rules(BuyerType buyer, int price, int days, int trial, int? seats, int? extraSeat, bool contactOnly, bool sound) =>
        Assert.Equal(sound, BillingRules.PlanProblem(buyer, price, days, trial, seats, extraSeat, contactOnly) is null);

    [Fact]
    public void Only_a_priced_plan_that_is_not_contact_only_can_be_ordered()
    {
        Assert.True(BillingRules.CanBeOrdered(100, false));
        Assert.False(BillingRules.CanBeOrdered(0, false));
        Assert.False(BillingRules.CanBeOrdered(0, true));
    }

    [Fact]
    public void Seller_is_complete_without_mersis_but_not_without_a_tax_number()
    {
        var seller = new Seller { Title = "A", Address = "B", TaxOffice = "C", TaxNumber = "1234567890", Email = "a@b.c", Phone = "1" };
        Assert.True(seller.IsComplete);
        seller.TaxNumber = " ";
        Assert.False(seller.IsComplete);
    }

    [Theory]
    [InlineData(false, "Onay geri alındı (18.11.2026 12:47): TEST")]
    [InlineData(true, "Cayma kabul edildi (18.11.2026 12:47): TEST")]
    public void Reversal_line_names_a_withdrawal_and_uses_turkey_time(bool withdrawal, string expected) =>
        Assert.Equal(expected, BillingRules.ReversalLine(new DateTime(2026, 11, 18, 9, 47, 0, DateTimeKind.Utc), "TEST", withdrawal));
}
