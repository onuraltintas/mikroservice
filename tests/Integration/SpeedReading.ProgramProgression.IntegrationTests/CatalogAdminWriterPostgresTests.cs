using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Content;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

/// <summary>
/// Review H1 (finding 5): production runs the owned context with EnableRetryOnFailure. Creating or editing an
/// active exercise takes a title lock in a transaction; that transaction must run inside the execution strategy,
/// otherwise EF refuses it ("does not support user-initiated transactions") and the admin gets a 500.
/// </summary>
public sealed class CatalogAdminWriterPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Configuration = """{"engineType":"focus"}""";

    private OwnedSpeedReadingDbContext Open() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseNpgsql(postgres.ConnectionString, builder =>
        {
            builder.EnableRetryOnFailure(2, TimeSpan.Zero, null);
            builder.MigrationsHistoryTable("__ef_migrations_history", "speed_reading");
        })
        .Options);

    private static ISpeedReadingCatalogAdminWriter Writer(OwnedSpeedReadingDbContext db) =>
        (ISpeedReadingCatalogAdminWriter)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingCatalogAdminWriter")!, db)!;

    [Fact]
    public async Task Active_exercises_can_be_created_and_edited_when_the_context_retries_on_failure()
    {
        await using var setup = Open();
        await setup.Database.EnsureDeletedAsync();
        await setup.Database.MigrateAsync();
        var actor = Guid.NewGuid();
        var type = Guid.NewGuid();
        setup.ExerciseTypes.Add(ExerciseType.Create(type, "Fixation", "Fixation", "focus"));
        await setup.SaveChangesAsync();

        await using (var db = Open())
        {
            var created = await Writer(db).CreateExerciseAsync(actor,
                new CreateExerciseRequest("Odak 1", null, 1, type, Configuration, null), "exercise-create-0001");
            Assert.True(created.IsActive);

            var duplicate = () => Writer(db).CreateExerciseAsync(actor,
                new CreateExerciseRequest(" odak 1 ", null, 1, type, Configuration, null), "exercise-create-0002");
            await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(duplicate);
        }

        await using (var db = Open())
        {
            var id = (await db.Exercises.AsNoTracking().SingleAsync(item => item.Title == "Odak 1")).Id;
            var updated = await Writer(db).UpdateExerciseAsync(actor, id,
                new UpdateExerciseRequest("Odak 2", null, 2, type, Configuration, null), "exercise-update-0001");
            Assert.Equal("Odak 2", updated.Title);
        }

        await using var check = Open();
        Assert.Equal(["Odak 2"], await check.Exercises.AsNoTracking().Select(item => item.Title).ToListAsync());
    }
}
