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
public static class PracticeExamRules
{
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
        new("YDT_EN", "YDT · İngilizce", 4, Array.AsReadOnly(new PracticeSection[] { new("ENG", "İngilizce", 80) }))
    });

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
                decimal.Round(row.Correct - row.Wrong / (decimal)profile.WrongAnswerDivisor, 2, MidpointRounding.AwayFromZero));
        }).ToArray();
        var correct = summaries.Sum(x => x.Correct); var wrong = summaries.Sum(x => x.Wrong);
        // Roadmap D2: the total is the sum of the rounded section nets, so a report card adds up.
        return new(profile.Code, profile.Name, correct, wrong, summaries.Sum(x => x.Empty), summaries.Sum(x => x.Net), summaries);
    }

    public static void Validate(PracticeExamInput input, ExamType examType, int correct, int wrong, int empty)
    {
        var summary = Summarize(input);
        if (examType != (summary.ExamCode == "LGS" ? ExamType.LGS : ExamType.YKS))
            throw new ArgumentException("Deneme kapsamı sınav türüyle eşleşmiyor.");
        if (summary.Correct != correct || summary.Wrong != wrong || summary.Empty != empty)
            throw new ArgumentException("Ders toplamları sınavın doğru, yanlış ve boş toplamlarıyla eşleşmelidir.");
    }
}
