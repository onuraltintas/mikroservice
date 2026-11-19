using System.Reflection;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;

namespace CourseManagement.IntegrationTests;

/// <summary>The retention warning names the owner by user id; the notification service finds the address in Identity.</summary>
public sealed class CourseManagementOwnerEmailByUserTests
{
    private sealed class Queue : IEmailDeliveryQueue
    {
        public List<(string Recipient, string Subject)> Sent { get; } = [];
        public Task QueueAsync(Guid messageId, string consumerType, string recipient, string subject, string body, Guid? subjectUserId = null,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((recipient, subject));
            return Task.CompletedTask;
        }
    }

    private sealed class Directory(Dictionary<Guid, RecipientContact> contacts) : IRecipientDirectory
    {
        public Task<IReadOnlyDictionary<Guid, RecipientContact>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, RecipientContact>>(contacts.Where(item => userIds.Contains(item.Key)).ToDictionary());
    }

    private static CourseManagementOwnerEmailRequestedV1 Mail(string email, Guid? userId) =>
        new(Guid.NewGuid(), email, "Konu", "Başlık", ["Paragraf"], "https://maxkampus.com/plan", "Planı yenile", userId);

    private static Task ConsumeAsync(CourseManagementOwnerEmailConsumer consumer, CourseManagementOwnerEmailRequestedV1 message)
    {
        var context = DispatchProxy.Create<ConsumeContext<CourseManagementOwnerEmailRequestedV1>, MessageContext>();
        ((MessageContext)(object)context).Message = message;
        return consumer.Consume(context);
    }

    [Fact]
    public async Task An_owner_named_by_user_id_gets_the_mail_at_the_address_identity_knows()
    {
        var owner = Guid.NewGuid();
        var queue = new Queue();
        var consumer = new CourseManagementOwnerEmailConsumer(queue,
            new Directory(new() { [owner] = new RecipientContact(owner, " sahip@example.test ", "TEST", "Sahip") }));
        await ConsumeAsync(consumer, Mail(string.Empty, owner));
        await ConsumeAsync(consumer, Mail(string.Empty, Guid.NewGuid()));
        await ConsumeAsync(consumer, Mail("davet@example.test", null));
        Assert.Equal(["sahip@example.test", "davet@example.test"], queue.Sent.Select(item => item.Recipient));
    }

    public class MessageContext : DispatchProxy
    {
        public object Message { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "get_Message" => Message,
            "get_CancellationToken" => CancellationToken.None,
            "get_MessageId" => (Guid?)Guid.NewGuid(),
            _ => throw new NotSupportedException(method?.Name)
        };
    }
}
