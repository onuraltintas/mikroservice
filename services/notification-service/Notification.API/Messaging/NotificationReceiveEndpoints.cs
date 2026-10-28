using MassTransit;
using Notification.Application.Consumers;
using Notification.API.Services;
using Notification.Application.Privacy;
using Notification.Infrastructure.Persistence;

namespace Notification.API.Messaging;

/// <summary>
/// The single list of consumers and their RabbitMQ queues. Registration and endpoint
/// configuration are both driven from it, so a consumer can no longer be registered
/// without a queue (which silently dropped five coaching update notifications).
/// Queue names are part of the production contract: renaming one orphans its queue.
/// </summary>
public static class NotificationReceiveEndpoints
{
    public sealed record Endpoint(string Queue, Type Consumer, bool CourseManagement = false);

    public static IReadOnlyList<Endpoint> All { get; } =
    [
        new("invitation-created", typeof(InvitationCreatedConsumer)),
        new("user-registered", typeof(UserRegisteredConsumer)),
        new("user-email-confirmed", typeof(UserEmailConfirmedConsumer)),
        new("user-created", typeof(UserCreatedConsumer)),
        new("user-password-setup-requested", typeof(UserPasswordSetupRequestedConsumer)),
        new("send-notification", typeof(SendNotificationConsumer)),
        new("user-forgot-password", typeof(UserForgotPasswordConsumer)),
        new("coaching-assignment-created", typeof(AssignmentCreatedConsumer)),
        new("coaching-assignment-updated", typeof(AssignmentUpdatedConsumer)),
        new("coaching-assignment-submitted", typeof(AssignmentSubmittedConsumer)),
        new("coaching-assignment-graded", typeof(AssignmentGradedConsumer)),
        new("coaching-exam-updated", typeof(ExamUpdatedConsumer)),
        new("coaching-exam-result-added", typeof(ExamResultAddedConsumer)),
        new("coaching-exam-result-updated", typeof(ExamResultUpdatedConsumer)),
        new("coaching-session-scheduled", typeof(SessionScheduledConsumer)),
        new("coaching-session-updated", typeof(SessionUpdatedConsumer)),
        new("coaching-session-cancelled", typeof(SessionCancelledConsumer)),
        new("coaching-goal-created", typeof(GoalCreatedConsumer)),
        new("coaching-goal-updated", typeof(GoalUpdatedConsumer)),
        new("coaching-study-plan-published", typeof(StudyPlanPublishedConsumer)),
        new("coaching-study-plan-updated", typeof(StudyPlanUpdatedConsumer)),
        new("coaching-weekly-reflection-saved", typeof(WeeklyReflectionSavedConsumer)),
        new("coaching-weekly-decision-changed", typeof(WeeklyDecisionChangedConsumer)),
        new("coaching-reminder-due", typeof(CoachingReminderDueConsumer)),
        new("course-management-lesson-scheduled", typeof(CourseManagementLessonScheduledConsumer), CourseManagement: true),
        new("course-management-lesson-rescheduled", typeof(CourseManagementLessonRescheduledConsumer), CourseManagement: true),
        new("course-management-lesson-cancelled", typeof(CourseManagementLessonCancelledConsumer), CourseManagement: true),
        new("course-management-assignment-published", typeof(CourseManagementAssignmentPublishedConsumer), CourseManagement: true),
        new("course-management-assignment-submitted", typeof(CourseManagementAssignmentSubmittedConsumer), CourseManagement: true),
        new("course-management-assignment-reviewed", typeof(CourseManagementAssignmentReviewedConsumer), CourseManagement: true),
        new("course-management-payment-recorded", typeof(CourseManagementPaymentRecordedConsumer), CourseManagement: true),
        new("course-management-reminder-due", typeof(CourseManagementReminderDueConsumer), CourseManagement: true),
        new("notification-privacy-erasure-assessment", typeof(NotificationErasureAssessmentRequestedConsumer)),
        new("notification-privacy-erasure-execution", typeof(NotificationErasureExecutionRequestedConsumer)),
        new("notification-realtime", typeof(NotificationRealtimeConsumer)),
    ];

    /// <summary>The endpoints this instance runs; Course Management ones only when enabled.</summary>
    public static IEnumerable<Endpoint> Active(bool courseManagementEnabled) =>
        All.Where(endpoint => courseManagementEnabled || !endpoint.CourseManagement);

    public static void AddNotificationConsumers(this IBusRegistrationConfigurator configurator, bool courseManagementEnabled)
    {
        foreach (var endpoint in Active(courseManagementEnabled))
        {
            configurator.AddConsumer(endpoint.Consumer);
        }
    }

    /// <summary>One queue per consumer, each with retry and the EF inbox/outbox.</summary>
    public static void ConfigureNotificationEndpoints(
        this IBusFactoryConfigurator bus,
        IBusRegistrationContext context,
        bool courseManagementEnabled)
    {
        foreach (var endpoint in Active(courseManagementEnabled))
        {
            bus.ReceiveEndpoint(endpoint.Queue, e => ConfigureEndpoint(e, context, endpoint.Consumer));
        }
    }

    public static void ConfigureEndpoint(
        IReceiveEndpointConfigurator endpoint,
        IBusRegistrationContext context,
        Type consumer)
    {
        endpoint.UseMessageRetry(retry =>
            retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
        endpoint.UseEntityFrameworkOutbox<NotificationDbContext>(context);
        endpoint.ConfigureConsumer(context, consumer);
    }
}
