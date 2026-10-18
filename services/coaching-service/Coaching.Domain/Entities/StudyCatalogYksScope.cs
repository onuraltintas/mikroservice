namespace Coaching.Domain.Entities;

public sealed class StudyCatalogYksScope
{
    private StudyCatalogYksScope() { }
    public Guid TopicId { get; private set; }
    public string ExamCode { get; private set; } = "";
    public string ScoreType { get; private set; } = "";
    public string TestCode { get; private set; } = "";
    public string? ForeignLanguage { get; private set; }
    public string Evidence { get; private set; } = "";
    public Guid[] ExcludedOutcomeIds { get; private set; } = [];

    public void ExcludeOutcomes(IEnumerable<Guid> outcomes)
    {
        var ids=outcomes.Distinct().Order().ToArray();
        if(ids.Any(x=>x==Guid.Empty))throw new ArgumentException("Geçerli kapsam dışı kazanım kimlikleri belirtin.");
        ExcludedOutcomeIds=ids;
    }

    public static StudyCatalogYksScope Create(Guid topic,string exam,string score,string test,string? language,string evidence)
    {
        if(topic==Guid.Empty || exam is not ("TYT" or "AYT" or "YDT")
            || score is not ("TYT" or "SAY" or "EA" or "SOZ" or "DIL")
            || exam=="AYT" && score is not ("SAY" or "EA" or "SOZ")
            || exam=="YDT" && (score!="DIL" || language!="English")
            || exam!="YDT" && language is not null || !test.StartsWith(exam+".",StringComparison.Ordinal))
            throw new ArgumentException("Geçerli YKS oturumu, puan türü, test ve dil kapsamı belirtin.");
        return new(){TopicId=topic,ExamCode=exam,ScoreType=score,TestCode=StudyCatalogText.Require(test,40),
            ForeignLanguage=language,Evidence=StudyCatalogText.Require(evidence,2000)};
    }
}

public sealed class OfficialCurriculumLessonBinding
{
    private OfficialCurriculumLessonBinding() { }
    public Guid EditionId { get; private set; }
    public Guid LessonId { get; private set; }
    public static OfficialCurriculumLessonBinding Create(Guid edition,Guid lesson)
    {
        if(edition==Guid.Empty || lesson==Guid.Empty)throw new ArgumentException("Program ve ders gereklidir.");
        return new(){EditionId=edition,LessonId=lesson};
    }
}
