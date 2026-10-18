using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;

namespace Coaching.Infrastructure.StudyPlanning;

public static class YksCatalogEligibility
{
    public static IQueryable<StudyCatalogTopic> Topics(CoachingDbContext db,StudentLearningContext? context)
    {
        var topics=db.StudyCatalogTopics.AsQueryable();
        if(context?.Purpose!=LearningPurpose.Yks)return topics;
        return topics.Where(t=>db.Set<StudyCatalogYksScope>().Any(s=>s.TopicId==t.Id&&s.ScoreType==context.PrimaryScoreType
            &&(s.ExamCode!="YDT" || s.ForeignLanguage==context.ForeignLanguage)));
    }
}
