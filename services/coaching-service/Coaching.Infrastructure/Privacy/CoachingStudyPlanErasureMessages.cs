using Coaching.Infrastructure.Data;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Coaching.Infrastructure.Privacy;

internal static class CoachingStudyPlanErasureMessages
{
    public static IQueryable<Coaching.Domain.Entities.IdempotencyRecord> DecisionWorkRequests(CoachingDbContext context, Guid subject)
        => context.Set<Coaching.Domain.Entities.IdempotencyRecord>().Where(record =>
            (record.Scope == "coaching.weekly-decision.study" || record.Scope == "coaching.weekly-decision.assignment")
            && context.Set<Coaching.Domain.Entities.WeeklyDecision>().Any(decision => (decision.StudentId == subject || decision.TeacherId == subject || decision.ResponsibleTeacherId == subject)
                && record.Key.StartsWith(decision.Id.ToString() + ":")));
    public static IQueryable<OutboxMessage> ForStudent(CoachingDbContext context, Guid studentId)
    {
        if (!context.Database.IsRelational())
            return context.Set<OutboxMessage>().Where(x => (x.MessageType.Contains("urn:message:EduPlatform.Shared.Contracts.Events.Coaching:StudyPlanPublishedEvent")
                || x.MessageType.Contains("urn:message:EduPlatform.Shared.Contracts.Events.Coaching:StudyPlanUpdatedEvent")
                || x.MessageType.Contains("urn:message:EduPlatform.Shared.Contracts.Events.Coaching:CoachingReminderDueEvent")
                || x.MessageType.Contains("urn:message:EduPlatform.Shared.Contracts.Events.Coaching:WeeklyReflectionSavedEvent")
                || x.MessageType.Contains("urn:message:EduPlatform.Shared.Contracts.Events.Coaching:WeeklyDecisionChangedEvent"))
                && HasStudent(x.Body, studentId));
        const string messageType = "%urn:message:EduPlatform.Shared.Contracts.Events.Coaching:StudyPlanPublishedEvent%";
        const string reminderType = "%urn:message:EduPlatform.Shared.Contracts.Events.Coaching:CoachingReminderDueEvent%";
        const string updatedType = "%urn:message:EduPlatform.Shared.Contracts.Events.Coaching:StudyPlanUpdatedEvent%";
        const string reflectionType = "%urn:message:EduPlatform.Shared.Contracts.Events.Coaching:WeeklyReflectionSavedEvent%";
        const string decisionType = "%urn:message:EduPlatform.Shared.Contracts.Events.Coaching:WeeklyDecisionChangedEvent%";
        var subject = studentId.ToString();
        return context.Set<OutboxMessage>().FromSqlInterpolated($"""
            SELECT * FROM coaching."OutboxMessage"
            WHERE ("MessageType" LIKE {messageType} OR "MessageType" LIKE {reminderType} OR "MessageType" LIKE {updatedType}
                OR "MessageType" LIKE {reflectionType} OR "MessageType" LIKE {decisionType})
              AND (COALESCE("Body"::jsonb -> 'message' ->> 'studentId', "Body"::jsonb -> 'message' ->> 'recipientUserId') = {subject}
                OR "Body"::jsonb -> 'message' ->> 'teacherId' = {subject}
                OR COALESCE("Body"::jsonb -> 'message' -> 'teacherIds','[]'::jsonb) @> jsonb_build_array({subject}))
            """);
    }

    private static bool HasStudent(string body, Guid studentId)
    {
        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("message", out var message)) return false;
        return new[] { "studentId", "recipientUserId", "teacherId" }.Any(key => message.TryGetProperty(key, out var user) && user.TryGetGuid(out var id) && id == studentId)
            || message.TryGetProperty("teacherIds", out var teachers) && teachers.EnumerateArray().Any(x => x.TryGetGuid(out var id) && id == studentId);
    }
}
