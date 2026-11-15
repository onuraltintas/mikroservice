using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using EduPlatform.Shared.Contracts.Billing;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SpeedReading.API.Controllers;
using SpeedReading.Application.Institutions;
using SpeedReading.Application.Subscription;
using SpeedReading.Domain.Institutions;
using SpeedReading.Infrastructure.ExternalServices;

namespace Identity.API.IntegrationTests;

/// <summary>An institution's admin gives the seats of its Hızlı Okuma plan to its own students (billing stage 7).</summary>
public sealed class SpeedReadingInstitutionSeatsTests
{
    private static readonly Guid Institution = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Admin = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Student = Guid.Parse("44444444-4444-4444-8444-444444444444");

    private sealed class Billing : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string? Body, string? Key)> Requests { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Answer { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.ToString(), request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.TryGetValues("X-Internal-Service-Key", out var keys) ? keys.Single() : null));
            return Answer(request);
        }
        public static HttpResponseMessage Json(object body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json") };
    }

    private sealed class Access : ISpeedReadingBillingAccess
    {
        public List<Guid> Forgotten { get; } = [];
        public bool Enabled => true;
        public Task<BillingAccessDecision> GetAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Forget(Guid userId) => Forgotten.Add(userId);
        public void ForgetAll() => throw new NotSupportedException();
    }

    private sealed class Members(bool isStudent) : ISpeedReadingInstitutionMemberships
    {
        public Task<SpeedReadingInstitutionMembershipPage> GetMembersAsync(Guid institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default,
            SpeedReadingInstitutionMemberRole? role = null, bool? isActive = null, int? gradeLevel = null, Guid? teacherUserId = null, Guid? memberUserId = null)
        {
            (institutionId, role, isActive, memberUserId).Should().Be((Institution, SpeedReadingInstitutionMemberRole.Student, true, Student));
            IReadOnlyList<SpeedReadingInstitutionMembershipRecord> items = isStudent ? [null!] : [];
            return Task.FromResult(new SpeedReadingInstitutionMembershipPage(items, items.Count, 1, 1));
        }

        public Task<SpeedReadingMembershipChange> SetMembershipAsync(Guid institutionId, Guid userId, SpeedReadingInstitutionMemberRole role, bool isActive, Guid actorId,
            DateTime at, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Authorization(bool manages) : ISpeedReadingInstitutionAdministrationAuthorization
    {
        public Task<bool> CanManageAsync(Guid userId, Guid institutionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(manages && userId == Admin && institutionId == Institution);
        public Task<IReadOnlyList<Guid>> GetManagedInstitutionIdsAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static BillingSeatPoolResponse Pool(int used) => new(Guid.NewGuid(), "TEST Kurum 20 koltuk", new DateTime(2027, 11, 15, 9, 0, 0, DateTimeKind.Utc), 20, used, 0, null, 150m,
        [new BillingSeatResponse(Student, false, new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc), true)]);

    private static (SpeedReadingInstitutionSeatsController Controller, Billing Billing, Access Access) Create(
        bool enabled = true, bool manages = true, bool isStudent = true, string role = "InstitutionAdmin")
    {
        var billing = new Billing();
        var access = new Access();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Billing:Enabled"] = enabled ? "true" : "false", ["Services:BillingService"] = "http://billing.test", ["INTERNAL_SERVICE_API_KEY"] = "test-key"
        }).Build();
        var controller = new SpeedReadingInstitutionSeatsController(new BillingSeatsClient(new HttpClient(billing), configuration, NullLogger<BillingSeatsClient>.Instance), access, new Members(isStudent),
            new Authorization(manages), NullLogger<SpeedReadingInstitutionSeatsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Admin.ToString()), new Claim(ClaimTypes.Role, role)], "test", ClaimTypes.Name, ClaimTypes.Role))
                }
            }
        };
        return (controller, billing, access);
    }

    [Fact]
    public async Task The_admin_sees_the_institutions_pool_and_nothing_of_an_institution_they_do_not_manage()
    {
        var (controller, billing, _) = Create();
        billing.Answer = _ => Billing.Json(Pool(1));
        var shown = (await controller.Get(Institution)).Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<InstitutionSeatsResponse>().Subject;
        (shown.Enabled, shown.Pool!.Capacity, shown.Pool.Used).Should().Be((true, 20, 1));
        billing.Requests.Should().ContainSingle().Which.Should().Be((HttpMethod.Get,
            $"http://billing.test/internal/billing/v1/seats?product=speed-reading&organizationId={Institution:D}", (string?)null, "test-key"));

        billing.Answer = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        ((InstitutionSeatsResponse)((OkObjectResult)await controller.Get(Institution)).Value!).Should().Be(new InstitutionSeatsResponse(true, null));

        var (stranger, untouched, _) = Create(manages: false);
        (await stranger.Get(Institution)).Should().BeOfType<ForbidResult>();
        (await stranger.Assign(Institution, Student)).Should().BeOfType<ForbidResult>();
        (await stranger.Release(Institution, Student)).Should().BeOfType<ForbidResult>();
        untouched.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_seat_goes_only_to_an_active_student_of_the_institution_and_the_remembered_access_is_dropped()
    {
        var (controller, billing, access) = Create();
        billing.Answer = _ => Billing.Json(new BillingSeatChangeResponse(true, "Assigned", Pool(1)));
        (await controller.Assign(Institution, Student)).Should().BeOfType<OkObjectResult>();
        var sent = billing.Requests.Should().ContainSingle().Subject;
        (sent.Method, sent.Url).Should().Be((HttpMethod.Post, "http://billing.test/internal/billing/v1/seats/assign"));
        JsonSerializer.Deserialize<BillingSeatChangeRequest>(sent.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Be(new BillingSeatChangeRequest("speed-reading", null, Institution, Student, Admin, null));
        access.Forgotten.Should().Equal(Student);

        var (other, untouched, _) = Create(isStudent: false);
        (await other.Assign(Institution, Student)).Should().BeOfType<NotFoundObjectResult>();
        untouched.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("PoolFull", "Seats.PoolFull")]
    [InlineData("NoPool", "Seats.NoPool")]
    public async Task A_full_or_missing_pool_is_told_as_it_is(string code, string expected)
    {
        var (controller, billing, access) = Create();
        billing.Answer = _ => Billing.Json(new BillingSeatChangeResponse(false, code, null));
        var conflict = (await controller.Assign(Institution, Student)).Should().BeOfType<ConflictObjectResult>().Subject;
        JsonSerializer.Serialize(conflict.Value).Should().Contain(expected);
        access.Forgotten.Should().BeEmpty();
    }

    [Fact]
    public async Task A_seat_is_taken_back_even_from_somebody_who_left_and_an_outage_is_told_as_one()
    {
        var (controller, billing, access) = Create(isStudent: false);
        billing.Answer = _ => Billing.Json(new BillingSeatChangeResponse(true, "Released", Pool(0)));
        (await controller.Release(Institution, Student)).Should().BeOfType<OkObjectResult>();
        billing.Requests.Single().Url.Should().EndWith("/seats/release");
        access.Forgotten.Should().Equal(Student);

        billing.Answer = _ => Billing.Json(new BillingSeatChangeResponse(false, "NotAssigned", null));
        (await controller.Release(Institution, Student)).Should().BeOfType<NotFoundObjectResult>();

        billing.Answer = _ => new HttpResponseMessage(HttpStatusCode.BadGateway);
        ((ObjectResult)await controller.Release(Institution, Student)).StatusCode.Should().Be(503);
        ((ObjectResult)await controller.Get(Institution)).StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task A_membership_that_ends_frees_the_seat_and_does_not_fail_when_billing_is_away()
    {
        var billing = new Billing { Answer = _ => Billing.Json(new BillingSeatChangeResponse(true, "Released", Pool(0))) };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Billing:Enabled"] = "true", ["Services:BillingService"] = "http://billing.test", ["INTERNAL_SERVICE_API_KEY"] = "test-key"
        }).Build();
        var client = new BillingSeatsClient(new HttpClient(billing), configuration, NullLogger<BillingSeatsClient>.Instance);
        await client.ReleaseWhenLeavingAsync(Institution, Student, Admin);
        var sent = billing.Requests.Should().ContainSingle().Subject;
        sent.Url.Should().EndWith("/seats/release");
        JsonSerializer.Deserialize<BillingSeatChangeRequest>(sent.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Be(new BillingSeatChangeRequest("speed-reading", null, Institution, Student, Admin, "Öğrencinin kurum üyeliği sona erdi."));

        billing.Answer = _ => new HttpResponseMessage(HttpStatusCode.BadGateway);
        await client.ReleaseWhenLeavingAsync(Institution, Student, Admin);
        billing.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Without_billing_the_page_is_told_so_and_no_seat_is_given()
    {
        var (controller, billing, _) = Create(enabled: false, role: "SystemAdmin");
        ((OkObjectResult)await controller.Get(Institution)).Value.Should().Be(new InstitutionSeatsResponse(false, null));
        (await controller.Assign(Institution, Student)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.Release(Institution, Student)).Should().BeOfType<NotFoundObjectResult>();
        billing.Requests.Should().BeEmpty();
    }
}
