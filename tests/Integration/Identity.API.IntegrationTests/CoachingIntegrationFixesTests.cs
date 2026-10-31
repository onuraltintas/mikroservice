using System.Collections.Concurrent;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;
using Notification.Application.Services;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;

namespace Identity.API.IntegrationTests;

// Coaching audit 31.10.2026, package K3: notifications and integration between Coaching, Identity and Notification.
public sealed class CoachingIntegrationFixesTests
{
    private static readonly Guid Student = Guid.NewGuid();

    // HIGH 1: an Identity outage must not lose the in-app notice nor the e-mail copy; the address is resolved later.
    [Fact]
    public async Task DirectoryOutage_KeepsTheInAppNotice_AndSendsTheEmailOnceIdentityIsBack()
    {
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var app = new CoachingNotificationPreferencesTests.RecordingNotifications();
        var mail = new CoachingNotificationPreferencesTests.RecordingMail();
        var directory = new SwitchableDirectory { Down = true };
        directory.Contacts[Student] = new RecipientContact(Student, "ada@example.test", "Ada", "Yılmaz");
        var clock = new MutableClock(new DateTime(2030, 1, 4, 9, 0, 0, DateTimeKind.Utc));
        var policy = new NotificationChannelPolicy(db, directory, mail,
            Options.Create(new PublicAppUrlOptions { BaseUrl = "https://onuraltintas.net" }), clock: clock);
        var dispatcher = new CoachingNotificationDispatcher(app, policy);
        var eventId = Guid.NewGuid();

        await dispatcher.SendAsync(eventId, [Student], "Yeni koçluk seansı", "Seansın planlandı", "SessionScheduled", "s1", default);
        await dispatcher.SendAsync(eventId, [Student], "Yeni koçluk seansı", "Seansın planlandı", "SessionScheduled", "s1", default);

        app.Calls.Should().HaveCount(2, "the in-app notice does not depend on Identity");
        mail.Messages.Should().BeEmpty();
        (await db.PendingContactDeliveries.CountAsync()).Should().Be(1, "a redelivered event waits only once");

        var processor = new PendingContactDeliveryProcessor(db, directory, policy, clock);
        (await processor.ProcessAsync(default)).Should().Be(0, "Identity is still down");
        (await db.PendingContactDeliveries.SingleAsync()).AttemptCount.Should().Be(1);

        directory.Down = false;
        clock.Now = clock.Now.AddHours(1);
        (await processor.ProcessAsync(default)).Should().Be(1);
        var message = mail.Messages.Should().ContainSingle().Subject;
        message.Recipient.Should().Be("ada@example.test");
        message.MessageId.Should().Be(MessageIds.Derive($"coaching-notification-email:{eventId:N}:{Student:N}"),
            "the late e-mail keeps the id of the normal path, so a later redelivery cannot send it twice");
        (await db.PendingContactDeliveries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DirectoryOutage_GivesUpOnTheEmailAfterTwoDays()
    {
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var mail = new CoachingNotificationPreferencesTests.RecordingMail();
        var directory = new SwitchableDirectory { Down = true };
        var clock = new MutableClock(new DateTime(2030, 1, 4, 9, 0, 0, DateTimeKind.Utc));
        var policy = new NotificationChannelPolicy(db, directory, mail,
            Options.Create(new PublicAppUrlOptions { BaseUrl = "https://onuraltintas.net" }), clock: clock);
        await new CoachingNotificationDispatcher(new CoachingNotificationPreferencesTests.RecordingNotifications(), policy)
            .SendAsync(Guid.NewGuid(), [Student], "Ödev", "Yeni ödev", "AssignmentCreated", "a1", default);
        var processor = new PendingContactDeliveryProcessor(db, directory, policy, clock);
        clock.Now = clock.Now.AddDays(3);
        await processor.ProcessAsync(default);
        (await db.PendingContactDeliveries.CountAsync()).Should().Be(0);
        mail.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignmentSubmitted_DirectoryOutage_StillTellsTheTeacher()
    {
        var teacher = Guid.NewGuid();
        var dispatcher = new RecordingDispatcher();
        var services = new ServiceCollection();
        services.AddSingleton<ICoachingNotificationDispatcher>(dispatcher);
        services.AddSingleton<IRecipientDirectory>(new SwitchableDirectory { Down = true });
        services.AddMassTransitTestHarness(x =>
        {
            x.UseSuiteTimeouts();
            x.AddConsumer<AssignmentSubmittedConsumer>();
            x.UsingInMemory((context, config) => config.ConfigureEndpoints(context));
        });
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new AssignmentSubmittedEvent(Guid.NewGuid(), Student, DateTime.UtcNow, teacher) { AssignmentTitle = "Paragraf" });
            await TestHarnessTimeouts.UntilAsync(() => dispatcher.Calls.Count >= 1);
            dispatcher.Calls.Single().Message.Should().Contain("Bir öğrenci");
        }
        finally { await harness.Stop(); }
    }

    // HIGH 2 and MEDIUM 6: billing e-mails of teachers and institutions open the coaching staff portal; admin alerts open the
    // admin panel. Both have their own address setting.
    [Fact]
    public async Task BillingEmails_UseTheStaffPortalAndAdminPanelAddresses()
    {
        var teacher = Guid.NewGuid();
        var (provider, mail) = await StartNoticeHarnessAsync(options =>
        {
            options.BaseUrl = "https://app.example.test";
            options.StaffPortalUrl = "https://staff.example.test/staff";
            options.AdminPanelUrl = "https://admin.example.test";
        });
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            await harness.Bus.Publish(Notice(CoachingSubscriptionNoticeKinds.Expiring, teacher, "ogretmen@example.test", "Teacher"));
            await harness.Bus.Publish(Notice(CoachingSubscriptionNoticeKinds.InstitutionExpired, null, "kurum@example.test", "Institution"));
            await harness.Bus.Publish(Notice(CoachingSubscriptionNoticeKinds.TransferSubmitted, null, null, "Individual", "Ada · EFT-1"));
            await TestHarnessTimeouts.UntilAsync(() => mail.Messages.Count >= 4);

            mail.Messages.Single(x => x.Recipient == "ogretmen@example.test").Body
                .Should().Contain("https://staff.example.test/staff/?product=coaching").And.NotContain("auth/login");
            mail.Messages.Single(x => x.Recipient == "kurum@example.test").Body
                .Should().Contain("https://staff.example.test/staff/?product=coaching");
            mail.Messages.Where(x => x.Recipient == "finans@example.test").Should()
                .OnlyContain(x => x.Body.Contains("https://admin.example.test/dashboard/coaching/subscriptions"));
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public void StaffAndAdminAddresses_FallBackToTheCentralAddress()
    {
        var options = new PublicAppUrlOptions { BaseUrl = "https://eduivme.example.test/" };
        options.StaffPortalLink().Should().Be("https://eduivme.example.test/staff/?product=coaching");
        options.AdminPanelLink("/dashboard/coaching/subscriptions").Should().Be("https://eduivme.example.test/dashboard/coaching/subscriptions");
        options.StaffPortalUrl = "https://staff.example.test/staff/";
        options.StaffPortalLink().Should().Be("https://staff.example.test/staff/?product=coaching");
    }

    // MEDIUM 10: production e-mail links must be https.
    [Theory]
    [InlineData("https://eduivme.com", true, true)]
    [InlineData("https://onuraltintas.net/staff", true, true)]
    [InlineData("http://eduivme.com", true, false)]
    [InlineData("http://localhost:4200", false, true)]
    [InlineData("https://localhost", true, false)]
    public void PublicAddresses_MustBeHttpsInProduction(string value, bool production, bool valid) =>
        PublicAppUrlOptions.IsValidForEnvironment(value, production).Should().Be(valid);

    // LOW 13: the password-setup e-mail shows its deadline in Türkiye time, not as a raw UTC timestamp.
    [Fact]
    public async Task PasswordSetupEmail_ShowsTheDeadlineInTurkeyTime()
    {
        var mail = new RecordingEmailQueue();
        var services = new ServiceCollection();
        services.AddDbContext<NotificationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<INotificationDbContext>(provider => provider.GetRequiredService<NotificationDbContext>());
        services.AddSingleton<IEmailDeliveryQueue>(mail);
        services.AddSingleton<INotificationService>(new CoachingNotificationPreferencesTests.RecordingNotifications());
        services.Configure<PublicAppUrlOptions>(options => options.BaseUrl = "https://app.example.test");
        services.AddMassTransitTestHarness(x =>
        {
            x.UseSuiteTimeouts();
            x.AddConsumer<UserPasswordSetupRequestedConsumer>();
            x.UsingInMemory((context, config) => config.ConfigureEndpoints(context));
        });
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new UserPasswordSetupRequestedV1(Guid.NewGuid(), "ada@example.test", "Ada", "Yılmaz", "Teacher", "coaching",
                "token-1", new DateTime(2030, 1, 4, 18, 30, 0, DateTimeKind.Utc), DateTime.UtcNow));
            await TestHarnessTimeouts.UntilAsync(() => mail.Messages.Count >= 1);
            mail.Messages.Single().Body.Should().Contain("04.01.2030 21:30").And.NotContain("2030-01-04T18:30");
        }
        finally { await harness.Stop(); }
    }

