using SpeedReading.Application.Subscription;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingSubscriptionAccessRulesTests
{
    [Theory]
    [InlineData("/api/speed-reading/daily-progress/today-exercises", "GET")]
    [InlineData("/api/speed-reading/daily-progress/complete-exercise", "POST")]
    [InlineData("/api/speed-reading/learning-paths/personalized", "GET")]
    [InlineData("/api/speed-reading/exercise-sessions/start", "POST")]
    [InlineData("/api/speed-reading/student-reading/available", "GET")]
    [InlineData("/api/speed-reading/reports/student", "GET")]
    [InlineData("/api/speed-reading/adaptive-learning/dashboard", "GET")]
    [InlineData("/api/speed-reading/adaptive-learning/profile/extra", "PUT")]
    public void PaidStudentEndpointsRequireSubscription(string path, string method)
    {
        Assert.True(SpeedReadingSubscriptionAccessRules.RequiresSubscription(path, method));
    }

    [Theory]
    [InlineData("/api/speed-reading/assessment/status", "GET")]
    [InlineData("/api/speed-reading/assessment/attempts", "POST")]
    [InlineData("/api/speed-reading/adaptive-learning/profile", "GET")]
    [InlineData("/api/speed-reading/adaptive-learning/profile", "PUT")]
    [InlineData("/api/speed-reading/adaptive-learning/profile/status", "GET")]
    [InlineData("/api/speed-reading/subscriptions/my-modules", "GET")]
    [InlineData("/api/speed-reading/payment/verify", "POST")]
    [InlineData("/api/speed-reading/reading-texts/00000000-0000-0000-0000-000000000001", "GET")]
    [InlineData("/api/speed-reading/exercises/00000000-0000-0000-0000-000000000001", "GET")]
    [InlineData("/api/speed-reading/reading-questions", "GET")]
    [InlineData("/api/speed-reading/age-group-configurations", "GET")]
    [InlineData("/health", "GET")]
    public void AssessmentAndPurchasePrerequisitesRemainAvailable(string path, string method)
    {
        Assert.False(SpeedReadingSubscriptionAccessRules.RequiresSubscription(path, method));
    }

    [Fact]
    public void SimilarPrefixCannotBypassPaidAccess()
    {
        Assert.True(SpeedReadingSubscriptionAccessRules.RequiresSubscription(
            "/api/speed-reading/assessment-extra/paid", "GET"));
    }

    [Fact]
    public void ProfileStatusWriteStillRequiresSubscription()
    {
        Assert.True(SpeedReadingSubscriptionAccessRules.RequiresSubscription(
            "/api/speed-reading/adaptive-learning/profile/status", "PUT"));
    }
}
