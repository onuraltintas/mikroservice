using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record WeeklyOutcomeWork(Guid TopicId,string TopicName,Guid OutcomeId,string Code,string Name,Guid EstimateId,int EstimateNumber,int Minutes);
public sealed record WeeklyLessonWork(Guid LessonId,string LessonName,IReadOnlyList<WeeklyOutcomeWork> Outcomes);
public sealed record WeeklyBlockedTime(DateOnly Date,int? StartMinute,int Minutes);
public sealed record WeeklyOutcomeSlice(DateOnly Date,int StartMinute,Guid LessonId,string LessonName,WeeklyOutcomeWork Outcome,int Minutes,bool ReusedSlot);
public sealed record WeeklyLessonFinish(Guid LessonId,string LessonName,int OutcomeCount,int WeeklyMinutes,int RequiredMinutes,int ScheduledMinutes,int RemainingMinutes,DateOnly? FinishDate,int ExtraWeeklyMinutes);
public sealed record WeeklyOutcomeSchedule(IReadOnlyList<WeeklyOutcomeSlice> Tasks,IReadOnlyList<WeeklyLessonFinish> Lessons,DateOnly? RecommendedEndDate,int FreeMinutes);

public static class WeeklyOutcomeScheduler
{
    public static DateOnly DefaultEnd(DateOnly start)
    { var june=new DateOnly(start.Year,6,1);return start<=june?june:june.AddYears(1); }

    public static WeeklyOutcomeSchedule Build(DateOnly start,DateOnly end,IReadOnlyList<WeeklyDraftSlot> slots,
        IReadOnlyList<WeeklyLessonWork> lessons,bool reuse,IReadOnlyList<WeeklyBlockedTime> protectedWork)
    {
        if(start==default||end<start||end.DayNumber-start.DayNumber>730||start.DayNumber>DateOnly.MaxValue.DayNumber-730)
            throw new ArgumentException("Başlangıç ve bitişi kontrol edin; bir plan en fazla iki yıllık aralığı kapsayabilir.");
        var queues=lessons.Select(x=>new WorkQueue(x,slots.Where(s=>s.LessonId==x.LessonId).Sum(s=>s.DurationMinutes))).ToArray();
        var byLesson=queues.ToDictionary(x=>x.Work.LessonId);var blocked=protectedWork.ToLookup(x=>x.Date);
        var tasks=new List<WeeklyOutcomeSlice>();var free=0;var horizon=start.AddDays(730);
        for(var day=0;day<=horizon.DayNumber-start.DayNumber;day++)
        {
            var date=start.AddDays(day);
            if(date>end&&queues.All(x=>x.Remaining==0))break;
            var daySlots=slots.Where(s=>s.Day==date.DayOfWeek).OrderBy(s=>s.StartMinute).ThenBy(s=>s.Id).ToArray();
            var untimed=blocked[date].Where(x=>x.StartMinute is null).Sum(x=>x.Minutes);
            foreach(var slot in daySlots)
            {
                var intervals=new List<(int From,int To)>{(slot.StartMinute,slot.StartMinute+slot.DurationMinutes)};
                foreach(var reserved in blocked[date].Where(x=>x.StartMinute.HasValue))
                {
                    var from=reserved.StartMinute!.Value;var to=from+reserved.Minutes;
                    intervals=intervals.SelectMany(i=>i.To<=from||i.From>=to?new[]{i}:new[]{(i.From,Math.Min(i.To,from)),(Math.Max(i.From,to),i.To)}.Where(p=>p.Item2>p.Item1)).ToList();
                }
                foreach(var interval in intervals)
                {
                    var minute=interval.From;var reserved=Math.Min(untimed,interval.To-minute);minute+=reserved;untimed-=reserved;
                    while(minute<interval.To)
                    {
                        byLesson.TryGetValue(slot.LessonId,out var queue);
                        if(queue is null||queue.Remaining==0)
                            queue=reuse?queues.Where(x=>x.Remaining>0).OrderByDescending(x=>x.Remaining).ThenBy(x=>x.Work.LessonId).FirstOrDefault():null;
                        if(queue is null){if(date<=end)free+=interval.To-minute;break;}
                        var outcome=queue.Work.Outcomes[queue.Index];var minutes=Math.Min(interval.To-minute,queue.CurrentRemaining);
                        if(date<=end)
                        {
                            tasks.Add(new(date,minute,queue.Work.LessonId,queue.Work.LessonName,outcome,minutes,slot.LessonId!=queue.Work.LessonId));
                            if(tasks.Count>10000)throw new ArgumentException("Plan 10.000 çalışma parçasını aşıyor; tarih aralığını kısaltın.");
                            queue.Scheduled+=minutes;
                        }
                        queue.Consume(minutes);minute+=minutes;if(queue.Remaining==0)queue.Finish=date;
                    }
                }
            }
        }
        var weeks=Math.Max(1,(int)Math.Ceiling((end.DayNumber-start.DayNumber+1)/7d));
        var summaries=queues.Select(q=>new WeeklyLessonFinish(q.Work.LessonId,q.Work.LessonName,q.Work.Outcomes.Count,q.WeeklyMinutes,q.Required,q.Scheduled,q.Required-q.Scheduled,q.Finish,
            (int)Math.Ceiling((q.Required-q.Scheduled)/(weeks*30d))*30)).ToArray();
        DateOnly? recommended=summaries.Length>0&&summaries.All(x=>x.FinishDate.HasValue)?summaries.Max(x=>x.FinishDate):null;
        return new(tasks,summaries,recommended,free);
    }

    private sealed class WorkQueue
    {
        public WeeklyLessonWork Work{get;}public int WeeklyMinutes{get;}public int Required{get;}public int Remaining{get;private set;}
        public int Index{get;private set;}public int CurrentRemaining{get;private set;}public int Scheduled;public DateOnly? Finish;
        public WorkQueue(WeeklyLessonWork work,int weeklyMinutes)
        {Work=work;WeeklyMinutes=weeklyMinutes;Required=Remaining=work.Outcomes.Sum(x=>x.Minutes);CurrentRemaining=work.Outcomes.FirstOrDefault()?.Minutes??0;}
        public void Consume(int minutes)
        {Remaining-=minutes;CurrentRemaining-=minutes;if(CurrentRemaining==0&&Remaining>0)CurrentRemaining=Work.Outcomes[++Index].Minutes;}
    }
}
