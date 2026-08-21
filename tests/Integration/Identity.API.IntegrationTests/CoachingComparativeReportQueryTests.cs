using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetInstitutionCoachingComparison;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingComparativeReportQueryTests
{
    [Fact]
    public async Task ComparativeReportQuery_ShouldUseIdentityScopedStudentRoster()
    {
        var institutionId = Guid.NewGuid();
        var studentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var expected = new InstitutionCoachingComparisonDto(
            institutionId,
            8,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            studentIds.Length,
            4,
            6,
            5,
            4,
            80m,
            2,
            4,
            72.5m,
            3,
            4,
            3,
            75m,
            5,
            2,
            68);
        var identity = new StubReportIdentityClient(studentIds);
        var repository = new StubComparativeReportRepository(expected);
        var handler = new GetInstitutionCoachingComparisonQueryHandler(
            repository,
            identity,
            CreatePolicy(Guid.NewGuid(), "SystemAdmin"));

        var result = await handler.Handle(
            new GetInstitutionCoachingComparisonQuery(
                institutionId,
                expected.FromDate,
                expected.ToDate,
                expected.GradeLevel),
            CancellationToken.None);

        result.Should().Be(expected);
        identity.Requests.Should().ContainSingle();
        identity.Requests[0].Should().Be((institutionId, 8));
        repository.RequestedStudentIds.Should().BeEquivalentTo(studentIds);
    }

    [Fact]
    public async Task ComparativeReportQuery_ShouldRejectNonAdministrator()
    {
        var handler = new GetInstitutionCoachingComparisonQueryHandler(
            new StubComparativeReportRepository(null),
            new StubReportIdentityClient([]),
            CreatePolicy(Guid.NewGuid(), "Teacher"));

        var action = () => handler.Handle(
            new GetInstitutionCoachingComparisonQuery(Guid.NewGuid()),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
    }

    private static ICoachingAccessPolicy CreatePolicy(Guid userId, params string[] roles) =>
        new CoachingAccessPolicy(new StubCurrentUserService(userId, roles));

    private sealed class StubComparativeReportRepository(InstitutionCoachingComparisonDto? result)
        : ICoachingComparativeReportRepository
    {
        public IReadOnlyCollection<Guid> RequestedStudentIds { get; private set; } = [];

        public Task<InstitutionCoachingComparisonDto> GetInstitutionComparisonAsync(
            Guid institutionId,
            IReadOnlyCollection<Guid> studentIds,
            int? gradeLevel,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            RequestedStudentIds = studentIds;
            return Task.FromResult(result ?? throw new InvalidOperationException("Not expected"));
        }
    }

    private sealed class StubReportIdentityClient(IReadOnlyCollection<Guid> studentIds)
        : ICoachingIdentityReportClient
    {
        public List<(Guid InstitutionId, int? GradeLevel)> Requests { get; } = [];

        public Task<IReadOnlyCollection<Guid>> GetActiveStudentIdsAsync(
            Guid viewerUserId,
            Guid institutionId,
            int? gradeLevel,
            CancellationToken cancellationToken)
        {
            Requests.Add((institutionId, gradeLevel));
            return Task.FromResult(studentIds);
        }
    }

    private sealed class StubCurrentUserService(Guid userId, string[] roles) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
