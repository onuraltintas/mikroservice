using System.Net;
using System.Text;
using Coaching.API.Security;
using Coaching.Application.Subscriptions;
using Coaching.Infrastructure.ExternalServices;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Contracts.Events.Billing;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace Identity.API.IntegrationTests;

/// <summary>Koçluk asks the billing service which students may use it (billing stage 8).</summary>
public sealed class CoachingBillingAccessTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);
    private const string Closed = """{"productCode":"coaching","requireSubscription":true,"entitlements":[]}""";
    private const string Entitled = """{"productCode":"coaching","requireSubscription":true,"entitlements":[{"key":"coaching.student","limitValue":null,"endsAt":"2026-12-15T09:00:00Z"}]}""";

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Value { get; set; } = new(Now);
        public override DateTimeOffset GetUtcNow() => Value;
    }

    private sealed class Billing : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public Func<HttpResponseMessage> Answer { get; set; } = () => Json(Closed);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Answer());
        }
        public static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private static (CoachingBillingAccessClient Client, Billing Billing, Clock Clock) Create(bool enabled = true)
    {
        var billing = new Billing();
        var clock = new Clock();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Billing:Enabled"] = enabled ? "true" : "false", ["Services:BillingService"] = "http://billing.test", ["INTERNAL_SERVICE_API_KEY"] = "test-key"
        }).Build();
        return (new CoachingBillingAccessClient(new HttpClient(billing), configuration, new MemoryCache(new MemoryCacheOptions()),
            new CoachingBillingCacheState(), clock, NullLogger<CoachingBillingAccessClient>.Instance), billing, clock);
    }

    [Fact]
    public void While_only_teachers_and_institutions_pay_students_stay_free_and_a_new_student_needs_their_seat()
    {
        // User decision 23.11.2026.
        CoachingBillingRules.Decide(new("coaching", false, [], null, true), Now)
            .Should().Be(new CoachingBillingDecision(true, false, null, true, true));
        CoachingBillingRules.Decide(new("coaching", false, []), Now).StaffSeatsRequired.Should().BeFalse();
        var teacher = Guid.NewGuid();
        var institution = Guid.NewGuid();
        Coaching.Infrastructure.Profiles.CoachingInvitationStore.SeatOwner("Student", null, teacher, false)
            .Should().Be(CoachingSeatOwner.Teacher(teacher));
        Coaching.Infrastructure.Profiles.CoachingInvitationStore.SeatOwner("Student", institution, teacher, true)
            .Should().Be(CoachingSeatOwner.Institution(institution));
        // An institution teacher invites a student who already is the institution's: no new seat. A teacher takes none.
        Coaching.Infrastructure.Profiles.CoachingInvitationStore.SeatOwner("Student", institution, teacher, false).Should().BeNull();
        Coaching.Infrastructure.Profiles.CoachingInvitationStore.SeatOwner("Teacher", institution, null, true).Should().BeNull();
    }

    [Fact]
    public void A_student_has_Kocluk_with_a_running_student_entitlement_whether_bought_or_given_with_a_seat()
    {
        CoachingBillingRules.Decide(null, Now).Should().Be(new CoachingBillingDecision(true, false, null, true));
        CoachingBillingRules.Decide(new("coaching", false, []), Now).Allowed.Should().BeTrue();
        CoachingBillingRules.Decide(new("coaching", true, [new("coaching.teacher", null, Now.AddDays(5))]), Now).Allowed.Should().BeFalse();
        CoachingBillingRules.Decide(new("coaching", true, [new("coaching.student", null, Now)]), Now).Allowed.Should().BeFalse();
        CoachingBillingRules.Decide(new("coaching", true, [new("coaching.student", null, Now.AddDays(30))]), Now)
            .Should().Be(new CoachingBillingDecision(true, true, Now.AddDays(30), true));

        var open = CoachingBillingRules.Summary(new(true, false, null, true));
        (open.HasAccess, open.EnforcementEnabled, open.Status).Should().Be((true, false, null));
        var active = CoachingBillingRules.Summary(new(true, true, Now.AddDays(30), true));
        (active.HasAccess, active.EnforcementEnabled, active.Status, active.AccessUntil).Should().Be((true, true, "Active", Now.AddDays(30)));
        CoachingBillingRules.Summary(new(false, true, null, true)).HasAccess.Should().BeFalse();
    }

    [Fact]
    public async Task Billing_is_asked_with_the_service_key_and_a_change_there_makes_the_next_question_go_to_billing()
    {
        var (client, billing, _) = Create();
        (await client.GetAsync(User)).Allowed.Should().BeFalse();
        var request = billing.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.ToString().Should().Be($"http://billing.test/internal/billing/v1/entitlements?userId={User:D}&product=coaching");
        request.Headers.GetValues("X-Internal-Service-Key").Should().Equal("test-key");

        billing.Answer = () => Billing.Json(Entitled);
        (await client.GetAsync(User)).Allowed.Should().BeFalse("the first answer is still fresh");
        await new CoachingBillingEntitlementsChangedConsumer(client).Consume(Context(new BillingEntitlementsChangedV1(User, ["speed-reading"], Now)));
        (await client.GetAsync(User)).Allowed.Should().BeFalse("another product's change is not ours");
        await new CoachingBillingEntitlementsChangedConsumer(client).Consume(Context(new BillingEntitlementsChangedV1(User, ["coaching"], Now)));
        (await client.GetAsync(User)).Allowed.Should().BeTrue();

        billing.Answer = () => Billing.Json("""{"productCode":"coaching","requireSubscription":false,"entitlements":[]}""");
        await new CoachingBillingSettingsChangedConsumer(client).Consume(Context(new BillingProductSettingsChangedV1("coaching", false, Now)));
        (await client.GetAsync(User)).Should().Be(new CoachingBillingDecision(true, false, null, true));
    }

    [Fact]
    public async Task When_billing_is_away_the_last_answer_stands_and_an_unknown_student_is_let_in()
    {
        var (client, billing, clock) = Create();
        billing.Answer = () => Billing.Json(Entitled);
        (await client.GetAsync(User)).Allowed.Should().BeTrue();
        billing.Answer = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        client.ForgetAll();
        (await client.GetAsync(User)).Should().Be(new CoachingBillingDecision(true, true, new DateTime(2026, 12, 15, 9, 0, 0, DateTimeKind.Utc), true));
        clock.Value = clock.Value.AddDays(31);
        (await client.GetAsync(User)).Allowed.Should().BeFalse("the remembered entitlement has ended");
        (await client.GetAsync(Guid.NewGuid())).Should().Be(new CoachingBillingDecision(true, false, null, false));
    }

    [Fact]
    public async Task Where_billing_runs_it_answers_for_the_student_the_parent_and_the_onboarding_alike()
    {
        var (client, billing, _) = Create();
        billing.Answer = () => Billing.Json(Entitled);
        // The service's own records are not looked at: no database is given.
        var service = new CoachingSubscriptionService(null!, client);
        var access = await service.GetMyAccessAsync(User);
        (access.HasAccess, access.EnforcementEnabled, access.Subscriptions.Count).Should().Be((true, true, 0));
        billing.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task The_middleware_keeps_no_note_of_its_own_where_billing_decides()
    {
        var (client, billing, _) = Create();
        billing.Answer = () => Billing.Json(Entitled);
        var service = new CoachingSubscriptionService(null!, client);
        var cache = new MemoryCache(new MemoryCacheOptions());
        async Task<int> CallAsync()
        {
            var context = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddSingleton<IMemoryCache>(cache).BuildServiceProvider(),
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", User.ToString()), new Claim(ClaimTypes.Role, "Student")], "test", ClaimTypes.Name, ClaimTypes.Role))
            };
            context.Request.Path = "/api/coaching/assignments";
            context.Response.Body = new MemoryStream();
            await new CoachingSubscriptionAccessMiddleware(_ => Task.CompletedTask).InvokeAsync(context, service, client);
            return context.Response.StatusCode;
        }
        (await CallAsync()).Should().Be(200);
        cache.Count.Should().Be(0);

        // Billing takes the seat away and says so: the very next request is refused.
        billing.Answer = () => Billing.Json(Closed);
        client.Forget(User);
        (await CallAsync()).Should().Be(403);
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var context = System.Reflection.DispatchProxy.Create<ConsumeContext<T>, ContextProxy<T>>();
        ((ContextProxy<T>)(object)context).Value = message;
        return context;
    }

    public class ContextProxy<T> : System.Reflection.DispatchProxy where T : class
    {
        public T Value { get; set; } = null!;
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "get_Message" ? Value : throw new NotSupportedException(targetMethod?.Name);
    }
}
