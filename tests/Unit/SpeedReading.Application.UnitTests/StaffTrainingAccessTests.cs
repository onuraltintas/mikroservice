using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Domain.Programs;
using System.Security.Claims;
using SpeedReading.API.Security;
using SpeedReading.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SpeedReading.Application.UnitTests;

public sealed class StaffTrainingAccessTests
{
    [Theory]
    [InlineData("Admin", true, false, true)]
    [InlineData("SystemAdmin", true, false, true)]
    [InlineData("Admin", false, false, false)]
    [InlineData("Admin", true, true, false)]
    [InlineData("Editor", true, false, false)]
    public async Task Preview_guard_allows_only_own_active_staff_training(string role, bool ownTraining, bool student, bool allowed)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var userId = Guid.NewGuid();
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Test", "Test", Guid.NewGuid(), 0, 100, "{}", 1, 2, 2, 4, 28, true, 1, 0, null, false, DateTime.UtcNow, "test", null, null);
        var owner = ownTraining ? userId : Guid.NewGuid();
        db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), owner, template, 0, 0, owner, DateTime.UtcNow, isStaffTraining: true));
        await db.SaveChangesAsync();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)], "test");
        if (student) identity.AddClaim(new Claim(ClaimTypes.Role, "Student"));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        context.Request.Method = "POST";
        context.Request.Path = "/api/speed-reading/exercise-sessions/start";
        context.Response.Body = new MemoryStream();
        var called = false;
        await new ExercisePreviewMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, db);
        Assert.Equal(allowed, called);
        Assert.Equal(allowed ? 200 : 403, context.Response.StatusCode);
    }

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
