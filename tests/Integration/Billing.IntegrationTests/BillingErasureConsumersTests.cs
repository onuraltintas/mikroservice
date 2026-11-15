using Billing.Application;
using Billing.Infrastructure;
using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Billing.IntegrationTests;

/// <summary>Billing answers Identity's account erasure: what it holds (dry run) and that it has erased.</summary>
public sealed class BillingErasureConsumersTests
{
    private static readonly Guid Subject = Guid.Parse("11111111-1111-4111-8111-111111111111");

    private sealed class Lifecycle : IBillingLifecycle
    {
        public List<Guid> Erased { get; } = [];
        public Task<int> SendRemindersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ErasureAssessment> AssessErasureAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ErasureAssessment(3, 2, 1, 1, 1));
        public Task<int> EraseUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            Erased.Add(userId);
            return Task.FromResult(Erased.Count == 1 ? 4 : 0);
        }
        public Task<int> RedactExpiredAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static async Task<(ServiceProvider Provider, ITestHarness Harness, Lifecycle Lifecycle)> StartAsync()
    {
        var lifecycle = new Lifecycle();
        var provider = new ServiceCollection()
            .AddSingleton<IBillingLifecycle>(lifecycle)
            .AddSingleton(TimeProvider.System)
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<BillingErasureAssessmentRequestedConsumer>();
                x.AddConsumer<BillingErasureExecutionRequestedConsumer>();
                x.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
            })
            .BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (provider, harness, lifecycle);
    }

    [Fact]
    public async Task An_account_assessment_reports_what_billing_holds_and_never_blocks_the_erasure()
    {
        var (provider, harness, _) = await StartAsync();
        try
        {
            var request = Guid.NewGuid();
            await harness.Bus.Publish(new PersonalDataErasureAssessmentRequestedV1(Guid.NewGuid(), request, Subject, DateTime.UtcNow, true));
            Assert.True(await harness.Published.Any<PersonalDataErasureAssessmentCompletedV1>(x => x.Context.Message.RequestId == request));
            var answer = harness.Published.Select<PersonalDataErasureAssessmentCompletedV1>().Single().Context.Message;
            Assert.Equal(("Billing", Subject, true, false), (answer.ServiceName, answer.SubjectUserId, answer.CanProceed, answer.HasActiveLegalHold));
            Assert.Equal((3, 2, 1, 1, 1, 3), (answer.RecordCounts!["orders"], answer.RecordCounts["payments"], answer.RecordCounts["subscriptions"],
                answer.RecordCounts["seats"], answer.RecordCounts["invoices"], answer.RecordCounts["RetainedFinancialRecords"]));
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public async Task An_account_erasure_closes_billing_and_completes_under_one_id_however_often_it_is_delivered()
    {
        var (provider, harness, lifecycle) = await StartAsync();
        try
        {
            var request = Guid.NewGuid();
            var message = new PersonalDataErasureExecutionRequestedV1(Guid.NewGuid(), request, Subject, DateTime.UtcNow, PersonalDataScope.Account);
            await harness.Bus.Publish(message);
            Assert.True(await harness.Published.Any<PersonalDataErasureExecutionCompletedV1>(x => x.Context.Message.RequestId == request));
            await harness.Bus.Publish(message);
            for (var attempt = 0; attempt < 200 && lifecycle.Erased.Count < 2; attempt++) await Task.Delay(50);
            await Task.Delay(200);
            // Both deliveries answer under the same message id, so Identity counts the completion once.
            var completions = harness.Published.Select<PersonalDataErasureExecutionCompletedV1>().ToArray();
            Assert.All(completions, item => Assert.Equal(("Billing", BillingErasure.CompletionId(request), (Guid?)BillingErasure.CompletionId(request)),
                (item.Context.Message.ServiceName, item.Context.Message.EventId, item.Context.MessageId)));
            Assert.Equal([Subject, Subject], lifecycle.Erased);
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public async Task A_request_for_one_product_alone_leaves_billing_untouched()
    {
        var (provider, harness, lifecycle) = await StartAsync();
        try
        {
            await harness.Bus.Publish(new PersonalDataErasureAssessmentRequestedV1(Guid.NewGuid(), Guid.NewGuid(), Subject, DateTime.UtcNow, true, PersonalDataScope.Coaching));
            await harness.Bus.Publish(new PersonalDataErasureExecutionRequestedV1(Guid.NewGuid(), Guid.NewGuid(), Subject, DateTime.UtcNow, PersonalDataScope.SpeedReading));
            Assert.True(await harness.Consumed.Any<PersonalDataErasureAssessmentRequestedV1>());
            Assert.True(await harness.Consumed.Any<PersonalDataErasureExecutionRequestedV1>());
            Assert.Empty(lifecycle.Erased);
            Assert.False(harness.Published.Select<PersonalDataErasureAssessmentCompletedV1>().Any());
            Assert.False(harness.Published.Select<PersonalDataErasureExecutionCompletedV1>().Any());
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public void The_completion_id_is_stable_for_a_request_and_differs_between_requests()
    {
        var first = Guid.NewGuid();
        Assert.Equal(BillingErasure.CompletionId(first), BillingErasure.CompletionId(first));
        Assert.NotEqual(BillingErasure.CompletionId(first), BillingErasure.CompletionId(Guid.NewGuid()));
    }
}
