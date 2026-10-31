using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.Privacy;

/// <summary>
/// Subscription, seat, trial, bank transfer and payment records of an erased user (and the notices a parent sent for a
/// child) are kept unchanged for <see cref="CoachingErasureExecution.FinancialRetentionYears"/> years after the erasure.
/// After that the personal fields (name, e-mail, payer name, notes, withdrawal reasons, the buyer in the accepted
/// documents) are removed and the user id is replaced by a random id; amounts, dates, plans and payment references stay
/// for the accounts.
/// </summary>
public static class CoachingFinancialRetention
{
    public const string ErasedSeatReason = "Hesap silindi";
    public const string AnonymizedName = "Silinmiş kullanıcı";

    /// <summary>Replaces the given names and addresses in the stored document texts.</summary>
    private static string? Redact(string? json, params string?[] values)
    {
        if (json is null) return null;
        foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x) && x != AnonymizedName).Select(x => x!.Trim()).Distinct()
                     .OrderByDescending(x => x.Length))
        {
            json = json.Replace(value, AnonymizedName, StringComparison.Ordinal)
                .Replace(System.Text.Json.JsonEncodedText.Encode(value).ToString(), AnonymizedName, StringComparison.Ordinal);
        }
        return json;
    }

    public static async Task<int> CountAsync(CoachingDbContext db, Guid subjectUserId, CancellationToken ct) =>
        await db.CoachingSubscriptions.CountAsync(x => x.UserId == subjectUserId, ct)
        + await db.CoachingSubscriptionSeats.CountAsync(x => x.StudentId == subjectUserId, ct)
        // Coaching audit K1: notices a parent sent for a child are the parent's financial records too.
        + await db.CoachingBankTransferRequests.CountAsync(x => x.UserId == subjectUserId || x.SubmittedByUserId == subjectUserId, ct)
        + await db.CoachingPaymentRecords.CountAsync(x => x.UserId == subjectUserId, ct)
        + await db.Set<CoachingSubscriptionTrial>().CountAsync(x => x.UserId == subjectUserId, ct);

    internal static async Task AnonymizeAsync(CoachingDbContext db, Guid subjectUserId, DateTime now, CancellationToken ct)
    {
        // Not stored anywhere else, so the anonymized rows cannot be linked back to the erased account.
        var surrogate = Guid.NewGuid();
        foreach (var subscription in await db.CoachingSubscriptions.Where(x => x.UserId == subjectUserId).ToListAsync(ct))
        {
            subscription.UserId = surrogate;
            subscription.UserName = null;
            subscription.UserEmail = null;
            subscription.Notes = null;
            subscription.UpdatedAt = now;
        }
        foreach (var seat in await db.CoachingSubscriptionSeats.Where(x => x.StudentId == subjectUserId).ToListAsync(ct))
            seat.StudentId = surrogate;
        foreach (var transfer in await db.CoachingBankTransferRequests.Where(x => x.UserId == subjectUserId).ToListAsync(ct))
        {
            // The accepted documents name the buyer; that name and address go as well (coaching audit K1).
            transfer.SalesDocumentsJson = Redact(transfer.SalesDocumentsJson, transfer.UserName, transfer.UserEmail, transfer.PayerName);
            transfer.UserId = surrogate;
            transfer.UserName = AnonymizedName;
            transfer.UserEmail = string.Empty;
            transfer.PayerName = null;
            transfer.Note = null;
            transfer.ReviewNote = null;
            transfer.WithdrawalReason = null;
            transfer.WithdrawalReviewNote = null;
            transfer.UpdatedAt = now;
            transfer.Version++;
        }
        // A parent's notices for a child: the child's purchase stays, the parent's identity and words go.
        var parentSurrogate = Guid.NewGuid();
        foreach (var transfer in await db.CoachingBankTransferRequests.Where(x => x.SubmittedByUserId == subjectUserId).ToListAsync(ct))
        {
            transfer.SalesDocumentsJson = Redact(transfer.SalesDocumentsJson, transfer.PayerName);
            transfer.SubmittedByUserId = parentSurrogate;
            transfer.PayerName = null;
            transfer.Note = null;
            transfer.WithdrawalReason = null;
            transfer.WithdrawalReviewNote = null;
            transfer.UpdatedAt = now;
            transfer.Version++;
        }
        foreach (var trial in await db.Set<CoachingSubscriptionTrial>().Where(x => x.UserId == subjectUserId).ToListAsync(ct))
            trial.UserId = surrogate;
        foreach (var payment in await db.CoachingPaymentRecords.Where(x => x.UserId == subjectUserId).ToListAsync(ct))
        {
            payment.UserId = surrogate;
            payment.UserName = AnonymizedName;
            payment.UserEmail = string.Empty;
        }
    }
}

public sealed class CoachingFinancialRetentionProcessor(CoachingDbContext db, TimeProvider clock,
    ILogger<CoachingFinancialRetentionProcessor> logger)
{
    public const int BatchSize = 50;

    /// <summary>Anonymizes the retained records whose period has ended. Subjects under an active legal hold wait.</summary>
    public async Task<int> AnonymizeDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var due = await db.CoachingErasureExecutions
            .Where(x => x.FinancialAnonymizedAt == null && x.FinancialRetentionUntil != null && x.FinancialRetentionUntil <= now
                && !db.CoachingLegalHolds.Any(hold => hold.SubjectUserId == x.SubjectUserId
                    && hold.PlacedAt <= now && (hold.ReleasedAt == null || hold.ReleasedAt > now)))
            .OrderBy(x => x.FinancialRetentionUntil).ThenBy(x => x.Id)
            .Select(x => x.Id).Take(BatchSize).ToListAsync(ct);
        foreach (var id in due)
        {
            await RunInTransactionAsync(async () =>
            {
                var execution = await db.CoachingErasureExecutions.SingleAsync(x => x.Id == id, ct);
                if (execution.FinancialAnonymizedAt.HasValue || execution.SubjectUserId is not { } subject) return;
                if (db.Database.IsRelational())
                {
                    // Same lock as erasure and payment flows for this user.
                    var financeKey = "coaching-finance:" + subject;
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({financeKey}, 0))", ct);
                }
                await CoachingFinancialRetention.AnonymizeAsync(db, subject, now, ct);
                execution.MarkFinancialDataAnonymized(now);
                await db.SaveChangesAsync(ct);
            });
        }
        if (due.Count > 0) logger.LogInformation("Anonymized retained financial records of {Count} erased accounts", due.Count);
        return due.Count;
    }

    private async Task RunInTransactionAsync(Func<Task> work)
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

public sealed class CoachingFinancialRetentionWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<CoachingFinancialRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processed;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    processed = await scope.ServiceProvider.GetRequiredService<CoachingFinancialRetentionProcessor>().AnonymizeDueAsync(stoppingToken);
                } while (processed == CoachingFinancialRetentionProcessor.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Financial retention anonymization failed; the next cycle will retry."); }
            await Task.Delay(TimeSpan.FromDays(1), clock, stoppingToken);
        }
    }
}
