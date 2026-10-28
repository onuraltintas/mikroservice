using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

// Roadmap O7: Kind is Learn, Review, Refresh or Spaced; Reason says why the engine chose it; PracticeShare sizes the question target.
public sealed record WeeklyOutcomeWork(Guid TopicId,string TopicName,Guid OutcomeId,string Code,string Name,Guid EstimateId,int EstimateNumber,int Minutes,bool IsTopicShare=false,
    string Kind="Learn",string? Reason=null,double PracticeShare=0);
public sealed record WeeklyLessonWork(Guid LessonId,string LessonName,IReadOnlyList<WeeklyOutcomeWork> Outcomes,int Priority=3);
public sealed record WeeklyBlockedTime(DateOnly Date,int? StartMinute,int Minutes);
public sealed record WeeklyOutcomeSlice(DateOnly Date,int StartMinute,Guid LessonId,string LessonName,WeeklyOutcomeWork Outcome,int Minutes,bool ReusedSlot,
    string Kind="Learn",string? Reason=null,int? PlannedQuestions=null);
/// <summary>Roadmap O7: engine version 2 options. Without them the scheduler keeps its version 1 behaviour.</summary>
public sealed record WeeklyPlanRules(IReadOnlyList<int> SpacedIntervalDays,int SpacedMinutes);
public sealed record WeeklyLessonFinish(Guid LessonId,string LessonName,int OutcomeCount,int WeeklyMinutes,int RequiredMinutes,int ScheduledMinutes,int RemainingMinutes,DateOnly? FinishDate,int ExtraWeeklyMinutes);
public sealed record WeeklyOutcomeSchedule(IReadOnlyList<WeeklyOutcomeSlice> Tasks,IReadOnlyList<WeeklyLessonFinish> Lessons,DateOnly? RecommendedEndDate,int FreeMinutes);

public static class WeeklyOutcomeScheduler
{
    public static DateOnly DefaultEnd(DateOnly start)
    { var june=new DateOnly(start.Year,6,1);return start<=june?june:june.AddYears(1); }

