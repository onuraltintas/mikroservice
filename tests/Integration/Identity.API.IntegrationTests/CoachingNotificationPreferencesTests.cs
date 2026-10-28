using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Services;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;

namespace Identity.API.IntegrationTests;

// Roadmap O8: users choose which coaching notifications they get in the app and by e-mail; important ones e-mail by default.
public sealed class CoachingNotificationPreferencesTests
{
    private static readonly Guid Student = Guid.NewGuid(), NoEmail = Guid.NewGuid();

    private sealed class Fixture : IAsyncDisposable
    {
        public NotificationDbContext Db { get; } = new(new DbContextOptionsBuilder<NotificationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public RecordingNotifications App { get; } = new();
        public RecordingMail Mail { get; } = new();
        public StubDirectory Directory { get; } = new();
        public CoachingNotificationDispatcher Dispatcher { get; }
        public NotificationPreferenceService Preferences { get; }
        public Fixture()
        {
            Directory.Contacts[Student] = new RecipientContact(Student, "ada@example.test", "Ada", "Yılmaz");
            Directory.Contacts[NoEmail] = new RecipientContact(NoEmail, null, "Can", "Demir");
            var policy = new NotificationChannelPolicy(Db, Directory, Mail, Options.Create(new PublicAppUrlOptions { BaseUrl = "https://onuraltintas.net" }));
            Dispatcher = new CoachingNotificationDispatcher(App, policy);
            Preferences = new NotificationPreferenceService(Db);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task ImportantCoachingEventsAlsoGoByEmailByDefault_OthersStayInTheApp()
    {
        await using var f = new Fixture(); var eventId = Guid.NewGuid();
        await f.Dispatcher.SendAsync(eventId, [Student, NoEmail], "Yeni koçluk seansı", "Seansın <b>planlandı</b>: 04.01.2030 10:00", "SessionScheduled", "s1", default);
        f.App.Calls.Select(x => x.UserId).Should().BeEquivalentTo(new[] { Student, NoEmail });
        var mail = f.Mail.Messages.Should().ContainSingle().Subject;
        mail.Recipient.Should().Be("ada@example.test");
        mail.Subject.Should().Contain("Yeni koçluk seansı");
        mail.Body.Should().Contain("Merhaba Ada").And.Contain("&lt;b&gt;planlandı&lt;/b&gt;").And.Contain("https://onuraltintas.net").And.Contain("Bildirim");
        mail.SubjectUserId.Should().Be(Student);

        await f.Dispatcher.SendAsync(Guid.NewGuid(), [Student], "Sınav sonucu açıklandı", "Puanın: 420", "ExamResultAdded", "e1", default);
        f.App.Calls.Should().HaveCount(3); f.Mail.Messages.Should().HaveCount(1);

        // A redelivered event reuses the same e-mail id, so the outbox cannot send it twice.
        await f.Dispatcher.SendAsync(eventId, [Student], "Yeni koçluk seansı", "Tekrar", "SessionScheduled", "s1", default);
        f.Mail.Messages.Select(x => x.MessageId).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task ChoicesTurnEachChannelOnOrOffPerCategory()
    {
        await using var f = new Fixture();
        await f.Preferences.SaveAsync(Student, [new("Sessions", false, false), new("Exams", true, true)], default);
        await f.Dispatcher.SendAsync(Guid.NewGuid(), [Student], "Koçluk seansı iptal edildi", "İptal", "SessionCancelled", "s1", default);
        f.App.Calls.Should().BeEmpty(); f.Mail.Messages.Should().BeEmpty();
        await f.Dispatcher.SendAsync(Guid.NewGuid(), [Student], "Sınav sonucu açıklandı", "Puanın: 420", "ExamResultAdded", "e1", default);
        f.App.Calls.Should().ContainSingle(); f.Mail.Messages.Should().ContainSingle();

        var view = await f.Preferences.GetAsync(Student, default);
        view.Single(x => x.Category == "Sessions").Should().BeEquivalentTo(new { InApp = false, Email = false });
        view.Single(x => x.Category == "Assignments").Should().BeEquivalentTo(new { InApp = true, Email = true });
        view.Single(x => x.Category == "Goals").Email.Should().BeFalse();
        view.Select(x => x.Label).Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x));

        await f.Preferences.SaveAsync(Student, [new("Sessions", true, true)], default);
        (await f.Preferences.GetAsync(Student, default)).Single(x => x.Category == "Sessions").InApp.Should().BeTrue();
        await f.Invoking(x => x.Preferences.SaveAsync(Student, [new("Unknown", true, true)], default)).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UnknownTypesStayInAppOnly_AndEveryCoachingTypeHasACategory()
    {
        await using var f = new Fixture();
        await f.Dispatcher.SendAsync(Guid.NewGuid(), [Student], "Başka", "Mesaj", "SomethingNew", null, default);
        f.App.Calls.Should().ContainSingle(); f.Mail.Messages.Should().BeEmpty();
        foreach (var type in new[] { "AssignmentCreated", "AssignmentUpdated", "AssignmentSubmitted", "AssignmentGraded", "AssignmentCancelled", "AssignmentDueReminder",
                     "SessionScheduled", "SessionUpdated", "SessionCancelled", "SessionReminder", "CoachingMessage", "ExamResultAdded", "ExamUpdated",
                     "ExamResultUpdated", "GoalCreated", "GoalUpdated", "StudyPlanReminder", "StudyPlanOverdueReminder", "FeeOverdueReminder",
                     "TeacherLinkEnded", "InvitationAccepted" })
            NotificationCategories.ForType(type).Should().NotBeNull(type);
        NotificationCategories.ForType("AssignmentDueReminder")!.EmailByDefault.Should().BeTrue();
        NotificationCategories.ForType("InvitationAccepted")!.EmailByDefault.Should().BeTrue();
    }

    [Fact]
    public void SubmissionTextNamesTheStudentAndTheAssignment()
    {
        var text = CoachingNotificationTexts.AssignmentSubmitted("Ada Yılmaz", "Çarpanlar testi", new DateTime(2030, 1, 4, 7, 0, 0, DateTimeKind.Utc));
        text.Title.Should().Be("Ödev teslim edildi: Çarpanlar testi");
        text.Message.Should().Contain("Ada Yılmaz").And.Contain("\"Çarpanlar testi\"").And.Contain("04.01.2030 10:00");
        CoachingNotificationTexts.AssignmentSubmitted(null, null, new DateTime(2030, 1, 4, 7, 0, 0, DateTimeKind.Utc)).Message.Should().Contain("Bir öğrenci");
        CoachingNotificationTexts.AssignmentGraded("Çarpanlar testi", 85m, "Güzel").Title.Should().Be("Ödev değerlendirildi: Çarpanlar testi");
        CoachingNotificationTexts.AssignmentDue("Çarpanlar testi", new DateTime(2030, 1, 4, 18, 0, 0, DateTimeKind.Utc)).Message.Should().Contain("04.01.2030 21:00");
        CoachingNotificationTexts.InvitationAccepted("Ada Yılmaz", "StudentToTeacher").Message.Should().Contain("Ada Yılmaz").And.Contain("öğrenci");
    }

    public sealed class RecordingNotifications : INotificationService
    {
        public List<(Guid UserId, string Title, string Type, Guid? SourceMessageId)> Calls { get; } = [];
        public Task SendNotificationAsync(Guid userId, string title, string message, string type, string? relatedEntityId = null, Guid? sourceMessageId = null)
        { Calls.Add((userId, title, type, sourceMessageId)); return Task.CompletedTask; }
    }
    public sealed class RecordingMail : IEmailDeliveryQueue
    {
        public ConcurrentQueue<(Guid MessageId, string ConsumerType, string Recipient, string Subject, string Body, Guid? SubjectUserId)> Messages { get; } = new();
        public Task QueueAsync(Guid messageId, string consumerType, string recipient, string subject, string body, Guid? subjectUserId = null, CancellationToken cancellationToken = default)
        { Messages.Enqueue((messageId, consumerType, recipient, subject, body, subjectUserId)); return Task.CompletedTask; }
    }
    public sealed class StubDirectory : IRecipientDirectory
    {
        public Dictionary<Guid, RecipientContact> Contacts { get; } = [];
        public Task<IReadOnlyDictionary<Guid, RecipientContact>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, RecipientContact>>(userIds.Where(Contacts.ContainsKey).ToDictionary(x => x, x => Contacts[x]));
    }
}
