using Coaching.Infrastructure.Data;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Privacy;

internal static class CoachingStudyPlanErasureMessages
{
    public static IQueryable<OutboxMessage> ForStudent(CoachingDbContext context, Guid studentId)
    {
        const string messageType = "%urn:message:EduPlatform.Shared.Contracts.Events.Coaching:StudyPlanPublishedEvent%";
        var subject = studentId.ToString();
        return context.Set<OutboxMessage>().FromSqlInterpolated($"""
            SELECT * FROM coaching."OutboxMessage"
            WHERE "MessageType" LIKE {messageType}
              AND "Body"::jsonb -> 'message' ->> 'studentId' = {subject}
            """);
    }
}
