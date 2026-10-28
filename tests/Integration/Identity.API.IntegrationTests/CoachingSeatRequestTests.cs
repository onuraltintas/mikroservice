using System.Security.Claims;
using Coaching.API.Controllers;
using Coaching.API.Security;
using Coaching.Application.Authorization;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap O10b: an institution asks for extra student seats in the middle of its license. The price is the plan's
// per-seat price for the rest of the license (prorated by days); the admin checks the bank transfer and approves.
// Extra seats belong to that license only and are not carried into a renewal.
public sealed partial class CoachingCmsAndSubscriptionServiceTests
{
    [Fact]
    public async Task ExtraSeatsArePricedForTheRestOfTheLicenseAndRaiseTheCapacityWhenApproved()
    {
        await using var s = await SeatSetup.CreateAsync(remainingDays: 183, extraSeatPrice: 100m);

        var quote = await s.Requests.QuoteAsync(s.License.Id, 5, default);
        (quote.Seats, quote.UnitPrice, quote.RemainingDays, quote.DurationDays).Should().Be((5, 100m, 183, 365));
        quote.Amount.Should().Be(Math.Round(100m * 5 * 183 / 365, 2));
        quote.Iban.Should().Be("TR000000000000000000000001", "the institution needs the bank details to pay");

        // Full plan: the second student does not fit yet.
        (await s.Institution.SetSeatAsync(s.License.Id, s.Students[0], false)).Should().BeTrue();
        (await s.Institution.SetSeatAsync(s.License.Id, s.Students[1], false)).Should().BeFalse();

        var request = await s.Requests.CreateAsync(new(s.License.Id, 1, "  EFT-KOLTUK-1 ", "Yeni şube"), default);
        request.Status.Should().Be("Pending");
        request.Amount.Should().Be(Math.Round(100m * 183 / 365, 2));
        request.PaymentReference.Should().Be("EFT-KOLTUK-1");
        (await s.Requests.ListMineAsync(default)).Should().ContainSingle(item => item.Id == request.Id);

        await s.Requests.ReviewAsync(s.Admin, request.Id, true, null, default);

        var license = await s.Db.CoachingSubscriptions.AsNoTracking().SingleAsync(item => item.Id == s.License.Id);
        license.ExtraSeats.Should().Be(1);
        var payment = await s.Db.CoachingPaymentRecords.SingleAsync();
        (payment.InstitutionId, payment.SubscriptionId, payment.Amount, payment.Reference).Should()
            .Be((s.InstitutionId, s.License.Id, request.Amount, "EFT-KOLTUK-1"));
        (await s.Requests.ListMineAsync(default)).Single().PaymentRecordId.Should().Be(payment.Id);

        (await s.Institution.SetSeatAsync(s.License.Id, s.Students[1], false)).Should().BeTrue("the extra seat is now available");
        (await s.Institution.SetSeatAsync(s.License.Id, s.Students[2], false)).Should().BeFalse();
        var summary = (await s.Institution.GetAsync())!;
        (summary.IncludedStudentSeats, summary.ExtraSeats, summary.UsedStudentSeats, summary.ExtraSeatPrice).Should().Be((2, 1, 2, 100m));
    }

    [Fact]
    public async Task SeatRequestsFollowTheRules()
    {
        await using var s = await SeatSetup.CreateAsync(remainingDays: 30, extraSeatPrice: 100m);

        (await Assert.ThrowsAsync<ArgumentException>(() => s.Requests.CreateAsync(new(s.License.Id, 0, "EFT-1", null), default))).Should().NotBeNull();
        await Assert.ThrowsAsync<ArgumentException>(() => s.Requests.CreateAsync(new(s.License.Id, 1001, "EFT-1", null), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Requests.CreateAsync(new(s.License.Id, 1, "  ", null), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Requests.CreateAsync(new(s.ForeignLicense.Id, 1, "EFT-1", null), default));

        var first = await s.Requests.CreateAsync(new(s.License.Id, 2, "EFT-1", null), default);
        (await Assert.ThrowsAsync<BusinessRuleException>(() => s.Requests.CreateAsync(new(s.License.Id, 1, "EFT-2", null), default)))
            .Code.Should().Be("Sales.SeatRequestPending");

        (await s.Requests.WithdrawAsync(first.Id, default)).Status.Should().Be("Withdrawn");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Requests.ReviewAsync(s.Admin, first.Id, true, null, default));

        var second = await s.Requests.CreateAsync(new(s.License.Id, 1, "EFT-2", null), default);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Requests.ReviewAsync(s.Admin, second.Id, false, "  ", default));
        await s.Requests.ReviewAsync(s.Admin, second.Id, false, "Havale bulunamadı", default);
        var rejected = (await s.Requests.ListMineAsync(default)).Single(item => item.Id == second.Id);
        (rejected.Status, rejected.ReviewNote).Should().Be(("Rejected", "Havale bulunamadı"));
        (await s.Db.CoachingSubscriptions.SingleAsync(item => item.Id == s.License.Id)).ExtraSeats.Should().Be(0);

        // A license that ended before the review gets no seats.
        var late = await s.Requests.CreateAsync(new(s.License.Id, 1, "EFT-3", null), default);
        var license = await s.Db.CoachingSubscriptions.SingleAsync(item => item.Id == s.License.Id);
        license.EndDate = DateTime.UtcNow.AddMinutes(-1); await s.Db.SaveChangesAsync();
        (await Assert.ThrowsAsync<BusinessRuleException>(() => s.Requests.ReviewAsync(s.Admin, late.Id, true, null, default)))
            .Code.Should().Be("Sales.SeatRequestLicenseEnded");

        (await s.Requests.ListAsync("Pending", default)).Should().ContainSingle(item => item.Id == late.Id);
    }

