using System.Data;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

public sealed class CourseManagementReminderWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration,
    ILogger<CourseManagementReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingSeconds = Math.Clamp(configuration.GetValue("CourseManagement:ReminderPollingSeconds", 15), 1, 300);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await QueueDueRemindersAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Course management reminder dispatch failed.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(pollingSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    private async Task QueueDueRemindersAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CourseManagementDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var dueReminders = await db.Reminders.FromSqlInterpolated($"""
            SELECT * FROM "CourseManagementReminders"
            WHERE "Status" = {(int)CourseManagementReminderStatus.Pending}
                AND "NotifyAt" <= {now}
            ORDER BY "NotifyAt"
            LIMIT 100
            FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);

        foreach (var reminder in dueReminders)
        {
            if (!CourseManagementReminderSchedule.IsDeliverable(reminder.Type, reminder.TargetAt, now))
            {
                reminder.Cancel();
                continue;
            }

            if (!reminder.MarkQueued(now)) continue;
            await publishEndpoint.Publish(new CourseManagementReminderDueV1(reminder.Id, reminder.WorkspaceId,
                reminder.RecipientUserId, reminder.Type.ToString(), reminder.TargetAt),
                publishContext => publishContext.MessageId = reminder.Id, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
