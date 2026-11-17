using FluentAssertions;
using Notification.Application.Configuration;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Alan adı planı (user decision 18.11.2026): every group signs in on its own address, so e-mail links open the
/// address of the reader's group (students maxkoc.com, parents veli.maxkoc.com, teachers ogretmen.eduivme.com,
/// institution managers kurum.eduivme.com, management admin.eduivme.com).
/// </summary>
public class DomainSplitLinkTests
{
    private static PublicAppUrlOptions Production() => new()
    {
        BaseUrl = "https://maxkoc.com",
        ParentBaseUrl = "https://veli.maxkoc.com",
        SpeedReadingBaseUrl = "https://masterhizliokuma.com",
        StaffPortalUrl = "https://ogretmen.eduivme.com/staff",
        InstitutionPortalUrl = "https://kurum.eduivme.com/staff/",
        AdminPanelUrl = "https://admin.eduivme.com"
    };

    [Fact]
    public void ParentLinksOpenTheParentAddress()
    {
        var options = Production();
        options.BuildPasswordResetLink("t", "veli@example.test", "Parent", "coaching").Should().StartWith("https://veli.maxkoc.com/auth/reset-password?");
        options.BuildEmailVerificationLink(Guid.NewGuid(), "t", "Parent", null).Should().StartWith("https://veli.maxkoc.com/auth/confirm-email?");
        options.BuildEmailChangeLink(Guid.NewGuid(), "t", "Parent").Should().StartWith("https://veli.maxkoc.com/auth/confirm-email-change?");
        options.BuildLoginLink("Parent", null).Should().Be("https://veli.maxkoc.com/auth/login");
    }

    [Fact]
    public void StudentAndManagementLinksKeepTheirAddresses()
    {
        var options = Production();
        options.BuildLoginLink("Student", "coaching").Should().Be("https://maxkoc.com/auth/login");
        options.BuildLoginLink("Student", "speed-reading").Should().Be("https://masterhizliokuma.com/auth/login");
        options.BuildLoginLink("SystemAdmin", null).Should().Be("https://admin.eduivme.com/auth/login");
    }

    [Fact]
    public void CoachingLinksSendParentPagesToTheParentAddress()
    {
        var options = Production();
        var child = Guid.NewGuid();
        options.CoachingLink($"/coaching-portal/parent/children/{child}").Should().Be($"https://veli.maxkoc.com/coaching-portal/parent/children/{child}");
        options.CoachingLink("/coaching-portal/parent").Should().Be("https://veli.maxkoc.com/coaching-portal/parent");
        options.CoachingLink("/coaching-portal/account/settings").Should().Be("https://maxkoc.com/coaching-portal/account/settings");
        options.CoachingLink("/coaching-portal/parentless").Should().Be("https://maxkoc.com/coaching-portal/parentless");
    }

    [Fact]
    public void StaffLinksOpenTheTeacherOrInstitutionAddress()
    {
        var options = Production();
        options.StaffPortalLink().Should().Be("https://ogretmen.eduivme.com/staff/?product=coaching");
        options.InstitutionPortalLink().Should().Be("https://kurum.eduivme.com/staff/?product=coaching");
    }

    [Fact]
    public void WithoutTheNewAddressesTheLinksStayOnTheCoachingSite()
    {
        var options = new PublicAppUrlOptions { BaseUrl = "https://koc.example.test", StaffPortalUrl = "https://staff.example.test/staff" };
        options.BuildLoginLink("Parent", null).Should().Be("https://koc.example.test/auth/login");
        options.CoachingLink("/coaching-portal/parent").Should().Be("https://koc.example.test/coaching-portal/parent");
        options.InstitutionPortalLink().Should().Be("https://staff.example.test/staff/?product=coaching");
    }
}
