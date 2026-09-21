using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using SpeedReading.Application.Privacy;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingErasureAssessmentServiceTests
{
    [Fact]
    public async Task Assess_ShouldBlockErasureWhenFinancialRecordsRequireRetention()
    {
        var repository = new StubInventoryRepository(new Dictionary<string, int>
        {
            ["profiles"] = 1,
            ["exerciseSessions"] = 5,
            ["financialRecords"] = 2
        });
        var service = new SpeedReadingErasureAssessmentService(
            repository,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 18, 0, 0, TimeSpan.Zero)));
        var message = new PersonalDataErasureAssessmentRequestedV1(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            DryRun: true, Scope: PersonalDataScope.SpeedReading);

        var result = await service.AssessAsync(message, CancellationToken.None);

        result.HasActiveLegalHold.Should().BeTrue();
        result.CanProceed.Should().BeFalse();
        result.RecordCounts.Should().Contain("exerciseSessions", 5);
    }

    [Fact]
    public async Task Assess_ShouldRejectNonDryRunRequests()
    {
        var service = new SpeedReadingErasureAssessmentService(
            new StubInventoryRepository([]), TimeProvider.System);
        var message = new PersonalDataErasureAssessmentRequestedV1(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            DryRun: false, Scope: PersonalDataScope.SpeedReading);

        var action = () => service.AssessAsync(message, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubInventoryRepository(IReadOnlyDictionary<string, int> counts)
        : ISpeedReadingPrivacyInventoryRepository
    {
        public Task<IReadOnlyDictionary<string, int>> CountByUserAsync(
            Guid userId,
            CancellationToken cancellationToken) => Task.FromResult(counts);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
