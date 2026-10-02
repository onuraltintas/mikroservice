using System.Text.Json;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

// Minutes are local wall-clock time within the stated time zone, not UTC timestamps.
public sealed record StudyAvailabilityWindow
{
    public DayOfWeek Day { get; }
    public int StartMinute { get; }
    public int EndMinute { get; }
    public StudyAvailabilityWindow(DayOfWeek day, int startMinute, int endMinute)
    {
        if (!Enum.IsDefined(day)) throw new ArgumentOutOfRangeException(nameof(day));
        if (startMinute < 0 || endMinute > 1440 || endMinute <= startMinute)
            throw new ArgumentOutOfRangeException(nameof(startMinute));
        Day = day;
        StartMinute = startMinute;
        EndMinute = endMinute;
    }
}

public sealed class StudyAvailability : AggregateRoot
{
    public Guid StudentId { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    private string WindowsJson { get; set; } = "[]";
    public IReadOnlyList<StudyAvailabilityWindow> Windows => Array.AsReadOnly(
        JsonSerializer.Deserialize<StudyAvailabilityWindow[]>(WindowsJson)!);
    private StudyAvailability() { }

    public static StudyAvailability Create(Guid studentId, string timeZoneId)
    {
        if (studentId == Guid.Empty) throw new ArgumentException("Student ownership is required.", nameof(studentId));
        var zone = StudyCatalogText.Require(timeZoneId, 100);
        TimeZoneInfo.FindSystemTimeZoneById(zone);
        return new StudyAvailability { StudentId = studentId, TimeZoneId = zone };
    }

    public void ReplaceWindows(IEnumerable<StudyAvailabilityWindow> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        var ordered = windows.OrderBy(x => x.Day).ThenBy(x => x.StartMinute).ToArray();
        for (var i = 1; i < ordered.Length; i++)
            if (ordered[i].Day == ordered[i - 1].Day && ordered[i].StartMinute < ordered[i - 1].EndMinute)
                throw new ArgumentException("Weekly availability windows overlap.", nameof(windows));
        WindowsJson = JsonSerializer.Serialize(ordered);
        UpdatedAt = DateTime.UtcNow;
    }
}
