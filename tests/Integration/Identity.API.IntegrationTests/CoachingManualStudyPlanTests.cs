using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.IntegrationTests.Fixtures;
using System.Security.Claims;
using System.Data.Common;
using Npgsql;

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
            var service = new CoachingManualStudyPlanService(db, new CoachingAccessPolicy(actor), new CoachingAutomaticStudyPlanPreviewService(db, new CoachingAccessPolicy(actor)));
            var input = new ManualStudyPlanInput("Haftalık plan", [new(new DateOnly(2026, 10, 5), "Matematik", 45, null, true)]);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateDraftAsync(null!));
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
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceDraftAsync(empty.Id, empty.Version,
                input with { Tasks = [new(new DateOnly(2026, 10, 5), "Konu", 45, Guid.NewGuid(), false)] }));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceDraftAsync(empty.Id, empty.Version,
                input with { Tasks = Enumerable.Repeat(new ManualStudyTaskInput(new DateOnly(2026, 10, 5), "Konu", 45, null, false), 501).ToArray() }));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceDraftAsync(empty.Id, empty.Version,
                input with { Tasks = [new(new DateOnly(2026, 10, 5), "Konu", 1440, null, false), new(new DateOnly(2026, 10, 5), "Konu", 1, null, false)] }));
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

    [Fact]
    public async Task ConcurrentPublications_OnlyOneWinsAndOnePlanRemainsActive()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options;
        await using var db = new CoachingDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var actor = new Actor { UserId = Guid.NewGuid() };
            var input = new ManualStudyPlanInput("Plan", [new(new DateOnly(2026, 10, 5), "Konu", 30, null, false)]);
            var service = new CoachingManualStudyPlanService(db, new CoachingAccessPolicy(actor), new CoachingAutomaticStudyPlanPreviewService(db, new CoachingAccessPolicy(actor)));
            var initial = await service.CreateDraftAsync(input);
            await service.PublishAsync(initial.Id, initial.Version);
            var next = await service.CreateDraftAsync(input);
            async Task<bool> Publish()
            {
                await using var separate = new CoachingDbContext(options);
                try
                {
                    await new CoachingManualStudyPlanService(separate, new CoachingAccessPolicy(actor), new CoachingAutomaticStudyPlanPreviewService(separate, new CoachingAccessPolicy(actor))).PublishAsync(next.Id, next.Version);
                    return true;
                }
                catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict") { return false; }
            }
            var results = await Task.WhenAll(Publish(), Publish());
            Assert.Single(results.Where(x => x));
            Assert.Single(results.Where(x => !x));
            Assert.Equal(1, await db.StudyPlanRevisions.CountAsync(x => x.IsActive));
            Assert.Equal(StudyPlanStatus.Archived, (await service.GetAsync(initial.Id))!.Status);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task ListsAndTaskOperations_AreOwnedVersionedAndPreserveCompletedWork()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options;
        await using var db = new CoachingDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var actor = new Actor { UserId = Guid.NewGuid() };
            var owner = actor.UserId;
            var service = new CoachingManualStudyPlanService(db, new CoachingAccessPolicy(actor), new CoachingAutomaticStudyPlanPreviewService(db, new CoachingAccessPolicy(actor)));
            var date = new DateOnly(2026, 10, 5);
            var input = new ManualStudyPlanInput("Plan", [new(date, "Birinci", 60, null, true), new(date.AddDays(1), "İkinci", 1440, null, false)]);
            Assert.Empty((await service.ListAsync(1, 20, null)).Items);
            var draft = await service.CreateDraftAsync(input);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteTaskAsync(draft.Id, draft.Tasks[0].Id, draft.Version, 55));
            var active = await service.PublishAsync(draft.Id, draft.Version);
            var first = active.Tasks.Single(x => x.Title == "Birinci");
            await Assert.ThrowsAsync<ArgumentException>(() => service.RescheduleTaskAsync(active.Id, first.Id, active.Version, date.AddDays(1)));
            await Assert.ThrowsAsync<ArgumentException>(() => service.RescheduleTaskAsync(active.Id, first.Id, active.Version, default));
            var moved = await service.RescheduleTaskAsync(active.Id, first.Id, active.Version, date.AddDays(2));
            Assert.True(moved.Version > active.Version);
            Assert.Equal(date.AddDays(2), moved.Tasks.Single(x => x.Id == first.Id).PlannedDate);
            Assert.True(moved.Tasks.Single(x => x.Id == first.Id).IsPinned);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteTaskAsync(active.Id, first.Id, active.Version, 55));
            var completed = await service.CompleteTaskAsync(active.Id, first.Id, moved.Version, 55);
            var completedTask = completed.Tasks.Single(x => x.Id == first.Id);
            Assert.True(completedTask.IsCompleted);
            Assert.Equal(55, completedTask.ActualMinutes);
            Assert.NotNull(completedTask.CompletedAt);
            var repeated = await service.CompleteTaskAsync(active.Id, first.Id, completed.Version, 55);
            Assert.Equal(completed.Version, repeated.Version);
            Assert.Equal(completedTask.CompletedAt, repeated.Tasks.Single(x => x.Id == first.Id).CompletedAt);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteTaskAsync(active.Id, first.Id, completed.Version, 56));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.RescheduleTaskAsync(active.Id, first.Id, completed.Version, date.AddDays(3)));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteTaskAsync(active.Id, Guid.NewGuid(), completed.Version, 30));
            var next = await service.CreateDraftAsync(input);
            var drafts = await service.ListAsync(1, 20, StudyPlanStatus.Draft);
            Assert.Equal(next.Id, Assert.Single(drafts.Items).Id);
            var list = await service.ListAsync(1, 1, null);
            Assert.Equal(2, list.TotalCount);
            Assert.Single(list.Items);
            Assert.Single((await service.ListAsync(2, 1, null)).Items);
            await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(0, 20, null));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(1, 51, null));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(1, 20, (StudyPlanStatus)9));
            await service.PublishAsync(next.Id, next.Version);
            var archived = (await service.GetAsync(active.Id))!;
            Assert.Equal(55, archived.Tasks.Single(x => x.Id == first.Id).ActualMinutes);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteTaskAsync(active.Id, first.Id, archived.Version, 55));
            actor.UserId = Guid.NewGuid();
            Assert.Empty((await service.ListAsync(1, 20, null)).Items);
            var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteTaskAsync(next.Id, next.Tasks[0].Id, 0, 30));
            Assert.Equal("StudyPlanning.NotFound", missing.Code);
            actor.UserId = owner;
            actor.Roles = ["Teacher"];
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ListAsync(1, 20, null));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task FailedActivation_RollsBackPreviousArchiveAndCanBeRetried()
    {
        var fault = new ActivationFault();
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).AddInterceptors(fault).Options;
        await using var db = new CoachingDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var actor = new Actor { UserId = Guid.NewGuid() };
            var service = new CoachingManualStudyPlanService(db, new CoachingAccessPolicy(actor), new CoachingAutomaticStudyPlanPreviewService(db, new CoachingAccessPolicy(actor)));
            var input = new ManualStudyPlanInput("Plan", [new(new DateOnly(2026, 10, 5), "Konu", 30, null, false)]);
            var first = await service.CreateDraftAsync(input);
            var active = await service.PublishAsync(first.Id, first.Version);
            var draft = await service.CreateDraftAsync(input);
            fault.Enabled = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(draft.Id, draft.Version));
            fault.Enabled = false;
            var unchanged = (await service.GetAsync(active.Id))!;
            Assert.Equal(StudyPlanStatus.Active, unchanged.Status);
            Assert.Equal(active.Version, unchanged.Version);
            Assert.Equal(StudyPlanStatus.Draft, (await service.GetAsync(draft.Id))!.Status);
            await service.PublishAsync(draft.Id, draft.Version);
            Assert.Equal(StudyPlanStatus.Archived, (await service.GetAsync(active.Id))!.Status);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData("create")]
    [InlineData("replace")]
    [InlineData("publish")]
    [InlineData("complete")]
    [InlineData("reschedule")]
    public async Task LostCommitAcknowledgement_ReturnsCommittedResult(string operation)
    {
        var fault = new CommitAcknowledgementFault();
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).AddInterceptors(fault).Options;
        await using var db = new CoachingDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var actor = new Actor { UserId = Guid.NewGuid() };
            var service = new CoachingManualStudyPlanService(db, new CoachingAccessPolicy(actor), new CoachingAutomaticStudyPlanPreviewService(db, new CoachingAccessPolicy(actor)));
            var input = new ManualStudyPlanInput("Plan", [new(new DateOnly(2026, 10, 5), "Konu", 30, null, false)]);
            var draft = operation == "create" ? null : await service.CreateDraftAsync(input);
            if (operation is "complete" or "reschedule") draft = await service.PublishAsync(draft!.Id, draft.Version);
            fault.Enabled = true;
            var result = operation switch
            {
                "create" => await service.CreateDraftAsync(input),
                "replace" => await service.ReplaceDraftAsync(draft!.Id, draft.Version, input with { Title = "Düzenlenmiş" }),
                "complete" => await service.CompleteTaskAsync(draft!.Id, draft.Tasks[0].Id, draft.Version, 25),
                "reschedule" => await service.RescheduleTaskAsync(draft!.Id, draft.Tasks[0].Id, draft.Version, new DateOnly(2026, 10, 6)),
                _ => await service.PublishAsync(draft!.Id, draft.Version)
            };
            Assert.Equal(1, fault.Failures);
            Assert.Equal(1, await db.StudyPlanRevisions.CountAsync());
            var persisted = (await service.GetAsync(result.Id))!;
            Assert.Equal(result.Version, persisted.Version);
            Assert.Equal(result.Status, persisted.Status);
            Assert.Equal(result.Title, persisted.Title);
            Assert.Single(persisted.Tasks);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class CommitAcknowledgementFault : DbTransactionInterceptor
    {
        public bool Enabled { get; set; }
        public int Failures { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Enabled)
            {
                Enabled = false;
                Failures++;
                throw new NpgsqlException("Simulated lost COMMIT acknowledgement.", new IOException("Connection interrupted."));
            }
            return Task.CompletedTask;
        }
    }

    private sealed class ActivationFault : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<StudyPlanRevision>()
                .Any(x => x.State == EntityState.Modified && x.Entity.IsActive))
                throw new InvalidOperationException("Simulated activation failure.");
            return ValueTask.FromResult(result);
        }
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
