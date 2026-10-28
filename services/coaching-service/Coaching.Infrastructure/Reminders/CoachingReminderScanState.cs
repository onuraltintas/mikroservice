using System.Collections.Concurrent;

namespace Coaching.Infrastructure.Reminders;

/// <summary>
/// Roadmap D1: remembers (per process) which morning study scans have nothing left to send for a local day, so the
/// reminder worker does not re-read every active plan each minute after 09:00. A plan made later that day gets its
/// reminders from the next day on.
/// </summary>
public sealed class CoachingReminderScanState
{
    private readonly ConcurrentDictionary<string, DateOnly> finished = new(StringComparer.Ordinal);

    public bool IsFinished(string kind, string zoneId, DateOnly date) =>
        finished.TryGetValue(kind + "|" + zoneId, out var day) && day == date;

    public void Finish(string kind, string zoneId, DateOnly date) => finished[kind + "|" + zoneId] = date;
}
