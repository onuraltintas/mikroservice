using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeedReading.API.Security;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingSubscriptionAccessMiddlewareTests
{
    [Fact]
    public async Task Staff_training_cannot_be_used_after_account_becomes_a_student()
    {
        using var db = CreateDb();
        var context = CreateContext("/api/speed-reading/daily-progress/today-exercises", "GET", "Student");
        var user = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var template = SpeedReading.Domain.Programs.ProgramTemplate.Import(Guid.NewGuid(), "Staff", "",
            Guid.NewGuid(), 0, 100, "{}", 1, 2, 5, 1, 1, true, 1, 0, null, false, DateTime.UtcNow, null, null, null);
        db.StudentProgramProgresses.Add(SpeedReading.Domain.Programs.StudentProgramProgress.Start(
            Guid.NewGuid(), user, template, 0, 0, user, DateTime.UtcNow, isStaffTraining: true));
        await db.SaveChangesAsync();
        var called = false;
        var middleware = new SpeedReadingSubscriptionAccessMiddleware(_ => { called = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(context, CreateSubscription(true), db);
        Assert.False(called);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/speed-reading/daily-progress/today-exercises", "GET", false, 403)]
    [InlineData("/api/speed-reading/learning-paths/personalized", "GET", false, 403)]
    [InlineData("/api/speed-reading/student-reading/available", "GET", false, 403)]
    [InlineData("/api/speed-reading/assessment/status", "GET", false, 200)]
    [InlineData("/api/speed-reading/adaptive-learning/profile", "PUT", false, 200)]
    [InlineData("/api/speed-reading/adaptive-learning/profile/status", "GET", false, 200)]
    [InlineData("/api/speed-reading/adaptive-learning/profile/settings", "GET", false, 200)]
    [InlineData("/api/speed-reading/adaptive-learning/profile/settings", "GET", true, 200)]
    [InlineData("/api/speed-reading/adaptive-learning/profile/settings", "PUT", false, 403)]
    [InlineData("/api/speed-reading/adaptive-learning/dashboard", "GET", false, 403)]
    [InlineData("/api/speed-reading/subscriptions/my-modules", "GET", false, 200)]
    [InlineData("/api/speed-reading/daily-progress/today-exercises", "GET", true, 200)]
    public async Task StudentAccessFollowsSubscriptionAndFreeRoutePolicy(
        string path, string method, bool active, int expectedStatus)
    {
        using var db = CreateDb();
        var subscription = CreateSubscription(active);
        var context = CreateContext(path, method, "Student");
        var nextCalled = false;
        var middleware = new SpeedReadingSubscriptionAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, subscription, db);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedStatus == 200, nextCalled);
    }

    [Theory]
    [InlineData("{}", 403)]
    [InlineData("{\"assessmentAttemptId\":\"11111111-1111-1111-1111-111111111111\"}", 200)]
    public async Task OnlyAssessmentStartCanBypassExpiredSubscription(string body, int expectedStatus)
    {
        using var db = CreateDb();
        var context = CreateContext("/api/speed-reading/exercise-sessions/start", "POST", "Student");
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        var nextCalled = false;
        var middleware = new SpeedReadingSubscriptionAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateSubscription(false), db);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedStatus == 200, nextCalled);
    }

    [Fact]
    public async Task StaffAccessDoesNotRequireStudentSubscription()
    {
        using var db = CreateDb();
        var context = CreateContext("/api/speed-reading/daily-progress/today-exercises", "GET", "Teacher");
        var nextCalled = false;
        var middleware = new SpeedReadingSubscriptionAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateSubscription(false), db);

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("/api/speed-reading/daily-progress/today-exercises", 403)]
    [InlineData("/api/speed-reading/review/due", 403)]
    [InlineData("/api/speed-reading/adaptive-learning/weak-areas", 403)]
    [InlineData("/api/speed-reading/adaptive-learning/recommendations", 403)]
    [InlineData("/api/speed-reading/adaptive-learning/daily-goal", 403)]
    [InlineData("/api/speed-reading/adaptive-learning/profile/status", 200)]
    [InlineData("/api/speed-reading/vocabulary/user", 403)]
    [InlineData("/api/speed-reading/content-feedback/recommended", 403)]
    [InlineData("/api/speed-reading/analytics/student/summary", 403)]
    [InlineData("/api/speed-reading/series-access/available", 403)]
    [InlineData("/api/speed-reading/series-access/11111111-1111-1111-1111-111111111111/access", 403)]
    [InlineData("/api/speed-reading/series-access/11111111-1111-1111-1111-111111111111/prerequisites", 403)]
    [InlineData("/api/speed-reading/content-feedback/analytics", 403)]
    [InlineData("/api/speed-reading/content-feedback/optimal-hours", 403)]
    [InlineData("/api/speed-reading/content-feedback/retry-needed", 403)]
    [InlineData("/api/speed-reading/gamification/user", 403)]
    [InlineData("/api/speed-reading/gamification/achievements/user", 403)]
    [InlineData("/api/speed-reading/assignments/my-assignments", 403)]
    [InlineData("/api/speed-reading/assignments/teacher-assignments", 200)]
    [InlineData("/api/speed-reading/teachers/me/students", 200)]
    public async Task DualRoleKeepsStaffManagementButRequiresStudentTrainingAccess(string path, int expectedStatus)
    {
        using var db = CreateDb();
        var context = CreateContext(path, "GET", "Student");
        ((ClaimsIdentity)context.User.Identity!).AddClaim(new Claim(ClaimTypes.Role, "Teacher"));
        var middleware = new SpeedReadingSubscriptionAccessMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, CreateSubscription(false), db);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
    }

    private static OwnedSpeedReadingDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DefaultHttpContext CreateContext(string path, string method, string role)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "test"));
        return context;
    }

    private static ISpeedReadingSubscription CreateSubscription(bool active)
    {
        var subscription = DispatchProxy.Create<ISpeedReadingSubscription, SubscriptionProxy>();
        ((SubscriptionProxy)subscription).Active = active;
        return subscription;
    }

    public class SubscriptionProxy : DispatchProxy
    {
        public bool Active { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ISpeedReadingSubscription.GetMyAccessAsync))
                return Task.FromResult(new UserAccessSummary([], Active, false));

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
