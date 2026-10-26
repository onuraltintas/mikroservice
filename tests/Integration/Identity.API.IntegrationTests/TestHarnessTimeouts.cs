using MassTransit;
using MassTransit.Testing;

namespace Identity.API.IntegrationTests;

/// <summary>
/// MassTransit harness timeouts for a full CI run. The suite runs thousands of tests in
/// parallel next to Testcontainers, and a 5 s inactivity window sometimes elapsed before
/// the in-memory consumer was scheduled, so <c>Consumed.Any</c> returned false at random.
/// Waits still return as soon as the message is seen; only a genuinely missing message
/// takes the full window. Checks for messages that must NOT exist run after consumption
/// with a short window (<see cref="PublishedDuringConsumeAsync{T}"/>): the synchronous
/// <c>Published.Select</c> would wait the full inactivity window.
/// </summary>
public static class TestHarnessTimeouts
{
    public static readonly TimeSpan Inactivity = TimeSpan.FromSeconds(30);

    public static void UseSuiteTimeouts(this IBusRegistrationConfigurator configurator) =>
        configurator.SetTestTimeouts(testTimeout: TimeSpan.FromMinutes(2), testInactivityTimeout: Inactivity);

    /// <summary>Polls until the condition holds or the inactivity window passes.</summary>
    public static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Inactivity;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    /// <summary>
    /// Whether a message of type <typeparamref name="T"/> was published. Call it after
    /// <c>Consumed.Any</c>: a consumer's publishes are recorded before its consumption is,
    /// so a short window is enough and a missing message does not cost the full window.
    /// </summary>
    public static async Task<bool> PublishedDuringConsumeAsync<T>(this ITestHarness harness)
        where T : class
    {
        using var window = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        return await harness.Published.Any<T>(window.Token);
    }

    /// <summary>
    /// The messages of type <typeparamref name="T"/> published so far, without blocking a
    /// thread. The synchronous <c>Published.Select</c> blocks for the whole inactivity window,
    /// and xUnit runs async tests on a small set of threads, so one blocked test delayed the
    /// consumers of every other harness test (the real cause of the random CI failures).
    /// </summary>
    public static async Task<IReadOnlyList<T>> PublishedMessagesAsync<T>(this ITestHarness harness)
        where T : class
    {
        var messages = new List<T>();
        using var window = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            await foreach (var item in harness.Published.SelectAsync<T>(window.Token))
            {
                messages.Add(item.Context.Message);
            }
        }
        catch (OperationCanceledException) when (window.IsCancellationRequested)
        {
        }

        return messages;
    }
}
