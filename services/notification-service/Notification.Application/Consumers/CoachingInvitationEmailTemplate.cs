using System.Net;
using EduPlatform.Shared.Contracts.Events.Identity;

namespace Notification.Application.Consumers;

public static class CoachingInvitationEmailTemplate
{
    public static (string Subject, string Body) Render(InvitationCreatedEvent invitation)
    {
        var role = invitation.InvitationType switch
        {
            "TeacherToInstitution" => "öğretmen",
            "StudentToInstitution" or "StudentToTeacher" => "öğrenci",
            _ => throw new ArgumentOutOfRangeException(nameof(invitation), "Unsupported coaching invitation type")
        };
        var inviter = WebUtility.HtmlEncode(invitation.InviterEmail);
        var link = WebUtility.HtmlEncode(invitation.Link ?? string.Empty);
        var note = string.IsNullOrWhiteSpace(invitation.Message)
            ? string.Empty
            : $"<p><em>Not: {WebUtility.HtmlEncode(invitation.Message)}</em></p>";

        var body = $"""
            <html>
            <body>
                <h1>Eduİvme Koçluk daveti</h1>
                <p>Merhaba,</p>
                <p><strong>{inviter}</strong> sizi Eduİvme Koçluk platformuna davet etti.</p>
                {note}
                <p>Henüz hesabınız yoksa davetin gönderildiği e-posta adresiyle {role} olarak kayıt olun ve giriş yapın.</p>
                <p><a href="{link}">Daveti görüntüle</a></p>
                <p>Bağlantıyı kopyalayabilirsiniz: {link}</p>
            </body>
            </html>
            """;

        return ("Eduİvme Koçluk daveti", body);
    }
}
