using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Privacy;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Privacy;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Review H2b (finding 9): a Speed Reading user can download every record the service keeps about them (KVKK access
/// right), the same way coaching does; other users' rows and internal working data or secrets are not included.
/// </summary>
public sealed class SpeedReadingDataExportTests
{
    [Fact]
    public async Task The_export_contains_the_users_own_records_including_payments_and_nothing_of_others()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await ExportsOnlyTheUsersRecords(db);
    }

    internal static async Task ExportsOnlyTheUsersRecords(OwnedSpeedReadingDbContext db, Guid? planId = null)
    {
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.UserProfiles.AddRange(SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), user, now), SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), other, now));
        db.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(Guid.NewGuid(), user, SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), now));
        Add(db, "LegacyPayment", new { Id = Guid.NewGuid(), UserId = user, UserName = "Ada", UserEmail = "ada@example.test", Amount = 100m, Status = "Success", PlanId = planId ?? Guid.NewGuid(),
            Provider = "Iyzico", ProviderToken = "secret-checkout-token", ProviderResponse = "{\"raw\":true}", CreatedAt = now, UpdatedAt = now });
        Add(db, "LegacyBankTransferPaymentRequest", new { Id = Guid.NewGuid(), UserId = user, UserName = "Ada", UserEmail = "ada@example.test", Amount = 250m, PlanId = planId ?? Guid.NewGuid(),
            PaymentReference = "EFT-77", Status = "Approved", CreatedAt = now, UpdatedAt = now });
        Add(db, "LegacyRsvpSession", new { Id = Guid.NewGuid(), UserId = user, TextContent = "Benim metnim", CreatedAt = now });
        Add(db, "LegacyRsvpSession", new { Id = Guid.NewGuid(), UserId = other, TextContent = "Başkasının metni", CreatedAt = now });
        await db.SaveChangesAsync();

        ISpeedReadingPersonalDataExport service = new SpeedReadingPersonalDataExportService(db, TimeProvider.System);
        var export = await service.ExportAsync(user, CancellationToken.None);
        var json = JsonSerializer.Serialize(export);

        export.UserId.Should().Be(user);
        export.SchemaVersion.Should().Be("speed-reading-1.0");
        export.Sections["profile"].Should().ContainSingle();
        export.Sections["institutionMemberships"].Should().ContainSingle();
        export.Sections["payments"].Should().ContainSingle().Which["Amount"].Should().Be(100m);
        export.Sections["bankTransfers"].Should().ContainSingle().Which["PaymentReference"].Should().Be("EFT-77");
        export.Sections["rsvpSessions"].Should().ContainSingle().Which["TextContent"].Should().Be("Benim metnim");
        json.Should().NotContain("Başkasının metni");
        json.Should().NotContain("secret-checkout-token");
        json.Should().NotContain("ProviderResponse");
        json.Should().NotContain(other.ToString());
    }

    internal static void Add(OwnedSpeedReadingDbContext db, string name, object values)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType($"SpeedReading.Infrastructure.Legacy.{name}")!;
        var entity = Activator.CreateInstance(type)!;
        foreach (var property in values.GetType().GetProperties())
            type.GetProperty(property.Name)!.SetValue(entity, property.GetValue(values));
        db.Add(entity);
    }
}

/// <summary>Every export query runs against the real provider (Npgsql translation of the generic filters).</summary>
[Collection("Database")]
public sealed class SpeedReadingDataExportPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_export_runs_on_postgres()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var now = DateTime.UtcNow;
            var product = Guid.NewGuid();
            var plan = Guid.NewGuid();
            SpeedReadingDataExportTests.Add(db, "LegacyProduct", new { Id = product, Slug = "hizliokuma", Name = "Hızlı Okuma", IsActive = true, IsPublic = true, CreatedAt = now, UpdatedAt = now });
            SpeedReadingDataExportTests.Add(db, "LegacySubscriptionPlan", new { Id = plan, ProductId = product, Slug = "aylik", Name = "Aylık", Price = 100m, IsActive = true,
                IsPublic = true, BillingPeriod = "Monthly", DurationDays = (int?)30, CreatedAt = now });
            await db.SaveChangesAsync();
            await SpeedReadingDataExportTests.ExportsOnlyTheUsersRecords(db, plan);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
