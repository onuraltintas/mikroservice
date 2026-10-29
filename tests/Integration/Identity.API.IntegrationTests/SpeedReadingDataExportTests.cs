using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Privacy;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Privacy;

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
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.UserProfiles.AddRange(SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), user, now), SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), other, now));
        db.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(Guid.NewGuid(), user, SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), now));
        Add(db, "LegacyPayment", new { Id = Guid.NewGuid(), UserId = user, UserName = "Ada", UserEmail = "ada@example.test", Amount = 100m, Status = "Success",
            Provider = "Iyzico", ProviderToken = "secret-checkout-token", ProviderResponse = "{\"raw\":true}", CreatedAt = now, UpdatedAt = now });
        Add(db, "LegacyBankTransferPaymentRequest", new { Id = Guid.NewGuid(), UserId = user, UserName = "Ada", UserEmail = "ada@example.test", Amount = 250m,
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

    private static void Add(OwnedSpeedReadingDbContext db, string name, object values)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType($"SpeedReading.Infrastructure.Legacy.{name}")!;
        var entity = Activator.CreateInstance(type)!;
        foreach (var property in values.GetType().GetProperties())
            type.GetProperty(property.Name)!.SetValue(entity, property.GetValue(values));
        db.Add(entity);
    }
}
