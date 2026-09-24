using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingInstitutionDelegationTests
{
    [Fact]
    public void Delegation_ShouldOnlyCoverTheVerifiedTeacherForTheRequest()
    {
        var teacherId = Guid.NewGuid();
        var delegation = new CoachingInstitutionDelegation();

        delegation.CanManage(teacherId).Should().BeFalse();
        delegation.Grant(teacherId);

        delegation.CanManage(teacherId).Should().BeTrue();
        delegation.CanManage(Guid.NewGuid()).Should().BeFalse();
        delegation.Invoking(value => value.Grant(Guid.NewGuid())).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InstitutionWriteScope_ShouldRejectForeignOrUnscopedResources()
    {
        var institutionId = Guid.NewGuid();
        var scope = new CoachingAdminScope(false, institutionId);

        CoachingInstitutionWriteRules.OwnsResource(scope, institutionId).Should().BeTrue();
        CoachingInstitutionWriteRules.OwnsResource(scope, Guid.NewGuid()).Should().BeFalse();
        CoachingInstitutionWriteRules.OwnsResource(scope, null).Should().BeFalse();
        CoachingInstitutionWriteRules.OwnsResource(CoachingAdminScope.Global, institutionId).Should().BeFalse();
    }

    [Fact]
    public void InstitutionWriteScope_ShouldOnlyAllowCurrentlyActiveOwnStudents()
    {
        var studentId = Guid.NewGuid();
        var scope = new CoachingAdminScope(false, Guid.NewGuid(), [studentId]);

        CoachingInstitutionWriteRules.OwnsActiveStudent(scope, studentId).Should().BeTrue();
        CoachingInstitutionWriteRules.OwnsActiveStudent(scope, Guid.NewGuid()).Should().BeFalse();
        CoachingInstitutionWriteRules.OwnsActiveStudent(CoachingAdminScope.Global, studentId).Should().BeFalse();
    }

    [Fact]
    public async Task NewAssignment_ShouldVerifyOwnInstitutionAndTeacherTargetsBeforeDelegating()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var delegation = new CoachingInstitutionDelegation();
        var identity = new StubIdentityAuthorization(institutionId);
        var authorization = new CoachingInstitutionWriteAuthorization(
            new StubScopeAuthorization(new CoachingAdminScope(false, institutionId)),
            identity, null!, null!, delegation);

        await authorization.Invoking(value => value.AuthorizeNewAssignmentAsync(
                teacherId, Guid.NewGuid(), [studentId], CancellationToken.None))
            .Should().ThrowAsync<BusinessRuleException>();
        identity.CallCount.Should().Be(0);
        delegation.CanManage(teacherId).Should().BeFalse();

        await authorization.AuthorizeNewAssignmentAsync(
            teacherId, institutionId, [studentId], CancellationToken.None);
        identity.CallCount.Should().Be(1);
        identity.LastRequestedInstitutionId.Should().Be(institutionId);
        delegation.CanManage(teacherId).Should().BeTrue();
    }

    [Fact]
    public async Task NewSession_ShouldRejectIdentityResponseForAnotherInstitution()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var delegation = new CoachingInstitutionDelegation();
        var authorization = new CoachingInstitutionWriteAuthorization(
            new StubScopeAuthorization(new CoachingAdminScope(false, institutionId)),
            new StubIdentityAuthorization(Guid.NewGuid()), null!, null!, delegation);

        await authorization.Invoking(value => value.AuthorizeNewSessionAsync(
                teacherId, Guid.NewGuid(), Coaching.Domain.Enums.SessionType.OneOnOne,
                null, CancellationToken.None))
            .Should().ThrowAsync<BusinessRuleException>();
        delegation.CanManage(teacherId).Should().BeFalse();
    }

    private sealed class StubScopeAuthorization(CoachingAdminScope scope) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(scope);
    }

    private sealed class StubIdentityAuthorization(Guid? resolvedInstitutionId) : ICoachingIdentityAuthorizationClient
    {
        public int CallCount { get; private set; }
        public Guid? LastRequestedInstitutionId { get; private set; }

        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId,
            IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId,
            bool isSystemAdministrator, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestedInstitutionId = requestedInstitutionId;
            return Task.FromResult(resolvedInstitutionId);
        }

        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(
            Guid viewerUserId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
            Guid viewerUserId, IReadOnlyCollection<Guid> studentIds,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
