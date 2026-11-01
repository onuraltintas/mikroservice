using Coaching.Domain.Enums;

namespace Coaching.Domain.Entities;

public sealed record PracticeSection(string Code, string Name, int QuestionCount);
public sealed record PracticeExamProfile(string Code, string Name, int WrongAnswerDivisor, IReadOnlyList<PracticeSection> Sections);
public sealed record PracticeSectionAnswers(string SectionCode, int Correct, int Wrong, int Empty);
public sealed record PracticeExamInput(string ExamCode, IReadOnlyList<PracticeSectionAnswers> Sections);
public sealed record PracticeTopicAnswers(string SectionCode, Guid TopicId, int Correct, int Wrong, int Empty, string? Reason,
    string? TopicName = null, string? LessonName = null);
public sealed record PracticeTopicAnalysis(IReadOnlyList<PracticeTopicAnswers> Topics, string? TestIdentity = null);
public sealed record PracticeSectionSummary(string SectionCode, string Name, int QuestionCount, int Correct, int Wrong, int Empty, decimal Net);
public sealed record PracticeExamSummary(string ExamCode, string Name, int Correct, int Wrong, int Empty, decimal Net, IReadOnlyList<PracticeSectionSummary> Sections);

// Standard practice-test formats, based on the 2026 MEB/ÖSYM guides. These are test
// sections, independent of the editable lesson/topic catalog. Net is not a placement score.
// WrongAnswerDivisor: n wrong answers cancel one correct answer; 0 means wrong answers do not count (YDS, YÖKDİL).
public static class PracticeExamRules
{
    /// <summary>KM1: correct − wrong / divisor, rounded to hundredths; a divisor of 0 means wrong answers cost nothing.</summary>
    public static decimal Net(int correct, int wrong, int divisor) =>
        decimal.Round(divisor == 0 ? correct : correct - wrong / (decimal)divisor, 2, MidpointRounding.AwayFromZero);

    /// <summary>KM1: the exam type a practice scope belongs to. LGS and YKS keep their old mapping.</summary>
    public static ExamType ExamTypeFor(string profileCode) => profileCode switch
    {
        "LGS" => ExamType.LGS,
        "AGS" => ExamType.AGS,
        _ when profileCode.StartsWith("OABT_", StringComparison.Ordinal) => ExamType.AGS,
        "YDS_EN" => ExamType.YDS,
        _ when profileCode.StartsWith("YOKDIL_", StringComparison.Ordinal) => ExamType.YOKDIL,
        _ => ExamType.YKS
    };

    /// <summary>Exam types whose results must be entered section by section against one of the profiles below.</summary>
    public static bool RequiresSections(ExamType type) => type is ExamType.LGS or ExamType.YKS or ExamType.AGS or ExamType.YDS or ExamType.YOKDIL;

