using System.Net;
using System.Text;
using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Contracts.Events.Billing;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.ExternalServices;

namespace Identity.API.IntegrationTests;

/// <summary>Hızlı Okuma asks the billing service who may train (billing stage 7).</summary>
public sealed class SpeedReadingBillingAccessTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Value { get; set; } = new(Now);
        public override DateTimeOffset GetUtcNow() => Value;
    }

    private sealed class Billing : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public Func<HttpResponseMessage> Answer { get; set; } = () => Json("""{"productCode":"speed-reading","requireSubscription":true,"entitlements":[]}""");
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Answer());
        }
        public static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        public static string Entitled(string until = "2026-12-15T09:00:00Z") =>
            $$"""{"productCode":"speed-reading","requireSubscription":true,"entitlements":[{"key":"speed-reading.student","limitValue":null,"endsAt":"{{until}}"}]}""";
    }

    private static (BillingAccessClient Client, Billing Billing, Clock Clock) Create(bool enabled = true, string? key = "test-key")
    {
        var billing = new Billing();
        var clock = new Clock();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Billing:Enabled"] = enabled ? "true" : "false", ["Services:BillingService"] = "http://billing.test/", ["INTERNAL_SERVICE_API_KEY"] = key
        }).Build();
        var client = new BillingAccessClient(new HttpClient(billing), configuration, new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            new BillingAccessCacheState(), clock, NullLogger<BillingAccessClient>.Instance);
        return (client, billing, clock);
    }

    [Fact]
    public void The_rules_open_a_product_that_asks_for_no_subscription_and_otherwise_look_for_a_running_student_entitlement()
    {
        SpeedReadingBillingRules.Decide(null, Now).Should().Be(new BillingAccessDecision(true, false, null, true));
        SpeedReadingBillingRules.Decide(new("speed-reading", false, []), Now).Allowed.Should().BeTrue();
        SpeedReadingBillingRules.Decide(new("speed-reading", true, null), Now).Allowed.Should().BeFalse();
        SpeedReadingBillingRules.Decide(new("speed-reading", true, [new("speed-reading.institution", null, Now.AddDays(5))]), Now).Allowed.Should().BeFalse();
        SpeedReadingBillingRules.Decide(new("speed-reading", true, [new("speed-reading.student", null, Now)]), Now).Allowed.Should().BeFalse();
        SpeedReadingBillingRules.Decide(new("speed-reading", true,
                [new("speed-reading.student", null, Now.AddDays(5)), new("speed-reading.student", null, Now.AddDays(40))]), Now)
            .Should().Be(new BillingAccessDecision(true, true, Now.AddDays(40), true));
        SpeedReadingBillingRules.Summary(new(true, true, null, true)).Should().BeEquivalentTo(new UserAccessSummary(["hizliokuma"], true, false));
        SpeedReadingBillingRules.Summary(new(false, true, null, true)).HasSpeedReading.Should().BeFalse();
    }

    [Fact]
    public async Task Billing_is_asked_with_the_service_key_and_the_answer_is_used_for_a_minute()
    {
        var (client, billing, _) = Create();
        billing.Answer = () => Billing.Json(Billing.Entitled());
        (await client.GetAsync(User)).Should().Be(new BillingAccessDecision(true, true, new DateTime(2026, 12, 15, 9, 0, 0, DateTimeKind.Utc), true));
        (await client.GetAsync(User)).Allowed.Should().BeTrue();
        var request = billing.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.ToString().Should().Be($"http://billing.test/internal/billing/v1/entitlements?userId={User:D}&product=speed-reading");
        request.Headers.GetValues("X-Internal-Service-Key").Should().Equal("test-key");

        BillingAccessClient.FreshFor.Should().Be(TimeSpan.FromMinutes(1));
        BillingAccessClient.RememberedFor.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public async Task A_change_of_entitlements_or_of_the_switch_makes_the_next_question_go_to_billing()
    {
        var (client, billing, _) = Create();
        (await client.GetAsync(User)).Allowed.Should().BeFalse();
        billing.Answer = () => Billing.Json(Billing.Entitled());
        (await client.GetAsync(User)).Allowed.Should().BeFalse("the first answer is still fresh");

        await new SpeedReadingBillingEntitlementsChangedConsumer(client).Consume(Context(new BillingEntitlementsChangedV1(User, ["coaching"], Now)));
        (await client.GetAsync(User)).Allowed.Should().BeFalse("another product's change is not ours");
        await new SpeedReadingBillingEntitlementsChangedConsumer(client).Consume(Context(new BillingEntitlementsChangedV1(User, ["coaching", "speed-reading"], Now)));
        (await client.GetAsync(User)).Allowed.Should().BeTrue();

        billing.Answer = () => Billing.Json("""{"productCode":"speed-reading","requireSubscription":false,"entitlements":[]}""");
        await new SpeedReadingBillingSettingsChangedConsumer(client).Consume(Context(new BillingProductSettingsChangedV1("coaching", false, Now)));
        (await client.GetAsync(User)).RequireSubscription.Should().BeTrue();
        await new SpeedReadingBillingSettingsChangedConsumer(client).Consume(Context(new BillingProductSettingsChangedV1("speed-reading", false, Now)));
        (await client.GetAsync(User)).Should().Be(new BillingAccessDecision(true, false, null, true));
    }

    [Fact]
    public async Task When_billing_is_away_the_last_answer_stands_until_its_entitlement_runs_out_and_an_unknown_user_is_let_in()
    {
        var (client, billing, clock) = Create();
        billing.Answer = () => Billing.Json(Billing.Entitled("2026-11-15T12:00:00Z"));
        (await client.GetAsync(User)).Allowed.Should().BeTrue();

        billing.Answer = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        client.ForgetAll();
        (await client.GetAsync(User)).Should().Be(new BillingAccessDecision(true, true, new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc), true));
        clock.Value = clock.Value.AddHours(4);
        (await client.GetAsync(User)).Allowed.Should().BeFalse("the remembered entitlement ended at noon");

        // Somebody billing was never asked about trains: an outage stops purchases, not training.
        (await client.GetAsync(Guid.NewGuid())).Should().Be(new BillingAccessDecision(true, false, null, false));
        billing.Answer = () => throw new HttpRequestException("connection refused");
        (await client.GetAsync(Guid.NewGuid())).Known.Should().BeFalse();
    }

    [Fact]
    public async Task A_product_billing_does_not_sell_is_open_and_a_deployment_without_the_key_fails_instead_of_opening()
    {
        var (client, billing, _) = Create();
        billing.Answer = () => new HttpResponseMessage(HttpStatusCode.NotFound);
        (await client.GetAsync(User)).Should().Be(new BillingAccessDecision(true, false, null, true));

        var (keyless, _, _) = Create(key: null);
        await FluentActions.Awaiting(() => keyless.GetAsync(User)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Without_billing_the_products_own_records_decide_and_with_it_billing_does()
    {
        var (disabled, untouched, _) = Create(enabled: false);
        disabled.Enabled.Should().BeFalse();
        var answer = new UserAccessSummary(["hizliokuma", "kocluk"], true, true);
        var own = System.Reflection.DispatchProxy.Create<ISpeedReadingSubscription, OwnRecords>();
        ((OwnRecords)(object)own).Answer = answer;
        (await new SpeedReadingAccess(disabled, own).GetAsync(User)).Should().BeSameAs(answer);
        untouched.Requests.Should().BeEmpty();

        var (enabled, billing, _) = Create();
        (await new SpeedReadingAccess(enabled, own).GetAsync(User)).Should().BeEquivalentTo(new UserAccessSummary([], false, false));
        billing.Requests.Should().ContainSingle();
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

    public class OwnRecords : System.Reflection.DispatchProxy
    {
        public UserAccessSummary Answer { get; set; } = null!;
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(ISpeedReadingSubscription.GetMyAccessAsync) ? Task.FromResult(Answer) : throw new NotSupportedException(targetMethod?.Name);
    }
}
