using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingInstitutionSubscriptionTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ScopeCapacitySuspensionAndConcurrentAssignmentsAreEnforced()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        await using var db = new CoachingDbContext(options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var institution = Guid.NewGuid(); var firstStudent = Guid.NewGuid(); var secondStudent = Guid.NewGuid();
            var actor = new Actor(); var scope = new Scope(new(false, institution, [firstStudent, secondStudent]));
            var plan = new CoachingSubscriptionPlan { Slug = "institution-test", Name = "Kurum yıllık", Audience = "Institution", IncludedStudentSeats = 1, DurationDays = 365 };
            var subscription = new CoachingSubscription { PlanId = plan.Id, InstitutionId = institution, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(365) };
            var foreign = new CoachingSubscription { PlanId = plan.Id, InstitutionId = Guid.NewGuid(), StartDate = subscription.StartDate, EndDate = subscription.EndDate };
            db.AddRange(plan, subscription, foreign); await db.SaveChangesAsync();
            var service = new CoachingInstitutionSubscriptionService(db, scope, actor);
            var summary = (await service.GetAsync())!; Assert.Equal(subscription.Id, summary.SubscriptionId); Assert.Equal(1, summary.IncludedStudentSeats);
            Assert.False(await service.SetSeatAsync(foreign.Id, firstStudent, false));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetSeatAsync(subscription.Id, Guid.NewGuid(), false));
            await using var firstDb = new CoachingDbContext(options); await using var secondDb = new CoachingDbContext(options);
            var assignments = await Task.WhenAll(
                new CoachingInstitutionSubscriptionService(firstDb, scope, actor).SetSeatAsync(subscription.Id, firstStudent, false),
                new CoachingInstitutionSubscriptionService(secondDb, scope, actor).SetSeatAsync(subscription.Id, secondStudent, false));
            Assert.Single(assignments.Where(result => result));
            summary = (await service.GetAsync())!; Assert.Equal(1, summary.UsedStudentSeats);
            var allocated = Assert.Single(summary.Students); Assert.True(allocated.IsCurrentMember);
            Assert.True(await service.SetSeatAsync(subscription.Id, allocated.StudentId, false));
            scope.Value = new(false, institution, []);
            Assert.False(Assert.Single((await service.GetAsync())!.Students).IsCurrentMember);
            Assert.True(await service.SetSeatAsync(subscription.Id, allocated.StudentId, true));
            Assert.Equal(0, (await service.GetAsync())!.UsedStudentSeats);
            Assert.Single(await db.CoachingSubscriptionSeats.ToListAsync());
            scope.Value = CoachingAdminScope.Global;
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetAsync());
            scope.Value = new(false, institution, [firstStudent]);
            subscription.Status = "Expired"; await db.SaveChangesAsync();
            Assert.Null(await service.GetAsync()); Assert.False(await service.SetSeatAsync(subscription.Id, firstStudent, false));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Scope(CoachingAdminScope scope) : ICoachingAdminScopeAuthorization
    {
        public CoachingAdminScope Value { get; set; } = scope;
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(Value);
    }
    private sealed class Actor : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public IEnumerable<string> Roles => ["InstitutionAdmin"];
        public string? Email => null; public string? FullName => null;
        public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
    }
}
