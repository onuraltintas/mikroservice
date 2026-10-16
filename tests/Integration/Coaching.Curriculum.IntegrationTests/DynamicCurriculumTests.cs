using System.Text.Json;
using Coaching.Infrastructure.Catalogs;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class DynamicCurriculumTests
{
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
        Assert.Empty((await f.Service.ChildrenAsync("exams", null, null, 1, default)).Items);
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
