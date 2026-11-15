using System.Net;
using System.Net.Http.Json;
using CourseManagement.API;
using EduPlatform.Shared.Contracts.Billing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CourseManagement.IntegrationTests;

/// <summary>Where billing runs, a workspace's plan and student limit are the owner's entitlement there (billing stage 9).</summary>
public sealed class WorkspaceBillingPlansTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Owner = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static BillingProductAccessResponse Access(bool required, params BillingGrantedEntitlement[] entitlements) =>
        new(BillingProductCodes.CourseManagement, required, entitlements);

    [Fact]
    public void A_product_that_asks_for_no_subscription_limits_nothing()
    {
        foreach (var plan in new[] { WorkspaceBillingPlans.Decide(null, Now), WorkspaceBillingPlans.Decide(Access(false), Now) })
        {
            Assert.Equal(WorkspaceBillingPlan.Open, plan);
            Assert.True(plan.CanWriteAt(Now));
            Assert.True(plan.CanHold(10_000));
            Assert.False(plan.HasPlan);
        }
    }

    [Fact]
    public void The_owners_entitlement_sets_the_limit_and_the_end()
    {
        var plan = WorkspaceBillingPlans.Decide(Access(true,
            new BillingGrantedEntitlement("course-management.workspace", 40, Now.AddDays(20)),
            new BillingGrantedEntitlement("course-management.workspace", 25, Now.AddDays(50)),
            new BillingGrantedEntitlement("course-management.workspace", 500, Now.AddDays(-1)),
            new BillingGrantedEntitlement("coaching.student", null, Now.AddDays(90))), Now);

        Assert.Equal((true, 40, new DateTimeOffset(Now.AddDays(50))), (plan.HasPlan, plan.ActiveStudentLimit, plan.EndsAt));
        Assert.True(plan.CanWriteAt(Now.AddDays(49)));
        Assert.False(plan.CanWriteAt(Now.AddDays(50)));
        Assert.True(plan.CanHold(40));
        Assert.False(plan.CanHold(41));
    }

    [Fact]
    public void A_plan_without_a_limit_holds_any_number_of_students()
    {
        var plan = WorkspaceBillingPlans.Decide(Access(true,
            new BillingGrantedEntitlement("course-management.workspace", 10, Now.AddDays(5)),
            new BillingGrantedEntitlement("course-management.workspace", null, Now.AddDays(5))), Now);
        Assert.Null(plan.ActiveStudentLimit);
        Assert.True(plan.CanHold(5_000));
    }

    [Fact]
    public void An_owner_without_a_plan_keeps_the_workspace_read_only_when_a_subscription_is_required()
    {
        var plan = WorkspaceBillingPlans.Decide(Access(true, new BillingGrantedEntitlement("course-management.workspace", 40, Now.AddMinutes(-1))), Now);
        Assert.False(plan.HasPlan);
        Assert.False(plan.CanWriteAt(Now));
        Assert.False(plan.CanHold(1));
    }

    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Answer { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Answer(request));
        }
    }

    private static (WorkspaceBillingPlans Plans, Handler Handler, FakeClock Clock) Create(bool enabled = true, string? key = "service-key")
    {
        var handler = new Handler();
        var clock = new FakeClock(new DateTimeOffset(Now));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Billing:Enabled"] = enabled ? "true" : "false",
            ["Services:BillingService"] = "http://billing-service:8080/",
            ["INTERNAL_SERVICE_API_KEY"] = key
        }).Build();
        var cache = new MemoryCache(new MemoryCacheOptions { Clock = new CacheClock(clock) });
        return (new WorkspaceBillingPlans(new HttpClient(handler), configuration, cache, clock, NullLogger<WorkspaceBillingPlans>.Instance), handler, clock);
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

#pragma warning disable CS0618
    private sealed class CacheClock(TimeProvider clock) : Microsoft.Extensions.Internal.ISystemClock
    {
        public DateTimeOffset UtcNow => clock.GetUtcNow();
    }
#pragma warning restore CS0618

    private static HttpResponseMessage Json(BillingProductAccessResponse access) => new(HttpStatusCode.OK) { Content = JsonContent.Create(access) };

    [Fact]
    public async Task The_owner_is_asked_with_the_service_key_and_the_answer_is_used_for_a_minute()
    {
        var (plans, handler, clock) = Create();
        handler.Answer = _ => Json(Access(true, new BillingGrantedEntitlement("course-management.workspace", 30, Now.AddDays(10))));

        var first = await plans.GetAsync(Owner);
        await plans.GetAsync(Owner);

        Assert.Equal(30, first.ActiveStudentLimit);
        var request = Assert.Single(handler.Requests);
        Assert.Equal($"http://billing-service:8080/internal/billing/v1/entitlements?userId={Owner:D}&product=course-management", request.RequestUri!.ToString());
        Assert.Equal("service-key", request.Headers.GetValues("X-Internal-Service-Key").Single());

        clock.Advance(WorkspaceBillingPlans.FreshFor + TimeSpan.FromSeconds(1));
        await plans.GetAsync(Owner);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task When_billing_is_away_the_last_answer_stands_and_an_owner_never_asked_about_is_not_limited()
    {
        var (plans, handler, clock) = Create();
        handler.Answer = _ => Json(Access(true, new BillingGrantedEntitlement("course-management.workspace", 30, Now.AddDays(10))));
        await plans.GetAsync(Owner);
        clock.Advance(TimeSpan.FromHours(2));
        handler.Answer = _ => throw new HttpRequestException("away");

        Assert.Equal(30, (await plans.GetAsync(Owner)).ActiveStudentLimit);
        Assert.Equal(WorkspaceBillingPlan.Open, await plans.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task A_deployment_that_cannot_ask_fails_instead_of_opening_the_product()
    {
        var (withoutKey, _, _) = Create(key: null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => withoutKey.GetAsync(Owner));
        var (disabled, handler, _) = Create(enabled: false);
        Assert.False(disabled.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => disabled.GetAsync(Owner));
        Assert.Empty(handler.Requests);
    }
}
