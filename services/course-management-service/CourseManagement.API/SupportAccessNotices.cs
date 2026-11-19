using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

/// <summary>
/// E-mails the institution's owner about support access (19.11.2026): when support asks, when access opens and when it
/// ends (also when it simply runs out). Runs in the e-mail worker; the owner's address is asked from Identity each time.
/// An owner without a readable address is skipped quietly; the panel shows the request anyway.
/// </summary>
public static class SupportAccessNotices
{
    public static async Task SendDueAsync(CourseManagementDbContext db, IPublishEndpoint publish, IdentityAccountLookup identity,
        CourseEmailOptions options, DateTimeOffset now, CancellationToken ct)
    {
        var candidates = await db.SupportAccessGrants.Where(item =>
                item.RequestNoticeSentAt == null
                || (item.StartNoticeSentAt == null && item.ApprovedAt != null)
                || (item.StartNoticeSentAt != null && item.EndNoticeSentAt == null))
            .OrderBy(item => item.RequestedAt).Take(50).ToListAsync(ct);
        foreach (var grant in candidates)
        {
            var notice = grant.DueNotice(now);
            if (notice is null)
            {
                grant.SettleSkippedNotices(now);
                await db.SaveChangesAsync(ct);
                continue;
            }
            var workspace = await db.Workspaces.AsNoTracking().SingleAsync(item => item.Id == grant.WorkspaceId, ct);
            var address = await identity.FindEmailAsync(workspace.OwnerUserId, ct);
            if (address is not null)
            {
                var (subject, title, paragraphs) = Text(notice.Value, grant, workspace.Name);
                await publish.Publish(new CourseManagementOwnerEmailRequestedV1(NoticeId(grant.Id, notice.Value), address, subject, title,
                        paragraphs, $"{options.PublicBaseUrl.TrimEnd('/')}/staff/#ayarlar?bolum=destek", "Destek erişimini görüntüleyin"),
                    context => context.MessageId = NoticeId(grant.Id, notice.Value), ct);
            }
            grant.MarkNoticeSent(notice.Value, now);
            grant.SettleSkippedNotices(now);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Stable id per grant and notice, so a retried run does not e-mail twice.</summary>
    public static Guid NoticeId(Guid grantId, SupportAccessNotice notice) => CourseEmailLinks.MessageId(grantId, $"support-{notice}");

    public static (string Subject, string Title, IReadOnlyList<string> Paragraphs) Text(SupportAccessNotice notice,
        SupportAccessGrant grant, string institution)
    {
        var sections = string.Join(", ", Enum.GetValues<SupportAccessSections>()
            .Where(item => item != SupportAccessSections.None && grant.Sections.HasFlag(item)).Select(SectionName));
        return notice switch
        {
            SupportAccessNotice.Requested => ("eduivme destek ekibi izin istiyor", "Destek ekibi kayıtlarınıza bakmak için izin istiyor",
            [
                $"eduivme destek ekibi, {institution} kurumunun Kurs Yönetimi kayıtlarına bakmak için izin istedi.",
                $"Neden: {grant.Reason}",
                "İzin vermezseniz hiçbir kaydınızı göremeyiz. İsterseniz süreyi ve bölümleri seçerek izin verebilir ya da isteği reddedebilirsiniz."
            ]),
            SupportAccessNotice.Started => ("Destek erişimi açıldı", "Destek erişimi açıldı",
            [
                $"{institution} için destek erişimi {CourseEmailContent.When(grant.ExpiresAt!.Value)} tarihine kadar açık.",
                $"Açık bölümler: {sections}.",
                grant.AllowsChanges
                    ? "Destek ekibi bu bölümlerde düzeltme de yapabilir: iletişim bilgisi, yoklama, tek dersin saati ve bir e-posta bildirimini kapatma. Para kayıtlarına dokunulmaz, öğrenci ve velilere bildirim gitmez."
                    : "Destek ekibi yalnız görüntüleyebilir; hiçbir kaydı değiştiremez.",
                "Her bakış ve her değişiklik kayda geçer. Erişimi istediğiniz an kapatabilirsiniz."
            ]),
            _ => ("Destek erişimi kapandı", "Destek erişimi kapandı",
            [
                grant.EndReason == "EndedByOwner" ? $"{institution} için destek erişimini kapattınız."
                    : grant.EndReason == "EndedBySupport" ? $"Destek ekibi {institution} için erişimi kapattı."
                    : $"{institution} için destek erişiminin süresi doldu.",
                "Bakılan ve değiştirilen her şeyin kaydı Ayarlar, Destek erişimi bölümünde."
            ])
        };
    }

    public static string SectionName(SupportAccessSections section) => section switch
    {
        SupportAccessSections.Students => "öğrenciler",
        SupportAccessSections.Schedule => "takvim ve yoklama",
        SupportAccessSections.Finance => "tahsilat",
        SupportAccessSections.Settings => "ayarlar",
        _ => section.ToString()
    };
}
