using System.Reflection;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Application.Interfaces;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class NotificationEmailErasureTests(PostgresFixture postgres)
{
    [Fact]
    public async Task PreviouslyClaimedEmail_IsNotSentAfterAccountErasure()
    {
        var services = new ServiceCollection();
        services.AddDbContext<NotificationDbContext>(x => x.UseNpgsql(postgres.ConnectionString));
        var email = new RecordingEmail(); services.AddSingleton<IEmailService>(email);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var subject = Guid.NewGuid(); var protection = new EphemeralDataProtectionProvider();
            await new EmailDeliveryQueue(db, protection).QueueAsync(Guid.NewGuid(), "Test", "test@example.com", "Test", "Test", subject);
            var worker = new EmailDeliveryWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance, protection);
            // Split the actual worker at its lease boundary to reproduce a queued-in-memory email.
            var claim = (Task)typeof(EmailDeliveryWorker).GetMethod("ClaimNextAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(worker, [CancellationToken.None])!;
            await claim; var item = claim.GetType().GetProperty("Result")!.GetValue(claim);
            Assert.NotNull(item);
            await new NotificationErasureExecutionService(db, TimeProvider.System).ExecuteAsync(
                new(Guid.NewGuid(), Guid.NewGuid(), subject, DateTime.UtcNow, PersonalDataScope.Account), CancellationToken.None);
            await (Task)typeof(EmailDeliveryWorker).GetMethod("DeliverAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(worker, [item, CancellationToken.None])!;
            Assert.Equal(0, email.Calls);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
    private sealed class RecordingEmail : IEmailService
    {
        public int Calls;
        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        { Calls++; return Task.CompletedTask; }
    }
}
