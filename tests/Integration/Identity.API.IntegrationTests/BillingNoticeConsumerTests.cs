using System.Collections.Concurrent;
using EduPlatform.Shared.Contracts.Events.Billing;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Configuration;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;

namespace Identity.API.IntegrationTests;

public sealed class BillingNoticeConsumerTests
{
    [Fact]
    public async Task The_buyer_is_emailed_in_the_products_name_with_a_link_to_its_own_subscription_page()
    {
        var student = Guid.NewGuid();
        var (provider, mail) = await StartAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            await harness.Bus.Publish(Notice(BillingNoticeKinds.OrderApproved, student, "ada@example.test", ["coaching"]));
            await harness.Bus.Publish(Notice(BillingNoticeKinds.OrderRejected, student, "okur@example.test", ["speed-reading"], "Ödeme <b>gelmedi</b>."));
            await harness.Bus.Publish(Notice(BillingNoticeKinds.Expiring, student, "paket@example.test", ["coaching", "speed-reading"], "Seats"));
            await TestHarnessTimeouts.UntilAsync(() => mail.Messages.Count >= 3);

            var approved = mail.Messages.Single(x => x.Recipient == "ada@example.test");
            approved.Subject.Should().Be("maxkoç: Ödemen onaylandı");
            approved.Body.Should().Contain("TEST Aylık").And.Contain("https://app.example.test/coaching-portal/account/payments");
            approved.SubjectUserId.Should().Be(student);
            var rejected = mail.Messages.Single(x => x.Recipient == "okur@example.test");
            rejected.Subject.Should().StartWith("Master Hızlı Okuma:");
            rejected.Body.Should().Contain("EDU-7K4Q2M").And.Contain("/student/subscription")
                .And.Contain("&lt;b&gt;gelmedi&lt;/b&gt;").And.NotContain("<b>gelmedi</b>");
            var bundle = mail.Messages.Single(x => x.Recipient == "paket@example.test");
            bundle.Subject.Should().StartWith("eduivme:").And.Contain("sona eriyor");
            System.Net.WebUtility.HtmlDecode(bundle.Body).Should().Contain("koltuk verdiğin kullanıcıların erişimi de kapanır");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public async Task The_seller_is_emailed_about_declared_payments_and_withdrawal_requests_with_a_link_to_the_admin_panel()
    {
        var (provider, mail) = await StartAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            await harness.Bus.Publish(Notice(BillingNoticeKinds.PaymentDeclared, null, "finans@example.test", ["coaching"], "Ada Öğrenci, EDU-7K4Q2M"));
            await harness.Bus.Publish(Notice(BillingNoticeKinds.WithdrawalRequested, null, "finans@example.test", ["coaching"], "Ada Öğrenci, EDU-7K4Q2M"));
            await TestHarnessTimeouts.UntilAsync(() => mail.Messages.Count >= 2);

            var declared = mail.Messages.Single(x => x.Subject.Contains("ödeme bildirimi", StringComparison.OrdinalIgnoreCase));
            System.Net.WebUtility.HtmlDecode(declared.Body).Should().Contain("https://app.example.test/dashboard/billing/orders").And.Contain("200,00 TL").And.Contain("Ada Öğrenci");
            declared.SubjectUserId.Should().BeNull();
            declared.ConsumerType.Should().Be("BillingNoticeConsumer.Seller");
            mail.Messages.Single(x => x.Subject.Contains("cayma", StringComparison.OrdinalIgnoreCase)).Body
                .Should().Contain("https://app.example.test/dashboard/billing/withdrawals");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public async Task An_institution_is_emailed_as_maxkampus_in_siz_with_a_link_to_maxkampus_plan()
    {
        // 20.11.2026: maxkampüs notices said "Kurs Yönetimi", "Aboneliğin…" and opened maxkoç's payments page.
        var owner = Guid.NewGuid();
        var (provider, mail) = await StartAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            await harness.Bus.Publish(Notice(BillingNoticeKinds.Expiring, owner, "kurum@example.test", ["course-management"]));
            await harness.Bus.Publish(Notice(BillingNoticeKinds.OrderApproved, owner, "onay@example.test", ["course-management"]));
            await TestHarnessTimeouts.UntilAsync(() => mail.Messages.Count >= 2);

            var expiring = mail.Messages.Single(x => x.Recipient == "kurum@example.test");
            expiring.Subject.Should().Be("maxkampüs: Planınız yakında sona eriyor");
            var body = System.Net.WebUtility.HtmlDecode(expiring.Body);
            body.Should().Contain("https://maxkampus.com/plan").And.Contain("Planım ve ödemeler").And.Contain("salt okunur")
                .And.NotContain("coaching-portal").And.NotContain("Aboneliğin");
            mail.Messages.Single(x => x.Recipient == "onay@example.test").Subject.Should().Be("maxkampüs: Ödemeniz onaylandı");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public void Every_kind_has_an_institution_text_too()
    {
        var kinds = typeof(BillingNoticeKinds).GetFields().Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!).ToArray();
        var describe = typeof(BillingNoticeConsumer).GetMethod("Describe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        foreach (var kind in kinds)
            describe.Invoke(null, [Notice(kind, Guid.NewGuid(), "a@example.test", ["course-management"])]).Should().NotBeNull($"{kind} needs an institution text");
        BillingNoticeConsumer.BuyerLink(new PublicAppUrlOptions { CourseManagementBaseUrl = "https://kampus.example.test/" }, ["course-management"])
            .Should().Be("https://kampus.example.test/plan");
    }

    [Fact]
    public void Every_kind_the_billing_service_sends_has_a_text()
    {
        var kinds = typeof(BillingNoticeKinds).GetFields().Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!).ToArray();
        kinds.Should().HaveCount(12);
        var describe = typeof(BillingNoticeConsumer).GetMethod("Describe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        foreach (var kind in kinds)
            describe.Invoke(null, [Notice(kind, Guid.NewGuid(), "a@example.test", ["coaching"])]).Should().NotBeNull($"{kind} needs a text");
        describe.Invoke(null, [Notice("Unknown", null, "a@example.test", ["coaching"])]).Should().BeNull();
    }

    private static BillingNoticeV1 Notice(string kind, Guid? user, string email, string[] products, string? detail = null) =>
        new(Guid.NewGuid(), user, email, kind, products, "TEST Aylık", new DateTime(2026, 12, 15, 9, 0, 0, DateTimeKind.Utc), detail, "EDU-7K4Q2M", 200m, "TRY", DateTime.UtcNow);

    private static async Task<(ServiceProvider, RecordingEmailQueue)> StartAsync()
    {
        var mail = new RecordingEmailQueue();
        var services = new ServiceCollection();
        services.AddSingleton<IEmailDeliveryQueue>(mail);
        services.Configure<PublicAppUrlOptions>(options => options.BaseUrl = "https://app.example.test");
        services.AddMassTransitTestHarness(x =>
        {
            x.UseSuiteTimeouts();
            x.AddConsumer<BillingNoticeConsumer>();
            x.UsingInMemory((context, config) => config.ConfigureEndpoints(context));
        });
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<ITestHarness>().Start();
        return (provider, mail);
    }

    private sealed class RecordingEmailQueue : IEmailDeliveryQueue
    {
        public ConcurrentQueue<QueuedMessage> Messages { get; } = new();
        public Task QueueAsync(Guid messageId, string consumerType, string recipient, string subject, string body,
            Guid? subjectUserId = null, CancellationToken cancellationToken = default)
        {
            Messages.Enqueue(new QueuedMessage(messageId, consumerType, recipient, subject, body, subjectUserId));
            return Task.CompletedTask;
        }
    }

    private sealed record QueuedMessage(Guid MessageId, string ConsumerType, string Recipient, string Subject, string Body, Guid? SubjectUserId);
}
