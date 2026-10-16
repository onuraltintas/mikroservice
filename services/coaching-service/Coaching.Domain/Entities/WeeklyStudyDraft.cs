using System.Text.Json;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

public sealed record WeeklyDraftSlot(Guid Id, Guid LessonId, string LessonName, string? ExamCode,
    DayOfWeek Day, int StartMinute, int DurationMinutes);

public sealed class WeeklyStudyDraft : AggregateRoot
{
    public Guid StudentId { get; private set; }
    public string TimeZoneId { get; private set; } = "Europe/Istanbul";
    private string SlotsJson { get; set; } = "[]";
    public IReadOnlyList<WeeklyDraftSlot> Slots => Array.AsReadOnly(JsonSerializer.Deserialize<WeeklyDraftSlot[]>(SlotsJson)!);
    private WeeklyStudyDraft() { }
    public static WeeklyStudyDraft Create(Guid student)
    {
        if (student == Guid.Empty) throw new ArgumentException("Öğrenci gereklidir.");
        return new() { StudentId=student };
    }
    public void Replace(string timeZoneId,IReadOnlyList<WeeklyDraftSlot> slots)
    {
        var zone=StudyCatalogText.Require(timeZoneId,100);TimeZoneInfo.FindSystemTimeZoneById(zone);
        if (slots.Count > 336 || slots.Any(x=>x is null || x.Id==Guid.Empty || x.LessonId==Guid.Empty || !Enum.IsDefined(x.Day)
            || x.StartMinute < 0 || x.StartMinute%30 != 0 || x.DurationMinutes is not (30 or 60) || x.StartMinute+x.DurationMinutes>1440)
            || slots.Select(x=>x.Id).Distinct().Count()!=slots.Count) throw new ArgumentException("Ders kartları 30/60 dakika olmalı ve yarım saatlerde başlamalıdır.");
        var ordered=slots.OrderBy(x=>x.Day).ThenBy(x=>x.StartMinute).ToArray();
        for(var i=1;i<ordered.Length;i++)
            if(ordered[i].Day==ordered[i-1].Day && ordered[i].StartMinute<ordered[i-1].StartMinute+ordered[i-1].DurationMinutes)
                throw new ArgumentException("Ders saatleri çakışıyor.");
        TimeZoneId=zone;SlotsJson=JsonSerializer.Serialize(ordered);UpdatedAt=DateTime.UtcNow;
    }
}
