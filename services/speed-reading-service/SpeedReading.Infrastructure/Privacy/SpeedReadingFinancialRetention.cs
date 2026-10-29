using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Legacy;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Privacy;

/// <summary>
/// Review H2b (decision 28.10.2026, same as coaching): subscriptions, paid card payments and bank transfer requests of
/// an erased user are kept unchanged for <see cref="SpeedReadingErasureExecutionReceipt.FinancialRetentionYears"/>
/// year(s). After that the personal fields are removed and the user id is replaced by a random id; amounts, dates,
/// plans and payment references stay for the accounts.
/// </summary>
internal static class SpeedReadingFinancialRetention
{
    public const string AnonymizedName = "Silinmiş kullanıcı";

    private static readonly string Paid = SpeedReadingPaymentRules.SuccessStatus.ToLowerInvariant();
    private static readonly string Approved = BankTransferPaymentRules.ApprovedStatus.ToLowerInvariant();

    /// <summary>
    /// Decision 28.10.2026 (same as coaching): only money that actually moved is kept after erasure — successful
    /// card payments, approved transfers and the subscriptions they bought. Unfinished or refused attempts are erased.
    /// </summary>
    public static IQueryable<LegacyPayment> UnpaidPayments(ISpeedReadingDataContext db, Guid userId) =>
        db.Payments.Where(item => item.UserId == userId && item.Status.ToLower() != Paid);

    public static IQueryable<LegacyBankTransferPaymentRequest> UnapprovedTransfers(ISpeedReadingDataContext db, Guid userId) =>
        db.BankTransferPaymentRequests.Where(item => item.UserId == userId && item.Status.ToLower() != Approved);

    /// <summary>Records kept after erasure.</summary>
    public static async Task<int> CountAsync(ISpeedReadingDataContext db, Guid userId, CancellationToken ct) =>
        await db.UserSubscriptions.CountAsync(item => item.UserId == userId, ct)
        + await db.Payments.CountAsync(item => item.UserId == userId && item.Status.ToLower() == Paid, ct)
        + await db.BankTransferPaymentRequests.CountAsync(item => item.UserId == userId && item.Status.ToLower() == Approved, ct);

    public static async Task AnonymizeAsync(ISpeedReadingDataContext db, Guid userId, DateTime now, CancellationToken ct)
    {
        // Not stored anywhere else, so the anonymised rows cannot be linked back to the erased account.
        var surrogate = Guid.NewGuid();
        foreach (var subscription in await db.UserSubscriptions.Where(item => item.UserId == userId).ToListAsync(ct))
        {
            subscription.UserId = surrogate;
            subscription.UserName = null;
            subscription.UserEmail = null;
            subscription.Notes = null;
        }
        foreach (var payment in await db.Payments.Where(item => item.UserId == userId).ToListAsync(ct))
        {
            payment.UserId = surrogate;
            payment.UserName = AnonymizedName;
            payment.UserEmail = string.Empty;
            payment.ProviderResponse = null;
            payment.ErrorMessage = null;
            payment.UpdatedAt = now;
        }
        foreach (var transfer in await db.BankTransferPaymentRequests.Where(item => item.UserId == userId).ToListAsync(ct))
        {
            transfer.UserId = surrogate;
            transfer.UserName = AnonymizedName;
            transfer.UserEmail = string.Empty;
            transfer.PayerName = null;
            transfer.Note = null;
            transfer.ReviewNote = null;
            transfer.UpdatedAt = now;
        }
    }
}

public sealed class SpeedReadingFinancialRetentionProcessor(OwnedSpeedReadingDbContext db, TimeProvider clock)
{
    public const int BatchSize = 50;

    /// <summary>Anonymises the kept records whose year has ended; returns how many erased accounts were processed.</summary>
    public async Task<int> AnonymizeDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var due = await db.ErasureExecutions
            .Where(item => item.FinancialAnonymizedAt == null && item.SubjectUserId != null
                && item.FinancialRetentionUntil != null && item.FinancialRetentionUntil <= now)
            .OrderBy(item => item.FinancialRetentionUntil).ThenBy(item => item.Id)
            .Select(item => item.Id)
            .Take(BatchSize)
            .ToListAsync(ct);
        foreach (var id in due)
        {
            await RunAsync(async () =>
            {
                var execution = await db.ErasureExecutions.SingleAsync(item => item.Id == id, ct);
                if (execution.FinancialAnonymizedAt.HasValue || execution.SubjectUserId is not { } subject) return;
                await SpeedReadingFinancialRetention.AnonymizeAsync(db, subject, now, ct);
                execution.MarkFinancialDataAnonymized(now);
                await db.SaveChangesAsync(ct);
            });
        }
        return due.Count;
    }

    private async Task RunAsync(Func<Task> work)
    {
        if (!db.Database.IsRelational())
        {
            try { await work(); } finally { db.ChangeTracker.Clear(); }
            return;
        }
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                await work();
                await transaction.CommitAsync();
            }
            finally { db.ChangeTracker.Clear(); }
        });
    }
}

public sealed class SpeedReadingFinancialRetentionWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<SpeedReadingFinancialRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the service finish starting before the first daily pass.
        await Task.Delay(TimeSpan.FromMinutes(5), clock, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processed;
                var total = 0;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    processed = await scope.ServiceProvider.GetRequiredService<SpeedReadingFinancialRetentionProcessor>().AnonymizeDueAsync(stoppingToken);
                    total += processed;
                } while (processed == SpeedReadingFinancialRetentionProcessor.BatchSize && !stoppingToken.IsCancellationRequested);
                if (total > 0) logger.LogInformation("Anonymized retained financial records of {Count} erased accounts", total);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Financial retention anonymization failed; the next cycle will retry."); }
            await Task.Delay(TimeSpan.FromDays(1), clock, stoppingToken);
        }
    }
}
