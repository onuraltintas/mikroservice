using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingBaselineSelectionPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task General_program_selection_translates_to_postgres_and_preserves_decimal_boundaries()
    {
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        // This connection belongs exclusively to the disposable Testcontainers database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var age = Guid.NewGuid();
            db.AgeGroupConfigurations.Add(AgeGroupConfiguration.Create(age, "test", "Test", 13, 16,
                100, 200, 300, 70, 15, 1, 1, true, null, Guid.NewGuid(), DateTime.UtcNow));
            ProgramTemplate Template(string name, int min, int max, int type, int order) =>
                ProgramTemplate.Import(Guid.NewGuid(), name, "", age, min, max, "{}", 1, 2, 5,
                    4, 28, true, order, type, type == 1 ? "LGS" : null, false,
                    DateTime.UtcNow, null, null, null);
            db.ProgramTemplates.AddRange(Template("Beginner", 0, 40, 0, 1),
                Template("Middle", 40, 70, 0, 2), Template("Exam", 0, 100, 1, 0));
            await db.SaveChangesAsync();
            var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!;
            var service = Activator.CreateInstance(type, db, null)!;
            var method = type.GetMethod("FindBaselineTemplateAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            foreach (var score in new[] { 40m, 40.5m, 70m })
            {
                var selected = await (Task<ProgramTemplate?>)method.Invoke(service, [age, score, CancellationToken.None])!;
                Assert.Equal("Middle", selected?.Name);
            }
            var unmatched = await (Task<ProgramTemplate?>)method.Invoke(service, [age, 71m, CancellationToken.None])!;
            Assert.Null(unmatched);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
