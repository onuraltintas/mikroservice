using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.API.Controllers;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Application.UnitTests;

public sealed class NextProgramApiTests
{
    [Fact]
    public async Task Staff_cannot_read_an_unassigned_students_recommendation()
    {
        var service = DispatchProxy.Create<ISpeedReadingStudentProgram, StudentProgramProxy>();
        var access = DispatchProxy.Create<ISpeedReadingTeacherAccess, DeniedTeacherAccess>();
        var controller = new StudentProgramManagementController(service, access)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"))
            } }
        };
        Assert.IsType<ForbidResult>(await controller.GetRecommendation(Guid.NewGuid(), null, CancellationToken.None));
        Assert.Equal(Guid.Empty, ((StudentProgramProxy)service).UserId);
    }

    [Fact]
    public async Task Recommendation_uses_authenticated_identity_and_returns_owned_suggestion()
    {
        var user = Guid.NewGuid();
        var service = DispatchProxy.Create<ISpeedReadingStudentProgram, StudentProgramProxy>();
        var proxy = (StudentProgramProxy)service;
        var controller = new StudentProgramController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], "test"))
            } }
        };
        var result = await controller.GetNextRecommendation(CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(user, proxy.UserId);
    }

    public class StudentProgramProxy : DispatchProxy
    {
        public Guid UserId { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            UserId = (Guid)args![0]!;
            return Task.FromResult<NextStudentProgramRecommendation?>(new(UserId, Guid.NewGuid(), Guid.NewGuid(), "Program", 28, false));
        }
    }

    public class DeniedTeacherAccess : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(false);
    }
}
