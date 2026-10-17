using Coaching.Infrastructure.Data;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Coaching.Infrastructure.Privacy;

internal static class CoachingStudyPlanErasureMessages
{
    public static IQueryable<OutboxMessage> ForStudent(CoachingDbContext context, Guid studentId)
    {
        if (!context.Database.IsRelational())
            return context.Set<OutboxMessage>().Where(x => (x.MessageType.Contains("urn:message:EduPlatform.Shared.Contracts.Events.Coaching:StudyPlanPublishedEvent")
                || x.MessageType.Contains("urn:message:EduPlatform.Shared.Contracts.Events.Coaching:StudyPlanUpdatedEvent")
                || x.MessageType.Contains("urn:message:EduPlatform.Shared.Contracts.Events.Coaching:CoachingReminderDueEvent"))
                && HasStudent(x.Body, studentId));
        const string messageType = "%urn:message:EduPlatform.Shared.Contracts.Events.Coaching:StudyPlanPublishedEvent%";
        const string reminderType = "%urn:message:EduPlatform.Shared.Contracts.Events.Coaching:CoachingReminderDueEvent%";
        const string updatedType = "%urn:message:EduPlatform.Shared.Contracts.Events.Coaching:StudyPlanUpdatedEvent%";
        var subject = studentId.ToString();
        return context.Set<OutboxMessage>().FromSqlInterpolated($"""
            SELECT * FROM coaching."OutboxMessage"
            WHERE ("MessageType" LIKE {messageType} OR "MessageType" LIKE {reminderType} OR "MessageType" LIKE {updatedType})
              AND COALESCE("Body"::jsonb -> 'message' ->> 'studentId', "Body"::jsonb -> 'message' ->> 'recipientUserId') = {subject}
            """);
    }

    private static bool HasStudent(string body, Guid studentId)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.TryGetProperty("message", out var message)
            && (message.TryGetProperty("studentId", out var student) || message.TryGetProperty("recipientUserId", out student))
            && student.TryGetGuid(out var id) && id == studentId;
    }
}