    [Fact]
    public async Task ExtraSeatsNeedAPerSeatPriceOnAnInstitutionPlan()
    {
        await using var s = await SeatSetup.CreateAsync(remainingDays: 100, extraSeatPrice: null);
        (await Assert.ThrowsAsync<BusinessRuleException>(() => s.Requests.QuoteAsync(s.License.Id, 1, default)))
            .Code.Should().Be("Sales.ExtraSeatsUnavailable");

        var subscriptions = new CoachingSubscriptionService(s.Db, new TestIdentityAuthorizationClient());
        await Settings(subscriptions, s.Admin, "settings-seat-" + Guid.NewGuid().ToString("N")[..10]);
        var plan = (await subscriptions.CreatePlanAsync(Plan("kurum-ek", "Kurum", "Institution", 5000m, 50) with { ExtraSeatPrice = 150m }, s.Admin))!.Value;
        (await subscriptions.GetPlanAsync(plan))!.ExtraSeatPrice.Should().Be(150m);
        await Assert.ThrowsAsync<ArgumentException>(() => subscriptions.CreatePlanAsync(
            Plan("bireysel-ek", "Bireysel", "Individual", 100m, null) with { ExtraSeatPrice = 10m }, s.Admin));
        await Assert.ThrowsAsync<ArgumentException>(() => subscriptions.CreatePlanAsync(
            Plan("kurum-sifir", "Kurum", "Institution", 5000m, 50) with { ExtraSeatPrice = 0m }, s.Admin));
    }

    [Fact]
    public void SeatRequestEndpointsAreForInstitutionAdminsAndTheReviewForTheGlobalAdmin()
    {
        var institution = typeof(CoachingInstitutionSubscriptionsController);
        institution.GetMethod("CreateSeatRequest")!.GetCustomAttributes(typeof(MfaCategoryAttribute), true).Should().NotBeEmpty();
        var admin = typeof(CoachingSalesAdminController);
        admin.GetMethod("SeatRequests").Should().NotBeNull();
        admin.GetMethod("ReviewSeatRequest").Should().NotBeNull();
        admin.GetCustomAttributes(typeof(RequireGlobalCoachingAdminAttribute), true).Should().NotBeEmpty();
    }

    private sealed class SeatSetup : IAsyncDisposable
    {
        public CoachingDbContext Db { get; } = CreateDbContext();
        public Guid Admin { get; } = Guid.NewGuid();
        public Guid InstitutionId { get; } = Guid.NewGuid();
        public Guid[] Students { get; } = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        public CoachingSubscription License { get; private set; } = null!;
        public CoachingSubscription ForeignLicense { get; private set; } = null!;
        private readonly SeatActor actor = new();
        private SeatScope Scope => new(new(false, InstitutionId, Students));
        public CoachingSeatRequestService Requests => new(Db, Scope, actor, TimeProvider.System);
        public CoachingInstitutionSubscriptionService Institution => new(Db, Scope, actor);

        public static async Task<SeatSetup> CreateAsync(int remainingDays, decimal? extraSeatPrice)
        {
            var s = new SeatSetup();
            var plan = new CoachingSubscriptionPlan { Slug = "kurum-yillik", Name = "Kurum yıllık", Audience = "Institution", Price = 5000m,
                IncludedStudentSeats = 1, DurationDays = 365, ExtraSeatPrice = extraSeatPrice };
            var now = DateTime.UtcNow;
            // A little less than whole days so the remaining days round up to the number asked for.
            s.License = new CoachingSubscription { PlanId = plan.Id, InstitutionId = s.InstitutionId, StartDate = now.AddDays(remainingDays - 365),
                EndDate = now.AddDays(remainingDays).AddMinutes(-5) };
            s.ForeignLicense = new CoachingSubscription { PlanId = plan.Id, InstitutionId = Guid.NewGuid(), StartDate = s.License.StartDate, EndDate = s.License.EndDate };
            s.Db.AddRange(plan, s.License, s.ForeignLicense, new CoachingSubscriptionSettings { Currency = "TRY", BankTransferEnabled = true,
                AccountHolder = "Eduİvme", BankName = "Banka", Iban = "TR000000000000000000000001" });
            // Students in the institution scope can be seated; membership rows are not needed for that.
            await s.Db.SaveChangesAsync();
            return s;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class SeatScope(CoachingAdminScope scope) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(scope);
    }

    private sealed class SeatActor : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public IEnumerable<string> Roles => ["InstitutionAdmin"];
        public string? Email => "kurum@example.test"; public string? FullName => "Kurum Yöneticisi";
        public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
    }
}
