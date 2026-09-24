using Coaching.Application.Attachments;
using Coaching.Application.Consumers;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAttachmentDeletionConsumerTests
{
    [Fact]
    public async Task Consumer_DeletesRequestedStorageKey()
    {
        var storage = new RecordingStorage();
        var services = new ServiceCollection();
        services.AddSingleton<IAssignmentAttachmentStorage>(storage);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(5));
            configurator.AddConsumer<AssignmentAttachmentDeletionRequestedConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new AssignmentAttachmentDeletionRequested(
                Guid.NewGuid(), Guid.NewGuid(), "assignments/test/photo.jpg"));

            (await harness.Consumed.Any<AssignmentAttachmentDeletionRequested>()).Should().BeTrue();
            storage.DeletedKeys.Should().ContainSingle().Which.Should().Be("assignments/test/photo.jpg");
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class RecordingStorage : IAssignmentAttachmentStorage
    {
        public List<string> DeletedKeys { get; } = [];

        public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(Guid assignmentId, Guid studentId, Guid attachmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StoredAssignmentAttachment> StoreAsync(string storageKey, Stream content, string expectedContentType, long expectedSizeBytes, string expectedSha256, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            DeletedKeys.Add(storageKey);
            return Task.CompletedTask;
        }
    }
}
