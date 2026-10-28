using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap O10b: institution extra-seat requests on PostgreSQL — one pending request per license, approval in a
// serializable transaction that raises the capacity and records the payment, and the migration's constraints.
public sealed class CoachingSeatRequestPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ExtraSeatRequestsRunOnPostgres()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var institution = Guid.NewGuid(); var admin = Guid.NewGuid(); var students = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var plan = new CoachingSubscriptionPlan { Slug = "pg-kurum", Name = "Kurum yıllık", Audience = "Institution", Price = 5000m, IncludedStudentSeats = 1,
            DurationDays = 365, ExtraSeatPrice = 365m };
        var license = new CoachingSubscription { PlanId = plan.Id, InstitutionId = institution, StartDate = DateTime.UtcNow.AddDays(-265),
            EndDate = DateTime.UtcNow.AddDays(100).AddMinutes(-5) };
        f.Db.AddRange(plan, license, new CoachingSubscriptionSettings { Currency = "TRY", BankTransferEnabled = true, Iban = "TR000000000000000000000001" });
        await f.Db.SaveChangesAsync();
        var scope = new Scope(new(false, institution, students));
        var requests = new CoachingSeatRequestService(f.Db, scope, new Actor(), TimeProvider.System);

        var quote = await requests.QuoteAsync(license.Id, 2, default);
        Assert.Equal((100, 200m), (quote.RemainingDays, quote.Amount));
        var first = await requests.CreateAsync(new(license.Id, 2, "EFT-PG-1", null), default);
        Assert.Equal("Sales.SeatRequestPending",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => requests.CreateAsync(new(license.Id, 1, "EFT-PG-2", null), default))).Code);

        await requests.ReviewAsync(admin, first.Id, true, null, default);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(2, (await f.Db.CoachingSubscriptions.SingleAsync()).ExtraSeats);
        var payment = await f.Db.CoachingPaymentRecords.SingleAsync();
        Assert.Equal((institution, 200m), (payment.InstitutionId!.Value, payment.Amount));

        var seats = new CoachingInstitutionSubscriptionService(f.Db, scope, new Actor());
        Assert.True(await seats.SetSeatAsync(license.Id, students[0], false));
        Assert.True(await seats.SetSeatAsync(license.Id, students[1], false));
        Assert.Equal(3, (await seats.GetAsync())!.IncludedStudentSeats);

        // The constraints hold without the service.
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            f.Db.Add(new CoachingSubscriptionPlan { Slug = "pg-bireysel", Name = "Bireysel", Audience = "Individual", DurationDays = 365, ExtraSeatPrice = 10m });
            await f.Db.SaveChangesAsync();
        });
        f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            f.Db.Add(new CoachingSeatRequest { SubscriptionId = license.Id, InstitutionId = institution, RequestedBy = admin, Seats = 1, UnitPrice = 1m,
                RemainingDays = 1, DurationDays = 1, Amount = 1m, PaymentReference = "X", CreatedAt = DateTime.UtcNow });
            f.Db.Add(new CoachingSeatRequest { SubscriptionId = license.Id, InstitutionId = institution, RequestedBy = admin, Seats = 1, UnitPrice = 1m,
                RemainingDays = 1, DurationDays = 1, Amount = 1m, PaymentReference = "Y", CreatedAt = DateTime.UtcNow });
            await f.Db.SaveChangesAsync();
        });
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
}