    public static void ValidateTopics(PracticeExamInput input, PracticeTopicAnalysis analysis)
    {
        Summarize(input);
        if (analysis.Topics is null || analysis.Topics.Count > 120 || analysis.TestIdentity?.Length > 150
            || analysis.Topics.Any(x => x is null || x.TopicId == Guid.Empty || x.Correct < 0 || x.Wrong < 0 || x.Empty < 0
                || (long)x.Correct + x.Wrong + x.Empty is < 1 or > 120
                || x.Reason is not (null or "Knowledge" or "Calculation" or "Interpretation" or "Time")
                || !input.Sections.Any(s => s.SectionCode == x.SectionCode))
            || analysis.Topics.Select(x => (x.SectionCode, x.TopicId)).Distinct().Count() != analysis.Topics.Count)
            throw new ArgumentException("Konu dağılımını, nedenleri ve deneme kimliğini kontrol edin.");
        foreach (var group in analysis.Topics.GroupBy(x => x.SectionCode))
        {
            var section = input.Sections.Single(x => x.SectionCode == group.Key);
            if (group.Sum(x => x.Correct) > section.Correct || group.Sum(x => x.Wrong) > section.Wrong || group.Sum(x => x.Empty) > section.Empty)
                throw new ArgumentException("Konu dağılımı dersin doğru, yanlış veya boş toplamını aşamaz.");
        }
    }
    public static IReadOnlyList<PracticeExamProfile> Profiles { get; } = Array.AsReadOnly(new PracticeExamProfile[] {
        new("LGS", "LGS", 3, Array.AsReadOnly(new PracticeSection[] {
            new("TUR", "Türkçe", 20), new("MAT", "Matematik", 20), new("FEN", "Fen Bilimleri", 20),
            new("INK", "T.C. İnkılap Tarihi ve Atatürkçülük", 10), new("DIN", "Din Kültürü ve Ahlak Bilgisi", 10), new("ENG", "İngilizce", 10) })),
        new("TYT", "TYT", 4, Array.AsReadOnly(new PracticeSection[] {
            new("TUR", "Türkçe", 40), new("MAT", "Temel Matematik", 40), new("TAR", "Tarih", 5), new("COG", "Coğrafya", 5),
            new("FEL", "Felsefe", 5), new("DIN", "Din Kültürü ve Ahlak Bilgisi", 5), new("FIZ", "Fizik", 7), new("KIM", "Kimya", 7), new("BIO", "Biyoloji", 6) })),
        new("AYT_SAY", "AYT · Sayısal", 4, Array.AsReadOnly(new PracticeSection[] {
            new("MAT", "Matematik", 40), new("FIZ", "Fizik", 14), new("KIM", "Kimya", 13), new("BIO", "Biyoloji", 13) })),
        new("AYT_EA", "AYT · Eşit Ağırlık", 4, Array.AsReadOnly(new PracticeSection[] {
            new("MAT", "Matematik", 40), new("EDB", "Türk Dili ve Edebiyatı", 24), new("TAR1", "Tarih-1", 10), new("COG1", "Coğrafya-1", 6) })),
        new("AYT_SOZ", "AYT · Sözel", 4, Array.AsReadOnly(new PracticeSection[] {
            new("EDB", "Türk Dili ve Edebiyatı", 24), new("TAR1", "Tarih-1", 10), new("COG1", "Coğrafya-1", 6),
            new("TAR2", "Tarih-2", 11), new("COG2", "Coğrafya-2", 11), new("FEL", "Felsefe Grubu", 12), new("DIN", "Din Kültürü ve Ahlak Bilgisi", 6) })),
        new("YDT_EN", "YDT · İngilizce", 4, Array.AsReadOnly(new PracticeSection[] { new("ENG", "İngilizce", 80) })),
        // KM1 (01.11.2026). AGS: 80 questions (ÖSYM, 2026-MEB-AGS konu dağılımı, 08.01.2026,
        // https://dokuman.osym.gov.tr/pdfdokuman/2026/MEB-AGS/konudagilimi08012026.pdf: "AGS Konuları (80 soru)", weights
        // Sözel Yetenek %18,75, Sayısal Yetenek %18,75, Tarih %7,5, Türkiye Coğrafyası %7,5, Eğitim Bilimleri ve Türk Millî
        // Eğitim Sistemi %37,5, Mevzuat %10 → 15/15/6/6/30/8). Wrong answers: "Doğru cevap sayısından yanlış cevap sayısının
        // dörtte biri çıkarılarak ... ham puanı bulunacaktır" (ÖSYM, 2025-MEB-AGS konu dağılımı,
        // https://dokuman.osym.gov.tr/pdfdokuman/2025/MEB-AGS/konudagilimi16012025.pdf).
        new("AGS", "AGS", 4, Array.AsReadOnly(new PracticeSection[] {
            new("SOZ", "Sözel Yetenek", 15), new("SAY", "Sayısal Yetenek", 15), new("TAR", "Tarih", 6), new("COG", "Türkiye Coğrafyası", 6),
            new("EGT", "Eğitim Bilimleri ve Türk Millî Eğitim Sistemi", 30), new("MEV", "Mevzuat", 8) })),
        // ÖABT: 50 questions per field ("AGS'de 80; ÖABT'de 50 çoktan seçmeli sorudan oluşan testler", same 2025 document; MEB
        // 2026 announcement "soru sayısı ... toplam 50"), same quarter rule. The 17 fields are listed in the 2026-MEB-AGS
        // Başvuru Kılavuzu, section 22 (https://dokuman.osym.gov.tr/web/2026/7/basvuru-kilavuzu-p9djhk-30105733.pdf). The guide
        // gives no fixed Alan Bilgisi / Alan Eğitimi question counts (only topic weights for a few fields), so one section.
        Oabt("OABT_TUR", "Türkçe"), Oabt("OABT_IMAT", "İlköğretim Matematik"), Oabt("OABT_FEN", "Fen Bilimleri"),
        Oabt("OABT_SOS", "Sosyal Bilgiler"), Oabt("OABT_TDE", "Türk Dili ve Edebiyatı"), Oabt("OABT_TAR", "Tarih"),
        Oabt("OABT_COG", "Coğrafya"), Oabt("OABT_MAT", "Matematik"), Oabt("OABT_FIZ", "Fizik"),
        Oabt("OABT_KIM", "Kimya/Kimya Teknolojisi"), Oabt("OABT_BIO", "Biyoloji"),
        Oabt("OABT_DIN", "Din Kültürü ve Ahlâk Bilgisi / İmam-Hatip Lisesi Meslek Dersleri"), Oabt("OABT_REH", "Rehberlik"),
        Oabt("OABT_SINIF", "Sınıf Öğretmenliği"), Oabt("OABT_OKUL", "Okul Öncesi"), Oabt("OABT_BED", "Beden Eğitimi"),
        Oabt("OABT_OZEL", "Özel Eğitim"),
        // YDS: "çoktan seçmeli 80 sorudan oluşan testler" (2026-YDS/1 Kılavuzu, section 3,
        // https://dokuman.osym.gov.tr/pdfdokuman/2026/YDS-1/kilavuz_yd1d18022026.pdf). Wrong answers do not count: "yalnız doğru
        // cevaplar dikkate alınır, yanlış cevaplar dikkate alınmaz" (2026 e-YDS Kılavuzu, section 3.6,
        // https://dokuman.osym.gov.tr/web/2026/8/2026-elektronik-yabanci-dil-sinavi-2026-e-yds-kilavuzu-1zhshr-05113115.pdf).
        new("YDS_EN", "YDS · İngilizce", 0, Array.AsReadOnly(new PracticeSection[] { new("ENG", "İngilizce", 80) })),
        // YÖKDİL: "Sınavda soru sayısı 80" and English in Fen Bilimleri, Sağlık Bilimleri, Sosyal Bilimler (2026-YÖKDİL/2
        // Kılavuzu, sections 1.1 and 3, https://dokuman.osym.gov.tr/pdfdokuman/2026/YOKDIL-2/kilavuz_ykdl2d16062026.pdf);
        // wrong answers do not count (e-YÖKDİL Kılavuzu, section 3.6,
        // https://dokuman.osym.gov.tr/pdfdokuman/2022/e-YOKDIL/kilavuz06042022.pdf).
        Yokdil("YOKDIL_FEN", "Fen Bilimleri"), Yokdil("YOKDIL_SAG", "Sağlık Bilimleri"), Yokdil("YOKDIL_SOS", "Sosyal Bilimler")
    });

