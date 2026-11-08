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
        if(!Enum.IsDefined(request.Mode))throw new ArgumentException("Öneri türünü kontrol et.");
        var focus=request.ExamFocus?.Trim().ToLowerInvariant() switch
        {
            null or ""=>null, "tyt"=>"tyt", "balanced"=>"balanced", "ayt"=>"ayt",
            _=>throw new ArgumentException("Sınav ağırlığı TYT ağırlıklı, Dengeli ya da AYT ağırlıklı olabilir.")
        };
        var availability=await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student,ct);
        if(availability is null||availability.Windows.Count==0)throw new KeyNotFoundException("Önce müsait saatlerini kaydet.");
        var draft=await db.Set<WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student,ct);
        var existing=draft?.Slots??[];
        var choices=(staff?await setup.GetCatalogForStudentAsync(student,null,1,ct):await setup.GetCatalogAsync(null,1,ct)).Lessons.ToDictionary(x=>x.Id);
        var context=await db.StudentLearningContexts.AsNoTracking().Include(x=>x.CoursePreferences).SingleOrDefaultAsync(x=>x.StudentUserId==student,ct);
        // The chosen lessons (Derslerim) the student may still use, as the annual preview reads them.
        var chosen=context?.CoursePreferences.Where(x=>x.IsIncluded&&choices.ContainsKey(x.LessonId)).OrderBy(x=>x.LessonId).ToArray()??[];
        var profile=await db.CoachingStudentProfiles.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==student,ct);
        var today=plans.TodayInTurkey();
        var codes=chosen.Select(x=>choices[x.LessonId].ExamCode).Concat(existing.Select(x=>x.ExamCode)).OfType<string>().Distinct().Order(StringComparer.Ordinal).ToArray();
        var end=(await plans.EndAsync(student,codes,today,ct)).End;
        var days=end.DayNumber-today.DayNumber+1;
        var needs=await plans.WeeklyNeedAsync(student,context,chosen.Select(x=>x.LessonId).ToArray(),choices,days,ct);
        // K1: middle school (LGS or grade 8 and below) takes a break after every session, everyone else after two.
        var middle=context?.Purpose==LearningPurpose.Lgs||profile is {EducationStatus:CoachingEducationStatus.Student,GradeLevel:<=8};
        if(context?.Purpose!=LearningPurpose.Yks)focus=null;
        var limit=WeeklyDayLimit.For(profile?.EducationStatus);
        var result=WeeklyProposalEngine.Propose(new(
            availability.Windows.Select(x=>new WeeklyProposalWindow(x.Day,x.StartMinute,x.EndMinute)).ToArray(),existing,
            chosen.Select(x=>new WeeklyProposalLesson(x.LessonId,choices[x.LessonId].Name,choices[x.LessonId].ExamCode,needs.GetValueOrDefault(x.LessonId),
                x.Priority,x.SelfReportedDifficulty>=4)).ToArray(),
            WeeklyProposalEngine.SessionsBeforeBreak(middle),limit,focus,request.Mode));
        var slots=result.Slots.Select(x=>new WeeklyDraftSlot(Guid.NewGuid(),x.LessonId,choices[x.LessonId].Name,choices[x.LessonId].ExamCode,
            x.Day,x.StartMinute,x.DurationMinutes)).ToArray();
        // The applied proposal must pass the weekly draft's own checks (30/60-minute cards on the half hour, no overlap).
        WeeklyStudyDraft.Create(student).Replace(CoachingTime.ZoneId,request.Mode==WeeklyProposalMode.Fill?[..existing,..slots]:slots);
        var lessons=result.Lessons.Select(x=>new WeeklyProposalLessonView(x.LessonId,x.Name,choices[x.LessonId].ExamCode,x.ExistingMinutes,x.ProposedMinutes,
            x.NeededMinutes,x.NoTime)).OrderBy(x=>x.LessonName,TurkishOrder).ThenBy(x=>x.LessonId).ToArray();
        return new(request.Mode,focus,slots,lessons,result.FreeMinutes,result.NeededMinutes,result.ShortageMinutes,limit,
            Math.Max(1,(int)Math.Ceiling(days/7d)),end,middle);
    }

    private Guid Student()=>access.CurrentUserId is {} id&&access.IsCurrentStudent(id)?id
        :throw new BusinessRuleException("Authorization.Forbidden","Yalnız kendi öğrenci hesabınızla kullanabilirsiniz.");
    private static BusinessRuleException Forbidden()=>new("Authorization.Forbidden","Öğretmen erişimi doğrulanamadı.");
}
