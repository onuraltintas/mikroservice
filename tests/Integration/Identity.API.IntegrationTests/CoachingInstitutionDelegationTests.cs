using Coaching.Application.Authorization;
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
}
