using EduPlatform.Shared.Security.Authorization;
using SpeedReading.API.Controllers;

namespace SpeedReading.Application.UnitTests;

public sealed class ProgramApprovalPermissionTests
{
    [Fact]
    public void Approval_requires_a_student_scoped_write_permission_not_report_or_catalog_permissions()
    {
        var permission = typeof(StudentProgramManagementController).GetMethod("Approve")!
            .GetCustomAttributes(typeof(HasPermissionAttribute), false).Cast<HasPermissionAttribute>().SingleOrDefault();
        Assert.Equal("Permissions.SpeedReading.StudentProgramApprove", permission?.Permission);
    }
}
