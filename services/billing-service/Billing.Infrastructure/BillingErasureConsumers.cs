using System.Security.Cryptography;
using System.Text;
using Billing.Application;
using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;

namespace Billing.Infrastructure;

/// <summary>
/// Billing's part in the erasure of an account, which Identity runs across the services: first it asks what is
/// there (a dry run), then it tells every service to erase. Billing closes the access at once and keeps the
/// financial records for the retention time (<see cref="IBillingLifecycle"/>). A product-only request (Koçluk or
/// Hızlı Okuma alone) does not touch billing: the account, its payments and its subscriptions stay.
/// </summary>
public static class BillingErasure
{
    public const string ServiceName = "Billing";

    /// <summary>The same request always completes under the same id, however often its message is delivered.</summary>
    public static Guid CompletionId(Guid requestId) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"billing-erasure:{requestId:N}")));
}

public sealed class BillingErasureAssessmentRequestedConsumer(IBillingLifecycle lifecycle, TimeProvider clock)
    : IConsumer<PersonalDataErasureAssessmentRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureAssessmentRequestedV1> context)
    {
        var message = context.Message;
        if (message.Scope != PersonalDataScope.Account) return;
        if (!message.DryRun) throw new InvalidOperationException("Only dry-run privacy assessments are supported.");
        var found = await lifecycle.AssessErasureAsync(message.SubjectUserId, context.CancellationToken);
        // Paid records never block an erasure: they are kept for the retention time and then stripped of names.
        await context.Publish(new PersonalDataErasureAssessmentCompletedV1(
            Guid.NewGuid(), message.RequestId, message.SubjectUserId, BillingErasure.ServiceName, CanProceed: true, HasActiveLegalHold: false,
            0, 0, 0, 0, 0, 0, clock.GetUtcNow().UtcDateTime,
            new Dictionary<string, int>
            {
                ["orders"] = found.Orders, ["payments"] = found.Payments, ["subscriptions"] = found.Subscriptions, ["seats"] = found.Seats,
                ["invoices"] = found.Invoices, ["RetainedFinancialRecords"] = found.Payments + found.Invoices
            }), context.CancellationToken);
    }
}

public sealed class BillingErasureExecutionRequestedConsumer(IBillingLifecycle lifecycle, TimeProvider clock)
    : IConsumer<PersonalDataErasureExecutionRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureExecutionRequestedV1> context)
    {
        var message = context.Message;
        if (message.Scope != PersonalDataScope.Account) return;
        // Safe to repeat: a second delivery changes nothing and completes again under the same id.
        var changed = await lifecycle.EraseUserAsync(message.SubjectUserId, context.CancellationToken);
        var id = BillingErasure.CompletionId(message.RequestId);
        await context.Publish(new PersonalDataErasureExecutionCompletedV1(id, message.RequestId, BillingErasure.ServiceName, changed, clock.GetUtcNow().UtcDateTime),
            publish =>
            {
                publish.MessageId = id;
                publish.CorrelationId = message.RequestId;
            }, context.CancellationToken);
    }
}
