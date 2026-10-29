using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SpeedReading.Application.Analytics;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H4 (finding 21): the platform/institution benchmark is cached per calendar day, so reports opened a few
/// seconds apart reuse it instead of scanning every reading session again (and filling the cache with one-off keys).
/// </summary>
public sealed class SpeedReadingBenchmarkCacheTests
{
    [Fact]
    public async Task Reports_opened_seconds_apart_share_one_benchmark_entry()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid();
        var completed = DateTime.UtcNow.Date.AddHours(1);
        db.ReadingSessions.Add(ReadingSession.Import(Guid.NewGuid(), student, Guid.NewGuid(), 60, 180, 4, 5, 80m, 70m,
            completed, completed, null, null, null));
        await db.SaveChangesAsync();
        var cache = new CountingCache();
        var analytics = (ILegacySpeedReadingAnalytics)Activator.CreateInstance(
            typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAnalytics")!,
            db, cache)!;
        var now = DateTime.UtcNow.Date.AddHours(12);

        await analytics.GetStudentReadingSpeedAsync(student, now.AddDays(-30), now, null, CancellationToken.None);
        await analytics.GetStudentReadingSpeedAsync(student, now.AddDays(-30).AddSeconds(7), now.AddSeconds(7), null, CancellationToken.None);

        cache.Keys.Where(key => key.StartsWith("speed-reading:benchmark", StringComparison.Ordinal)).Distinct().Should().ContainSingle();
    }

    private sealed class CountingCache : IMemoryCache
    {
        private readonly MemoryCache inner = new(new MemoryCacheOptions());
        public List<string> Keys { get; } = [];
        public ICacheEntry CreateEntry(object key)
        {
            Keys.Add(key.ToString()!);
            return inner.CreateEntry(key);
        }
        public void Dispose() => inner.Dispose();
        public void Remove(object key) => inner.Remove(key);
        public bool TryGetValue(object key, out object? value) => inner.TryGetValue(key, out value);
    }
}
