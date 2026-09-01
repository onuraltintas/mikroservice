using FluentAssertions;

namespace SpeedReading.Application.UnitTests;

public sealed class AdminConfigurationSecurityMetadataTests
{
    [Fact]
    public void Age_group_management_requires_the_shared_settings_permission()
    {
        var source = ReadController("AgeGroupConfigurationsController.cs");

        source.Should().Contain("[HasPermission(PlatformPermissions.SpeedReading.SettingsManage)]");
        source.Should().Contain("[HttpPost]");
        source.Should().Contain("[HttpPut(\"{id:guid}\")]");
        source.Should().Contain("[HttpDelete(\"{id:guid}\")]");
    }

    [Fact]
    public void Assessment_template_management_requires_the_shared_settings_permission()
    {
        var source = ReadController("AssessmentAdminController.cs");

        source.Should().Contain("[Route(\"api/speed-reading/admin/assessment-templates\")]");
        source.Should().Contain("[HasPermission(PlatformPermissions.SpeedReading.SettingsManage)]");
        source.Should().Contain("[HttpPost]");
        source.Should().Contain("[HttpPut(\"{id:guid}\")]");
        source.Should().Contain("[HttpDelete(\"{id:guid}\")]");
    }

    [Theory]
    [InlineData("AnnouncementsController.cs")]
    [InlineData("EmailTemplatesController.cs")]
    [InlineData("EmailCampaignsController.cs")]
    [InlineData("NotificationsController.cs")]
    public void Communication_admin_surfaces_require_the_shared_communications_permission(string fileName)
    {
        var source = ReadController(fileName);

        source.Should().Contain("[HasPermission(PlatformPermissions.SpeedReading.CommunicationsManage)]");
    }

    private static string ReadController(string fileName)
    {
        var repositoryRoot = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            repositoryRoot,
            "services",
            "speed-reading-service",
            "SpeedReading.API",
            "Controllers",
            fileName));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
}
