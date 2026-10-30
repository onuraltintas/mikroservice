using System.Reflection;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.RateLimiting;
using SpeedReading.API.Controllers;

namespace Identity.API.IntegrationTests;

/// <summary>Review H3 (findings 26, 27): teacher analytics asks for MFA like its admin/institution twins; document
/// exports share a bounded queue so a few teachers cannot saturate the small server.</summary>
public sealed class SpeedReadingExportAndMfaMetadataTests
{
    [Fact]
    public void Teacher_analytics_requires_mfa_like_the_admin_and_institution_versions()
    {
        typeof(TeacherAnalyticsController).GetCustomAttribute<MfaCategoryAttribute>()!.Category
            .Should().Be(MfaOperationCategories.SpeedReading);
    }

    [Theory]
    [InlineData(typeof(ReportsController), "ExportSnapshot")]
    [InlineData(typeof(ReadingTextsController), "ExportToPdf")]
    [InlineData(typeof(ReadingTextsController), "ExportToDocx")]
    [InlineData(typeof(ReadingTextsController), "ExportMultipleToPdf")]
    [InlineData(typeof(ReadingTextsController), "ExportMultipleToDocx")]
    [InlineData(typeof(VocabularyController), "Export")]
    public void Document_exports_go_through_the_shared_export_limit(Type controller, string action)
    {
        var method = controller.GetMethod(action, BindingFlags.Instance | BindingFlags.Public);
        method.Should().NotBeNull($"{controller.Name}.{action} exists");
        method!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName.Should().Be("document-export");
        method.GetCustomAttribute<EnableRateLimitingAttribute>().Should().NotBeNull();
    }
}
