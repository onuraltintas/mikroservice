using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Security.Interfaces;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shared.IntegrationTests.Fixtures;
using System.Security.Claims;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingStudyPlanOutboxTests(PostgresFixture postgres)
{
    [Fact]
    public async Task PublishingPlan_PersistsNotificationInSameTransactionWithoutRunningBroker()
    {
        var services = new ServiceCollection(); services.AddLogging();
        var fault = new PublicationFault();
        services.AddDbContext<CoachingDbContext>(o => o.UseNpgsql(postgres.ConnectionString).AddInterceptors(fault));
        services.AddMassTransit(x => {
            x.AddEntityFrameworkOutbox<CoachingDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); });
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var access = new CoachingAccessPolicy(new Actor());
            var service = new CoachingManualStudyPlanService(db, access, new CoachingAutomaticStudyPlanPreviewService(db, access),
                new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()));
            var draft = await service.CreateDraftAsync(new("Test plan", [new(new(2026, 10, 2), "Read", 30, null, false)]));
            Assert.Equal(0, await db.Set<OutboxMessage>().CountAsync());
            fault.Enabled = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(draft.Id, draft.Version));
            Assert.Equal(0, await db.Set<OutboxMessage>().AsNoTracking().CountAsync());
            Assert.Equal(StudyPlanStatus.Draft, (await service.GetAsync(draft.Id))!.Status);
            fault.Enabled = false;
            var active = await service.PublishAsync(draft.Id, draft.Version);
            Assert.Equal(StudyPlanStatus.Active, active.Status);
            var message = Assert.Single(await db.Set<OutboxMessage>().AsNoTracking().ToListAsync());
            Assert.Contains("StudyPlanPublishedEvent", message.MessageType);
            Assert.Contains(active.Id.ToString(), message.Body);
            await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => service.PublishAsync(draft.Id, draft.Version));
            Assert.Equal(1, await db.Set<OutboxMessage>().CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Actor : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid(); public string? Email => null; public string? FullName => null;
        public IEnumerable<string> Roles => ["Student"]; public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
    }
    private sealed class PublicationFault : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<StudyPlanRevision>().Any(x => x.Entity.IsActive))
                throw new InvalidOperationException("Simulated publication failure.");
            return ValueTask.FromResult(result);
        }
    }
}

internal sealed class RecordingStudyPublisher : ICoachingEventPublisher
{
    public List<object> Messages { get; } = [];
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    { Messages.Add(message); return Task.CompletedTask; }
}
