using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CourseManagement.API;
using CourseManagement.Application.CourseAssignments;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CourseManagement.Application.UnitTests;

public sealed class CourseAssignmentAttachmentCleanupTests
{
    [Fact]
    public async Task Cleanup_deletes_expired_file_and_metadata_but_preserves_unexpired_attachment()
    {
        var databaseName = $"course-attachment-cleanup-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<CourseManagementDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var now = DateTimeOffset.UtcNow;
        var expired = CreateAttachment("expired-key", now.AddDays(-181));
        var active = CreateAttachment("active-key", now.AddDays(-179));
        await using (var seed = new CourseManagementDbContext(options))
        {
            seed.AssignmentAttachments.AddRange(expired, active);
            await seed.SaveChangesAsync();
        }

        var storage = new TrackingAttachmentStorage("expired-key", "active-key");
        var services = new ServiceCollection();
        services.AddDbContext<CourseManagementDbContext>(builder => builder.UseInMemoryDatabase(databaseName));
        services.AddSingleton<ICourseAssignmentAttachmentStorage>(storage);
        using var provider = services.BuildServiceProvider();
        var cleanup = new CourseAssignmentAttachmentCleanupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CourseAssignmentAttachmentCleanupService>.Instance);

        await cleanup.StartAsync(CancellationToken.None);
        try
        {
            Assert.Equal("expired-key", await storage.DeleteAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(await WaitUntilMetadataIsRemovedAsync(options, expired.Id), "expired metadata should be removed after its file");
        }
        finally
        {
            await cleanup.StopAsync(CancellationToken.None);
        }

        await using var verification = new CourseManagementDbContext(options);
        Assert.False(await verification.AssignmentAttachments.AnyAsync(item => item.Id == expired.Id));
        Assert.True(await verification.AssignmentAttachments.AnyAsync(item => item.Id == active.Id));
        Assert.DoesNotContain("expired-key", storage.Files);
        Assert.Contains("active-key", storage.Files);
    }

    [Fact]
    public async Task Failed_orphan_delete_is_queued_and_worker_retries_it()
    {
        var databaseName = $"course-attachment-orphan-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<CourseManagementDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var storage = new TrackingAttachmentStorage("orphan-key") { FailDeletes = true };
        var services = new ServiceCollection();
        services.AddDbContext<CourseManagementDbContext>(builder => builder.UseInMemoryDatabase(databaseName));
        services.AddSingleton<ICourseAssignmentAttachmentStorage>(storage);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var cleanupQueue = new CourseAssignmentAttachmentCleanupQueue(scopeFactory,
            NullLogger<CourseAssignmentAttachmentCleanupQueue>.Instance);

        await cleanupQueue.DeleteOrQueueAsync(storage, "orphan-key");

        await using (var queued = new CourseManagementDbContext(options))
            Assert.Equal("orphan-key", (await queued.AttachmentCleanupRequests.SingleAsync()).StorageKey);

        var cleanup = new CourseAssignmentAttachmentCleanupService(scopeFactory,
            NullLogger<CourseAssignmentAttachmentCleanupService>.Instance);
        await cleanup.RunCleanupOnceAsync(CancellationToken.None);

        await using (var pending = new CourseManagementDbContext(options))
            Assert.Single(await pending.AttachmentCleanupRequests.ToListAsync());
        Assert.Contains("orphan-key", storage.Files);

        storage.FailDeletes = false;
        await cleanup.RunCleanupOnceAsync(CancellationToken.None);

        await using var verification = new CourseManagementDbContext(options);
        Assert.Empty(await verification.AttachmentCleanupRequests.ToListAsync());
        Assert.DoesNotContain("orphan-key", storage.Files);
        Assert.Equal(3, storage.DeleteAttempts);
    }

    [Fact]
    public async Task Cleanup_queue_persistence_failure_records_low_cardinality_metric()
    {
        var databaseName = $"course-attachment-cleanup-metric-{Guid.NewGuid():N}";
        var storage = new TrackingAttachmentStorage("orphan-key") { FailDeletes = true };
        var services = new ServiceCollection();
        services.AddDbContext<CourseManagementDbContext>(builder => builder
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new FailCleanupQueueSaveChangesInterceptor()));
        using var provider = services.BuildServiceProvider();
        var measurements = new ConcurrentBag<(long Value, int TagCount)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CourseManagementOperationMetrics.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == CourseManagementOperationMetrics.AttachmentCleanupQueueFailuresName)
                measurements.Add((value, tags.Length));
        });
        listener.Start();

        var queue = new CourseAssignmentAttachmentCleanupQueue(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CourseAssignmentAttachmentCleanupQueue>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.DeleteOrQueueAsync(storage, "orphan-key"));

        Assert.Equal((1L, 0), Assert.Single(measurements));
    }

    private static async Task<bool> WaitUntilMetadataIsRemovedAsync(DbContextOptions<CourseManagementDbContext> options, Guid id)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var db = new CourseManagementDbContext(options);
            if (!await db.AssignmentAttachments.AnyAsync(item => item.Id == id)) return true;
            await Task.Delay(10);
        }

        return false;
    }

    private static CourseAssignmentAttachment CreateAttachment(string storageKey, DateTimeOffset dueAt) =>
        CourseAssignmentAttachment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null,
            CourseAssignmentAttachmentPurpose.TeacherMaterial, "worksheet.pdf", "application/pdf", 32,
            new string('A', 64), storageKey, Guid.NewGuid(), dueAt.AddDays(-1), dueAt);

    private sealed class TrackingAttachmentStorage(params string[] storageKeys) : ICourseAssignmentAttachmentStorage
    {
        private readonly ConcurrentDictionary<string, byte> _files = new(storageKeys.ToDictionary(key => key, _ => (byte)0));

        public TaskCompletionSource<string> DeleteAttempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyCollection<string> Files => _files.Keys.ToArray();
        public bool FailDeletes { get; set; }
        public int DeleteAttempts { get; private set; }

        public Task<StoredCourseAssignmentAttachment> StoreAsync(string storageKey, Stream content, string contentType,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteAttempts++;
            if (FailDeletes) throw new IOException("Simulated attachment delete failure.");
            _files.TryRemove(storageKey, out _);
            DeleteAttempted.TrySetResult(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class FailCleanupQueueSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<CourseAssignmentAttachmentCleanupRequest>().Any() == true)
                throw new InvalidOperationException("Simulated cleanup queue persistence failure.");

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
