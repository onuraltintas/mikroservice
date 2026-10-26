using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Application.Commands.SubmitSupportRequest;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Identity.API.IntegrationTests;

/// <summary>
/// The two lease workers against real PostgreSQL (FOR UPDATE SKIP LOCKED, lease-guarded
/// ExecuteUpdate): success, retry with backoff, the attempt limit and a lost lease.
/// </summary>
public sealed class NotificationDeliveryWorkerTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string _connectionString = string.Empty;
    private readonly EphemeralDataProtectionProvider _protection = new();

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        string server;
        if (!string.IsNullOrWhiteSpace(external))
        {
            server = external;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true).Build();
            await _container.StartAsync();
            server = _container.GetConnectionString();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(server) { Database = $"notification_worker_{Guid.NewGuid():N}" }.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync();
        }

        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task Email_IsSentOnce_AndMarkedSent()
    {
        var email = new FakeEmail();
        var id = await QueueEmailAsync();
        await using var provider = Services(email);

        (await EmailWorker(provider).ProcessOnceAsync(CancellationToken.None)).Should().BeTrue();
        (await EmailWorker(provider).ProcessOnceAsync(CancellationToken.None)).Should().BeFalse("nothing else is due");

        email.Sent.Should().Equal("person@example.test");
        var stored = await ReadEmailAsync(id);
        stored.Status.Should().Be(EmailDeliveryStatus.Sent);
        stored.LeaseToken.Should().BeNull();
    }

    [Fact]
    public async Task FailedEmail_IsRetriedWithBackoff_AndGivesUpAfterTheAttemptLimit()
    {
        var email = new FakeEmail { Failure = new InvalidOperationException("smtp down") };
        var id = await QueueEmailAsync();
        await using var provider = Services(email);
        var worker = EmailWorker(provider);

        var before = DateTime.UtcNow;
        await worker.ProcessOnceAsync(CancellationToken.None);
        var retried = await ReadEmailAsync(id);
        retried.Status.Should().Be(EmailDeliveryStatus.Pending);
        retried.NextAttemptAt.Should().BeAfter(before.AddSeconds(1));
        retried.LastError.Should().Be("smtp down");

        await SetAttemptsAsync(id, EmailDeliveryWorker.MaxAttempts - 1);
        await worker.ProcessOnceAsync(CancellationToken.None);
        (await ReadEmailAsync(id)).Status.Should().Be(EmailDeliveryStatus.Failed);
    }

    [Fact]
    public async Task WorkerThatLostItsLease_DoesNotOverwriteTheNewOwner()
    {
        var id = await QueueEmailAsync();
        var newOwner = Guid.NewGuid();
        // While "sending", another worker takes the row over (lease expired and re-claimed).
        var email = new FakeEmail { DuringSend = () => StealLeaseAsync(id, newOwner) };
        await using var provider = Services(email);

        await EmailWorker(provider).ProcessOnceAsync(CancellationToken.None);

        var stored = await ReadEmailAsync(id);
        stored.Status.Should().Be(EmailDeliveryStatus.Processing);
        stored.LeaseToken.Should().Be(newOwner);
    }

    [Theory]
    [InlineData(SupportForwardResult.Sent, SupportForwardDeliveryStatus.Sent)]
    [InlineData(SupportForwardResult.Rejected, SupportForwardDeliveryStatus.Failed)]
    [InlineData(SupportForwardResult.Transient, SupportForwardDeliveryStatus.Pending)]
    public async Task SupportForward_FollowsTheIdentityAnswer(SupportForwardResult answer, SupportForwardDeliveryStatus expected)
    {
        Guid forwardId;
        await using (var context = CreateContext())
        {
            var request = new SupportRequest(Guid.NewGuid(), "Ada", "Lovelace", "a@example.test", "Konu", "Mesaj", "fwd");
            var forward = SupportForwardDelivery.Create(request.Id);
            context.SupportRequests.Add(request);
            context.SupportForwardDeliveries.Add(forward);
            await context.SaveChangesAsync();
            forwardId = forward.Id;
        }

        await using var provider = Services(new FakeEmail(), answer);
        var worker = new SupportForwardDeliveryWorker(
            provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<SupportForwardDeliveryWorker>.Instance);

        (await worker.ProcessOnceAsync(CancellationToken.None)).Should().BeTrue();

        await using var check = CreateContext();
        (await check.SupportForwardDeliveries.AsNoTracking().SingleAsync(x => x.Id == forwardId)).Status.Should().Be(expected);
    }

    [Fact]
    public void Backoff_DoublesUpToAbout4Minutes()
    {
        var now = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

        EmailDeliveryWorker.RetryAt(now, 1).Should().Be(now.AddSeconds(2));
        EmailDeliveryWorker.RetryAt(now, 4).Should().Be(now.AddSeconds(16));
        EmailDeliveryWorker.RetryAt(now, 12).Should().Be(now.AddSeconds(256));
    }

    private NotificationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(_connectionString).Options);

    private ServiceProvider Services(FakeEmail email, SupportForwardResult forward = SupportForwardResult.Sent)
    {
        var services = new ServiceCollection();
        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(_connectionString));
        services.AddSingleton<IEmailService>(email);
        services.AddSingleton<IIdentityInternalService>(new FixedForward(forward));
        return services.BuildServiceProvider();
    }

    private EmailDeliveryWorker EmailWorker(ServiceProvider provider) => new(
        provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance, _protection);

    private async Task<Guid> QueueEmailAsync()
    {
        var messageId = Guid.NewGuid();
        await using var context = CreateContext();
        await new EmailDeliveryQueue(context, _protection).QueueAsync(messageId, "Test", "person@example.test", "Konu", "Gövde");
        return (await context.EmailDeliveries.AsNoTracking().SingleAsync(x => x.MessageId == messageId)).Id;
    }

    private async Task<EmailDelivery> ReadEmailAsync(Guid id)
    {
        await using var context = CreateContext();
        return await context.EmailDeliveries.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task SetAttemptsAsync(Guid id, int attempts)
    {
        await using var context = CreateContext();
        await context.EmailDeliveries.Where(x => x.Id == id).ExecuteUpdateAsync(x => x
            .SetProperty(d => d.AttemptCount, attempts)
            .SetProperty(d => d.NextAttemptAt, DateTime.UtcNow.AddSeconds(-1)));
    }

    private async Task StealLeaseAsync(Guid id, Guid newOwner)
    {
        await using var context = CreateContext();
        await context.EmailDeliveries.Where(x => x.Id == id).ExecuteUpdateAsync(x => x
            .SetProperty(d => d.LeaseToken, newOwner)
            .SetProperty(d => d.LeaseUntil, DateTime.UtcNow.AddMinutes(5)));
    }

    private sealed class FakeEmail : IEmailService
    {
        public List<string> Sent { get; } = [];
        public Exception? Failure { get; init; }
        public Func<Task>? DuringSend { get; init; }

        public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (DuringSend is not null) await DuringSend();
            if (Failure is not null) throw Failure;
            Sent.Add(to);
        }
    }

    private sealed class FixedForward(SupportForwardResult result) : IIdentityInternalService
    {
        public Task<SupportForwardResult> ForwardSupportRequestAsync(
            SubmitSupportRequestCommand command, Guid supportRequestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);

        public Task<bool> VerifySupportCaptchaAsync(string? token, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