    // LOW 14: the due reminder title does not promise exactly one day when the assignment was given late.
    [Fact]
    public void DueReminderTitle_DoesNotClaimExactlyOneDay() =>
        CoachingNotificationTexts.AssignmentDue("Çarpanlar", new DateTime(2030, 1, 4, 18, 0, 0, DateTimeKind.Utc)).Title
            .Should().NotContain("bir gün").And.Contain("yaklaşıyor");

    // HIGH 3: accepting a coaching invitation tells the inviter who joined.
    [Fact]
    public async Task AcceptingACoachingInvitation_TellsTheInviter()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var teacher = f.AddAccount("Teacher"); var student = f.AddAccount("Student");
        f.Accounts.Users[student] = f.Accounts.Users[student] with { FirstName = "Ada", LastName = "Yılmaz" };
        await f.Store.ProvisionAsync(teacher, "Teacher", default);
        var events = new Events();
        var service = new CoachingInvitationStore(f.Db, f.Accounts, f.Store, TimeProvider.System, events, new ConfigurationBuilder().Build());
        var invitation = await service.SendTeacherAsync(teacher, f.Accounts.Users[student].Email, null, default);

        await service.AcceptAsync(student, invitation.Id, default);
        await service.AcceptAsync(student, invitation.Id, default);

