using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.ExportCoachingData;
using Coaching.API.Controllers;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingDataExportQueryTests
{
    [Fact]
    public void ExportEndpoint_ShouldRequireStudentRole()
    {
        typeof(DataPrivacyController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .Contain(attribute => attribute.Roles == "Student");
    }

    [Fact]
    public async Task Export_ShouldReturnOnlyTheAuthenticatedStudentsData()
    {
        var studentId = Guid.NewGuid();
        var expected = CoachingDataExportDto.Empty(studentId, DateTimeOffset.Parse("2026-09-21T12:00:00Z"));
        var handler = new ExportCoachingDataQueryHandler(
            new StubRepository(expected),
            new StubAccessPolicy(studentId, isStudent: true),
            new StubIdentityAuthorizationClient([studentId]),
            new FixedTimeProvider(expected.ExportedAt));

        var result = await handler.Handle(new ExportCoachingDataQuery(), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Export_ShouldRejectNonStudentAccounts()
    {
        var handler = new ExportCoachingDataQueryHandler(
            new StubRepository(null),
            new StubAccessPolicy(Guid.NewGuid(), isStudent: false),
            new StubIdentityAuthorizationClient([]),
            TimeProvider.System);

        var action = () => handler.Handle(new ExportCoachingDataQuery(), CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
    }

    [Fact]
    public async Task ExportRepository_ShouldExcludeAnotherStudentsGoals()
    {
        await using var context = new CoachingDbContext(
            new DbContextOptionsBuilder<CoachingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        context.AcademicGoals.AddRange(
            AcademicGoal.Create(studentId, "My goal", GoalCategory.SubjectMastery),
            AcademicGoal.Create(otherStudentId, "Other student's goal", GoalCategory.SubjectMastery));
        await context.SaveChangesAsync();

        var export = await new CoachingDataExportRepository(context)
            .ExportStudentDataAsync(studentId, DateTimeOffset.UtcNow, CancellationToken.None);

        export.StudentId.Should().Be(studentId);
        export.Goals.Should().ContainSingle(goal => goal.Title == "My goal");
        export.Goals.Should().NotContain(goal => goal.Title == "Other student's goal");
    }

    private sealed class StubRepository(CoachingDataExportDto? result) : ICoachingDataExportRepository
    {
        public Task<CoachingDataExportDto> ExportStudentDataAsync(
            Guid studentId,
            DateTimeOffset exportedAt,
            CancellationToken cancellationToken) =>
            Task.FromResult(result ?? throw new InvalidOperationException("Repository must not be called."));
    }

    private sealed class StubAccessPolicy(Guid userId, bool isStudent) : ICoachingAccessPolicy
    {
        public Guid? CurrentUserId => userId;
        public bool IsSystemAdministrator => false;
        public bool IsInstitutionAdministrator => false;
        public bool IsCurrentTeacher(Guid teacherId) => false;
        public bool IsCurrentStudent(Guid studentId) => isStudent && studentId == userId;
        public Guid RequireCurrentTeacher() => throw new NotSupportedException();
        public void RequireTeacher(Guid teacherId) => throw new NotSupportedException();
        public void RequireStudent(Guid studentId) => throw new NotSupportedException();
        public void RequireTeacherOrStudent(Guid teacherId, Guid studentId) => throw new NotSupportedException();
        public void RequireTeacherOrAssignedStudent(Guid teacherId, IEnumerable<Guid> studentIds) => throw new NotSupportedException();
    }

    private sealed class StubIdentityAuthorizationClient(IReadOnlyCollection<Guid> allowedStudentIds)
        : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) =>
            Task.FromResult<CoachingAdminAccessScope?>(null);

        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId, bool isSystemAdministrator, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken cancellationToken) =>
            Task.FromResult(allowedStudentIds);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
