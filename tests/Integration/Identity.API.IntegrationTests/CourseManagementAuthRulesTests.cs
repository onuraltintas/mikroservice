using Identity.API.Security;
using Microsoft.AspNetCore.Http;

namespace Identity.API.IntegrationTests;

public sealed class CourseManagementAuthRulesTests
{
    [Fact]
    public void CourseLoginRequiresTheSharedLoginCaptcha()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/auth/course-management/login";
        Assert.Equal("auth_login", AuthRecaptchaRules.GetRequiredAction(context.Request));
    }

    [Fact]
    public void CourseReadRequestsDoNotBecomeCaptchaProtected()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/auth/course-management/login";
        Assert.Null(AuthRecaptchaRules.GetRequiredAction(context.Request));
    }
}
