using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Coaching;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class YksFullImportPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task FullApprovedPackageWorksForStudentAndAssignedTeacherWithoutChangingExistingLgsWork()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
        var history=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());
        var package=YksPreparedDataTests.Read();var lookup=package.Outcomes.ToDictionary(x=>x.Key);
        var curriculum=CurriculumVersion.Create("yks-full-test","YKS 2026 esaslı hazırlık","v1",2026,2026,"MEBİ ve TTKB",package.SourceUrl);curriculum.SetPublished(true);f.Db.Add(curriculum);
        var lessonIds=package.Subjects.SelectMany(s=>s.LessonSourceIds).Distinct().ToArray();
        var lessons=new Dictionary<string,StudyCatalogLesson>();var units=new Dictionary<string,StudyCatalogUnit>();
        foreach(var id in lessonIds)
        {
            var topic=package.Topics.FirstOrDefault(t=>t.LessonSourceId==id);
            var exam=topic?.Exam??"AYT";
            var tag=topic is null?package.Subjects.First(s=>s.LessonSourceIds.Contains(id)).Tag:topic.OutcomeKeys.GroupBy(k=>lookup[k].Owner).OrderByDescending(g=>g.Count()).First().Key;
            var subject=package.Subjects.Single(s=>s.Tag==tag).Subject;
            var lesson=StudyCatalogLesson.Create("approved-catalog",id,subject+" ("+exam+")",null,exam);lesson.SetActive(true);lesson.AssignCurriculum(curriculum.Id);
            var unit=StudyCatalogUnit.Create("test","unit:"+id,lesson.Id,subject,1);unit.SetActive(true);f.Db.AddRange(lesson,unit);lessons[id]=lesson;units[id]=unit;
        }
        foreach(var row in package.Topics.Select(t=>(t.SourceId,t.LessonSourceId,t.Name)).Concat(package.EquivalentTopics.Select(t=>(t.TopicSourceId,t.LessonSourceId,t.Name))))
        {
            var topic=StudyCatalogTopic.Create("approved-catalog",row.Item1,lessons[row.LessonSourceId].Id,units[row.LessonSourceId].Id,row.Name,null,1);topic.SetActive(true);f.Db.Add(topic);
        }
        await f.Db.SaveChangesAsync();
        var importer=new CoachingYksCatalogImportService(f.Db,f.Scope,f.User);
        var imported=await importer.ImportAsync(package,"Tam paket PostgreSQL provası",default);
        Assert.Equal(imported,await importer.ImportAsync(package,"Tekrar aktarım kontrolü",default));
        Assert.Equal(3184,imported.Outcomes);Assert.Equal(history,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
        var student=f.User.UserId!.Value;var teacher=Guid.NewGuid();var accounts=new Accounts(teacher);
        var profile=CoachingStudentProfile.Create(student);profile.UpdateEducation(CoachingEducationStatus.Student,12,null,null,null,null,null,null);
        var context=StudentLearningContext.Create(student,2026,LearningPurpose.Yks,2027,"SAY",null);context.SetEducation(CoachingEducationStatus.Student,12,curriculum.Id);
        f.Db.AddRange(profile,context,CoachingTeacherProfile.Create(teacher),CoachingTeacherStudentAssignment.Create(teacher,student,null,null,teacher));await f.Db.SaveChangesAsync();
        f.User.Roles=["Student"];
        var access=new CoachingAccessPolicy(f.User);var setup=new CoachingLearningSetupService(f.Db,access,accounts);
        foreach(var (score,language,count) in new[]{("TYT",(string?)null,9),("SAY",null,13),("EA",null,13),("SOZ",null,16),("DIL","English",10)})
        {
            context.SetScope(2026,LearningPurpose.Yks,2027,score,language);await f.Db.SaveChangesAsync();
            var catalog=await setup.GetCatalogAsync(null,1,default);Assert.Equal(count,catalog.Lessons.Count);Assert.Equal(0,catalog.MissingDurationCount);
            foreach(var lesson in catalog.Lessons)
            {
                var page=await setup.GetCatalogAsync(lesson.Id,1,default);Assert.NotEmpty(page.Topics.Items);
                var published=await CoachingOutcomeCatalogService.ReadPublishedAsync(f.Db,page.Topics.Items[0].Id,2026,default,context);Assert.NotEmpty(published);
            }
        }
        context.SetScope(2026,LearningPurpose.Yks,2027,"EA",null);await f.Db.SaveChangesAsync();
        var ea=await setup.GetCatalogAsync(null,1,default);
        context.ReplaceSelections(ea.Lessons.Select(l=>StudentCoursePreference.Create(context.Id,l.Id,3,null)),[],true);
        var weekly=WeeklyStudyDraft.Create(student);
        var cards=ea.Lessons.SelectMany((lesson,index)=>Enumerable.Range(0,3).Select(n=>new{lesson,index=index*3+n})).Select(x=>
            new WeeklyDraftSlot(Guid.NewGuid(),x.lesson.Id,x.lesson.Name,x.lesson.ExamCode,(DayOfWeek)(x.index/8+1),480+x.index%8*60,60)).ToArray();
        weekly.Replace("Europe/Istanbul",cards);f.Db.Add(weekly);await f.Db.SaveChangesAsync();
        var request=new WeeklyOutcomePlanRequest(new(2026,10,19),new(2027,6,1),weekly.Version,true);
        var studentService=new CoachingWeeklyOutcomePlanService(f.Db,access,setup,TimeProvider.System);
        var preview=await studentService.PreviewAsync(request,default);Assert.Empty(preview.Issues);Assert.True(preview.CanCreate);
        Assert.All(preview.Schedule.Tasks,t=>Assert.InRange(t.Date,request.StartDate,request.EndDate));
        var teacherUser=new CurriculumManagementTests.Actor{UserId=teacher,Roles=["Teacher"]};var teacherPolicy=new CoachingAccessPolicy(teacherUser);
        var teacherAccess=new CoachingStudyPlanAccess(teacherPolicy,new OwnedCoachingDirectory(f.Db,accounts,teacherUser),f.Scope);
        var teacherSetup=new CoachingLearningSetupService(f.Db,teacherPolicy,accounts,teacherAccess);
        var teacherService=new CoachingWeeklyOutcomePlanService(f.Db,teacherPolicy,teacherSetup,TimeProvider.System,teacherAccess);
        var teacherPreview=await teacherService.PreviewForStudentAsync(student,request,default);
        Assert.Equal(preview.Fingerprint,teacherPreview.Fingerprint);Assert.True(teacherPreview.CanCreate);
        var created=await teacherService.CreateForStudentAsync(student,new("Öğretmen EA planı",request,teacherPreview.Fingerprint),default);
        Assert.True(await f.Db.StudyPlanTasks.AnyAsync(t=>t.RevisionId==created.Id));
        Assert.All(await f.Db.StudyPlanTasks.Where(t=>t.RevisionId==created.Id).ToArrayAsync(),t=>Assert.True(t.PlannedStartMinute.HasValue));
        Assert.Equal(history,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().Where(t=>t.RevisionId!=created.Id).ToArrayAsync()));
    }

    private sealed class Accounts(Guid teacher):ICoachingAccountDirectory
    {
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id=>new CoachingAccount(id,"Test","Kullanıcı","test@example.test",true,true,[id==teacher?"Teacher":"Student"],null)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct)=>Task.FromResult(true);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer,Guid target,CancellationToken ct)=>throw new NotSupportedException();
    }
}
