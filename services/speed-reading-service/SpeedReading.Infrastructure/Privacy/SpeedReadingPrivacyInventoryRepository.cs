using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Privacy;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Privacy;

public sealed class SpeedReadingPrivacyInventoryRepository(OwnedSpeedReadingDbContext context)
    : ISpeedReadingPrivacyInventoryRepository
{
    public async Task<IReadOnlyDictionary<string, int>> CountByUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var legacy = (ISpeedReadingDataContext)context;
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["profiles"] = await context.UserProfiles.CountAsync(item => item.UserId == userId, cancellationToken),
            ["exerciseSessions"] = await context.ExerciseSessions.CountAsync(item => item.StudentId == userId, cancellationToken),
            ["readingSessions"] = await context.ReadingSessions.CountAsync(item => item.UserId == userId, cancellationToken),
            ["readingAttempts"] = await context.StudentReadingAttempts.CountAsync(item => item.UserId == userId, cancellationToken),
            ["assessmentAttempts"] = await context.AssessmentAttempts.CountAsync(item => item.StudentId == userId, cancellationToken),
            ["programProgress"] = await context.StudentProgramProgresses.CountAsync(item => item.UserId == userId, cancellationToken),
            ["learningProgress"] =
                await context.StudentLearningPathProgresses.CountAsync(item => item.StudentId == userId, cancellationToken)
                + await context.StudentLearningNodeProgresses.CountAsync(item => item.StudentId == userId, cancellationToken),
            ["gamification"] =
                await context.UserGamifications.CountAsync(item => item.UserId == userId, cancellationToken)
                + await context.UserAchievements.CountAsync(item => item.UserId == userId, cancellationToken),
            ["vocabularyProgress"] = await context.UserVocabularyProgresses.CountAsync(item => item.UserId == userId, cancellationToken),
            ["subscriptions"] = await legacy.UserSubscriptions.CountAsync(item => item.UserId == userId, cancellationToken),
            ["financialRecords"] =
                await legacy.Payments.CountAsync(item => item.UserId == userId, cancellationToken)
                + await legacy.BankTransferPaymentRequests.CountAsync(item => item.UserId == userId, cancellationToken)
        };

        return counts;
    }
}
