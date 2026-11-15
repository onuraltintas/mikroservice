using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.Subscriptions;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.ExternalServices;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

/// <summary>Koçluk gives and takes seats at the billing service where it runs (billing stage 8).</summary>
[Collection("Database")]
public sealed class CoachingBillingSeatsTests(PostgresFixture postgres)
{
    private static readonly Guid Teacher = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid Institution = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Student = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed class Billing : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string? Body)> Requests { get; } = [];
        public Func<HttpResponseMessage> Answer { get; set; } = () => new HttpResponseMessage(HttpStatusCode.NotFound);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.GetValues("X-Internal-Service-Key").Should().Equal("test-key");
            Requests.Add((request.Method, request.RequestUri!.ToString(), request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return Answer();
        }
        public static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body, Web), Encoding.UTF8, "application/json") };
    }

    private sealed class Scope(CoachingAdminScope scope) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(scope);
    }

    private sealed class Actor : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public IEnumerable<string> Roles => ["InstitutionAdmin"];
        public string? Email => null; public string? FullName => null;
        public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
    }

    private sealed class Forgetting : ICoachingBillingAccess
    {
        public List<Guid> Forgotten { get; } = [];
        public bool Enabled => true;
        public Task<CoachingBillingDecision> GetAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Forget(Guid userId) => Forgotten.Add(userId);
        public void ForgetAll() => throw new NotSupportedException();
    }

    private static (CoachingBillingSeatsClient Client, Billing Billing) Create(bool enabled = true)
    {
        var billing = new Billing();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Billing:Enabled"] = enabled ? "true" : "false", ["Services:BillingService"] = "http://billing.test/", ["INTERNAL_SERVICE_API_KEY"] = "test-key"
        }).Build();
        return (new CoachingBillingSeatsClient(new HttpClient(billing), configuration, NullLogger<CoachingBillingSeatsClient>.Instance), billing);
    }

    private static BillingSeatPoolResponse Pool(params Guid[] seats) => new(Guid.NewGuid(), "TEST Koltuklu plan", new DateTime(2026, 12, 15, 9, 0, 0, DateTimeKind.Utc), 5, seats.Length, 1,
        new DateTime(2026, 12, 15, 9, 0, 0, DateTimeKind.Utc), 80m, seats.Select(seat => new BillingSeatResponse(seat, false, new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc), true)).ToArray());

    [Fact]
    public async Task A_teachers_pool_is_asked_by_the_teacher_and_an_institutions_by_the_institution()
    {
        var (client, billing) = Create();
        (await client.GetPoolAsync(CoachingSeatOwner.Teacher(Teacher))).Should().BeNull();
        billing.Answer = () => Billing.Json(new BillingSeatChangeResponse(true, "Assigned", Pool(Student)));
        (await client.AssignAsync(CoachingSeatOwner.Institution(Institution), Student, Teacher)).Done.Should().BeTrue();
        (await client.ReleaseAsync(CoachingSeatOwner.Teacher(Teacher), Student, Teacher, "TEST")).Done.Should().BeTrue();

        billing.Requests[0].Should().Be((HttpMethod.Get, $"http://billing.test/internal/billing/v1/seats?product=coaching&ownerUserId={Teacher:D}", (string?)null));
        billing.Requests[1].Url.Should().Be("http://billing.test/internal/billing/v1/seats/assign");
        JsonSerializer.Deserialize<BillingSeatChangeRequest>(billing.Requests[1].Body!, Web).Should().Be(new BillingSeatChangeRequest("coaching", null, Institution, Student, Teacher, null));
        billing.Requests[2].Url.Should().EndWith("/seats/release");
        JsonSerializer.Deserialize<BillingSeatChangeRequest>(billing.Requests[2].Body!, Web).Should().Be(new BillingSeatChangeRequest("coaching", Teacher, null, Student, Teacher, "TEST"));
    }

    [Fact]
    public async Task Leaving_does_not_fail_when_billing_is_away_and_asks_nothing_where_billing_does_not_run()
    {
        var (client, billing) = Create();
        billing.Answer = () => new HttpResponseMessage(HttpStatusCode.BadGateway);
        await client.ReleaseWhenLeavingAsync(CoachingSeatOwner.Teacher(Teacher), Student, "Öğretmen bağlantısı sona erdi.");
        billing.Requests.Should().ContainSingle();
        // A change somebody asked for is not swallowed: the page must hear that it did not happen.
        await FluentActions.Awaiting(() => client.ReleaseAsync(CoachingSeatOwner.Teacher(Teacher), Student, Teacher, null)).Should().ThrowAsync<HttpRequestException>();

        var (off, untouched) = Create(enabled: false);
        await off.ReleaseWhenLeavingAsync(CoachingSeatOwner.Teacher(Teacher), Student, "TEST");
        untouched.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task An_institutions_admin_sees_and_changes_the_pool_at_billing_and_Koclugun_own_seat_table_stays_untouched()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var (client, billing) = Create();
            var access = new Forgetting();
            var stranger = Guid.NewGuid();
            var service = new CoachingInstitutionSubscriptionService(db, new Scope(new(false, Institution, [Student])), new Actor(), client, access);

            (await service.GetAsync()).Should().BeNull("billing knows no running plan with seats");
            billing.Answer = () => Billing.Json(Pool(Student, stranger));
            var summary = (await service.GetAsync())!;
            (summary.PlanName, summary.IncludedStudentSeats, summary.UsedStudentSeats, summary.ExtraSeats, summary.ExtraSeatPrice).Should().Be(("TEST Koltuklu plan", 5, 2, 1, 80m));
            summary.Students.Select(seat => (seat.StudentId, seat.IsCurrentMember)).Should().Equal((Student, true), (stranger, false));
            billing.Requests.Should().OnlyContain(request => request.Url.Contains($"organizationId={Institution:D}"));

            billing.Requests.Clear();
            billing.Answer = () => Billing.Json(new BillingSeatChangeResponse(true, "Assigned", Pool(Student)));
            (await service.SetSeatAsync(summary.SubscriptionId, Student, false)).Should().BeTrue();
            billing.Requests.Should().ContainSingle().Which.Url.Should().EndWith("/seats/assign");
            access.Forgotten.Should().Equal(Student);

            // Somebody who is not the institution's student gets no seat, and billing is not even asked.
            await FluentActions.Awaiting(() => service.SetSeatAsync(summary.SubscriptionId, stranger, false)).Should().ThrowAsync<BusinessRuleException>();
            billing.Requests.Should().ContainSingle();
            // But a seat can be taken back from them.
            billing.Answer = () => Billing.Json(new BillingSeatChangeResponse(true, "Released", Pool()));
            (await service.SetSeatAsync(summary.SubscriptionId, stranger, true)).Should().BeTrue();
            billing.Requests.Last().Url.Should().EndWith("/seats/release");

            billing.Answer = () => Billing.Json(new BillingSeatChangeResponse(false, "PoolFull", null));
            (await service.SetSeatAsync(summary.SubscriptionId, Student, false)).Should().BeFalse();
            (await db.CoachingSubscriptionSeats.CountAsync()).Should().Be(0);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
