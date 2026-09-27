using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.API.Controllers;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingTeacherStudentApiSecurityTests
{
    [Fact]
    public void Teacher_roster_requires_speed_reading_teacher_role_and_reporting_permission()
    {
        var controller = typeof(SpeedReadingTeacherStudentsController);

        controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .Contain(attribute => attribute.Roles == "Teacher");
        controller.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == PlatformPermissions.SpeedReading.ReportView);
        controller.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Should()
            .Contain(attribute => attribute.Template == "api/speed-reading/teachers/me/students");
    }

    [Fact]
    public void Institution_teacher_student_management_requires_product_institution_management_scope()
    {
        var controller = typeof(SpeedReadingInstitutionTeacherStudentsController);

        controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .Contain(attribute => attribute.Roles == "SystemAdmin,InstitutionAdmin,InstitutionOwner");
        controller.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == PlatformPermissions.Institutions.Manage);
        controller.GetCustomAttributes(typeof(MfaCategoryAttribute), inherit: true)
            .Cast<MfaCategoryAttribute>()
            .Should()
            .Contain(attribute => attribute.Category == MfaOperationCategories.SpeedReading);
    }
}
