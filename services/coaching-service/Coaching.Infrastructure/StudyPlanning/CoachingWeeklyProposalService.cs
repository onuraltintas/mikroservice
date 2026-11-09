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

    private async Task<WeeklyProposalView> Propose(Guid student,WeeklyProposalRequest request,bool staff,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(!Enum.IsDefined(request.Mode))throw new StudyPlanValidationException(staff?"Öneri türünü kontrol edin.":"Öneri türünü kontrol et.");
        var focus=request.ExamFocus?.Trim().ToLowerInvariant() switch
        {
            null or ""=>null, "tyt"=>"tyt", "balanced"=>"balanced", "ayt"=>"ayt",
            _=>throw new StudyPlanValidationException("Sınav ağırlığı TYT ağırlıklı, Dengeli ya da AYT ağırlıklı olabilir.")
        };
        if(!staff&&request.Windows is not null)
            throw new StudyPlanValidationException("Öneri kayıtlı çalışma saatlerinden hazırlanır; saatlerini Çalışma tercihlerimde değiştirebilirsin.");
        var availability=await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student,ct);
        WeeklyProposalWindow[] windows;
        if(availability is {Windows.Count:>0})
        {
            if(request.Windows is not null)throw new StudyPlanValidationException("Öğrencinin kayıtlı çalışma saatleri var; öneri bu saatlerden hazırlanır.");
            windows=availability.Windows.Select(x=>new WeeklyProposalWindow(x.Day,x.StartMinute,x.EndMinute)).ToArray();
        }
        // Paket H2 review: a teacher's answers for a student without study times are used once and never saved.
        else if(staff&&request.Windows is {Count:>0})windows=TeacherWindows(request.Windows);
        else throw new StudyAvailabilityMissingException();
        var draft=await db.Set<WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student,ct);
        var fill=request.Mode==WeeklyProposalMode.Fill;
        var existing=draft?.Slots??[];
        var choices=(staff?await setup.GetCatalogForStudentAsync(student,null,1,ct):await setup.GetCatalogAsync(null,1,ct)).Lessons.ToDictionary(x=>x.Id);
        var context=await db.StudentLearningContexts.AsNoTracking().Include(x=>x.CoursePreferences).SingleOrDefaultAsync(x=>x.StudentUserId==student,ct);
        // The chosen lessons (Derslerim) the student may still use, as the annual preview reads them.
        var chosen=context?.CoursePreferences.Where(x=>x.IsIncluded&&choices.ContainsKey(x.LessonId)).OrderBy(x=>x.LessonId).ToArray()??[];
        var profile=await db.CoachingStudentProfiles.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==student,ct);
        var today=plans.TodayInTurkey();
        var work=await plans.LessonWorkAsync(student,context,chosen.Select(x=>x.LessonId).ToArray(),choices,ct);
        // K1: middle school (LGS or grade 8 and below) takes a break after every session, everyone else after two.
        var middle=context?.Purpose==LearningPurpose.Lgs||profile is {EducationStatus:CoachingEducationStatus.Student,GradeLevel:<=8};
        if(context?.Purpose!=LearningPurpose.Yks)focus=null;
        var limit=WeeklyDayLimit.For(profile?.EducationStatus);
        // Paket H2 review: the plan ends where the annual preview will end it once the proposal is applied: the exams of the weekly
        // draft that results (Fill: the saved cards and the proposed ones; Replace: the proposed ones only; never a course left without time).
        // A saved card is saved again with its course's current exam (the weekly draft re-reads it), so that one counts.
        string[] Codes(IEnumerable<Guid> lessons)=>(fill?existing.Select(x=>choices.TryGetValue(x.LessonId,out var lesson)?lesson.ExamCode:x.ExamCode):[])
            .Concat(lessons.Select(x=>choices[x].ExamCode))
            .OfType<string>().Distinct().Order(StringComparer.Ordinal).ToArray();
        var (end,result)=await WeeklyProposalEngine.SettleAsync(Codes(chosen.Select(x=>x.LessonId)),
            codes=>plans.EndAsync(student,codes.ToArray(),today,ct),
            planEnd=>
            {
                var days=planEnd.End.DayNumber-today.DayNumber+1;
                var occurrences=WeeklyProposalEngine.Occurrences(today,planEnd.End);
                var times=windows.Select(x=>occurrences[x.Day]).Where(x=>x>0).DefaultIfEmpty(1).Min();
                return WeeklyProposalEngine.Propose(new(windows,existing,
                    chosen.Select(x=>new WeeklyProposalLesson(x.LessonId,choices[x.LessonId].Name,choices[x.LessonId].ExamCode,
                        WeeklyProposalEngine.WeeklyNeed(work[x.LessonId].Work,work[x.LessonId].Topics,days,times),x.Priority,x.SelfReportedDifficulty>=4)).ToArray(),
                    WeeklyProposalEngine.SessionsBeforeBreak(middle),limit,focus,request.Mode,occurrences));
            },
            proposal=>Codes(proposal.Slots.Select(x=>x.LessonId).Distinct()));
        var slots=result.Slots.Select(x=>new WeeklyDraftSlot(Guid.NewGuid(),x.LessonId,choices[x.LessonId].Name,choices[x.LessonId].ExamCode,
            x.Day,x.StartMinute,x.DurationMinutes)).ToArray();
        // The applied proposal must pass the weekly draft's own checks (30/60-minute cards on the half hour, no overlap). A failure here
        // is a fault of the engine, not of the student's request.
        try{WeeklyStudyDraft.Create(student).Replace(CoachingTime.ZoneId,fill?[..existing,..slots]:slots);}
        catch(ArgumentException e){throw new InvalidOperationException("The weekly proposal broke the weekly draft rules.",e);}
        var lessons=result.Lessons.Select(x=>new WeeklyProposalLessonView(x.LessonId,x.Name,choices[x.LessonId].ExamCode,x.ExistingMinutes,x.ProposedMinutes,
            x.NeededMinutes,x.NoTime)).OrderBy(x=>x.LessonName,TurkishOrder).ThenBy(x=>x.LessonId).ToArray();
        var span=end.End.DayNumber-today.DayNumber+1;
        return new(request.Mode,focus,slots,lessons,result.FreeMinutes,result.NeededMinutes,result.ShortageMinutes,limit,
            Math.Max(1,(int)Math.Ceiling(span/7d)),end.End,middle,result.CoveredMinutes,draft?.Version);
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