    private static PracticeExamProfile Oabt(string code, string field) =>
        new(code, $"ÖABT · {field}", 4, Array.AsReadOnly(new PracticeSection[] { new("ALAN", "Alan Bilgisi ve Alan Eğitimi", 50) }));
    private static PracticeExamProfile Yokdil(string code, string field) =>
        new(code, $"YÖKDİL · {field} · İngilizce", 0, Array.AsReadOnly(new PracticeSection[] { new("ENG", "İngilizce", 80) }));

    public static PracticeExamSummary Summarize(PracticeExamInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var profile = Profiles.SingleOrDefault(x => x.Code == input.ExamCode)
            ?? throw new ArgumentException("Geçerli bir deneme kapsamı seçin.");
        if (input.Sections is null || input.Sections.Count != profile.Sections.Count
            || input.Sections.Any(x => x is null)
            || input.Sections.Select(x => x.SectionCode).Distinct().Count() != input.Sections.Count)
            throw new ArgumentException("Sınav kapsamındaki her ders bir kez girilmelidir.");
        var summaries = profile.Sections.Select(section => {
            var row = input.Sections.SingleOrDefault(x => x.SectionCode == section.Code);
            if (row is null || row.Correct < 0 || row.Wrong < 0 || row.Empty < 0
                || (long)row.Correct + row.Wrong + row.Empty != section.QuestionCount)
                throw new ArgumentException($"{section.Name}: doğru, yanlış ve boş toplamı {section.QuestionCount} olmalıdır.");
            return new PracticeSectionSummary(section.Code, section.Name, section.QuestionCount, row.Correct, row.Wrong, row.Empty,
                Net(row.Correct, row.Wrong, profile.WrongAnswerDivisor));
        }).ToArray();
        var correct = summaries.Sum(x => x.Correct); var wrong = summaries.Sum(x => x.Wrong);
        // Roadmap D2: the total is the sum of the rounded section nets, so a report card adds up.
        return new(profile.Code, profile.Name, correct, wrong, summaries.Sum(x => x.Empty), summaries.Sum(x => x.Net), summaries);
    }

    public static void Validate(PracticeExamInput input, ExamType examType, int correct, int wrong, int empty)
    {
        var summary = Summarize(input);
        if (examType != ExamTypeFor(summary.ExamCode))
            throw new ArgumentException("Deneme kapsamı sınav türüyle eşleşmiyor.");
        if (summary.Correct != correct || summary.Wrong != wrong || summary.Empty != empty)
            throw new ArgumentException("Ders toplamları sınavın doğru, yanlış ve boş toplamlarıyla eşleşmelidir.");
    }
}

/// <summary>
/// KM1 (01.11.2026): YDS and YÖKDİL scores need no yearly model: "Adayların doğru cevap sayıları toplanarak 100 üzerinden ...
/// puanları hesaplanır" (2026 e-YDS Kılavuzu, section 3.6; e-YÖKDİL Kılavuzu, section 3.6). With 80 questions that is 1.25 points
/// per correct answer. Questions ÖSYM cancels after the exam are not modelled.
/// </summary>
public static class LanguageScoreRules
{
    public static bool IsLanguageExam(string examCode) => examCode == "YDS_EN" || examCode.StartsWith("YOKDIL_", StringComparison.Ordinal);

    public static decimal Score(PracticeExamSummary summary)
    {
        if (!IsLanguageExam(summary.ExamCode)) throw new ArgumentException("Yalnız YDS ve YÖKDİL puanı doğru sayısından hesaplanır.");
        var questions = summary.Sections.Sum(x => x.QuestionCount);
        return decimal.Round(summary.Correct * 100m / questions, 2, MidpointRounding.AwayFromZero);
    }
}
