using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Coaching.Curriculum.IntegrationTests;

// KM1 (01.11.2026): the coaching product also prepares students for AGS (+ÖABT), YDS and YÖKDİL. Every number below comes
// from an official ÖSYM/MEB document (see PracticeExamRules for the source of each one).
public sealed class ExamTypesKm1Tests
{
    public static readonly TheoryData<string, string> OabtFields = new()
    {
        { "OABT_TUR", "Türkçe" }, { "OABT_IMAT", "İlköğretim Matematik" }, { "OABT_FEN", "Fen Bilimleri" },
        { "OABT_SOS", "Sosyal Bilgiler" }, { "OABT_TDE", "Türk Dili ve Edebiyatı" }, { "OABT_TAR", "Tarih" },
        { "OABT_COG", "Coğrafya" }, { "OABT_MAT", "Matematik" }, { "OABT_FIZ", "Fizik" },
        { "OABT_KIM", "Kimya/Kimya Teknolojisi" }, { "OABT_BIO", "Biyoloji" },
        { "OABT_DIN", "Din Kültürü ve Ahlâk Bilgisi / İmam-Hatip Lisesi Meslek Dersleri" }, { "OABT_REH", "Rehberlik" },
        { "OABT_SINIF", "Sınıf Öğretmenliği" }, { "OABT_OKUL", "Okul Öncesi" }, { "OABT_BED", "Beden Eğitimi" },
        { "OABT_OZEL", "Özel Eğitim" }
    };

    [Fact]
    public void AgsHasTheSixPublishedSectionsOfEightyQuestionsAndTheQuarterRule()
    {
        var ags = PracticeExamRules.Profiles.Single(x => x.Code == "AGS");
        Assert.Equal(new[] { ("SOZ", 15), ("SAY", 15), ("TAR", 6), ("COG", 6), ("EGT", 30), ("MEV", 8) },
            ags.Sections.Select(x => (x.Code, x.QuestionCount)).ToArray());
        Assert.Equal(80, ags.Sections.Sum(x => x.QuestionCount));
        Assert.Equal(4, ags.WrongAnswerDivisor);
        Assert.Equal(ExamType.AGS, PracticeExamRules.ExamTypeFor("AGS"));
    }

    [Theory, MemberData(nameof(OabtFields))]
    public void EveryOabtFieldHasFiftyQuestionsAndTheQuarterRule(string code, string name)
    {
        var profile = PracticeExamRules.Profiles.Single(x => x.Code == code);
        Assert.Equal($"ÖABT · {name}", profile.Name);
        Assert.Equal(50, profile.Sections.Sum(x => x.QuestionCount));
        Assert.Equal(4, profile.WrongAnswerDivisor);
        Assert.Equal(ExamType.AGS, PracticeExamRules.ExamTypeFor(code));
    }

    [Fact]
    public void ThereAreExactlySeventeenOabtFields() =>
        Assert.Equal(17, PracticeExamRules.Profiles.Count(x => x.Code.StartsWith("OABT_", StringComparison.Ordinal)));

    [Theory]
    [InlineData("YDS_EN", "YDS · İngilizce", ExamType.YDS)]
    [InlineData("YOKDIL_FEN", "YÖKDİL · Fen Bilimleri · İngilizce", ExamType.YOKDIL)]
    [InlineData("YOKDIL_SAG", "YÖKDİL · Sağlık Bilimleri · İngilizce", ExamType.YOKDIL)]
    [InlineData("YOKDIL_SOS", "YÖKDİL · Sosyal Bilimler · İngilizce", ExamType.YOKDIL)]
    public void LanguageExamsHaveEightyQuestionsAndNoPenalty(string code, string name, ExamType type)
    {
        var profile = PracticeExamRules.Profiles.Single(x => x.Code == code);
        Assert.Equal(name, profile.Name);
        Assert.Equal(80, profile.Sections.Sum(x => x.QuestionCount));
        Assert.Equal(0, profile.WrongAnswerDivisor);
        Assert.Equal(type, PracticeExamRules.ExamTypeFor(code));
        var summary = PracticeExamRules.Summarize(new(code, [new(profile.Sections[0].Code, 50, 30, 0)]));
        Assert.Equal(50m, summary.Net);
        Assert.Equal(62.5m, LanguageScoreRules.Score(summary));
        Assert.True(LanguageScoreRules.IsLanguageExam(code));
    }

