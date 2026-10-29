using System.Data.Common;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Domain.Vocabulary;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Decision 29.10.2026: finishing a 20-word vocabulary quiz no longer sends one round of queries per word (~80);
/// the words, the student's progress and gamification are read once and the result is the same.
/// </summary>
[Collection("Database")]
public sealed class SpeedReadingVocabularyBatchPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_twenty_word_quiz_is_recorded_with_a_handful_of_queries()
    {
        var counter = new CommandCounter();
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).AddInterceptors(counter).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var items = Enumerable.Range(1, 20).Select(index => VocabularyItem.Create(Guid.NewGuid(), $"kelime{index}", $"anlam{index}",
                null, null, null, "Genel", 1, null, user, now)).ToList();
            db.VocabularyItems.AddRange(items);
            await db.SaveChangesAsync();
            var recorder = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedVocabularyProgressRecorder")!;
            var answers = items.Select((item, index) => (item.Id, index % 4 != 0)).ToList();

            counter.Count = 0;
            var found = await (Task<bool>)recorder.GetMethod("RecordManyAsync", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [db, user, answers, now, true, CancellationToken.None])!;
            await db.SaveChangesAsync();

            Assert.True(found);
            Assert.InRange(counter.Count, 1, 8);
            db.ChangeTracker.Clear();
            Assert.Equal(20, await db.UserVocabularyProgresses.CountAsync(item => item.UserId == user));
            Assert.Equal(15, await db.UserVocabularyProgresses.CountAsync(item => item.UserId == user && item.Box == 2));
            Assert.Equal(1, await db.UserGamifications.CountAsync(item => item.UserId == user));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
