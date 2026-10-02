using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Security.Interfaces;
using System.Security.Claims;
using Shared.IntegrationTests.Fixtures;
using EduPlatform.Shared.Kernel.Exceptions;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingStudyAvailabilityServiceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Service_IsolatesOwnersAndRejectsStaleOrInvalidUpdates()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options;
        await using var db = new CoachingDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var student = Guid.NewGuid();
            var actor = new TestActor { UserId = student };
            var policy = new CoachingAccessPolicy(actor);
            var service = new CoachingStudyAvailabilityService(db, policy);
            Assert.Null(await service.GetAsync());
            var request = new StudyAvailabilityUpdate(null, "Europe/Istanbul", [new(DayOfWeek.Monday, 480, 540)]);
            var created = await service.ReplaceAsync(request);
            Assert.Single(created.Windows);
            Assert.Equal(0, created.Version);
            db.ChangeTracker.Clear();
            var changed = await service.ReplaceAsync(request with { ExpectedVersion = created.Version, Windows = [] });
            Assert.Empty(changed.Windows);
            Assert.True(changed.Version > created.Version);
            db.ChangeTracker.Clear();
            var stale = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReplaceAsync(request with { ExpectedVersion = 0 }));
            Assert.Equal("StudyPlanning.Conflict", stale.Code);
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceAsync(request with
            {
                ExpectedVersion = changed.Version,
                Windows = [new(DayOfWeek.Monday, 480, 540), new(DayOfWeek.Monday, 500, 560)]
            }));
            db.ChangeTracker.Clear();
            Assert.Empty((await service.GetAsync())!.Windows);
            var other = Guid.NewGuid();
            actor.UserId = other;
            Assert.Null(await service.GetAsync());
            actor.Roles = ["Teacher"];
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class TestActor : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles { get; set; } = ["Student"];
        public bool IsAuthenticated => UserId.HasValue;
        public ClaimsPrincipal? User => null;
    }
}