    public static WeeklyOutcomeSchedule Build(DateOnly start,DateOnly end,IReadOnlyList<WeeklyDraftSlot> slots,
        IReadOnlyList<WeeklyLessonWork> lessons,bool reuse,IReadOnlyList<WeeklyBlockedTime> protectedWork,WeeklyPlanRules? rules=null)
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
                        // Roadmap O7: a due spaced review takes the lesson's own hour before new work.
                        if(rules is not null&&queue is not null&&date<=end&&queue.Spaced.Where(x=>x.Due<=date).OrderBy(x=>x.Due).FirstOrDefault() is {} review)
                        {
                            var reviewMinutes=Math.Min(interval.To-minute,review.Remaining);
                            tasks.Add(new(date,minute,queue.Work.LessonId,queue.Work.LessonName,review.Outcome,reviewMinutes,false,"Spaced",review.Outcome.Reason,
                                WeeklyPlanEngine.Questions("Spaced",reviewMinutes,review.Outcome.PracticeShare)));
                            if(tasks.Count>10000)throw new ArgumentException("Plan 10.000 çalışma parçasını aşıyor; tarih aralığını kısaltın.");
                            review.Remaining-=reviewMinutes;if(review.Remaining==0)queue.Spaced.Remove(review);minute+=reviewMinutes;continue;
                        }
                        if(queue is null||queue.Remaining==0)
                            queue=!reuse?null:rules is null
                                ?queues.Where(x=>x.Remaining>0).OrderByDescending(x=>x.Remaining).ThenBy(x=>x.Work.LessonId).FirstOrDefault()
                                :queues.Where(x=>x.Remaining>0).OrderBy(x=>x.Work.Priority).ThenByDescending(x=>x.Remaining).ThenBy(x=>x.Work.LessonId).FirstOrDefault();
                        if(queue is null){if(date<=end)free+=interval.To-minute;break;}
                        var outcome=queue.Work.Outcomes[queue.Index];var minutes=Math.Min(interval.To-minute,queue.CurrentRemaining);
                        var finishesTopic=minutes==queue.CurrentRemaining&&(queue.Index+1>=queue.Work.Outcomes.Count||queue.Work.Outcomes[queue.Index+1].TopicId!=outcome.TopicId);
                        if(date<=end)
                        {
                            tasks.Add(new(date,minute,queue.Work.LessonId,queue.Work.LessonName,outcome,minutes,slot.LessonId!=queue.Work.LessonId,
                                outcome.Kind,outcome.Reason,rules is null?null:WeeklyPlanEngine.Questions(outcome.Kind,minutes,outcome.PracticeShare)));
                            if(tasks.Count>10000)throw new ArgumentException("Plan 10.000 çalışma parçasını aşıyor; tarih aralığını kısaltın.");
                            queue.Scheduled+=minutes;
                            if(rules is not null&&finishesTopic&&outcome.Kind is "Learn" or "Review")
                                foreach(var days in rules.SpacedIntervalDays)
                                    queue.Spaced.Add(new(date.AddDays(days),outcome with {Kind="Spaced",Minutes=rules.SpacedMinutes,
                                        Reason=$"Aralıklı tekrar: konuyu bitirdikten {days} gün sonra hatırlamak için"},rules.SpacedMinutes));
                        }
                        queue.Consume(minutes);minute+=minutes;if(queue.Remaining==0)queue.Finish=date;
                    }
                }
            }
        }
        var weeks=Math.Max(1,(int)Math.Ceiling((end.DayNumber-start.DayNumber+1)/7d));
        var summaries=queues.Select(q=>new WeeklyLessonFinish(q.Work.LessonId,q.Work.LessonName,q.Work.Outcomes.Select(x=>x.OutcomeId).Distinct().Count(),q.WeeklyMinutes,q.Required,q.Scheduled,q.Required-q.Scheduled,q.Finish,
            (int)Math.Ceiling((q.Required-q.Scheduled)/(weeks*30d))*30)).ToArray();
        DateOnly? recommended=summaries.Length>0&&summaries.All(x=>x.FinishDate.HasValue)?summaries.Max(x=>x.FinishDate):null;
        return new(rules is null?tasks:Merge(tasks),summaries,recommended,free);
    }

    /// <summary>Roadmap O7: back-to-back hours of the same work on one day become a single task.</summary>
    private static List<WeeklyOutcomeSlice> Merge(List<WeeklyOutcomeSlice> slices)
    {
        var merged=new List<WeeklyOutcomeSlice>();
        foreach(var slice in slices)
        {
            var last=merged.Count==0?null:merged[^1];
            if(last is not null&&last.Date==slice.Date&&last.LessonId==slice.LessonId&&last.ReusedSlot==slice.ReusedSlot&&last.Kind==slice.Kind
                &&last.Reason==slice.Reason&&last.Outcome.OutcomeId==slice.Outcome.OutcomeId&&last.Outcome.TopicId==slice.Outcome.TopicId
                &&last.StartMinute+last.Minutes==slice.StartMinute&&last.Minutes+slice.Minutes<=1440)
            {
                var minutes=last.Minutes+slice.Minutes;
                merged[^1]=last with {Minutes=minutes,PlannedQuestions=WeeklyPlanEngine.Questions(last.Kind,minutes,last.Outcome.PracticeShare)};
            }
            else merged.Add(slice);
        }
        return merged;
    }

    private sealed class SpacedReview(DateOnly due,WeeklyOutcomeWork outcome,int remaining)
    {public DateOnly Due{get;}=due;public WeeklyOutcomeWork Outcome{get;}=outcome;public int Remaining{get;set;}=remaining;}

    private sealed class WorkQueue
    {
        public WeeklyLessonWork Work{get;}public int WeeklyMinutes{get;}public int Required{get;}public int Remaining{get;private set;}
        public int Index{get;private set;}public int CurrentRemaining{get;private set;}public int Scheduled;public DateOnly? Finish;
        public List<SpacedReview> Spaced{get;}=[];
        public WorkQueue(WeeklyLessonWork work,int weeklyMinutes)
        {Work=work;WeeklyMinutes=weeklyMinutes;Required=Remaining=work.Outcomes.Sum(x=>x.Minutes);CurrentRemaining=work.Outcomes.FirstOrDefault()?.Minutes??0;}
        public void Consume(int minutes)
        {Remaining-=minutes;CurrentRemaining-=minutes;if(CurrentRemaining==0&&Remaining>0)CurrentRemaining=Work.Outcomes[++Index].Minutes;}
    }
}
