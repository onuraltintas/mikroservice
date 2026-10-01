using System.Security.Claims;
using SpeedReading.API.Security;

namespace SpeedReading.Application.UnitTests;

public sealed class StaffTrainingAccessTests
{
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