        var accepted = events.Messages.OfType<InvitationAcceptedEvent>().Should().ContainSingle("a repeated accept changes nothing").Subject;
        (accepted.InvitationId, accepted.InviterId, accepted.InviteeId, accepted.InviteeName, accepted.InvitationType, accepted.Product)
            .Should().Be((invitation.Id, teacher, student, "Ada Yılmaz", "StudentToTeacher", "coaching"));
    }

    // MEDIUM 6: the teacher invitation link opens the staff portal from its own setting.
    [Fact]
    public async Task TeacherInvitationLink_UsesTheStaffPortalSetting()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var inst = Guid.NewGuid(); f.Accounts.Institutions.Add(inst);
        var admin = f.AddAccount("InstitutionAdmin"); f.Accounts.Admins[admin] = new(false, inst);
        var teacher = f.AddAccount("Teacher");
        var events = new Events();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Coaching:PublicBaseUrl"] = "https://eduivme.example.test",
            ["Coaching:StaffPortalUrl"] = "https://staff.example.test/staff/"
        }).Build();
        var service = new CoachingInvitationStore(f.Db, f.Accounts, f.Store, TimeProvider.System, events, configuration);

        await service.SendInstitutionAsync(admin, inst, f.Accounts.Users[teacher].Email, "Teacher", null, null, default);

        events.Messages.OfType<InvitationCreatedEvent>().Single().Link.Should().Be("https://staff.example.test/staff/?product=coaching");
    }

    // MEDIUM 5: when subscriptions are enforced, an expired teacher or institution cannot invite, and a pending invitation
    // of theirs cannot be accepted.
    [Fact]
    public async Task ExpiredTeacher_CannotInvite_AndTheirInvitationCannotBeAccepted()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var teacher = f.AddAccount("Teacher"); var student = f.AddAccount("Student");
        await f.Store.ProvisionAsync(teacher, "Teacher", default);
        var plan = new CoachingSubscriptionPlan { Slug = "ogretmen", Name = "Öğretmen", Audience = "Teacher", DurationDays = 30 };
        var subscription = new CoachingSubscription { PlanId = plan.Id, UserId = teacher, Status = "Active",
            StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(20) };
        f.Db.AddRange(new CoachingSubscriptionSettings { RequireActiveSubscription = true }, plan, subscription);
        await f.Db.SaveChangesAsync();
        var service = new CoachingInvitationStore(f.Db, f.Accounts, f.Store, TimeProvider.System, new Events(), new ConfigurationBuilder().Build());
        var invitation = await service.SendTeacherAsync(teacher, f.Accounts.Users[student].Email, null, default);

        subscription.EndDate = DateTime.UtcNow.AddDays(-1);
        await f.Db.SaveChangesAsync();

        var send = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SendTeacherAsync(teacher, "yeni@example.test", null, default));
        send.Message.Should().Contain("abonel");
        var accept = await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(student, invitation.Id, default));
        accept.Message.Should().Contain("abonel");
        f.Db.CoachingTeacherStudentAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpiredInstitution_CannotInvite_WhileAnActiveOneCan()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var inst = Guid.NewGuid(); f.Accounts.Institutions.Add(inst);
        var admin = f.AddAccount("InstitutionAdmin"); f.Accounts.Admins[admin] = new(false, inst);
        var plan = new CoachingSubscriptionPlan { Slug = "kurum", Name = "Kurum", Audience = "Institution", DurationDays = 365 };
        var subscription = new CoachingSubscription { PlanId = plan.Id, InstitutionId = inst, Status = "Active",
            StartDate = DateTime.UtcNow.AddDays(-400), EndDate = DateTime.UtcNow.AddDays(-35) };
        f.Db.AddRange(new CoachingSubscriptionSettings { RequireActiveSubscription = true }, plan, subscription);
        await f.Db.SaveChangesAsync();
        var service = new CoachingInvitationStore(f.Db, f.Accounts, f.Store, TimeProvider.System, new Events(), new ConfigurationBuilder().Build());

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SendInstitutionAsync(admin, inst, "ogrenci@example.test", "Student", null, null, default));
        subscription.EndDate = DateTime.UtcNow.AddDays(30);
        await f.Db.SaveChangesAsync();
        (await service.SendInstitutionAsync(admin, inst, "ogrenci@example.test", "Student", null, null, default)).Should().NotBeNull();
    }

    [Fact]
    public async Task WithoutEnforcement_InvitationsStayOpen()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var teacher = f.AddAccount("Teacher");
        await f.Store.ProvisionAsync(teacher, "Teacher", default);
        f.Db.Add(new CoachingSubscriptionSettings { RequireActiveSubscription = false });
        await f.Db.SaveChangesAsync();
        var service = new CoachingInvitationStore(f.Db, f.Accounts, f.Store, TimeProvider.System, new Events(), new ConfigurationBuilder().Build());
        (await service.SendTeacherAsync(teacher, "ogrenci@example.test", null, default)).Should().NotBeNull();
    }

    // LOW 11: two messages sent at once conflict on the conversation instead of losing a count (and notifying twice).
    [Fact]
    public async Task ConversationCountIsAConcurrencyToken()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        f.Db.Model.FindEntityType(typeof(CoachingConversation))!.FindProperty(nameof(CoachingConversation.MessageCount))!
            .IsConcurrencyToken.Should().BeTrue();
    }

    private static CoachingSubscriptionNoticeV1 Notice(string kind, Guid? user, string? email, string audience, string? detail = null) =>
        new(Guid.NewGuid(), user, email, kind, audience, audience == "Institution" ? "Kurum yıllık" : "Bireysel aylık",
            new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc), detail, DateTime.UtcNow);

    private static async Task<(ServiceProvider, RecordingEmailQueue)> StartNoticeHarnessAsync(Action<PublicAppUrlOptions> urls)
    {
        var mail = new RecordingEmailQueue();
        var services = new ServiceCollection();
        services.AddSingleton<ICoachingNotificationDispatcher>(new RecordingDispatcher());
        services.AddSingleton<IEmailDeliveryQueue>(mail);
        services.Configure(urls);
        services.Configure<CoachingNoticeOptions>(options => options.AdminAlertEmail = "finans@example.test");
        services.AddMassTransitTestHarness(x =>
        {
            x.UseSuiteTimeouts();
            x.AddConsumer<CoachingSubscriptionNoticeConsumer>();
            x.UsingInMemory((context, config) => config.ConfigureEndpoints(context));
        });
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<ITestHarness>().Start();
        return (provider, mail);
    }

    private sealed class SwitchableDirectory : IRecipientDirectory
    {
        public bool Down { get; set; }
        public Dictionary<Guid, RecipientContact> Contacts { get; } = [];
        public Task<IReadOnlyDictionary<Guid, RecipientContact>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
            Down ? throw new RecipientDirectoryUnavailableException("Identity is down.", new HttpRequestException("refused"))
                : Task.FromResult<IReadOnlyDictionary<Guid, RecipientContact>>(userIds.Where(Contacts.ContainsKey).ToDictionary(x => x, x => Contacts[x]));
    }

    private sealed class MutableClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    private sealed class RecordingDispatcher : ICoachingNotificationDispatcher
    {
        public ConcurrentQueue<(Guid[] RecipientIds, string Title, string Message, string Type)> Calls { get; } = new();
        public Task SendAsync(Guid eventMessageId, IReadOnlyCollection<Guid> recipientIds, string title, string message,
            string type, string relatedEntityId, CancellationToken cancellationToken)
        {
            Calls.Enqueue((recipientIds.ToArray(), title, message, type));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmailQueue : IEmailDeliveryQueue
    {
        public ConcurrentQueue<(string Recipient, string Subject, string Body)> Messages { get; } = new();
        public Task QueueAsync(Guid messageId, string consumerType, string recipient, string subject, string body, Guid? subjectUserId = null,
            CancellationToken cancellationToken = default)
        {
            Messages.Enqueue((recipient, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class Events : Coaching.Application.Interfaces.ICoachingEventPublisher
    {
        public ConcurrentQueue<object> Messages { get; } = new();
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            Messages.Enqueue(message);
            return Task.CompletedTask;
        }
    }
}
