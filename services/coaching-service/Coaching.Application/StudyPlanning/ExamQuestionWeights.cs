using System.Globalization;
using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

/// <summary>
/// Paket G #41 (07.11.2026): how much a course counts in the student's exam, as the number of questions it has there. The
/// weekly proposal orders courses by the student's priority first and by this weight second, so with too few study hours
/// Matematik and Türkçe come before Din Kültürü and Felsefe. The counts are the platform's practice exam sections
/// (<see cref="PracticeExamRules.Profiles"/>, from the 2026 ÖSYM/MEB guides). A YKS course's test is read from its topics'
/// YKS scopes for the student's score type (TYT.MAT, AYT.FEN, …); other exams match the course name to a section (circumflex
/// folded, the text in brackets tried too: "Din Kültürü ve Ahlâk Bilgisi", "Yabancı Dil (İngilizce)"). A section shared by
/// several courses (TYT Temel Matematik for Matematik and Geometri) is split equally between them; the split counts every
/// course passed in, i.e. all eligible catalog courses of the student's exam, not only the chosen ones, so a course's
/// weight does not change with the student's selection. A course that matches no section has no weight (null); the
/// proposal ranks such courses after the weighted ones, by topic count.
/// </summary>
public static class ExamQuestionWeights
{
    public sealed record Lesson(Guid Id, string Name, string? ExamCode, IReadOnlyCollection<string> YksTestCodes);

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    // The YKS test codes of the catalog (CoachingCatalogAdminToolsService.KnownTestCodes) and the practice sections they are made of.
    private static readonly IReadOnlyDictionary<string, (string Exam, string[] Sections)> YksTests = new Dictionary<string, (string, string[])>
    {
        ["TYT.TUR"] = ("TYT", ["TUR"]), ["TYT.MAT"] = ("TYT", ["MAT"]), ["TYT.TAR"] = ("TYT", ["TAR"]), ["TYT.GEO"] = ("TYT", ["COG"]),
        ["TYT.FEL"] = ("TYT", ["FEL"]), ["TYT.DIN"] = ("TYT", ["DIN"]), ["TYT.FIZ"] = ("TYT", ["FIZ"]), ["TYT.KIM"] = ("TYT", ["KIM"]),
        ["TYT.BIO"] = ("TYT", ["BIO"]), ["AYT.MAT"] = ("AYT_SAY", ["MAT"]), ["AYT.FEN"] = ("AYT_SAY", ["FIZ", "KIM", "BIO"]),
        ["AYT.EDESOS1"] = ("AYT_EA", ["EDB", "TAR1", "COG1"]), ["AYT.SOS2"] = ("AYT_SOZ", ["TAR2", "COG2", "FEL", "DIN"]),
        ["YDT.EN"] = ("YDT_EN", ["ENG"])
    };

    // A test made of several sections: the course name tells which one is its own (Biyoloji (AYT) → Biyoloji, 13 questions).
    private static readonly IReadOnlyDictionary<string, string[]> SectionWords = new Dictionary<string, string[]>
    {
        ["FIZ"] = ["fizik"], ["KIM"] = ["kimya"], ["BIO"] = ["biyoloji"], ["EDB"] = ["edebiyat"], ["TAR1"] = ["tarih"], ["TAR2"] = ["tarih"],
        ["COG1"] = ["coğrafya"], ["COG2"] = ["coğrafya"], ["FEL"] = ["felsefe", "psikoloji", "sosyoloji", "mantık"], ["DIN"] = ["din"]
    };

    /// <summary>The question count of each course that has one, rounded to tenths.</summary>
    public static IReadOnlyDictionary<Guid, decimal> For(LearningPurpose purpose, IReadOnlyList<Lesson> lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        var parts = lessons.ToDictionary(x => x.Id, x => purpose == LearningPurpose.Yks ? YksParts(x) : OtherParts(x));
        var sharing = parts.Values.SelectMany(x => x.Select(p => p.Key).Distinct()).GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
        return parts.Where(x => x.Value.Count > 0)
            .ToDictionary(x => x.Key, x => Math.Round(x.Value.Sum(p => p.Questions / sharing[p.Key]), 1, MidpointRounding.AwayFromZero));
    }

