using Coaching.Application.Interfaces;

namespace Identity.API.IntegrationTests;

internal sealed class NoopCoachingEventPublisher : ICoachingEventPublisher
{
    public List<object> Messages { get; } = [];

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}
