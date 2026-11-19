using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

/// <summary>Corrections under support access, owner notices, delivery results and switching an e-mail off (19.11.2026).</summary>
public sealed class SupportAccessChangesTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("8ae5aaed-08f6-4d4a-a9a3-5cbeb6c9b125");
    private static readonly Guid AdminId = Guid.Parse("5b8d8ad6-2c1d-4a63-9d2c-6c0f9c3c1a11");
    private static readonly Guid OwnerId = Guid.Parse("f9dcfd6b-7932-47e8-9f51-7ca0738fa983");
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static SupportAccessGrant Requested() =>
        SupportAccessGrant.Request(WorkspaceId, AdminId, "Yoklama yanlış girilmiş, düzeltmemiz gerekiyor.", Now);

    [Fact]
    public void Corrections_need_the_owners_extra_permission_and_stay_in_the_opened_sections()
    {
        var viewOnly = Requested();
        viewOnly.Approve(OwnerId, 24, SupportAccessSections.Schedule, Now);
        Assert.True(viewOnly.Opens(SupportAccessSections.Schedule, AdminId, Now.AddHours(1)));
        Assert.False(viewOnly.OpensForChange(SupportAccessSections.Schedule, AdminId, Now.AddHours(1)));

        var correcting = Requested();
        correcting.Approve(OwnerId, 1, SupportAccessSections.Schedule, allowChanges: true, Now);
        Assert.True(correcting.AllowsChanges);
        Assert.True(correcting.OpensForChange(SupportAccessSections.Schedule, AdminId, Now.AddMinutes(30)));
        Assert.False(correcting.OpensForChange(SupportAccessSections.Students, AdminId, Now.AddMinutes(30)));
        Assert.False(correcting.OpensForChange(SupportAccessSections.Schedule, Guid.NewGuid(), Now.AddMinutes(30)));
        Assert.False(correcting.OpensForChange(SupportAccessSections.Schedule, AdminId, Now.AddHours(1)), "it expires with the grant");
        correcting.End("EndedByOwner", Now.AddMinutes(10));
        Assert.False(correcting.OpensForChange(SupportAccessSections.Schedule, AdminId, Now.AddMinutes(11)));
    }

    [Fact]
    public void The_owner_hears_of_the_request_the_opening_and_the_end_once_each()
    {
        var grant = Requested();
        Assert.Equal(SupportAccessNotice.Requested, grant.DueNotice(Now));
        grant.MarkNoticeSent(SupportAccessNotice.Requested, Now);
        Assert.Null(grant.DueNotice(Now));

        grant.Approve(OwnerId, 1, SupportAccessSections.Students, Now);
        Assert.Equal(SupportAccessNotice.Started, grant.DueNotice(Now));
        grant.MarkNoticeSent(SupportAccessNotice.Started, Now);
        Assert.Null(grant.DueNotice(Now.AddMinutes(30)));

        // Running out is an end too, without anyone pressing a button.
        Assert.Equal(SupportAccessNotice.Ended, grant.DueNotice(Now.AddHours(2)));
        grant.MarkNoticeSent(SupportAccessNotice.Ended, Now.AddHours(2));
        Assert.Null(grant.DueNotice(Now.AddHours(3)));
    }

    [Fact]
    public void A_declined_request_sends_no_end_notice_and_an_approval_before_the_request_notice_counts_as_both()
    {
        var declined = Requested();
        declined.MarkNoticeSent(SupportAccessNotice.Requested, Now);
        declined.Decline(OwnerId, Now);
        Assert.Null(declined.DueNotice(Now));

        var quick = Requested();
        quick.Approve(OwnerId, 24, SupportAccessSections.Settings, Now);
        Assert.Equal(SupportAccessNotice.Started, quick.DueNotice(Now));
        quick.MarkNoticeSent(SupportAccessNotice.Started, Now);
        Assert.NotNull(quick.RequestNoticeSentAt);
        Assert.Null(quick.DueNotice(Now));
    }

    [Fact]
    public void Access_approved_and_ended_before_any_notice_still_tells_both()
    {
        var grant = Requested();
        grant.Approve(OwnerId, 1, SupportAccessSections.Students, Now);
        grant.End("EndedByOwner", Now.AddSeconds(1));

        Assert.Equal(SupportAccessNotice.Started, grant.DueNotice(Now.AddSeconds(2)));
        grant.MarkNoticeSent(SupportAccessNotice.Started, Now.AddSeconds(2));
        Assert.Equal(SupportAccessNotice.Ended, grant.DueNotice(Now.AddSeconds(3)));

        var answered = Requested();
        answered.Decline(OwnerId, Now);
        Assert.Null(answered.DueNotice(Now));
        answered.SettleSkippedNotices(Now);
        Assert.NotNull(answered.RequestNoticeSentAt);
    }

    [Fact]
    public void A_change_entry_keeps_codes_and_field_names_not_the_values()
    {
        var grant = Requested();
        grant.Approve(OwnerId, 1, SupportAccessSections.Students, allowChanges: true, Now);
        var studentId = Guid.NewGuid();
        var log = SupportAccessLog.Change(grant, SupportAccessSections.Students, "Öğrenci iletişim bilgisi düzeltildi", studentId,
            null, "e-posta, telefon", Now);

        Assert.True(log.IsChange);
        Assert.Equal(studentId, log.TargetId);
        Assert.Equal("e-posta, telefon", log.After);
        Assert.False(SupportAccessLog.Create(grant, SupportAccessSections.Students, "Öğrenci listesi", Now).IsChange);
    }

    [Fact]
    public void Support_entries_need_no_grant_since_the_owner_approval_was_dropped()
    {
        var studentId = Guid.NewGuid();
        var look = SupportAccessLog.View(WorkspaceId, AdminId, SupportAccessSections.Finance, "Tahsilat", Now);
        var change = SupportAccessLog.Change(WorkspaceId, AdminId, SupportAccessSections.Schedule, "Yoklama düzeltildi", studentId, "Absent", "Present", Now);

        Assert.Null(look.GrantId);
        Assert.False(look.IsChange);
        Assert.Null(change.GrantId);
        Assert.True(change.IsChange);
        Assert.Equal(("Absent", "Present"), (change.Before, change.After));
        Assert.Throws<ArgumentException>(() => SupportAccessLog.View(Guid.Empty, AdminId, SupportAccessSections.Students, "x", Now));
    }

    [Fact]
    public void Delivery_is_recorded_from_every_address_s_answer()
    {
        CourseEmail Sent(params string[] to)
        {
            var email = CourseEmail.Create(WorkspaceId, CourseEmailKind.Absence, Guid.NewGuid(), $"absence:{Guid.NewGuid():N}", Now, Now);
            email.MarkSent("Devamsızlık", to, Now);
            return email;
        }

        var both = Sent("veli@example.test", "ogrenci@example.test");
        Assert.Equal(["veli@example.test", "ogrenci@example.test"], both.RecipientList);
        both.RecordDelivery(1, 1, Now);
        Assert.Equal(CourseEmailDelivery.PartlyFailed, both.Delivery);
        Assert.Equal(1, both.FailedRecipientCount);

        var ok = Sent("veli@example.test");
        ok.RecordDelivery(1, 0, Now);
        Assert.Equal(CourseEmailDelivery.Delivered, ok.Delivery);

        var bounced = Sent("veli@example.test");
        bounced.RecordDelivery(0, 1, Now);
        Assert.Equal(CourseEmailDelivery.Failed, bounced.Delivery);

        var waiting = CourseEmail.Create(WorkspaceId, CourseEmailKind.Absence, Guid.NewGuid(), "absence:x", Now, Now);
        waiting.RecordDelivery(1, 0, Now);
        Assert.Equal(CourseEmailDelivery.Unknown, waiting.Delivery);
    }

    [Fact]
    public void Support_can_switch_a_notification_off_but_not_on()
    {
        var settings = WorkspaceEmailSettings.CreateDefault(WorkspaceId, Now);
        settings.Update([new CourseEmailRule(CourseEmailKind.Absence, true, CourseEmailAudience.Guardians, "Not"),
            new CourseEmailRule(CourseEmailKind.WeeklySchedule, true, CourseEmailAudience.Both, null)], 3, true, 3, 9, null, true, OwnerId, Now);

        Assert.True(settings.SwitchOff(CourseEmailKind.Absence, Now));
        Assert.False(settings.IsEnabled(CourseEmailKind.Absence));
        Assert.Equal("Not", settings.Rule(CourseEmailKind.Absence).Note);
        Assert.True(settings.IsEnabled(CourseEmailKind.WeeklySchedule));
        Assert.False(settings.SwitchOff(CourseEmailKind.Absence, Now), "already off");
        Assert.False(settings.SwitchOff(CourseEmailKind.PaymentReceived, Now));
    }
}
