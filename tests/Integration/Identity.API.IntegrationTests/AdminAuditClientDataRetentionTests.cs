using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using FluentAssertions;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notification.Infrastructure.Persistence;
using Npgsql;
using SpeedReading.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Identity.API.IntegrationTests;

/// <summary>Admin audit records stay; their client IP and user agent are cleared after three months (user decision, 27.10.2026).</summary>
public sealed class AdminAuditClientDataRetentionTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private PostgreSqlContainer? _container;
    private string _server = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (!string.IsNullOrWhiteSpace(external)) { _server = external; return; }
        _container = new PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true).Build();
        await _container.StartAsync();
        _server = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public void ClientDataIsKeptForThreeMonths()
    {
        AdminAuditRetention.ClientDataCutoff(Now).Should().Be(new DateTimeOffset(2026, 7, 27, 9, 0, 0, TimeSpan.Zero));
        AdminAuditRetention.ClientDataCutoff(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(3)))
            .Should().Be(new DateTimeOffset(2026, 7, 27, 9, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("coaching")]
    [InlineData("identity")]
    [InlineData("notification")]
    [InlineData("speed-reading")]
    public async Task OldRecordsLoseOnlyTheirClientIpAndUserAgent(string service)
    {
        var connection = new NpgsqlConnectionStringBuilder(_server) { Database = $"audit_retention_{Guid.NewGuid():N}" }.ConnectionString;
        await using var db = Create(service, connection);
        await db.Database.EnsureCreatedAsync();
        try
        {
            var old = Record(Now.AddMonths(-3).AddMinutes(-1));
            var recent = Record(Now.AddMonths(-3).AddMinutes(1));
            db.AddRange(old, recent);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var purger = Purger(service, db);
            (await purger.ClearClientDataBeforeAsync(AdminAuditRetention.ClientDataCutoff(Now), default)).Should().Be(1);
            (await purger.ClearClientDataBeforeAsync(AdminAuditRetention.ClientDataCutoff(Now), default)).Should().Be(0);

            var rows = await db.Set<AdminAuditRecord>().AsNoTracking().ToDictionaryAsync(x => x.Id);
            rows[old.Id].Should().BeEquivalentTo(old with { ClientIp = null, UserAgent = null },
                options => options.Excluding(x => x.OccurredAt));
            rows[old.Id].OccurredAt.Should().BeCloseTo(old.OccurredAt, TimeSpan.FromMilliseconds(1));
            rows[recent.Id].ClientIp.Should().Be("203.0.113.7");
            rows[recent.Id].UserAgent.Should().Be("Test browser");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task TheDailyWorkerIsRegisteredUnlessTurnedOff()
    {
        static ServiceProvider Build(string? enabled)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                enabled is null ? [] : [new(AdminAuditRetention.EnabledSetting, enabled)]).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<CoachingDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddAdminAuditClientDataRetention<CoachingAdminAuditClientDataPurger>(configuration);
            return services.BuildServiceProvider();
        }

        await using (var on = Build(null))
            on.GetServices<IHostedService>().Should().ContainSingle(x => x is AdminAuditClientDataRetentionWorker);
        await using (var off = Build("false"))
        {
            off.GetServices<IHostedService>().Should().BeEmpty();
            off.CreateScope().ServiceProvider.GetRequiredService<IAdminAuditClientDataPurger>()
                .Should().BeOfType<CoachingAdminAuditClientDataPurger>();
        }
    }

    private static AdminAuditRecord Record(DateTimeOffset occurredAt) => new(
        Guid.NewGuid(), occurredAt, "test", Guid.NewGuid().ToString(), "SystemAdmin", null, "POST", "/api/admin/things",
        200, Guid.NewGuid().ToString("N"), "203.0.113.7", "Test browser", "Update", "Thing", "42", "[\"Name\"]");

    private static DbContext Create(string service, string connection) => service switch
    {
        "coaching" => new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection).Options),
        "identity" => new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options),
        "notification" => new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(connection).Options),
        _ => new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(connection).Options)
    };

    private static IAdminAuditClientDataPurger Purger(string service, DbContext db) => service switch
    {
        "coaching" => new CoachingAdminAuditClientDataPurger((CoachingDbContext)db),
        "identity" => new IdentityAdminAuditClientDataPurger((IdentityDbContext)db),
        "notification" => new NotificationAdminAuditClientDataPurger((NotificationDbContext)db),
        _ => new SpeedReadingAdminAuditClientDataPurger((OwnedSpeedReadingDbContext)db)
    };
}
