using System.Text.Json;
using Coaching.Infrastructure.Catalogs;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class YksPreparedDataTests
{
    internal static YksDataPackage Read()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"data/coaching-outcomes/yks-2026-basis-v1.json")))root=root.Parent;
        Assert.NotNull(root);
        return JsonSerializer.Deserialize<YksDataPackage>(File.ReadAllText(Path.Combine(root!.FullName,"data/coaching-outcomes/yks-2026-basis-v1.json")),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    [Fact]
    public void EveryExamTopicIsCoveredExactlyOnceOrDeclaredEquivalent_AndArabicIsExcluded()
    {
        var p=Read();CoachingYksCatalogImportService.Validate(p);
        Assert.Equal(1122,p.Topics.Count+p.EquivalentTopics.Count);
        Assert.Equal(487,p.Topics.Count(t=>t.Exam=="TYT"));Assert.Equal(75,p.Topics.Count(t=>t.Exam=="YDT"));
        Assert.All(p.Topics.Where(t=>t.Exam=="YDT").SelectMany(t=>t.Scopes),s=>Assert.Equal("English",s.Language));
        Assert.DoesNotContain(p.Subjects,s=>s.Subject.Contains("Arap",StringComparison.OrdinalIgnoreCase));
        Assert.All(p.EquivalentTopics,e=>Assert.Contains(p.Topics,t=>t.SourceId==e.CanonicalTopicSourceId));
        Assert.All(p.Outcomes.Where(o=>o.SourceKind=="Official"),o=>Assert.True(o.SourcePage>0&&o.SourceUrl.Contains("ttkb.meb.gov.tr")));
        Assert.All(p.Outcomes.Where(o=>o.SourceKind=="ExamSkill"),o=>Assert.True(o.SourcePage is null&&o.Code.StartsWith("PREP.")));
    }

    [Fact]
    public void PublishedScopeBudgetsConserveEveryMinuteAndDoNotUseVideoDuration()
    {
        var p=Read();var topics=p.Topics.ToDictionary(t=>t.SourceId);
        foreach(var o in p.Outcomes)
        {
            Assert.Equal(o.LearningMinutes,o.Shares.Sum(s=>s.LearningMinutes));
            Assert.Equal(o.PracticeMinutes,o.Shares.Sum(s=>s.PracticeMinutes));
            Assert.Equal(o.CheckMinutes,o.Shares.Sum(s=>s.CheckMinutes));
            Assert.All(o.Shares,s=>{Assert.Contains(o.Key,topics[s.TopicSourceId].OutcomeKeys);Assert.InRange(s.LearningMinutes+s.PracticeMinutes+s.CheckMinutes,1,1440);});
            Assert.Contains("video süresi",o.DurationEvidence);Assert.NotEmpty(o.DurationEvidence);
        }
        Assert.Equal(68058,p.Outcomes.Sum(o=>o.LearningMinutes+o.PracticeMinutes+o.CheckMinutes));
    }

    [Fact]
    public void EveryOfficialGeographyOneExclusionIsRemovedForEaAndRetainedForSoz()
    {
        var p=Read();var red=new[]{"11.1.2","11.1.4","11.2.2","11.2.8","11.2.9","11.2.12","11.2.18","11.3.5","11.3.7","11.4.2","11.4.5","12.1.2","12.2.1","12.2.3","12.2.8","12.2.17","12.3.4","12.3.6","12.4.1","12.4.4"};
        foreach(var code in red)
        {
            var outcome=Assert.Single(p.Outcomes.Where(o=>o.Owner=="GEO"&&o.Code==code&&o.SourceKind=="Official"));
            var topics=p.Topics.Where(t=>t.OutcomeKeys.Contains(outcome.Key)).ToArray();Assert.NotEmpty(topics);
            foreach(var t in topics)
            {
                Assert.All(t.Scopes.Where(s=>s.ScoreType=="EA"),s=>Assert.Contains(outcome.Key,s.ExcludedOutcomeKeys));
                Assert.Contains(t.Scopes,s=>s.ScoreType=="SOZ"&&!s.ExcludedOutcomeKeys.Contains(outcome.Key));
            }
        }
        var biyom=Assert.Single(p.Outcomes.Where(o=>o.Owner=="GEO"&&o.Name=="Biyomlar"));
        Assert.Contains(p.Topics,t=>t.OutcomeKeys.Contains(biyom.Key)&&t.Scopes.Any(s=>s.ScoreType=="EA"&&!s.ExcludedOutcomeKeys.Contains(biyom.Key)));
    }
}
