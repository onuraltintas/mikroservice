using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;

namespace Coaching.Curriculum.IntegrationTests;

// Paket G #41 (07.11.2026): the weekly proposal breaks a tie of the student's priority by the questions a course has in her exam.
public sealed class ExamQuestionWeightsTests
{
    private static ExamQuestionWeights.Lesson Yks(string name,params string[] tests)=>new(Guid.NewGuid(),name,null,tests);

    [Fact]
    public void YksCoursesWeighTheirTestSectionsAndShareASectionEqually()
    {
        var lessons=new[]{Yks("Matematik (TYT)","TYT.MAT"),Yks("Geometri (TYT)","TYT.MAT"),Yks("Türkçe (TYT)","TYT.TUR"),Yks("Din Kültürü ve Ahlak Bilgisi (TYT)","TYT.DIN"),
            Yks("Biyoloji (AYT)","AYT.FEN"),Yks("Fizik (AYT)","AYT.FEN"),Yks("Matematik (AYT)","AYT.MAT"),Yks("Fen Denemesi","AYT.FEN"),Yks("Kapsamsız"),Yks("Bilinmeyen","XYZ")};
        var weights=ExamQuestionWeights.For(LearningPurpose.Yks,lessons);
        Assert.Equal(20m,weights[lessons[0].Id]);Assert.Equal(20m,weights[lessons[1].Id]);Assert.Equal(40m,weights[lessons[2].Id]);
        Assert.Equal(5m,weights[lessons[3].Id]);Assert.Equal(13m,weights[lessons[4].Id]);Assert.Equal(14m,weights[lessons[5].Id]);
        Assert.Equal(40m,weights[lessons[6].Id]);
        // A course of a mixed test whose name matches no section counts as the test's average section.
        Assert.Equal(13.3m,weights[lessons[7].Id]);
        Assert.False(weights.ContainsKey(lessons[8].Id));Assert.False(weights.ContainsKey(lessons[9].Id));
    }

    [Fact]
    public void ASozelCourseInTwoTestsAddsBothSections()
    {
        var lessons=new[]{Yks("Tarih (AYT)","AYT.EDESOS1","AYT.SOS2"),Yks("Türk Dili ve Edebiyatı (AYT)","AYT.EDESOS1"),Yks("Felsefe (AYT)","AYT.SOS2"),Yks("Psikoloji (AYT)","AYT.SOS2")};
        var weights=ExamQuestionWeights.For(LearningPurpose.Yks,lessons);
        Assert.Equal(21m,weights[lessons[0].Id]);Assert.Equal(24m,weights[lessons[1].Id]);Assert.Equal(6m,weights[lessons[2].Id]);Assert.Equal(6m,weights[lessons[3].Id]);
    }

    // Review of PR #126: catalog names with a circumflex or the language in brackets still find their section.
    [Fact]
    public void RealLgsCatalogNamesFindTheirSections()
    {
        ExamQuestionWeights.Lesson Lgs(string name)=>new(Guid.NewGuid(),name,"LGS",[]);
        var lessons=new[]{Lgs("Türkçe"),Lgs("Matematik"),Lgs("Fen Bilimleri"),Lgs("T.C. İnkılap Tarihi ve Atatürkçülük"),
            Lgs("Din Kültürü ve Ahlâk Bilgisi"),Lgs("Yabancı Dil (İngilizce)"),Lgs("Seçmeli Okuma Becerileri")};
        var weights=ExamQuestionWeights.For(LearningPurpose.Lgs,lessons);
        Assert.Equal([20m,20m,20m,10m,10m,10m],lessons.Take(6).Select(x=>weights[x.Id]));
        Assert.False(weights.ContainsKey(lessons[6].Id));
    }

    [Fact]
    public void TheSameUnknownCourseTestIsSharedWhateverItsSpelling()
    {
        var lessons=new[]{Yks("Fen Denemesi","AYT.FEN"),Yks("Fen Tekrarı"," ayt.fen ")};
        var weights=ExamQuestionWeights.For(LearningPurpose.Yks,lessons);
        Assert.Equal(6.7m,weights[lessons[0].Id]);Assert.Equal(6.7m,weights[lessons[1].Id]);
    }

    [Fact]
    public void OtherExamsMatchTheCourseNameToASection()
    {
        ExamQuestionWeights.Lesson Lesson(string name,string exam)=>new(Guid.NewGuid(),name,exam,[]);
        var lessons=new[]{Lesson("Matematik","LGS"),Lesson("T.C. İnkılap Tarihi ve Atatürkçülük","LGS"),Lesson("Seçmeli Okuma","LGS"),
            Lesson("Sözel Yetenek (AGS)","AGS"),Lesson("İlköğretim Matematik (ÖABT)","OABT_IMAT"),Lesson("YDS İngilizce","YDS")};
        var weights=ExamQuestionWeights.For(LearningPurpose.Lgs,lessons);
        Assert.Equal(20m,weights[lessons[0].Id]);Assert.Equal(10m,weights[lessons[1].Id]);Assert.False(weights.ContainsKey(lessons[2].Id));
        Assert.Equal(15m,weights[lessons[3].Id]);Assert.Equal(50m,weights[lessons[4].Id]);Assert.Equal(80m,weights[lessons[5].Id]);
    }
}
