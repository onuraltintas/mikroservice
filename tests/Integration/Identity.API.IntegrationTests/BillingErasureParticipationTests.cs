using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Identity.Application.DataSubjectRequests;

namespace Identity.API.IntegrationTests;

/// <summary>Where the billing service runs, an account erasure waits for its answer too (billing stage 9).</summary>
public sealed class BillingErasureParticipationTests
{
    [Fact]
    public void An_account_erasure_waits_for_billing_only_where_billing_runs()
    {
        DataErasureAssessmentScopePolicy.RequiredServices(PersonalDataScope.Account, billingEnabled: false)
            .Should().Equal("Coaching", "Notification", "SpeedReading", "CourseManagement");
        DataErasureAssessmentScopePolicy.RequiredServices(PersonalDataScope.Account, billingEnabled: true)
            .Should().Equal("Coaching", "Notification", "SpeedReading", "CourseManagement", "Billing");
    }

    // Rehearsal 15.11.2026: with Kurs Yönetimi not installed an account erasure could never complete.
    [Fact]
    public void An_account_erasure_waits_for_Kurs_Yonetimi_only_where_it_is_installed()
    {
        DataErasureAssessmentScopePolicy.RequiredServices(PersonalDataScope.Account, billingEnabled: true, courseManagementEnabled: false)
            .Should().Equal("Coaching", "Notification", "SpeedReading", "Billing");
        DataErasureAssessmentScopePolicy.RequiredServices(PersonalDataScope.Account, billingEnabled: false, courseManagementEnabled: false)
            .Should().Equal("Coaching", "Notification", "SpeedReading");
        DataErasureAssessmentScopePolicy.RequiredServices(PersonalDataScope.Account, billingEnabled: true, courseManagementEnabled: true)
            .Should().Equal("Coaching", "Notification", "SpeedReading", "CourseManagement", "Billing");
    }

    [Theory]
    [InlineData(PersonalDataScope.Coaching, "Coaching")]
    [InlineData(PersonalDataScope.SpeedReading, "SpeedReading")]
    public void A_product_only_request_leaves_the_account_and_its_payments_in_place(PersonalDataScope scope, string service)
    {
        DataErasureAssessmentScopePolicy.RequiredServices(scope, billingEnabled: true).Should().Equal(service);
    }
}
