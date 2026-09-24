using EduPlatform.Shared.Contracts.Events.Identity;
using FluentAssertions;
using Notification.Application.Consumers;

namespace Identity.API.IntegrationTests;

public sealed class CoachingInvitationEmailTemplateTests
{
    [Theory]
    [InlineData("TeacherToInstitution", "öğretmen")]
    [InlineData("StudentToInstitution", "öğrenci")]
    [InlineData("StudentToTeacher", "öğrenci")]
    public void RendersCoachingInviteWithRegistrationGuidance(string invitationType, string role)
    {
        var invitation = CreateInvitation(invitationType);

        var (subject, body) = CoachingInvitationEmailTemplate.Render(invitation);

        subject.Should().Be("Eduİvme Koçluk daveti");
        body.Should().Contain($"{role} olarak kayıt");
        body.Should().Contain("davetin gönderildiği e-posta adresi");
        body.Should().Contain("https://onuraltintas.net/coaching-portal/invitations?id=");
        body.Should().NotContain("Hızlı Okuma");
    }

    [Fact]
    public void EncodesUserSuppliedContentInHtml()
    {
        var invitation = CreateInvitation("StudentToInstitution") with
        {
            InviterEmail = "<script>alert(1)</script>@example.com",
            Message = "<img src=x onerror=alert(1)>"
        };

        var (_, body) = CoachingInvitationEmailTemplate.Render(invitation);

        body.Should().NotContain("<script>").And.NotContain("<img");
        body.Should().Contain("&lt;script&gt;").And.Contain("&lt;img");
    }

    private static InvitationCreatedEvent CreateInvitation(string invitationType) => new(
        Guid.NewGuid(),
        "institution@example.com",
        "student@example.com",
        null,
        invitationType,
        "Hoş geldiniz",
        $"https://onuraltintas.net/coaching-portal/invitations?id={Guid.NewGuid()}",
        DateTime.UtcNow);
}
