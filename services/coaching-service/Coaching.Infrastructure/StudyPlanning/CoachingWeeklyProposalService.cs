using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

/// <summary>
/// Paket H2: the need-based weekly proposal from the saved availability, the weekly draft, the chosen lessons (Derslerim) and their
/// weekly need (the annual preview's numbers). It saves nothing; the student (or her teacher) applies it as a weekly draft.
/// </summary>
public sealed class CoachingWeeklyProposalService(CoachingDbContext db,ICoachingAccessPolicy access,ILearningSetupService setup,TimeProvider clock,
    CoachingStudyPlanAccess? staffAccess=null):IWeeklyProposalService
{
    private static readonly StringComparer TurkishOrder=StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"),false);
    private readonly CoachingWeeklyOutcomePlanService plans=new(db,access,setup,clock,staffAccess);

    public Task<WeeklyProposalView> ProposeAsync(WeeklyProposalRequest request,CancellationToken ct)=>Propose(Student(),request,false,ct);
    public async Task<WeeklyProposalView> ProposeForStudentAsync(Guid student,WeeklyProposalRequest request,CancellationToken ct)
    {
        if(staffAccess is null)throw Forbidden();await staffAccess.RequireAsync(student,ct);
        return await Propose(student,request,true,ct);
    }
    public Task<WeeklyTempoOptionsView> TempoOptionsAsync(WeeklyTempoOptionsRequest request,CancellationToken ct)=>Tempo(Student(),request,false,ct);
    public async Task<WeeklyTempoOptionsView> TempoOptionsForStudentAsync(Guid student,WeeklyTempoOptionsRequest request,CancellationToken ct)
    {
        if(staffAccess is null)throw Forbidden();await staffAccess.RequireAsync(student,ct);
        return await Tempo(student,request,true,ct);
    }

    /// <summary>What a proposal is made from: the study times, the saved cards, the chosen lessons with their work, and the rules.</summary>
    private sealed record Setup(WeeklyProposalWindow[] Windows,WeeklyStudyDraft? Draft,IReadOnlyList<WeeklyDraftSlot> Existing,
        Dictionary<Guid,LearningLessonView> Choices,StudentCoursePreference[] Chosen,DateOnly Today,Dictionary<Guid,(int Work,int Topics)> Work,
        bool Middle,string? Focus,int Limit,IReadOnlyDictionary<DayOfWeek,int>? Budgets)
    {
        /// <summary>Paket I5: the review days the student chose (none while she has not).</summary>
        public IReadOnlyList<int> SpacedDays{get;init;}=[];
        /// <summary>Re-review of PR #132: the plan end per exam list, read once per request (it depends only on student, exams and today).</summary>
        public Dictionary<string,CoachingWeeklyOutcomePlanService.PlanEnd> Ends{get;}=new(StringComparer.Ordinal);
        /// <summary>
        /// The engine's input when the need is spread over today → <paramref name="periodEnd"/>: each lesson's work and reviews in that
        /// period divided by the times the rarest study weekday (one that may hold study) comes in it.
        /// </summary>
        public WeeklyProposalInput Input(DateOnly periodEnd,WeeklyProposalMode mode)
        {
            var days=periodEnd.DayNumber-Today.DayNumber+1;
            var occurrences=WeeklyProposalEngine.Occurrences(Today,periodEnd);
            var times=Windows.Where(x=>Budgets is null||Budgets.GetValueOrDefault(x.Day)>0).Select(x=>occurrences[x.Day]).Where(x=>x>0).DefaultIfEmpty(1).Min();
            return new(Windows,Existing,Chosen.Select(x=>new WeeklyProposalLesson(x.LessonId,Choices[x.LessonId].Name,Choices[x.LessonId].ExamCode,
                    WeeklyProposalEngine.WeeklyNeed(Work[x.LessonId].Work,Work[x.LessonId].Topics,days,times,SpacedDays),x.Priority,x.SelfReportedDifficulty>=4)).ToArray(),
                WeeklyProposalEngine.SessionsBeforeBreak(Middle),Limit,Focus,mode,occurrences,Budgets);
        }
        /// <summary>
        /// Paket H2 review: the plan ends where the annual preview will end it once the proposal is applied: the exams of the weekly
        /// draft that results (Fill: the saved cards and the proposed ones; Replace: the proposed ones only; never a course left without
        /// time). A saved card is saved again with its course's current exam (the weekly draft re-reads it), so that one counts.
        /// </summary>
        public string[] Codes(bool fill,IEnumerable<Guid> lessons)=>(fill?Existing.Select(x=>Choices.TryGetValue(x.LessonId,out var lesson)?lesson.ExamCode:x.ExamCode):[])
            .Concat(lessons.Select(x=>Choices[x].ExamCode))
            .OfType<string>().Distinct().Order(StringComparer.Ordinal).ToArray();
    }

    private async Task<Setup> Load(Guid student,string? examFocus,IReadOnlyList<StudyWindowInput>? teacherWindows,bool staff,CancellationToken ct)
    {
        var focus=examFocus?.Trim().ToLowerInvariant() switch
        {
            null or ""=>null, "tyt"=>"tyt", "balanced"=>"balanced", "ayt"=>"ayt",
            _=>throw new StudyPlanValidationException("Sınav ağırlığı TYT ağırlıklı, Dengeli ya da AYT ağırlıklı olabilir.")
        };
        if(!staff&&teacherWindows is not null)
            throw new StudyPlanValidationException("Öneri kayıtlı çalışma saatlerinden hazırlanır; saatlerini Çalışma tercihlerimde değiştirebilirsin.");
        var availability=await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student,ct);
        WeeklyProposalWindow[] windows;
        IReadOnlyDictionary<DayOfWeek,int>? budgets=null;
        if(availability is {Windows.Count:>0})
        {
            if(teacherWindows is not null)throw new StudyPlanValidationException("Öğrencinin kayıtlı çalışma saatleri var; öneri bu saatlerden hazırlanır.");
            windows=availability.Windows.Select(x=>new WeeklyProposalWindow(x.Day,x.StartMinute,x.EndMinute)).ToArray();
            budgets=await BudgetsAsync(student,availability,ct);
        }
        // Paket H2 review: a teacher's answers for a student without study times are used once and never saved.
        else if(staff&&teacherWindows is {Count:>0})windows=TeacherWindows(teacherWindows);
        else throw new StudyAvailabilityMissingException();
        var draft=await db.Set<WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student,ct);
        var choices=(staff?await setup.GetCatalogForStudentAsync(student,null,1,ct):await setup.GetCatalogAsync(null,1,ct)).Lessons.ToDictionary(x=>x.Id);
        var context=await db.StudentLearningContexts.AsNoTracking().Include(x=>x.CoursePreferences).SingleOrDefaultAsync(x=>x.StudentUserId==student,ct);
        // The chosen lessons (Derslerim) the student may still use, as the annual preview reads them.
        var chosen=context?.CoursePreferences.Where(x=>x.IsIncluded&&choices.ContainsKey(x.LessonId)).OrderBy(x=>x.LessonId).ToArray()??[];
        var profile=await db.CoachingStudentProfiles.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==student,ct);
        var work=await plans.LessonWorkAsync(student,context,chosen.Select(x=>x.LessonId).ToArray(),choices,ct);
        // K1: middle school (LGS or grade 8 and below) takes a break after every session, everyone else after two.
        var middle=context?.Purpose==LearningPurpose.Lgs||profile is {EducationStatus:CoachingEducationStatus.Student,GradeLevel:<=8};
        if(context?.Purpose!=LearningPurpose.Yks)focus=null;
        return new(windows,draft,draft?.Slots??[],choices,chosen,plans.TodayInTurkey(),work,middle,focus,WeeklyDayLimit.For(profile?.EducationStatus),budgets)
            {SpacedDays=await CoachingStudyPlanPreferencesService.SpacedDaysAsync(db,student,ct)};
    }

    /// <summary>
    /// Paket H4a: the day budgets of the student's routine profile while her study times are still the ones it wrote (availabilityMatches);
    /// otherwise (no profile, a hand edit, unreadable answers) none.
    /// </summary>
    private async Task<IReadOnlyDictionary<DayOfWeek,int>?> BudgetsAsync(Guid student,StudyAvailability availability,CancellationToken ct)
    {
        var routine=await db.Set<StudyRoutineProfile>().AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student,ct);
        if(routine is null||routine.AvailabilityVersion!=availability.Version)return null;
        try
        {
            var view=CoachingStudyRoutineService.View(routine,availability,false);
            return view.AvailabilityMatches?view.DayBudgets.GroupBy(x=>x.Day).ToDictionary(x=>x.Key,x=>x.Min(y=>y.Minutes)):null;
        }
        catch(System.Text.Json.JsonException){return null;}
    }

    /// <summary>
    /// The proposal for a mode and an optional finishBy: the plan end settles on the exams of the week that results (Paket H2 review) and
    /// the need is spread until <see cref="WeeklyProposalEngine.PeriodEnd"/>. The proposal and the tempo options both use it, so an
    /// option is exactly what the wizard gets when it asks for it.
    /// </summary>
    private async Task<(DateOnly End,WeeklyProposalResult Result)> Run(Guid student,Setup s,WeeklyProposalMode mode,DateOnly? finishBy,CancellationToken ct)
    {
        var fill=mode==WeeklyProposalMode.Fill;
        var (end,result)=await WeeklyProposalEngine.SettleAsync(s.Codes(fill,s.Chosen.Select(x=>x.LessonId)),
            async codes=>
            {
                var key=string.Join('\n',codes.Order(StringComparer.Ordinal));
                if(!s.Ends.TryGetValue(key,out var planEnd))s.Ends[key]=planEnd=await plans.EndAsync(student,codes.ToArray(),s.Today,ct);
                return planEnd;
            },
            planEnd=>WeeklyProposalEngine.Propose(s.Input(WeeklyProposalEngine.PeriodEnd(finishBy,planEnd.End)??planEnd.End,mode)),
            proposal=>s.Codes(fill,proposal.Slots.Select(x=>x.LessonId).Distinct()));
        return (end.End,result);
    }

    private async Task<WeeklyProposalView> Propose(Guid student,WeeklyProposalRequest request,bool staff,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckMode(request.Mode,staff);
        var s=await Load(student,request.ExamFocus,request.Windows,staff,ct);
        var fill=request.Mode==WeeklyProposalMode.Fill;
        // Review of PR #132 (S4): finishBy is checked against the plan end this mode gives without it (the tempo options' plan end).
        if(request.FinishBy is {} finishBy)CheckFinishBy(finishBy,s.Today,(await Run(student,s,request.Mode,null,ct)).End,staff);
        var (end,result)=await Run(student,s,request.Mode,request.FinishBy,ct);
        // Review of PR #136: would a whole new week give time to a lesson that has none now (a week saved in another order)?
        IReadOnlyList<Guid> gains=[];
        if(fill&&result.Lessons.Any(x=>x.NoTime&&x.NeededMinutes>0))
            gains=WeeklyProposalEngine.RebuildGains(result,(await Run(student,s,WeeklyProposalMode.Replace,request.FinishBy,ct)).Result);
        var slots=result.Slots.Select(x=>new WeeklyDraftSlot(Guid.NewGuid(),x.LessonId,s.Choices[x.LessonId].Name,s.Choices[x.LessonId].ExamCode,
            x.Day,x.StartMinute,x.DurationMinutes)).ToArray();
        // The applied proposal must pass the weekly draft's own checks (30/60-minute cards on the half hour, no overlap). A failure here
        // is a fault of the engine, not of the student's request.
        try{WeeklyStudyDraft.Create(student).Replace(CoachingTime.ZoneId,fill?[..s.Existing,..slots]:slots);}
        catch(ArgumentException e){throw new InvalidOperationException("The weekly proposal broke the weekly draft rules.",e);}
        var lessons=result.Lessons.Select(x=>new WeeklyProposalLessonView(x.LessonId,x.Name,s.Choices[x.LessonId].ExamCode,x.ExistingMinutes,x.ProposedMinutes,
            x.NeededMinutes,x.NoTime)).OrderBy(x=>x.LessonName,TurkishOrder).ThenBy(x=>x.LessonId).ToArray();
        var span=end.DayNumber-s.Today.DayNumber+1;
        return new(request.Mode,s.Focus,slots,lessons,result.FreeMinutes,result.NeededMinutes,result.ShortageMinutes,s.Limit,
            Math.Max(1,(int)Math.Ceiling(span/7d)),end,s.Middle,result.CoveredMinutes,s.Draft?.Version,WeeklyProposalEngine.PeriodEnd(request.FinishBy,end))
            {RebuildLessonIds=lessons.Select(x=>x.LessonId).Where(gains.Contains).ToArray()};
    }

    /// <summary>
    /// Paket H4a: the tempo step. Relaxed ends at the plan end (the one the proposal in this mode settles on), Balanced after 85 % and
    /// Intense after 70 % of the days until it (each at least today + 6 and never after the plan end); options that end on the same day
    /// are merged into the most relaxed one. Review of PR #132 (B1, S4): each option is the proposal with its finishBy and the mode:
    /// its need, the minutes it counts (free sessions + saved minutes it counts) and whether it has no shortage. Nothing is saved.
    /// </summary>
    private async Task<WeeklyTempoOptionsView> Tempo(Guid student,WeeklyTempoOptionsRequest request,bool staff,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckMode(request.Mode,staff);
        var s=await Load(student,request.ExamFocus,request.Windows,staff,ct);
        var planEnd=(await Run(student,s,request.Mode,null,ct)).End;
        var days=planEnd.DayNumber-s.Today.DayNumber;
        DateOnly After(decimal share)
        {
            var date=s.Today.AddDays((int)Math.Round(days*share,MidpointRounding.AwayFromZero));
            if(date<s.Today.AddDays(MinimumFinishDays))date=s.Today.AddDays(MinimumFinishDays);
            return date>planEnd?planEnd:date;
        }
        var wanted=new (StudyTempo Tempo,DateOnly FinishBy)[]{(StudyTempo.Relaxed,planEnd),(StudyTempo.Balanced,After(.85m)),(StudyTempo.Intense,After(.70m))};
        // Third review of PR #132: each option is the proposal for its final date (re-run when the settled end cuts the period short).
        var settled=await WeeklyProposalEngine.TempoDatesAsync(wanted,s.Today.AddDays(MinimumFinishDays),planEnd,
            async date=>await Run(student,s,request.Mode,date,ct));
        var options=settled.Select(x=>new WeeklyTempoOption(x.Tag,x.FinishBy,x.Result.NeededMinutes,
            x.Result.FreeMinutes+x.Result.Lessons.Sum(l=>l.ExistingMinutes),x.Result.ShortageMinutes==0)).ToArray();
        return new(planEnd,options);
    }

    private static void CheckMode(WeeklyProposalMode mode,bool staff)
    {
        if(!Enum.IsDefined(mode))throw new StudyPlanValidationException(staff?"Öneri türünü kontrol edin.":"Öneri türünü kontrol et.");
    }

    /// <summary>
    /// Paket H4a: a finishBy is the plan end, or a day at least <see cref="MinimumFinishDays"/> days away and not after the plan end. A day
    /// after the plan end gets the message with the latest date, also when the plan end is closer than today + 6 (review of PR #132).
    /// </summary>
    public const int MinimumFinishDays=6;
    private static void CheckFinishBy(DateOnly finishBy,DateOnly today,DateOnly planEnd,bool staff)
    {
        if(finishBy==planEnd)return;
        if(finishBy>planEnd)throw new StudyPlanValidationException(staff
            ?$"Bitiş tarihi en geç {planEnd:dd.MM.yyyy} olabilir; öğrencinin planı o gün bitiyor."
            :$"Bitiş tarihi en geç {planEnd:dd.MM.yyyy} olabilir; planın o gün bitiyor.");
        if(finishBy<today.AddDays(MinimumFinishDays))throw new StudyPlanValidationException("Bitiş tarihi bugünden en az 6 gün sonra olmalı.");
    }

    /// <summary>At most 50 windows inside the day that do not overlap (the rules of a saved availability).</summary>
    private static WeeklyProposalWindow[] TeacherWindows(IReadOnlyList<StudyWindowInput> input)
    {
        const string message="Çalışma saatlerini kontrol edin: en çok 50 aralık olabilir; aralıklar gün içinde kalmalı ve birbiriyle çakışmamalı.";
        if(input.Count>50||input.Any(x=>x is null))throw new StudyPlanValidationException(message);
        StudyAvailabilityWindow[] valid;
        try{valid=input.Select(x=>new StudyAvailabilityWindow(x.Day,x.StartMinute,x.EndMinute)).ToArray();}
        catch(ArgumentException){throw new StudyPlanValidationException(message);}
        var ordered=valid.OrderBy(x=>x.Day).ThenBy(x=>x.StartMinute).ToArray();
        for(var i=1;i<ordered.Length;i++)
            if(ordered[i].Day==ordered[i-1].Day&&ordered[i].StartMinute<ordered[i-1].EndMinute)throw new StudyPlanValidationException(message);
        return ordered.Select(x=>new WeeklyProposalWindow(x.Day,x.StartMinute,x.EndMinute)).ToArray();
    }

    private Guid Student()=>access.CurrentUserId is {} id&&access.IsCurrentStudent(id)?id
        :throw new BusinessRuleException("Authorization.Forbidden","Yalnız kendi öğrenci hesabınızla kullanabilirsiniz.");
    private static BusinessRuleException Forbidden()=>new("Authorization.Forbidden","Öğretmen erişimi doğrulanamadı.");
}
