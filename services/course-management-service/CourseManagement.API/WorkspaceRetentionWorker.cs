using System.Globalization;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

/// <summary>
/// maxkampüs data retention (user decisions 20.11.2026; rules in <see cref="WorkspaceRetention"/>): once a day every
/// workspace is checked against billing. 30 days before the deletion the owner gets an e-mail; on the day the panel data
/// is deleted (<see cref="WorkspaceContentPurge"/>). Runs only where billing is enabled; an owner billing cannot be asked
/// about is skipped until the next run, never deleted on a guess.
/// </summary>
public sealed class WorkspaceRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<WorkspaceRetentionWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Every = TimeSpan.FromHours(24);
    private static readonly TimeSpan FirstRunAfter = TimeSpan.FromMinutes(10);
    private static readonly CultureInfo Turkish = new("tr-TR");
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);
    public const int MaxWorkspacesPerRun = 2_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("CourseManagement:Retention:Enabled", true)) return;
        try
        {
            await Task.Delay(FirstRunAfter, clock, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Workspace retention run failed; it is tried again in a day.");
                }
                await Task.Delay(Every, clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public sealed record RunResult(int Checked, int Warned, int Purged, int Skipped);

    public async Task<RunResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var plans = scope.ServiceProvider.GetRequiredService<WorkspaceBillingPlans>();
        if (!plans.Enabled) return new RunResult(0, 0, 0, 0);
        var db = scope.ServiceProvider.GetRequiredService<CourseManagementDbContext>();
        var workspaces = await db.Workspaces.AsNoTracking().Where(item => !item.IsErasureReadOnly)
            .OrderBy(item => item.CreatedAt).Take(MaxWorkspacesPerRun)
            .Select(item => new { item.Id, item.OwnerUserId, item.Name, item.CreatedAt, item.RetentionWarningForEndsAt, item.RetentionWarnedAt, item.ContentPurgedAt })
            .ToListAsync(cancellationToken);
        int warned = 0, purged = 0, skipped = 0;
        var answers = new Dictionary<Guid, BillingProductAccessResponse?>();
        foreach (var workspace in workspaces)
        {
            BillingProductAccessResponse? access;
            try
            {
                if (!answers.TryGetValue(workspace.OwnerUserId, out access))
                    answers[workspace.OwnerUserId] = access = await plans.GetFreshAccessAsync(workspace.OwnerUserId, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
                && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Billing could not be asked about a workspace owner; retention skips the workspace this time.");
                skipped++;
                continue;
            }
            if (access is null) continue;
            var now = clock.GetUtcNow();
            var running = (access.Entitlements ?? []).Any(item => item.Key == WorkspaceBillingPlans.WorkspaceEntitlement && item.EndsAt > now.UtcDateTime);
            DateTimeOffset? lastEnd = access.LastEndsAt is { } ended ? new DateTimeOffset(DateTime.SpecifyKind(ended, DateTimeKind.Utc)) : null;
            var step = WorkspaceRetention.Decide(access.RequireSubscription, running, lastEnd, workspace.CreatedAt,
                workspace.RetentionWarningForEndsAt, workspace.RetentionWarnedAt, workspace.ContentPurgedAt, now);
            switch (step)
            {
                case WorkspaceRetentionStep.Warn:
                    await WarnAsync(scope.ServiceProvider, workspace.Id, workspace.OwnerUserId, workspace.Name, lastEnd!.Value, now, cancellationToken);
                    warned++;
                    break;
                case WorkspaceRetentionStep.Purge:
                    var rows = await WorkspaceContentPurge.PurgeAsync(db, workspace.Id, now, cancellationToken);
                    db.ChangeTracker.Clear();
                    logger.LogInformation("Workspace panel data deleted for retention: {Rows} rows.", rows);
                    purged++;
                    break;
            }
        }
        return new RunResult(workspaces.Count, warned, purged, skipped);
    }

    private static async Task WarnAsync(IServiceProvider services, Guid workspaceId, Guid ownerUserId, string workspaceName,
        DateTimeOffset planEndedAt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<CourseManagementDbContext>();
        var publisher = services.GetRequiredService<IPublishEndpoint>();
        var options = services.GetRequiredService<CourseEmailOptions>();
        var workspace = await db.Workspaces.SingleAsync(item => item.Id == workspaceId, cancellationToken);
        workspace.MarkRetentionWarned(planEndedAt, now);
        await publisher.Publish(WarningEmail(options, ownerUserId, workspaceName, planEndedAt, WorkspaceRetention.DeletionDate(planEndedAt, now)), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    public static CourseManagementOwnerEmailRequestedV1 WarningEmail(CourseEmailOptions options, Guid ownerUserId, string workspaceName,
        DateTimeOffset planEndedAt, DateTimeOffset deletionDate)
    {
        string Day(DateTimeOffset value) => value.ToOffset(TurkeyOffset).ToString("d MMMM yyyy", Turkish);
        return new(Guid.NewGuid(), string.Empty, $"maxkampüs: {workspaceName} panel verileri {Day(deletionDate)} tarihinde silinecek",
            "Panel verileriniz silinecek",
            [
                $"{workspaceName} çalışma alanının planı {Day(planEndedAt)} tarihinde sona erdi ve yenilenmedi; panel o günden beri salt okunur.",
                $"Kullanım koşullarımız gereği plan bitiminden bir yıl sonra panel verileri silinir. {workspaceName} çalışma alanının öğrenci, veli, ders, yoklama, ödev ve tahsilat kayıtları {Day(deletionDate)} tarihinde silinecek.",
                "Verilerinizi korumak için bu tarihten önce planınızı yenileyin. İsterseniz kayıtlarınızı silinmeden önce panelden dışa aktarabilirsiniz."
            ],
            $"{options.PublicBaseUrl.TrimEnd('/')}/plan", "Planı yenile", ownerUserId);
    }
}
