using Coaching.Application.Content;
using Coaching.Infrastructure.Data;
using Coaching.Application.Subscriptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCmsAndSubscriptionRulesTests
{
    [Fact]
    public void CmsStarterContentMigration_SeedsCoachingPagesAndNavigation()
    {
        using var context = new CoachingDbContext(
            new DbContextOptionsBuilder<CoachingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        var script = context.GetService<IMigrator>().GenerateScript("20260928082116_AddCoachingCmsMedia");

        script.Should()
            .Contain("INSERT INTO coaching.cms_entries")
            .And.Contain("nasil-calisir")
            .And.Contain("sikca-sorulan-sorular")
            .And.Contain("ogrenciler")
            .And.Contain("ogretmenler")
            .And.Contain("kurumlar")
            .And.Contain("home-hero")
            .And.Contain("home-process-hedef")
            .And.Contain("hedefi-calisma-planina-donusturmek")
            .And.Contain("INSERT INTO coaching.cms_navigation_items");
    }

    [Fact]
    public void CmsPage_IsPublicOnlyAfterPublicationTime()
    {
        var now = new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);

        CoachingCmsPublicationRules.IsPubliclyAvailable(false, null, now).Should().BeFalse();
        CoachingCmsPublicationRules.IsPubliclyAvailable(true, now.AddSeconds(1), now).Should().BeFalse();
        CoachingCmsPublicationRules.IsPubliclyAvailable(true, now, now).Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true, false, false, true)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, false, false, false, true)]
    [InlineData(true, true, true, false, true)]
    public void SubscriptionAccess_PreservesFreeAndStaffAccess(
        bool enforcementEnabled,
        bool isStudent,
        bool isStaff,
        bool hasActiveSubscription,
        bool expected)
    {
        CoachingSubscriptionAccessRules.HasAccess(
            enforcementEnabled,
            isStudent,
            isStaff,
            hasActiveSubscription).Should().Be(expected);
    }

    [Theory]
    [InlineData("/api/coaching/assignments/student/123", true)]
    [InlineData("/api/coaching/subscription-plans", false)]
    [InlineData("/api/coaching/cms/pages/about", false)]
    [InlineData("/health/ready", false)]
    public void SubscriptionAccess_OnlyProtectsCoachingLearningEndpoints(string path, bool expected)
    {
        CoachingSubscriptionAccessRules.RequiresSubscription(path).Should().Be(expected);
    }

    [Fact]
    public void NormalizeSlug_UsesAsciiAndPreservesWordBoundaries()
    {
        CoachingManagementRules.NormalizeSlug(" Koçluk Paketleri ve Öğretmen ")
            .Should().Be("kocluk-paketleri-ve-ogretmen");
    }

    [Theory]
    [InlineData("image/png", "hero.png", true)]
    [InlineData("image/jpeg", "hero.jpg", true)]
    [InlineData("image/webp", "hero.webp", true)]
    [InlineData("image/svg+xml", "hero.svg", false)]
    [InlineData("image/png", "payload.exe", false)]
    [InlineData("image/png", "../hero.png", false)]
    public void CmsMediaPolicy_AllowsSupportedImageTypesAndRejectsUnsafeNames(
        string contentType,
        string fileName,
        bool expected)
    {
        var validate = () => CoachingCmsMediaPolicy.GetValidatedExtension(contentType, fileName, 8);
        if (expected)
        {
            validate().Should().NotBeNullOrWhiteSpace();
        }
        else
        {
            validate.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void CmsMediaPolicy_RejectsEmptyAndOversizedImages()
    {
        var empty = () => CoachingCmsMediaPolicy.GetValidatedExtension("image/png", "hero.png", 0);
        var oversized = () => CoachingCmsMediaPolicy.GetValidatedExtension("image/png", "hero.png", CoachingCmsMediaPolicy.MaxFileSizeBytes + 1);

        empty.Should().Throw<ArgumentOutOfRangeException>();
        oversized.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CmsMediaPolicy_ValidatesImageSignatures()
    {
        CoachingCmsMediaPolicy.HasValidSignature("image/png", [137, 80, 78, 71, 13, 10, 26, 10]).Should().BeTrue();
        CoachingCmsMediaPolicy.HasValidSignature("image/png", [0, 1, 2, 3, 4, 5, 6, 7]).Should().BeFalse();
        CoachingCmsMediaPolicy.HasValidSignature("image/jpeg", [255, 216, 255]).Should().BeTrue();
    }

    [Theory]
    [InlineData("TR330006100519786457841326", true)]
    [InlineData("TR330006100519786457841327", false)]
    [InlineData("TR33 0006 1005 1978 6457 8413 26", true)]
    public void IbanValidation_ChecksFormattingAndChecksum(string iban, bool expected)
    {
        CoachingManagementRules.IsValidIban(iban).Should().Be(expected);
    }

    [Theory]
    [InlineData("Teacher", 10, true)]
    [InlineData("Teacher", null, false)]
    [InlineData("Individual", null, true)]
    [InlineData("Individual", 10, false)]
    public void SubscriptionPlanAudience_RequiresSeatsOnlyForTeacherAndInstitutionPlans(
        string audience,
        int? seats,
        bool expected)
    {
        CoachingManagementRules.IsValidPlan(audience, 100m, false, "Monthly", 30, seats)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("/coaching/about", true)]
    [InlineData("https://example.com/terms", true)]
    [InlineData("//example.com/redirect", false)]
    [InlineData("javascript:alert(1)", false)]
    public void NavigationUrl_RejectsProtocolRelativeAndScriptUrls(string url, bool expected)
    {
        CoachingManagementRules.IsSafeNavigationUrl(url).Should().Be(expected);
    }
}
