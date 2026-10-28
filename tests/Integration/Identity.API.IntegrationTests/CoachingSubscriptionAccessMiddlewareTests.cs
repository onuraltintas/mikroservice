using System.Reflection;
using System.Security.Claims;
using Coaching.API.Security;
using Coaching.Application.Subscriptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingSubscriptionAccessMiddlewareTests
{
    [Theory]
    [InlineData("/api/assignments/student/123", false, "Student", 403)]
    [InlineData("/api/assignments/123/submit", false, "Student", 403)]
    [InlineData("/api/sessions/student/123", false, "Student", 403)]
    [InlineData("/api/exams/student/123", false, "Student", 403)]
    [InlineData("/api/goals/123/progress", false, "Student", 403)]
    [InlineData("/api/reports/student/123/progress", false, "Student", 403)]
    [InlineData("/api/calendar/student.ics", false, "Student", 403)]
    [InlineData("/api/coaching/study-planning/plans", false, "Student", 403)]
    [InlineData("/api/assignments/student/123", true, "Student", 200)]
    [InlineData("/api/coaching/subscriptions/my-access", false, "Student", 200)]
    [InlineData("/api/coaching/subscriptions/bank-transfer-requests", false, "Student", 200)]
    [InlineData("/api/coaching-agreements", false, "Student", 200)]
    [InlineData("/api/data-privacy/export", false, "Student", 200)]
    [InlineData("/api/assignments/teacher/123", false, "Teacher", 200)]
    public async Task LearningAccessBlocksExpiredStudentsAndPreservesRenewalAndStaffAccess(
        string path, bool active, string role, int status)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "test"));
        var subscription = DispatchProxy.Create<ICoachingSubscription, SubscriptionProxy>();
        ((SubscriptionProxy)subscription).Active = active;
        var called = false;
        var middleware = new CoachingSubscriptionAccessMiddleware(_ => { called = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(context, subscription);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(status == 200, called);
        if (status == 403)
        {
            context.Response.Body.Position = 0;
            Assert.Contains("CoachingSubscriptionRequired", await new StreamReader(context.Response.Body).ReadToEndAsync());
        }
    }

    // Roadmap D1: an allowed student is not re-checked for a minute; a refused one is checked on every request so a
    // payment that was just approved opens access at once.
    [Fact]
    public async Task AllowedAccessIsRememberedBrieflyButRefusalIsNot()
    {
        var services = new ServiceCollection().AddLogging().AddMemoryCache().BuildServiceProvider();
        var user = Guid.NewGuid().ToString();
        var subscription = DispatchProxy.Create<ICoachingSubscription, SubscriptionProxy>(); var proxy = (SubscriptionProxy)subscription;
        async Task<int> Request()
        {
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Path = "/api/assignments/student/123"; context.Response.Body = new MemoryStream();
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user), new Claim(ClaimTypes.Role, "Student")], "test"));
            await new CoachingSubscriptionAccessMiddleware(_ => Task.CompletedTask).InvokeAsync(context, subscription);
            return context.Response.StatusCode;
        }
        proxy.Active = false;
        Assert.Equal(403, await Request()); Assert.Equal(403, await Request()); Assert.Equal(2, proxy.Calls);
        proxy.Active = true;
        Assert.Equal(200, await Request()); Assert.Equal(200, await Request()); Assert.Equal(3, proxy.Calls);
    }

    public class SubscriptionProxy : DispatchProxy
    {
        public bool Active { get; set; }
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(ICoachingSubscription.GetMyAccessAsync)) throw new NotSupportedException(targetMethod?.Name);
            Calls++;
            return Task.FromResult(new CoachingSubscriptionAccessSummary(Active, true, null, null, null, []));
        }
    }
}
