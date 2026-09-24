using Coaching.Application.Attachments;
using MassTransit;

namespace Coaching.Application.Consumers;

public sealed class AssignmentAttachmentDeletionRequestedConsumer(
    IAssignmentAttachmentStorage storage) : IConsumer<AssignmentAttachmentDeletionRequested>
{
    public Task Consume(ConsumeContext<AssignmentAttachmentDeletionRequested> context) =>
        storage.DeleteAsync(context.Message.StorageKey, context.CancellationToken);
}