    [Fact]
    public void ExistingProfilesKeepTheirTypesAndNets()
    {
        Assert.Equal(ExamType.LGS, PracticeExamRules.ExamTypeFor("LGS"));
        foreach (var code in new[] { "TYT", "AYT_SAY", "AYT_EA", "AYT_SOZ", "YDT_EN" }) Assert.Equal(ExamType.YKS, PracticeExamRules.ExamTypeFor(code));
        Assert.Equal(1.67m, PracticeExamRules.Net(2, 1, 3));
        Assert.Equal(1.75m, PracticeExamRules.Net(2, 1, 4));
        Assert.Equal(2m, PracticeExamRules.Net(2, 1, 0));
        Assert.False(LanguageScoreRules.IsLanguageExam("YDT_EN"));
        Assert.Equal(new[] { ExamType.LGS, ExamType.YKS, ExamType.AGS, ExamType.YDS, ExamType.YOKDIL },
            Enum.GetValues<ExamType>().Where(PracticeExamRules.RequiresSections).ToArray());
        // The stored numeric values of the old types do not move.
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, Enum.GetValues<ExamType>().Select(x => (int)x).ToArray());
    }

    [Fact]
    public void ValidateMatchesTheProfileToItsExamType()
    {
        var input = new PracticeExamInput("YDS_EN", [new("ENG", 60, 10, 10)]);
        PracticeExamRules.Validate(input, ExamType.YDS, 60, 10, 10);
        Assert.Throws<ArgumentException>(() => PracticeExamRules.Validate(input, ExamType.YKS, 60, 10, 10));
        Assert.Throws<ArgumentException>(() => PracticeExamRules.Validate(input, ExamType.YOKDIL, 60, 10, 10));
    }

    [Fact]
    public void InstitutionAndStudentExamsTakeTheTypeOfTheirScope()
    {
        var exam = Exam.CreateInstitutionWide(Guid.NewGuid(), Guid.NewGuid(), "AGS deneme 1", "OABT_SINIF", DateTime.UtcNow, 100, null);
        Assert.Equal(ExamType.AGS, exam.ExamType);
        var mock = Exam.CreateStudentReported(Guid.NewGuid(), "YÖKDİL", ExamType.YOKDIL, DateTime.UtcNow.AddDays(-1), 100);
        var result = ExamResult.Create(mock.Id, mock.StudentOwnerId!.Value, 60);
        result.SetAnswerStatistics(48, 10, 22);
        result.SetPracticeExam(new("YOKDIL_SAG", [new("ENG", 48, 10, 22)]), ExamType.YOKDIL);
        Assert.Throws<ArgumentException>(() => result.SetPracticeExam(new("YDS_EN", [new("ENG", 48, 10, 22)]), ExamType.YOKDIL));
    }

    [Theory]
    [InlineData(LearningPurpose.Ags, null, "AGS")]
    [InlineData(LearningPurpose.Ags, "OABT_OZEL", "AGS,OABT_OZEL")]
    [InlineData(LearningPurpose.Yds, null, "YDS_EN")]
    [InlineData(LearningPurpose.Yokdil, "YOKDIL_FEN", "YOKDIL_FEN")]
    [InlineData(LearningPurpose.Lgs, null, "LGS")]
    [InlineData(LearningPurpose.Yks, "SAY", "TYT,AYT_SAY")]
    [InlineData(LearningPurpose.General, null, "")]
    public void PurposesKnowTheirCatalogExamAndNetScopes(LearningPurpose purpose, string? scoreType, string scopes) =>
        Assert.Equal(scopes, string.Join(',', LearningPurposes.NetExamCodes(purpose, scoreType)));

    [Fact]
    public void PurposesMapToTheirCatalogExam()
    {
        Assert.Equal(new[] { null, "LGS", "TYT", null, null, "AGS", "YDS", "YOKDIL" },
            Enum.GetValues<LearningPurpose>().Select(LearningPurposes.CatalogExamCode).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, Enum.GetValues<LearningPurpose>().Select(x => (int)x).ToArray());
    }

    [Fact]
    public void ScopeRulesForTheNewPurposes()
    {
        var student = Guid.NewGuid();
        var ags = StudentLearningContext.Create(student, 2026, LearningPurpose.Ags, 2027, null, null);
        Assert.Null(ags.PrimaryScoreType);
        ags.SetScope(2026, LearningPurpose.Ags, 2027, "oabt_sinif", null);
        Assert.Equal("OABT_SINIF", ags.PrimaryScoreType);
        Assert.Throws<ArgumentException>(() => ags.SetScope(2026, LearningPurpose.Ags, null, null, null));
        Assert.Throws<ArgumentException>(() => ags.SetScope(2026, LearningPurpose.Ags, 2027, "SAY", null));
        Assert.Throws<ArgumentException>(() => ags.SetScope(2026, LearningPurpose.Ags, 2027, null, "English"));

        var yds = StudentLearningContext.Create(student, 2026, LearningPurpose.Yds, 2027, null, null);
        Assert.Equal("English", yds.ForeignLanguage);
        Assert.Throws<ArgumentException>(() => yds.SetScope(2026, LearningPurpose.Yds, 2027, null, "German"));
        Assert.Throws<ArgumentException>(() => yds.SetScope(2026, LearningPurpose.Yds, 2027, "YDS_EN", null));

        var yokdil = StudentLearningContext.Create(student, 2026, LearningPurpose.Yokdil, 2027, "YOKDIL_SAG", "English");
        Assert.Equal(("YOKDIL_SAG", "English"), (yokdil.PrimaryScoreType, yokdil.ForeignLanguage));
        Assert.Throws<ArgumentException>(() => yokdil.SetScope(2026, LearningPurpose.Yokdil, 2027, null, null));
        Assert.Throws<ArgumentException>(() => yokdil.SetScope(2026, LearningPurpose.Yokdil, 2027, "FEN", null));

        // LGS and YKS keep their rules.
        Assert.Throws<ArgumentException>(() => StudentLearningContext.Create(student, 2026, LearningPurpose.Lgs, 2027, null, "English"));
        Assert.Throws<ArgumentException>(() => StudentLearningContext.Create(student, 2026, LearningPurpose.Yks, 2027, "DIL", "German"));
        Assert.Null(StudentLearningContext.Create(student, 2026, LearningPurpose.Yks, 2027, "SAY", null).ForeignLanguage);
    }

    [Fact]
    public void ChoosingASessionAndChoosingAnOwnDateReplaceEachOther()
    {
        var today = new DateOnly(2026, 11, 1);
        var context = StudentLearningContext.Create(Guid.NewGuid(), 2026, LearningPurpose.Yds, 2027, null, null);
        context.SetTargetExamDate(new DateOnly(2027, 3, 1), today);
        var session = Guid.NewGuid();
        context.SetTargetExamSession(session);
        Assert.Equal((session, (DateOnly?)null), (context.TargetExamSessionId, context.TargetExamDate));
        context.SetTargetExamDate(new DateOnly(2027, 3, 2), today);
        Assert.Equal(((Guid?)null, (DateOnly?)new DateOnly(2027, 3, 2)), (context.TargetExamSessionId, context.TargetExamDate));
        context.SetTargetExamSession(session); context.SetTargetExamDate(null, today);
        Assert.Equal(session, context.TargetExamSessionId);
    }

    [Fact]
    public void SessionsAreValidatedAndReplacedById()
    {
        var exam = StudyCatalogExam.Create("YDS", "Yabancı Dil Bilgisi Seviye Tespit Sınavı");
        var removed = exam.ReplaceSessions([
            new(null, new(2027, 11, 21), "2027/2", new(2027, 10, 5), new(2027, 10, 13)),
            new(null, new(2027, 3, 28), "2027/1", new(2027, 2, 3), new(2027, 2, 11))]);
        Assert.Empty(removed);
        Assert.Equal(new[] { "2027/1", "2027/2" }, exam.Sessions.Select(x => x.Label).ToArray());
        Assert.Equal(new DateOnly(2027, 3, 28), exam.NextSessionDate(new(2026, 11, 1)));
        Assert.Equal(new DateOnly(2027, 11, 21), exam.NextSessionDate(new(2027, 3, 28)));
        Assert.Equal(new DateOnly(2027, 11, 21), exam.NextSessionDate(new(2028, 1, 1)));
        var spring = exam.Sessions[0];
        removed = exam.ReplaceSessions([new(spring.Id, new(2027, 3, 29), " 2027/1 ", null, null)]);
        Assert.Single(removed);
        Assert.Equal((spring.Id, new DateOnly(2027, 3, 29), "2027/1", (DateOnly?)null), (exam.Sessions.Single().Id, exam.Sessions.Single().Date, exam.Sessions.Single().Label, exam.Sessions.Single().ApplicationEnd));
        Assert.Throws<ArgumentException>(() => exam.ReplaceSessions([new(null, new(2027, 3, 1), "A", null, null), new(null, new(2027, 3, 1), "B", null, null)]));
        Assert.Throws<ArgumentException>(() => exam.ReplaceSessions([new(null, new(2027, 3, 1), "", null, null)]));
        Assert.Throws<ArgumentException>(() => exam.ReplaceSessions([new(null, new(2027, 3, 1), "A", new(2027, 2, 1), null)]));
        Assert.Throws<ArgumentException>(() => exam.ReplaceSessions([new(null, new(2027, 3, 1), "A", new(2027, 2, 10), new(2027, 2, 1))]));
        Assert.Throws<ArgumentException>(() => exam.ReplaceSessions([new(null, new(2027, 3, 1), "A", new(2027, 2, 10), new(2027, 3, 1))]));
        Assert.Throws<ArgumentException>(() => exam.ReplaceSessions([new(Guid.NewGuid(), new(2027, 3, 1), "A", null, null)]));
        Assert.Throws<ArgumentException>(() => exam.ReplaceSessions(Enumerable.Range(0, 41).Select(i => new StudyCatalogExamSessionInput(null, new DateOnly(2027, 1, 1).AddDays(i), "e-YDS", null, null)).ToArray()));
        Assert.Null(StudyCatalogExam.Create("LGS", "LGS").NextSessionDate(new(2026, 11, 1)));
    }
}
