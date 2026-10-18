using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Microsoft.EntityFrameworkCore;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using Coaching.Domain.Profiles;
using EduPlatform.Shared.Contracts.Coaching;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class YksCatalogTests
{
    [Theory]
    [InlineData("SAY",null,"math")]
    [InlineData("EA",null,"math")]
    [InlineData("SOZ",null,"philosophy")]
    [InlineData("DIL","English","english")]
    [InlineData("DIL","German",null)]
    public async Task CatalogFiltersScoreTypeTopicsAndLanguage(string score,string? language,string? expected)
    {
        await using var f=new CurriculumManagementTests.Fixture();var student=f.User.UserId!.Value;f.User.Roles=["Student"];
        var profile=CoachingStudentProfile.Create(student);profile.UpdateEducation(CoachingEducationStatus.Student,12,null,null,null,null,null,null);
        var curriculum=CurriculumVersion.Create("yks-test","YKS","test",2026,2026,"Test",null);curriculum.SetPublished(true);
        var context=StudentLearningContext.Create(student,2026,LearningPurpose.Yks,2027,score,language);
        context.SetEducation(CoachingEducationStatus.Student,12,curriculum.Id);f.Db.AddRange(profile,curriculum,context);
        foreach(var (source,exam,test,scores,lang) in new[]{("math","AYT","AYT.MAT",new[]{"SAY","EA"},(string?)null),("philosophy","TYT","AYT.SOS2",new[]{"SOZ"},(string?)null),("english","YDT","YDT.EN",new[]{"DIL"},"English")})
        {
            var lesson=StudyCatalogLesson.Create("test",source,source,null,exam);lesson.AssignCurriculum(curriculum.Id);lesson.SetActive(true);
            var unit=StudyCatalogUnit.Create("test",source,lesson.Id,source,1);unit.SetActive(true);
            var topic=StudyCatalogTopic.Create("test",source,lesson.Id,unit.Id,source,null,1,30);topic.SetActive(true);
            f.Db.AddRange(lesson,unit,topic);
            foreach(var s in scores)f.Db.Add(StudyCatalogYksScope.Create(topic.Id,test.StartsWith("AYT")?"AYT":exam,s,test,lang,"Doğrulanmış sınav kapsamı"));
        }
        await f.Db.SaveChangesAsync();
        var catalog=await new CoachingLearningSetupService(f.Db,new CoachingAccessPolicy(f.User),new Accounts()).GetCatalogAsync(null,1,default);
        if(expected is null){Assert.Empty(catalog.Lessons);Assert.Empty(catalog.Topics.Items);}
        else
        {
            var lesson=Assert.Single(catalog.Lessons);Assert.Equal(expected,lesson.Name);
            var topics=await new CoachingLearningSetupService(f.Db,new CoachingAccessPolicy(f.User),new Accounts()).GetCatalogAsync(lesson.Id,1,default);
            Assert.Equal(expected,Assert.Single(topics.Topics.Items).Name);
        }
    }

    [Fact]
    public void MissingOfficialClassroomHoursAreNotInvented()
    {
        var topic=OfficialCurriculumTopic.Create(Guid.NewGuid(),"9.1","Kaynakta süre yok",1,null);
        Assert.Null(topic.LessonPeriods);
        Assert.Throws<ArgumentException>(()=>OfficialCurriculumTopic.Create(Guid.NewGuid(),"9.1","Geçersiz",1,0));
    }

    [Theory]
    [InlineData("TYT","SAY","TYT.MAT",null)]
    [InlineData("AYT","EA","AYT.MAT",null)]
    [InlineData("AYT","SOZ","AYT.SOS2",null)]
    [InlineData("YDT","DIL","YDT.EN","English")]
    public void ExamScopesKeepScoreTypeAndLanguage(string exam,string score,string test,string? language)
    {
        var row=StudyCatalogYksScope.Create(Guid.NewGuid(),exam,score,test,language,"ÖSYM ve MEB kaynaklarıyla doğrulanan kapsam");
        Assert.Equal(score,row.ScoreType);Assert.Equal(language,row.ForeignLanguage);
        Assert.Throws<ArgumentException>(()=>StudyCatalogYksScope.Create(row.TopicId,"YDT","SAY","YDT.EN","English","Geçersiz kapsam"));
        Assert.Throws<ArgumentException>(()=>StudyCatalogYksScope.Create(row.TopicId,"YDT","DIL","YDT.AR","Arabic","Kapsam dışı dil"));
    }

    [Fact]
    public async Task CanonicalOutcomeCanServeExplicitlyBoundTytAndAytLessonsWithoutDuplicatingItsIdentity()
    {
        await using var f=new CurriculumManagementTests.Fixture();var old=await f.Seed();
        var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var lesson=StudyCatalogLesson.Create("test","yks-math","Matematik (TYT)",null,"TYT");lesson.SetActive(true);
        var unit=StudyCatalogUnit.Create("test","yks-unit",lesson.Id,"Sayılar",1);unit.SetActive(true);
        var topic=StudyCatalogTopic.Create("test","yks-topic",lesson.Id,unit.Id,"Ortak kapsam",null,1);topic.SetActive(true);
        var mapping=StudyCatalogTopicOutcome.Create(topic.Id,outcome.Id,"Açık program ders bağlantısı");mapping.Review(true,"İncelenen bağlantı",f.User.UserId!.Value);
        f.Db.AddRange(lesson,unit,topic,mapping,OfficialCurriculumLessonBinding.Create(outcome.EditionId,lesson.Id));
        await f.Db.SaveChangesAsync();
        var published=await new CoachingOutcomeCatalogService(f.Db,f.Scope,f.User).GetPublishedAsync(topic.Id,2026,default);
        Assert.Equal(outcome.Id,Assert.Single(published).OutcomeId);
        Assert.Equal(25,(await f.Db.StudyPlanTasks.SingleAsync()).ActualMinutes);
        Assert.Equal(old.Id,(await f.Db.StudyPlanTasks.SingleAsync()).TopicId);
    }

    private sealed class Accounts:ICoachingAccountDirectory
    {
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id=>new CoachingAccount(id,"Test","Öğrenci","test@example.test",true,true,["Student"],null)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct)=>Task.FromResult(true);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer,Guid target,CancellationToken ct)=>throw new NotSupportedException();
    }
}
