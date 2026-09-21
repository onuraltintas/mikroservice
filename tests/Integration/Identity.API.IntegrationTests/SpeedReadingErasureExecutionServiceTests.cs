using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Privacy;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Privacy;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingErasureExecutionServiceTests
{
    [Fact]
    public async Task Execute_ShouldDeleteOnlySubjectOwnedDataAndRemainIdempotent()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        context.UserProfiles.AddRange(
            SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), subjectId, DateTime.UtcNow),
            SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), otherUserId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var request = Request(subjectId);
        var service = new SpeedReadingErasureExecutionService(
            context, new Inventory(financialRecords: 0), TimeProvider.System);

        var first = await service.ExecuteAsync(request, CancellationToken.None);
        var replay = await service.ExecuteAsync(request, CancellationToken.None);

        first.DeletedRecordCount.Should().Be(1);
        replay.Id.Should().Be(first.Id);
        (await context.UserProfiles.CountAsync(profile => profile.UserId == subjectId)).Should().Be(0);
        (await context.UserProfiles.CountAsync(profile => profile.UserId == otherUserId)).Should().Be(1);
        (await context.Set<SpeedReadingErasureExecution>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Execute_ShouldRecheckFinancialRetentionBeforeDeleting()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        context.UserProfiles.Add(
            SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), subjectId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var service = new SpeedReadingErasureExecutionService(
            context, new Inventory(financialRecords: 1), TimeProvider.System);

        var action = () => service.ExecuteAsync(Request(subjectId), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*financial retention*");
        (await context.UserProfiles.CountAsync(profile => profile.UserId == subjectId)).Should().Be(1);
    }

    private static PersonalDataErasureExecutionRequestedV1 Request(Guid subjectId)
    {
        var requestId = Guid.NewGuid();
        return new PersonalDataErasureExecutionRequestedV1(
            requestId, requestId, subjectId, DateTime.UtcNow, PersonalDataScope.SpeedReading);
    }

    private static OwnedSpeedReadingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OwnedSpeedReadingDbContext(options);
    }

    private sealed class Inventory(int financialRecords) : ISpeedReadingPrivacyInventoryRepository
    {
        public Task<IReadOnlyDictionary<string, int>> CountByUserAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>
            {
                ["financialRecords"] = financialRecords
            });
    }
}