    private static List<(string Key, decimal Questions)> YksParts(Lesson lesson)
    {
        var parts = new List<(string Key, decimal Questions)>();
        foreach (var code in lesson.YksTestCodes.Select(x => x.Trim().ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal))
        {
            if (!YksTests.TryGetValue(code, out var test)) continue;
            var sections = Profile(test.Exam).Sections.Where(x => test.Sections.Contains(x.Code)).ToArray();
            var own = sections.Length == 1 ? sections : sections.Where(x => SectionWords.TryGetValue(x.Code, out var words)
                && words.Any(word => Fold(lesson.Name).Contains(word, StringComparison.Ordinal))).ToArray();
            // A course of a mixed test whose name matches no section counts as the test's average section.
            if (own.Length == 0) parts.Add((code + "*", sections.Sum(x => x.QuestionCount) / (decimal)sections.Length));
            else parts.AddRange(own.Select(x => ($"{(test.Exam == "TYT" ? "TYT" : "AYT")}:{x.Code}", (decimal)x.QuestionCount)));
        }
        return parts;
    }

    private static List<(string Key, decimal Questions)> OtherParts(Lesson lesson)
    {
        var exam = lesson.ExamCode?.Trim().ToUpperInvariant() switch
        {
            "LGS" => "LGS", "AGS" => "AGS", "YDS" => "YDS_EN", "YOKDIL" => "YOKDIL_FEN",
            { } code when code.StartsWith("OABT_", StringComparison.Ordinal) => code,
            _ => null
        };
        var profile = exam is null ? null : PracticeExamRules.Profiles.SingleOrDefault(x => x.Code == exam);
        if (profile is null) return [];
        var names = Names(lesson.Name);
        var section = profile.Sections.Count == 1 ? profile.Sections[0] : profile.Sections.FirstOrDefault(x =>
            names.Any(name => Matches(name, Fold(x.Name)) || Aliases.TryGetValue(x.Code, out var alias) && alias.Any(a => Matches(name, a))));
        return section is null ? [] : [($"{profile.Code}:{section.Code}", section.QuestionCount)];
    }

    private static PracticeExamProfile Profile(string code) => PracticeExamRules.Profiles.Single(x => x.Code == code);

    // Names the catalog uses for a section in other words (LGS "Yabancı Dil" is the English test).
    private static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>
    {
        ["ENG"] = ["yabancı dil", "ingilizce"], ["INK"] = ["inkılap tarihi"], ["DIN"] = ["din kültürü"]
    };

    private static bool Matches(string name, string section) =>
        name.Length > 0 && (name.Contains(section, StringComparison.Ordinal) || section.Contains(name, StringComparison.Ordinal));

    // "Yabancı Dil (İngilizce)" → "yabancı dil" and "ingilizce": the text outside and inside the brackets, each folded. A short
    // bracket ("(AGS)", "(TYT)") is the exam, not a section name.
    private static string[] Names(string value)
    {
        var open = value.IndexOf('('); var close = value.IndexOf(')', Math.Max(open, 0));
        if (open < 0) return [Fold(value)];
        var inner = close > open ? Fold(value[(open + 1)..close]) : "";
        return new[] { Fold(value[..open]), inner.Length > 4 ? inner : "" }.Where(x => x.Length > 0).ToArray();
    }

    // Lower case in Turkish, the circumflex folded (Ahlâk → ahlak) and the spaces collapsed.
    private static string Fold(string value) => string.Join(' ', value.ToLower(Turkish).Replace('â', 'a').Replace('î', 'i').Replace('û', 'u')
        .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
