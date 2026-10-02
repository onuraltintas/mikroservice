using Coaching.Application.StudyPlanning;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingTopicSearchService(CoachingDbContext db) : IStudyTopicSearchService
{
    public async Task<TargetSearchPage<StudyTopicView>> SearchAsync(string? search, int? gradeNumber, string? examCode,
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        search = search?.Trim();
        examCode = string.IsNullOrWhiteSpace(examCode) ? null : examCode.Trim().ToUpperInvariant();
        if (pageNumber is < 1 or > 10000 || pageSize is < 1 or > 50 || search?.Length > 200
            || gradeNumber is < 1 or > 12 || examCode is not (null or "LGS" or "TYT" or "AYT" or "YDT" or "TDP"))
            throw new ArgumentException("Arama filtrelerini kontrol edin.");
        var query = from topic in db.StudyCatalogTopics.AsNoTracking()
                    join unit in db.StudyCatalogUnits on topic.UnitId equals unit.Id
                    join lesson in db.StudyCatalogLessons on topic.LessonId equals lesson.Id
                    where topic.IsActive && unit.IsActive && lesson.IsActive && unit.LessonId == lesson.Id
                        && (topic.ParentId == null || db.StudyCatalogTopics.Any(parent => parent.Id == topic.ParentId
                            && parent.IsActive && parent.UnitId == topic.UnitId && parent.LessonId == topic.LessonId))
                    select new { topic.Id, topic.Name, LessonName = lesson.Name, UnitName = unit.Name,
                        lesson.GradeNumber, lesson.ExamCode, topic.EstimatedMinutes };
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\")
                || EF.Functions.ILike(x.LessonName, pattern, "\\") || EF.Functions.ILike(x.UnitName, pattern, "\\"));
        }
        if (gradeNumber.HasValue) query = query.Where(x => x.GradeNumber == gradeNumber);
        if (examCode is not null) query = query.Where(x => x.ExamCode == examCode);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.LessonName).ThenBy(x => x.UnitName).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new StudyTopicView(x.Id, x.Name, x.LessonName, x.UnitName, x.GradeNumber, x.ExamCode, x.EstimatedMinutes))
            .ToListAsync(cancellationToken);
        return new(items, count, pageNumber, pageSize);
    }
}
