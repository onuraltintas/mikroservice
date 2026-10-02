using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record AutomaticStudyTopic(Guid TopicId, int RequiredMinutes);
public sealed record StudyCapacityReservation(DateOnly PlannedDate, int Minutes);
public sealed record ScheduledStudyTopic(Guid TopicId, DateOnly PlannedDate, int PlannedMinutes);
public sealed record UnscheduledStudyTopic(Guid TopicId, int RemainingMinutes);
public sealed record StudyDraftSchedule(IReadOnlyList<ScheduledStudyTopic> Tasks,
    IReadOnlyList<UnscheduledStudyTopic> UnscheduledTopics, int AvailableMinutes, int ScheduledMinutes,
    int UnscheduledMinutes, int UnusedMinutes);

// Pure daily-capacity calculation: no persistence, clock, identity or automatic publication.
public static class StudyPlanDraftScheduler
{
    public static StudyDraftSchedule Generate(DateOnly startDate, int days,
        IReadOnlyList<StudyAvailabilityWindow> windows, IReadOnlyList<AutomaticStudyTopic> topics,
        IReadOnlyList<StudyCapacityReservation> reservations)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(reservations);
        if (startDate == default || days is < 1 or > 90 || startDate.DayNumber > DateOnly.MaxValue.DayNumber - (days - 1))
            throw new ArgumentException("Planning dates must cover 1–90 valid local calendar days.");
        if (windows.Count > 42 || windows.Any(x => x is null)
            || topics.Count > 500 || topics.Any(x => x is null || x.TopicId == Guid.Empty || x.RequiredMinutes is < 1 or > 1440)
            || topics.Select(x => x.TopicId).Distinct().Count() != topics.Count
            || reservations.Count > 500 || reservations.Any(x => x is null || x.Minutes is < 1 or > 1440))
            throw new ArgumentException("Invalid planning inputs or limits exceeded.");
        var ordered = windows.OrderBy(x => x.Day).ThenBy(x => x.StartMinute).ToArray();
        for (var i = 1; i < ordered.Length; i++)
            if (ordered[i].Day == ordered[i - 1].Day && ordered[i].StartMinute < ordered[i - 1].EndMinute)
                throw new ArgumentException("Availability windows must not overlap.");
        var lastDate = startDate.AddDays(days - 1);
        if (reservations.Any(x => x.PlannedDate < startDate || x.PlannedDate > lastDate))
            throw new ArgumentException("Reserved tasks must be within the planning dates.");
        var reservedByDate = reservations.GroupBy(x => x.PlannedDate).ToDictionary(x => x.Key, x => x.Sum(y => y.Minutes));
        var capacityByDay = windows.GroupBy(x => x.Day).ToDictionary(x => x.Key, x => x.Sum(y => y.EndMinute - y.StartMinute));
        var capacities = new int[days];
        for (var day = 0; day < days; day++)
        {
            var date = startDate.AddDays(day);
            capacities[day] = capacityByDay.GetValueOrDefault(date.DayOfWeek) - reservedByDate.GetValueOrDefault(date);
            if (capacities[day] < 0) throw new ArgumentException("Protected tasks exceed availability; adjust study hours before generating a draft.");
        }
        var available = capacities.Sum();
        var tasks = new List<ScheduledStudyTopic>();
        var unscheduled = new List<UnscheduledStudyTopic>();
        var dayIndex = 0;
        // Input order is explicit priority; a topic may span dates, never exceed a day's remaining capacity.
        foreach (var topic in topics)
        {
            var remaining = topic.RequiredMinutes;
            while (remaining > 0 && dayIndex < days && tasks.Count < 500)
            {
                if (capacities[dayIndex] == 0) { dayIndex++; continue; }
                var minutes = Math.Min(remaining, capacities[dayIndex]);
                tasks.Add(new(topic.TopicId, startDate.AddDays(dayIndex), minutes));
                remaining -= minutes;
                capacities[dayIndex] -= minutes;
            }
            if (remaining > 0) unscheduled.Add(new(topic.TopicId, remaining));
        }
        var scheduled = tasks.Sum(x => x.PlannedMinutes);
        return new(tasks.AsReadOnly(), unscheduled.AsReadOnly(), available, scheduled,
            unscheduled.Sum(x => x.RemainingMinutes), available - scheduled);
    }
}
