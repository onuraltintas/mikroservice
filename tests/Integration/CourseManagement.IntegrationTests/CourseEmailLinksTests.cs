using CourseManagement.API;
using CourseManagement.Domain;
using Notification.Application.Consumers;

namespace CourseManagement.IntegrationTests;

/// <summary>Institution e-mails (19.11.2026): signed unsubscribe links, the week window, samples and the e-mail layout.</summary>
public sealed class CourseEmailLinksTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("8ae5aaed-08f6-4d4a-a9a3-5cbeb6c9b125");

    [Fact]
    public void Unsubscribe_links_are_signed_for_one_workspace_and_address()
    {
        var options = new CourseEmailOptions { PublicBaseUrl = "https://kurum.example.test/", UnsubscribeKey = new string('k', 40) };
        var token = CourseEmailLinks.Token(options, WorkspaceId, "Veli@Ornek.com");

        Assert.True(CourseEmailLinks.IsValid(options, WorkspaceId, "veli@ornek.com", token));
        Assert.False(CourseEmailLinks.IsValid(options, Guid.NewGuid(), "veli@ornek.com", token));
        Assert.False(CourseEmailLinks.IsValid(options, WorkspaceId, "baska@ornek.com", token));
        Assert.False(CourseEmailLinks.IsValid(options, WorkspaceId, "veli@ornek.com", null));

        var url = CourseEmailLinks.UnsubscribeUrl(options, WorkspaceId, "veli@ornek.com");
        Assert.StartsWith("https://kurum.example.test/api/course-management/public/email/unsubscribe?w=", url);
        var encoded = url.Split("&e=")[1].Split('&')[0];
        Assert.Equal("veli@ornek.com", CourseEmailLinks.DecodeEmail(encoded));
        Assert.Equal(CourseEmailLinks.MessageId(WorkspaceId, "a@b.c"), CourseEmailLinks.MessageId(WorkspaceId, "a@b.c"));
        Assert.NotEqual(CourseEmailLinks.MessageId(WorkspaceId, "a@b.c"), CourseEmailLinks.MessageId(WorkspaceId, "d@b.c"));
    }

    [Fact]
    public void Next_week_runs_from_monday_to_monday_in_turkiye_time()
    {
        var sundayEvening = new DateTimeOffset(2026, 11, 22, 15, 30, 0, TimeSpan.Zero); // 18:30 in Türkiye
        var (from, to) = CourseEmailContent.NextWeek(sundayEvening);

        Assert.Equal(new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.FromHours(3)), from);
        Assert.Equal(new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.FromHours(3)), to);
    }

    [Fact]
    public void Every_kind_has_a_sample_for_the_preview()
    {
        foreach (var kind in Enum.GetValues<CourseEmailKind>())
        {
            var sample = CourseEmailContent.Sample(kind);
            Assert.False(string.IsNullOrWhiteSpace(sample.Subject));
            Assert.NotEmpty(sample.Paragraphs);
        }
    }

    [Fact]
    public void The_layout_escapes_every_piece_of_institution_text()
    {
        var html = CourseManagementEmailConsumer.Layout("&lt;b&gt;Kurs&lt;/b&gt;", "Başlık", "<p>x</p>", "", "https://a.test/u?x=1&amp;y=2");

        Assert.Contains("&lt;b&gt;Kurs&lt;/b&gt;", html);
        Assert.Contains("buradan çıkabilirsiniz", html);
        Assert.DoesNotContain("<b>Kurs</b>", html);
    }

    [Fact]
    public void Owner_notices_say_what_support_may_do_and_link_only_to_eduivme()
    {
        var grant = SupportAccessGrant.Request(WorkspaceId, Guid.NewGuid(), "Yoklama yanlış girilmiş, bakmamız gerekiyor.",
            new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));
        var asked = SupportAccessNotices.Text(SupportAccessNotice.Requested, grant, "TEST Kurs");
        Assert.Contains(asked.Paragraphs, line => line.Contains("Yoklama yanlış girilmiş"));

        grant.Approve(Guid.NewGuid(), 24, SupportAccessSections.Schedule | SupportAccessSections.Students, allowChanges: true,
            new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));
        var opened = SupportAccessNotices.Text(SupportAccessNotice.Started, grant, "TEST Kurs");
        Assert.Contains(opened.Paragraphs, line => line.Contains("öğrenciler, takvim ve yoklama"));
        Assert.Contains(opened.Paragraphs, line => line.Contains("düzeltme de yapabilir"));
        Assert.NotEqual(SupportAccessNotices.NoticeId(grant.Id, SupportAccessNotice.Requested),
            SupportAccessNotices.NoticeId(grant.Id, SupportAccessNotice.Started));

        Assert.Equal("https://kurum.eduivme.com/staff/#ayarlar?bolum=destek",
            CourseManagementOwnerEmailConsumer.SafeLink("https://kurum.eduivme.com/staff/#ayarlar?bolum=destek"));
        Assert.Null(CourseManagementOwnerEmailConsumer.SafeLink("https://eduivme.com.example.test/x"));
        Assert.Null(CourseManagementOwnerEmailConsumer.SafeLink("http://kurum.eduivme.com/"));
    }
}
