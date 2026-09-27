using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminStudentHistoryApiTests
{
    [Fact]
    public async Task StudentHistory_ReturnsNotFoundForStudentOutsideTenantScope()
    {
        var studentId = Guid.NewGuid();
        var controller = CreateController([]);

        var result = await controller.GetStudentHistory(
            studentId,
            repository: null!,
            type: CoachingStudentHistoryType.Assignments,
            pageNumber: 1,
            pageSize: 25,
            cancellationToken: CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 101)]
    public async Task StudentHistory_RejectsUnboundedPagination(int pageNumber, int pageSize)
    {
        var studentId = Guid.NewGuid();
        var controller = CreateController([studentId]);

        var result = await controller.GetStudentHistory(
            studentId,
            repository: null!,
            type: CoachingStudentHistoryType.Assignments,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: CancellationToken.None);

        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task StudentHistory_RejectsUnknownRecordType()
    {
        var studentId = Guid.NewGuid();
        var controller = CreateController([studentId]);

        var result = await controller.GetStudentHistory(
            studentId,
            repository: null!,
            type: (CoachingStudentHistoryType)999,
            pageNumber: 1,
            pageSize: 25,
            cancellationToken: CancellationToken.None);

        result.Should().BeOfType<BadRequestResult>();
    }

    private static CoachingAdminController CreateController(IReadOnlyCollection<Guid> studentIds) =>
        new(null!, new FixedScopeAuthorization(new CoachingAdminScope(
            IsGlobal: false,
            InstitutionId: Guid.NewGuid(),
            StudentIds: studentIds)), null!);

    private sealed class FixedScopeAuthorization(CoachingAdminScope scope) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(scope);
    }
}
