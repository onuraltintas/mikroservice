using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class YksCatalogTests
{
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
}
