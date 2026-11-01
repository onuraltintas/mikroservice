using System.Text.Json;
using Coaching.Infrastructure.Catalogs;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using Coaching.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class DynamicCurriculumTests
{
    [Fact]
    // KM1 (01.11.2026): the single exam date became a list of sessions; ExamDate on the node is the next session's date.
    public async Task ExamSessionsAreOptionalAndCanBeSetChangedAndClearedWithoutChangingExamCode()
    {
        await using var f=new CurriculumManagementTests.Fixture();
        var exam=await f.Service.SaveAsync("exams",null,Request(new {Name="Tarihli sınav",Reason="Sınav tarihi ekle",Code="DATE",Sessions=new[]{new{Date="2027-06-20",Label="2027"}}}),default);
        Assert.Equal("2027-06-20",Date(exam));
        var session=Assert.Single(exam.Sessions);
        exam=await f.Service.SaveAsync("exams",exam.Id,Request(new {Name=exam.Name,Reason="Sınav tarihini güncelle",exam.Fingerprint,Code="DATE",
            Sessions=new object[]{new{Id=session.Id,Date="2027-06-27",Label="2027",ApplicationStart="2027-02-04",ApplicationEnd="2027-03-02"},new{Date="2027-11-21",Label="2027/2"}}}),default);
        Assert.Equal(session.Id,exam.Sessions[0].Id);Assert.Equal(new DateOnly(2027,3,2),exam.Sessions[0].ApplicationEnd);
        Assert.Equal("2027-06-27",Date(await f.Service.GetAsync("exams",exam.Id,default)));
        Assert.Equal("2027-06-27",Date(Assert.Single((await f.Service.ChildrenAsync("exams",null,"Tarihli",1,default)).Items)));
        // Leaving the sessions out keeps them; an empty list clears them.
        exam=await f.Service.SaveAsync("exams",exam.Id,new(exam.Name+" (yeni ad)","Sınavın adını düzelt",Fingerprint:exam.Fingerprint,Code:"DATE"),default);
        Assert.Equal(2,exam.Sessions.Count);
        exam=await f.Service.SaveAsync("exams",exam.Id,new(exam.Name,"Tarihi daha sonra belirle",Fingerprint:exam.Fingerprint,Code:"DATE",Sessions:[]),default);
        Assert.Null(Date(exam));Assert.Empty(exam.Sessions);Assert.Equal("DATE",exam.Code);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SaveAsync("exams",exam.Id,new(exam.Name,"Aynı tarih iki kez",Fingerprint:exam.Fingerprint,Code:"DATE",
            Sessions:[new(null,new(2027,6,20),"A",null,null),new(null,new(2027,6,20),"B",null,null)]),default));
    }

    [Fact]
    public async Task ASessionAStudentAimsForCannotBeRemoved()
    {
        await using var f=new CurriculumManagementTests.Fixture();
        var exam=await f.Service.SaveAsync("exams",null,new("YDS","Sınav türü ekle",Code:"YDS",Sessions:[new(null,new(2027,3,28),"2027/1",null,null),new(null,new(2027,11,21),"2027/2",null,null)]),default);
        var context=StudentLearningContext.Create(Guid.NewGuid(),2026,LearningPurpose.Yds,2027,null,null);context.SetTargetExamSession(exam.Sessions[1].Id);
        f.Db.Add(context);await f.Db.SaveChangesAsync();
        exam=await f.Service.GetAsync("exams",exam.Id,default);
        var error=await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SaveAsync("exams",exam.Id,new(exam.Name,"Güz oturumunu kaldır",Fingerprint:exam.Fingerprint,Code:"YDS",
            Sessions:[new(exam.Sessions[0].Id,new(2027,3,28),"2027/1",null,null)]),default));
        Assert.Contains("1 öğrenci",error.Message);
        // Its date may still change.
        exam=await f.Service.SaveAsync("exams",exam.Id,new(exam.Name,"Güz oturumu tarihi değişti",Fingerprint:exam.Fingerprint,Code:"YDS",
            Sessions:[new(exam.Sessions[0].Id,new(2027,3,28),"2027/1",null,null),new(exam.Sessions[1].Id,new(2027,11,28),"2027/2",null,null)]),default);
        Assert.Contains("1 öğrencinin hedef oturumu",string.Join(" ",exam.Blockers));
    }
    private static string? Date(CurriculumNode node)
    {
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(node));
        return json.RootElement.GetProperty("ExamDate").GetString();
    }

    [Fact]
    public async Task ExamLookupReturnsDatabaseEntriesRatherThanAFixedList()
    {
        await using var f=new CurriculumManagementTests.Fixture();
        await f.Service.SaveAsync("exams",null,new("KPSS Genel Yetenek","Sınav türü ekle",Code:"KPSS",Sessions:[new(null,new(2027,6,20),"2027",null,null)]),default);
        var result=Assert.IsType<OkObjectResult>(await new CoachingCatalogLookupsController(f.Db).Exams(default));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var exam=Assert.Single(json.RootElement.GetProperty("data").EnumerateArray(),x=>x.GetProperty("Code").GetString()=="KPSS");
        Assert.Equal("2027-06-20",exam.GetProperty("ExamDate").GetString());
    }

    [Fact]
    public async Task ExamStatusControlsLessonActivation_AndLessonCanMoveToAnotherExamBeforeOutcomesExist()
    {
        await using var f=new CurriculumManagementTests.Fixture();
        var exam=await f.Service.SaveAsync("exams",null,new("KPSS","Sınav türü ekle",Code:"KPSS"),default);
        exam=await f.Service.SetActiveAsync("exams",exam.Id,new(exam.Fingerprint,"Sınavı pasife al",false),default);
        var lesson=await f.Service.SaveAsync("lessons",null,new("Türkçe","Ders ekle",ParentId:exam.Id,ExamCode:"KPSS"),default);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SetActiveAsync("lessons",lesson.Id,new(lesson.Fingerprint,"Dersi etkinleştir",true),default));
        exam=await f.Service.GetAsync("exams",exam.Id,default);
        await f.Service.SetActiveAsync("exams",exam.Id,new(exam.Fingerprint,"Sınavı etkinleştir",true),default);
        lesson=await f.Service.GetAsync("lessons",lesson.Id,default);
        lesson=await f.Service.SetActiveAsync("lessons",lesson.Id,new(lesson.Fingerprint,"Dersi etkinleştir",true),default);
        exam=await f.Service.GetAsync("exams",exam.Id,default);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SetActiveAsync("exams",exam.Id,new(exam.Fingerprint,"Sınavı pasife al",false),default));
        var target=await f.Db.Set<StudyCatalogExam>().SingleAsync(x=>x.Code=="LGS");
        lesson=await f.Service.GetAsync("lessons",lesson.Id,default);
        var moved=await f.Service.MoveAsync("lessons",lesson.Id,new(target.Id,lesson.Fingerprint,"Dersin sınavını değiştir"),default);
        Assert.Equal(lesson.Id,moved.Id);Assert.Equal("LGS",moved.ExamCode);
    }

    [Fact]
    public async Task ReferencedProgramScopeCannotChange_AndUnusedSourceRecordsCanBeEditedAndDeleted()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();
        var original=await f.Db.Set<OfficialCurriculumEdition>().SingleAsync();var doc=await f.Service.GetAsync("programs",original.Id,default);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>f.Service.SaveAsync("programs",original.Id,Request(new {Name="Farklı ders",Reason="Programı değiştir",Fingerprint=doc.Fingerprint,original.Code,original.ExamCode,original.GradeNumber,original.AcademicYearStart,original.AcademicYearEnd,original.SourceUrl,original.DocumentSha256,original.ScopeEvidenceUrl}),default));
        var program=await f.Service.SaveAsync("programs",null,Request(new {Name="Türkçe",Reason="Yeni program ekle",Code="new-program",ExamCode="LGS",GradeNumber=8,AcademicYearStart=2026,AcademicYearEnd=2026,SourceUrl="https://meb.gov.tr/program",DocumentSha256=new string('a',64),ScopeEvidenceUrl="https://meb.gov.tr/kapsam"}),default);
        program=await f.Service.SaveAsync("programs",program.Id,Request(new {Name="Türkçe",Reason="Geçerlilik yılını düzelt",Fingerprint=program.Fingerprint,Code="new-program",ExamCode="LGS",GradeNumber=8,AcademicYearStart=2026,AcademicYearEnd=2027,SourceUrl="https://meb.gov.tr/program",DocumentSha256=new string('a',64),ScopeEvidenceUrl="https://meb.gov.tr/kapsam"}),default);
        Assert.Equal(2027,program.AcademicYearEnd);
        var topic=await f.Service.SaveAsync("programTopics",null,Request(new {Name="Sözcük",Reason="Kaynak konu ekle",ParentId=program.Id,Code="SOZ",OutcomeCount=2,LessonPeriods=5}),default);
        await f.Service.DeleteAsync("programTopics",topic.Id,new(topic.Fingerprint,"Kullanılmayan konuyu sil",topic.Id),default);
        program=await f.Service.GetAsync("programs",program.Id,default);
        await f.Service.DeleteAsync("programs",program.Id,new(program.Fingerprint,"Kullanılmayan programı sil",program.Id),default);
        Assert.Single(await f.Db.Set<OfficialCurriculumEdition>().ToArrayAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationBackfillsEveryLegacyExamCode_AndPreservesPlansOnRepeatedUpgrade()
    {
        await using var f=await CurriculumPostgresTests.Pg.Create("20261020065330_MebiLgsProgramCatalog"); await f.Data.Seed(legacyPlanSchema: true);
        var before=JsonSerializer.Serialize(await CurriculumManagementTests.LegacyStudyTasks(f.Db).AsNoTracking().ToArrayAsync());
        await f.Db.GetService<IMigrator>().MigrateAsync("20261016185534_CurriculumManagementHierarchy");
        var legacy=StudyCatalogLesson.Create("test","legacy-ales","Matematik",12,"ALES");f.Db.Add(legacy);await f.Db.SaveChangesAsync();
        await f.Db.Database.MigrateAsync(); await f.Db.Database.MigrateAsync(); f.Db.ChangeTracker.Clear();
        Assert.True(await f.Db.Set<StudyCatalogExam>().AnyAsync(x=>x.Code=="ALES"));
        Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
        var exam=await f.Db.Set<StudyCatalogExam>().SingleAsync(x=>x.Code=="ALES");
        Assert.Equal(legacy.Id,Assert.Single((await f.Service.ChildrenAsync("lessons",exam.Id,null,1,default)).Items).Id);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task DynamicExamTopicSearchAndNonLgsProgramWorkWithRealForeignKeys()
    {
        await using var f=await CurriculumPostgresTests.Pg.Create();
        var exam=await f.Service.SaveAsync("exams",null,new("ALES","Yeni sınav ekle",Code:"ALES"),default);
        var lesson=await f.Service.SaveAsync("lessons",null,new("Matematik","Ders ekle",ParentId:exam.Id,ExamCode:"ALES",GradeNumber:12),default);
        lesson=await f.Service.SetActiveAsync("lessons",lesson.Id,new(lesson.Fingerprint,"Dersi etkinleştir",true),default);
        var unit=await f.Service.SaveAsync("units",null,new("Sayılar","Ünite ekle",ParentId:lesson.Id),default);
        unit=await f.Service.SetActiveAsync("units",unit.Id,new(unit.Fingerprint,"Üniteyi etkinleştir",true),default);
        var topic=await f.Service.SaveAsync("topics",null,new("Sayı problemleri","Konu ekle",ParentId:unit.Id,EstimatedMinutes:45),default);
        await f.Service.SetActiveAsync("topics",topic.Id,new(topic.Fingerprint,"Konuyu etkinleştir",true),default);
        Assert.Equal(topic.Id,Assert.Single((await new CoachingTopicSearchService(f.Db).SearchAsync("problemleri",12,"ALES",1,20)).Items).Id);
        var program=await f.Service.SaveAsync("programs",null,Request(new {Name="Matematik",Reason="Kaynak program ekle",Code="ales-mat",ExamCode="ALES",AcademicYearStart=2026,AcademicYearEnd=2026,SourceUrl="https://meb.gov.tr/program",DocumentSha256=new string('a',64),ScopeEvidenceUrl="https://meb.gov.tr/kapsam"}),default);
        Assert.Equal("ALES",program.ExamCode);Assert.Null(program.GradeNumber);
        Assert.Equal(program.Id,Assert.Single((await f.Service.ChildrenAsync("programs",null,null,1,default,true)).Items).Id);
        var source=await f.Service.SaveAsync("programTopics",null,Request(new {Name="Sayılar",Reason="Program konu grubu ekle",ParentId=program.Id,Code="SAY",OutcomeCount=1,LessonPeriods=5}),default);
        Assert.Equal(source.Id,Assert.Single((await f.Service.ChildrenAsync("programTopics",program.Id,null,1,default,true)).Items).Id);
        await Assert.ThrowsAsync<NotSupportedException>(()=>f.Db.GetService<IMigrator>().MigrateAsync("20261016195911_DynamicCurriculumExams"));
        Assert.True(await f.Db.Set<OfficialCurriculumEdition>().AnyAsync(x=>x.Id==program.Id&&x.GradeNumber==null));
        await Assert.ThrowsAsync<ArgumentException>(()=>new CoachingTopicSearchService(f.Db).SearchAsync(null,null,"UNKNOWN",1,20));
        var invalid=StudyCatalogLesson.Create("test","bad-exam","Bilinmeyen",null,"BAD");f.Db.Add(invalid);
        await Assert.ThrowsAsync<DbUpdateException>(()=>f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task TopicTabsFilterRootSubtopicsAndLeavesIndependently()
    {
        await using var f=new CurriculumManagementTests.Fixture();var root=await f.Seed();
        var child=await f.Service.SaveAsync("topics",null,new("Alt konu","Kazanımı alt konuya aktar",ParentId:root.Id,MoveParentOutcomes:true),default);
        Assert.Equal(root.Id,Assert.Single((await f.Service.ChildrenAsync("topics",null,null,1,default,true,"root")).Items).Id);
        Assert.Equal(child.Id,Assert.Single((await f.Service.ChildrenAsync("topics",null,null,1,default,true,"sub")).Items).Id);
        Assert.Equal(child.Id,Assert.Single((await f.Service.ChildrenAsync("topics",null,null,1,default,true,"leaf")).Items).Id);
        Assert.Single((await f.Service.ChildrenAsync("outcomes",null,null,1,default,true)).Items);
    }

    [Fact]
    public async Task ExamIndependentOfSchoolGradeCanHaveItsOwnSourceProgram()
    {
        await using var f=new CurriculumManagementTests.Fixture();
        await f.Service.SaveAsync("exams",null,new("KPSS","Sınav türü ekle",Code:"KPSS"),default);
        var program=await f.Service.SaveAsync("programs",null,Request(new {Name="Türkçe",Reason="Sınıftan bağımsız kaynak ekle",Code="kpss-turkce",ExamCode="KPSS",AcademicYearStart=2026,AcademicYearEnd=2026,SourceUrl="https://osym.gov.tr/program",DocumentSha256=new string('a',64),ScopeEvidenceUrl="https://osym.gov.tr/kapsam"}),default);
        Assert.Null(program.GradeNumber);
    }

    [Fact]
    public async Task NewExamCanOwnLesson_UnknownExamRejected_ReferencedExamCannotBeDeleted()
    {
        await using var f = new CurriculumManagementTests.Fixture();
        var exam = await f.Service.SaveAsync("exams", null, new("Kamu Personeli Seçme Sınavı", "Sınav türü ekle", Code: "KPSS"), default);
        var lesson = await f.Service.SaveAsync("lessons", null, new("Türkçe", "Dersi sınava bağla", ParentId: exam.Id, ExamCode: "KPSS"), default);
        Assert.Equal("exams", lesson.ParentKind); Assert.Equal(exam.Id, lesson.ParentId);
        Assert.Equal(lesson.Id, Assert.Single((await f.Service.ChildrenAsync("lessons", exam.Id, null, 1, default)).Items).Id);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveAsync("lessons", null, new("Hatalı ders", "Bilinmeyen sınav", ExamCode: "UNKNOWN"), default));
        exam = await f.Service.GetAsync("exams", exam.Id, default);
        Assert.False(exam.CanDelete);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync("exams", exam.Id, new(exam.Fingerprint, "Kullanılan sınavı sil", exam.Id), default));
        var edited = await f.Service.SaveAsync("exams", exam.Id, new("KPSS Genel Yetenek", "Sınav adını düzenle", Fingerprint: exam.Fingerprint, Code: "KPSS"), default);
        Assert.Equal(exam.Id, edited.Id); Assert.Equal("KPSS", (await f.Db.StudyCatalogLessons.SingleAsync()).ExamCode);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveAsync("exams", exam.Id, new("Başka kod", "Kod değişikliği", Fingerprint: edited.Fingerprint, Code: "ALES"), default));
    }

    [Fact]
    public async Task ExamDuplicatesAndStaleEditsRejected_UnusedExamCanBeDeleted()
    {
        await using var f = new CurriculumManagementTests.Fixture();
        var exam = await f.Service.SaveAsync("exams", null, new("ALES", "Yeni sınav ekle", Code: "ales"), default);
        Assert.Equal("ALES", exam.Code);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveAsync("exams", null, new("Mükerrer", "Aynı kodu dene", Code: "ALES"), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("exams", exam.Id, new("Yeni ad", "Adını değiştir", Fingerprint: "stale", Code: "ALES"), default));
        await f.Service.DeleteAsync("exams", exam.Id, new(exam.Fingerprint, "Kullanılmayan sınavı sil", exam.Id), default);
        Assert.DoesNotContain((await f.Service.ChildrenAsync("exams", null, null, 1, default)).Items, x => x.Code == "ALES");
    }

    [Fact]
    public async Task ProgramAndSourceTopicCanBeManagedForAnyRegisteredExam()
    {
        await using var f = new CurriculumManagementTests.Fixture();
        var exam = await f.Service.SaveAsync("exams", null, new("ALES", "Sınav kaydı ekle", Code: "ALES"), default);
        var program = await f.Service.SaveAsync("programs", null, Request(new { Name="Matematik", Reason="Kaynak program ekle", Code="ales-mat-2026", ExamCode="ALES", GradeNumber=12,
            AcademicYearStart=2026, AcademicYearEnd=2027, SourceUrl="https://meb.gov.tr/program", DocumentSha256=new string('a',64), ScopeEvidenceUrl="https://meb.gov.tr/kapsam" }), default);
        var source = await f.Service.SaveAsync("programTopics", null, Request(new { Name="Sayılar", Reason="Program konusu ekle", ParentId=program.Id, Code="SAY", OutcomeCount=5, LessonPeriods=12 }), default);
        Assert.Equal(source.Id, Assert.Single((await f.Service.ChildrenAsync("programTopics",program.Id,null,1,default)).Items).Id);
        source = await f.Service.SaveAsync("programTopics", source.Id, Request(new { Name="Sayılar ve İşlemler", Reason="Konu adını düzelt", Fingerprint=source.Fingerprint, Code="SAY", OutcomeCount=5, LessonPeriods=15 }), default);
        Assert.Equal("Sayılar ve İşlemler", source.Name);
        var lesson = await f.Service.SaveAsync("lessons", null, new("Matematik", "Sınava ders ekle", ParentId:exam.Id, ExamCode:"ALES", GradeNumber:12), default);
        var unit = await f.Service.SaveAsync("units", null, new("Sayılar", "Ünite ekle", ParentId:lesson.Id), default);
        var topic = await f.Service.SaveAsync("topics", null, new("Tam sayılar", "Konu ekle", ParentId:unit.Id), default);
        await f.Service.SaveAsync("outcomes", null, new("Tam sayılarla işlem yapar", "Kazanım ekle", ParentId:topic.Id, Code:"SAY.1", OfficialTopicId:source.Id, SourcePage:1), default);
        program = await f.Service.GetAsync("programs",program.Id,default);
        Assert.False(program.CanDelete);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync("programs",program.Id,new(program.Fingerprint,"Kullanılan programı sil",program.Id),default));
    }

    private static CurriculumSaveRequest Request(object value) => JsonSerializer.Deserialize<CurriculumSaveRequest>(JsonSerializer.Serialize(value))!;
}
