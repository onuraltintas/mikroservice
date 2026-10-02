using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using System.Security.Claims;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingManualStudyPlanTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Draft_Edit_Publish_PreservesHistoryAndEnforcesOwnership()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options;
        await using var db = new CoachingDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var actor = new Actor { UserId = Guid.NewGuid() };
            var service = new CoachingManualStudyPlanService(db, new CoachingAccessPolicy(actor));
            var input = new ManualStudyPlanInput("Haftalık plan", [new(new DateOnly(2026, 10, 5), "Matematik", 45, null, true)]);
            var draft = await service.CreateDraftAsync(input);
            Assert.Equal(StudyPlanStatus.Draft, draft.Status);
            Assert.Single(draft.Tasks);
            Assert.True(draft.Tasks[0].IsPinned);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateDraftAsync(input));
            var edited = await service.ReplaceDraftAsync(draft.Id, draft.Version, input with { Title = "Yeni başlık" });
            Assert.Equal("Yeni başlık", edited.Title);
            Assert.True(edited.Version > draft.Version);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PublishAsync(draft.Id, draft.Version));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceDraftAsync(draft.Id, edited.Version,
                input with { Tasks = [new(default, "Geçersiz", 45, null, false)] }));
            Assert.Equal("Yeni başlık", (await service.GetAsync(draft.Id))!.Title);
            var active = await service.PublishAsync(draft.Id, edited.Version);
            Assert.Equal(StudyPlanStatus.Active, active.Status);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReplaceDraftAsync(active.Id, active.Version, input));
            var next = await service.CreateDraftAsync(input);
            await service.PublishAsync(next.Id, next.Version);
            Assert.Equal(StudyPlanStatus.Archived, (await service.GetAsync(active.Id))!.Status);
            Assert.Single((await service.GetAsync(active.Id))!.Tasks);
            Assert.Equal(1, await db.StudyPlanRevisions.CountAsync(x => x.IsActive));
            var empty = await service.CreateDraftAsync(input with { Tasks = [] });
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PublishAsync(empty.Id, empty.Version));
            Assert.Equal(StudyPlanStatus.Active, (await service.GetAsync(next.Id))!.Status);
            actor.UserId = Guid.NewGuid();
            Assert.Null(await service.GetAsync(next.Id));
            var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => service.PublishAsync(next.Id, 0));
            Assert.Equal("StudyPlanning.NotFound", missing.Code);
            actor.Roles = ["Teacher"];
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateDraftAsync(input));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Actor : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles { get; set; } = ["Student"];
        public bool IsAuthenticated => UserId.HasValue;
        public ClaimsPrincipal? User => null;
    }
}
