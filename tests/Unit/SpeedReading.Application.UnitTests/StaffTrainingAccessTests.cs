using System.Security.Claims;
using SpeedReading.API.Security;
using SpeedReading.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SpeedReading.Application.UnitTests;

public sealed class StaffTrainingAccessTests
{
    [Fact]
    public async Task Student_with_teacher_role_cannot_call_staff_enrollment_endpoint()
    {
        var controller = new StaffTrainingController(null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "Teacher"), new Claim(ClaimTypes.Role, "Student")], "test"))
            } }
        };
        Assert.IsType<ForbidResult>(await controller.Start(new StartStudentProgramRequest(Guid.NewGuid()), CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.GetPrograms(CancellationToken.None));
    }

    [Theory]
    [InlineData("Admin", false, true)]
    [InlineData("SystemAdmin", false, true)]
    [InlineData("Teacher", false, true)]
    [InlineData("Editor", false, false)]
    [InlineData("InstitutionAdmin", false, false)]
    [InlineData("Student", false, false)]
    [InlineData("Teacher", true, false)]
    [InlineData("Admin", true, false)]
    public void Staff_training_is_only_for_non_student_admins_and_teachers(string role, bool student, bool allowed)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test");
        if (student) identity.AddClaim(new Claim(ClaimTypes.Role, "Student"));
        Assert.Equal(allowed, StaffTrainingAccess.IsAllowed(new ClaimsPrincipal(identity)));
    }
}
