using Coaching.API.Controllers;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAgreementApiMetadataTests
{
    [Fact]
    public void Controller_MustRequireAuthentication()
    {
        typeof(CoachingAgreementsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should()
            .NotBeEmpty();
    }

    [Fact]
    public void Publish_MustRequireSystemAdminMfaAndManagePermission()
    {
        var method = typeof(CoachingAgreementsController)
            .GetMethod(nameof(CoachingAgreementsController.Publish));

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .Contain(attribute => attribute.Roles == "SystemAdmin")
            .And.Contain(attribute => attribute.Policy == "MfaRequired");
        method.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "Permissions.Coaching.Manage");
        method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true)
            .Should()
            .NotBeEmpty();
    }

    [Theory]
    [InlineData(nameof(CoachingAgreementsController.AcknowledgeCurrent))]
    [InlineData(nameof(CoachingAgreementsController.WithdrawAcknowledgement))]
    public void StudentEvidenceActions_MustRequireStudentRole(string actionName)
    {
        var method = typeof(CoachingAgreementsController).GetMethod(actionName);

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .Contain(attribute => attribute.Roles == "Student");
    }
}
