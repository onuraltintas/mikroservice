using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

public sealed class CourseEmailTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("8ae5aaed-08f6-4d4a-a9a3-5cbeb6c9b125");
    private static readonly Guid OwnerId = Guid.Parse("f9dcfd6b-7932-47e8-9f51-7ca0738fa983");
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static CourseEmailRule On(CourseEmailKind kind, string? note = null) =>
        new(kind, true, CourseEmailAudience.Guardians, note);

    [Fact]
    public void Every_kind_starts_switched_off_with_its_default_audience()
    {
        var settings = WorkspaceEmailSettings.CreateDefault(WorkspaceId, Now);

        Assert.Equal(Enum.GetValues<CourseEmailKind>().Length, settings.Rules.Count);
        Assert.All(settings.Rules, rule => Assert.False(rule.Enabled));
        Assert.Equal(CourseEmailAudience.Both, settings.Rule(CourseEmailKind.WeeklySchedule).Audience);
        Assert.Equal(CourseEmailAudience.Guardians, settings.Rule(CourseEmailKind.Absence).Audience);
        Assert.False(settings.IsEnabled(CourseEmailKind.Absence));
    }

    [Fact]
    public void Switching_on_needs_the_informed_consent_statement_once()
    {
        var settings = WorkspaceEmailSettings.CreateDefault(WorkspaceId, Now);

        Assert.Throws<InvalidOperationException>(() =>
            settings.Update([On(CourseEmailKind.Absence)], 3, true, 3, 9, null, false, OwnerId, Now));

        settings.Update([On(CourseEmailKind.Absence, "  Lütfen arayın.  ")], 3, true, 3, 9, " Kurs@Ornek.com ", true, OwnerId, Now);
        Assert.True(settings.IsEnabled(CourseEmailKind.Absence));
        Assert.Equal("Lütfen arayın.", settings.Rule(CourseEmailKind.Absence).Note);
        Assert.Equal("kurs@ornek.com", settings.ReplyToEmail);
        Assert.Equal(OwnerId, settings.InformedConsentByUserId);

        settings.Update([On(CourseEmailKind.PaymentReceived)], 3, true, 3, 9, null, false, OwnerId, Now.AddDays(1));
        Assert.True(settings.IsEnabled(CourseEmailKind.PaymentReceived));
        Assert.False(settings.IsEnabled(CourseEmailKind.Absence));
        Assert.Equal(Now, settings.InformedConsentAt);
    }

    [Theory]
    [InlineData(0, 3, 9)]
    [InlineData(8, 3, 9)]
    [InlineData(3, 0, 9)]
    [InlineData(3, 31, 9)]
    [InlineData(3, 3, 6)]
    [InlineData(3, 3, 22)]
    public void Out_of_range_timings_are_rejected(int daysBefore, int overdueAfter, int hour)
    {
        var settings = WorkspaceEmailSettings.CreateDefault(WorkspaceId, Now);

        Assert.ThrowsAny<ArgumentException>(() => settings.Update([], daysBefore, true, overdueAfter, hour, null, true, OwnerId, Now));
    }

    [Fact]
    public void Notes_addresses_and_duplicate_kinds_are_validated()
    {
        var settings = WorkspaceEmailSettings.CreateDefault(WorkspaceId, Now);

        Assert.Throws<ArgumentException>(() => settings.Update([On(CourseEmailKind.Absence, new string('a', 301))],
            3, true, 3, 9, null, true, OwnerId, Now));
        Assert.Throws<ArgumentException>(() => settings.Update([], 3, true, 3, 9, "not-an-address", true, OwnerId, Now));
        Assert.Throws<ArgumentException>(() => settings.Update([On(CourseEmailKind.Absence), On(CourseEmailKind.Absence)],
            3, true, 3, 9, null, true, OwnerId, Now));
    }

    [Fact]
    public void A_waiting_email_gathers_occasions_once_and_records_how_it_ended()
    {
        var lessonA = Guid.NewGuid();
        var lessonB = Guid.NewGuid();
        var email = CourseEmail.Create(WorkspaceId, CourseEmailKind.Absence, Guid.NewGuid(), "absence:x:20261120:1", Now, Now, [lessonA]);

        email.AddReferences([lessonA, lessonB]);
        Assert.Equal([lessonA, lessonB], email.References);

        email.MarkSent("Ali derse gelmedi", ["veli@ornek.com"], Now);
        Assert.Equal(CourseEmailStatus.Sent, email.Status);
        Assert.Equal("veli@ornek.com", email.Recipients);
        Assert.Equal(1, email.RecipientCount);

        email.AddReferences([Guid.NewGuid()]);
        Assert.Equal(2, email.References.Count);
        Assert.Throws<InvalidOperationException>(() => email.MarkSent("again", ["x@ornek.com"], Now));
    }

    [Fact]
    public void Skipping_keeps_the_reason()
    {
        var email = CourseEmail.Create(WorkspaceId, CourseEmailKind.PaymentReceived, Guid.NewGuid(), "payment:1", Now, Now);

        email.Skip("Alıcı e-postası yok", Now);

        Assert.Equal(CourseEmailStatus.Skipped, email.Status);
        Assert.Equal("Alıcı e-postası yok", email.SkipReason);
    }
}
