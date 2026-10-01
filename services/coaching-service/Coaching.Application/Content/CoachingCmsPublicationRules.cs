namespace Coaching.Application.Content;

public static class CoachingCmsPublicationRules
{
    public static bool IsPubliclyAvailable(
        bool isPublished,
        DateTime? scheduledPublishAt,
        DateTime nowUtc) =>
        isPublished
        && (!scheduledPublishAt.HasValue || scheduledPublishAt.Value <= nowUtc);
}
