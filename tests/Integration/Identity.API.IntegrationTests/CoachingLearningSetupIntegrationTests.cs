using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Repositories;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingLearningSetupIntegrationTests
{
    [Fact]
    public async Task PersonalizedPreviewRequiresCompleteAndCurrentContext_WhileLegacyReadIsUnaffected()
    {
        await using var f = await CoachingLearningSetupTests.Fixture.Create();
        var service = new CoachingAutomaticStudyPlanPreviewService(f.Db, new CoachingAccessPolicy(f.Actor), f.Service);
        var request = new AutomaticStudyPreviewRequest(new(2026, 10, 19), 7, 0, [new(Guid.NewGuid(), 30)], 0);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.PreviewAsync(request));
        Assert.Equal("StudyPlanning.Conflict", error.Code);
        await f.Service.SaveAsync(new(null, 2026, LearningPurpose.Lgs, 2027, null, null, [], [], false), default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.PreviewAsync(request));
        Assert.Empty(f.Db.StudyPlanRevisions);
    }

    [Fact]
    public async Task PersonalExportIncludesOnlyOwnPersistedSetup()
    {
        await using var f = await CoachingLearningSetupTests.Fixture.Create();
        await f.Service.SaveAsync(new(null, 2026, LearningPurpose.Yks, 2027, "EA", null, [], [], false), default);
        var other = StudentLearningContext.Create(Guid.NewGuid(), 2026, LearningPurpose.Lgs, 2027, null, null);
        f.Db.Add(other); await f.Db.SaveChangesAsync();
        var export = await new CoachingDataExportRepository(f.Db).ExportStudentDataAsync(f.Student, DateTimeOffset.UtcNow, default);
        Assert.Equal(f.Student, export.LearningContext!.StudentUserId);
        Assert.Equal("EA", export.LearningContext.PrimaryScoreType);
        Assert.NotEqual(other.StudentUserId, export.LearningContext.StudentUserId);
    }

    [Theory]
    [InlineData(LearningPurpose.School, 2027, null, null)]
    [InlineData(LearningPurpose.Lgs, 2027, "SAY", null)]
    [InlineData(LearningPurpose.Yks, 2032, "SAY", null)]
    [InlineData(LearningPurpose.Yks, 2027, "SAY", "English")]
    [InlineData(LearningPurpose.Yks, 2027, "DIL", "Unknown")]
    [InlineData(LearningPurpose.Undecided, null, "TYT", null)]
    public void ContextRejectsInconsistentExamInformation(LearningPurpose purpose, int? year, string? score, string? language)
        => Assert.Throws<ArgumentException>(() => StudentLearningContext.Create(Guid.NewGuid(), 2026, purpose, year, score, language));

    [Theory]
    [InlineData(0, null)] [InlineData(6, null)] [InlineData(1, 0)] [InlineData(1, 6)]
    public void CoursePriorityAndDifficultyAreValidated(int priority, int? difficulty)
        => Assert.Throws<ArgumentException>(() => StudentCoursePreference.Create(Guid.NewGuid(), Guid.NewGuid(), priority, difficulty));
}
