using System.Text;
using Coaching.API.Controllers;
using Coaching.Application.Attachments;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.ExportCoachingData;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.API.IntegrationTests;

// Roadmap O3: one-to-one messages between a teacher and a student, and between a teacher and a student's parent,
// with files, read receipts and notifications. The institution can review a conversation with a recorded reason;
// KVKK erasure, export and retention cover the messages.
public sealed class CoachingMessagingTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\nödev sorusu");

    [Fact]
    public async Task ATeacherAndTheirStudentWriteToEachOtherWithReadReceiptsAndOneNotificationPerUnreadBatch()
    {
        await using var s = await Setup.CreateAsync();
        var contact = (await s.Messages.ContactsAsync(s.Student, default)).Should().ContainSingle().Subject;
        (contact.UserId, contact.Name, contact.Role, contact.StudentId).Should().Be((s.Teacher, "Can Öğretmen", "Teacher", s.Student));

        var started = await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default);
        (await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default)).Id.Should().Be(started.Id, "a pair has one conversation");
        (started.Kind, started.OtherUserId, started.OtherName, started.OtherRole, started.CanSend).Should().Be(("TeacherStudent", s.Teacher, "Can Öğretmen", "Teacher", true));

        await s.Messages.SendAsync(s.Student, started.Id, new("Merhaba hocam, 5. soruyu anlamadım.", null), default);
        s.Clock.Advance(TimeSpan.FromMinutes(1));
        await s.Messages.SendAsync(s.Student, started.Id, new("Fotoğrafını da ekleyeyim mi?", null), default);

        var teacherList = await s.Messages.ListAsync(s.Teacher, 1, 20, default);
        var item = teacherList.Items.Should().ContainSingle().Subject;
        (item.OtherName, item.OtherRole, item.UnreadCount, item.LastMessagePreview).Should().Be(("Ada Yılmaz", "Student", 2, "Fotoğrafını da ekleyeyim mi?"));
        (await s.Messages.UnreadCountAsync(s.Teacher, default)).Should().Be(1, "the badge counts conversations with unread messages");
        var notice = s.Events.Messages.OfType<CoachingMessageReceivedEvent>().Should().ContainSingle("one notification per unread batch").Subject;
        (notice.ConversationId, notice.RecipientUserId, notice.RecipientRole, notice.SenderName).Should().Be((started.Id, s.Teacher, "Teacher", "Ada Yılmaz"));

        var thread = await s.Messages.GetAsync(s.Teacher, started.Id, null, default);
        thread.Messages.Select(x => x.Body).Should().Equal("Merhaba hocam, 5. soruyu anlamadım.", "Fotoğrafını da ekleyeyim mi?");
        thread.Messages.Should().OnlyContain(x => !x.Mine);
        (await s.Messages.ListAsync(s.Teacher, 1, 20, default)).Items.Single().UnreadCount.Should().Be(0, "opening the conversation reads it");
        (await s.Messages.GetAsync(s.Student, started.Id, null, default)).Messages.Should().OnlyContain(x => x.Mine && x.ReadByOther);

        s.Clock.Advance(TimeSpan.FromMinutes(1));
        var reply = await s.Messages.SendAsync(s.Teacher, started.Id, new("Ekle, birlikte bakalım.", null), default);
        reply.Mine.Should().BeTrue(); reply.ReadByOther.Should().BeFalse();
        s.Events.Messages.OfType<CoachingMessageReceivedEvent>().Last().Should().Match<CoachingMessageReceivedEvent>(x =>
            x.RecipientUserId == s.Student && x.RecipientRole == "Student" && x.SenderName == "Can Öğretmen");
    }

    [Fact]
    public async Task ALinkedParentWritesToTheChildsTeacherAndTheChildDoesNotSeeIt()
    {
        await using var s = await Setup.CreateAsync();
        var parent = s.Person("Parent", "Ayşe", "Yılmaz");
        s.LinkParent(parent, s.Student);

        var contact = (await s.Messages.ContactsAsync(parent, default)).Should().ContainSingle().Subject;
        (contact.UserId, contact.Role, contact.StudentId, contact.StudentName).Should().Be((s.Teacher, "Teacher", s.Student, "Ada Yılmaz"));
        (await s.Messages.ContactsAsync(s.Teacher, default)).Should().Contain(x => x.UserId == parent && x.Role == "Parent" && x.StudentName == "Ada Yılmaz")
            .And.Contain(x => x.UserId == s.Student && x.Role == "Student");

        var conversation = await s.Messages.StartAsync(s.Teacher, new(parent, s.Student), default);
        conversation.Kind.Should().Be("TeacherParent");
        (await s.Messages.StartAsync(parent, new(s.Teacher, s.Student), default)).Id.Should().Be(conversation.Id);
        await s.Messages.SendAsync(s.Teacher, conversation.Id, new("Ada bu hafta çok iyi çalıştı.", null), default);

        s.Events.Messages.OfType<CoachingMessageReceivedEvent>().Single().Should().Match<CoachingMessageReceivedEvent>(x => x.RecipientUserId == parent && x.RecipientRole == "Parent");
        (await s.Messages.GetAsync(parent, conversation.Id, null, default)).Messages.Single().Body.Should().Be("Ada bu hafta çok iyi çalıştı.");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Messages.GetAsync(s.Student, conversation.Id, null, default));
        (await s.Messages.ListAsync(s.Student, 1, 20, default)).Items.Should().BeEmpty();

        var stranger = s.Person("Parent", "Başka", "Veli");
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Messages.StartAsync(stranger, new(s.Teacher, s.Student), default));
    }

    [Fact]
    public async Task OnlyLinkedPeopleCanStartReadOrWrite()
    {
        await using var s = await Setup.CreateAsync();
        var otherTeacher = s.Person("Teacher", "Başka", "Öğretmen");
        var conversation = await s.Messages.StartAsync(s.Teacher, new(s.Student, s.Student), default);

        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Messages.StartAsync(otherTeacher, new(s.Student, s.Student), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Messages.GetAsync(otherTeacher, conversation.Id, null, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Messages.SendAsync(otherTeacher, conversation.Id, new("Merhaba", null), default));
        (await s.Messages.ContactsAsync(otherTeacher, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task WhenTheLinkEndsTheConversationStaysReadableButClosed()
    {
        await using var s = await Setup.CreateAsync();
        var conversation = await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default);
        await s.Messages.SendAsync(s.Student, conversation.Id, new("Teşekkürler hocam.", null), default);
        (await s.F.Db.CoachingTeacherStudentAssignments.SingleAsync()).SetActive(false, s.Teacher);
        await s.F.Db.SaveChangesAsync();

        (await s.Messages.ListAsync(s.Student, 1, 20, default)).Items.Single().CanSend.Should().BeFalse();
        var closed = await Assert.ThrowsAsync<BusinessRuleException>(() => s.Messages.SendAsync(s.Teacher, conversation.Id, new("Son not", null), default));
        closed.Code.Should().Be("Messaging.Closed");
        (await s.Messages.GetAsync(s.Teacher, conversation.Id, null, default)).Messages.Should().ContainSingle();
        (await s.Messages.ContactsAsync(s.Student, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task MessagesNeedTextOrAFileAndStayWithinTheLimit()
    {
        await using var s = await Setup.CreateAsync();
        var conversation = await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default);

        await Assert.ThrowsAsync<ArgumentException>(() => s.Messages.SendAsync(s.Student, conversation.Id, new("   ", null), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Messages.SendAsync(s.Student, conversation.Id, new(new string('a', 4001), null), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Messages.SendAsync(s.Student, conversation.Id, new("Bip\u0007", null), default));
        (await s.Messages.SendAsync(s.Student, conversation.Id, new("  Satır 1\nSatır 2  ", null), default)).Body.Should().Be("Satır 1\nSatır 2");
    }

    [Fact]
    public async Task FilesAreCheckedScannedAndOnlyOpenedByTheConversation()
    {
        await using var s = await Setup.CreateAsync();
        var conversation = await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default);

        var file = await s.Messages.UploadAsync(s.Student, conversation.Id, "soru.pdf", "application/pdf", new MemoryStream(Pdf), default);
        (file.FileName, file.ContentType, file.SizeBytes).Should().Be(("soru.pdf", "application/pdf", Pdf.Length));
        var sent = await s.Messages.SendAsync(s.Student, conversation.Id, new(null, [file.Id]), default);
        sent.Attachments.Should().ContainSingle(x => x.Id == file.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Messages.SendAsync(s.Student, conversation.Id, new("Tekrar", [file.Id]), default));

        await using (var opened = await s.Messages.OpenAttachmentAsync(s.Teacher, conversation.Id, file.Id, default))
        {
            using var copy = new MemoryStream(); await opened.Content.CopyToAsync(copy);
            copy.ToArray().Should().Equal(Pdf); opened.FileName.Should().Be("soru.pdf");
        }
        var outsider = s.Person("Teacher", "Başka", "Öğretmen");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Messages.OpenAttachmentAsync(outsider, conversation.Id, file.Id, default));

        await Assert.ThrowsAsync<ArgumentException>(() => s.Messages.UploadAsync(s.Student, conversation.Id, "kurulum.exe", "application/octet-stream", new MemoryStream([1, 2]), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Messages.UploadAsync(s.Student, conversation.Id, "sahte.pdf", "application/pdf", new MemoryStream([1, 2, 3]), default));
        s.Scanner.Infected = true;
        await Assert.ThrowsAsync<ArgumentException>(() => s.Messages.UploadAsync(s.Student, conversation.Id, "virus.pdf", "application/pdf", new MemoryStream(Pdf), default));
        (await s.F.Db.Set<CoachingMessageAttachment>().CountAsync()).Should().Be(1, "rejected files leave no record");
        Directory.EnumerateFiles(s.Root, "*", SearchOption.AllDirectories).Should().ContainSingle("rejected files leave nothing on disk");

        s.Scanner.Infected = false;
        var teachersFile = await s.Messages.UploadAsync(s.Teacher, conversation.Id, "cevap.pdf", "application/pdf", new MemoryStream(Pdf), default);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Messages.SendAsync(s.Student, conversation.Id, new("Benim değil", [teachersFile.Id]), default));
    }

    [Fact]
    public async Task TheInstitutionReviewsAConversationOnlyWithAReasonAndBothSidesSeeThatItDid()
    {
        await using var s = await Setup.CreateAsync(institution: true);
        var conversation = await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default);
        await s.Messages.SendAsync(s.Student, conversation.Id, new("Ödevimi yapamadım.", null), default);

        var listed = (await s.Messages.ListInstitutionAsync(s.Admin, s.Institution!.Value, 1, 20, default)).Items.Should().ContainSingle().Subject;
        (listed.TeacherName, listed.StudentName, listed.CounterpartName, listed.MessageCount).Should().Be(("Can Öğretmen", "Ada Yılmaz", "Ada Yılmaz", 1));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Messages.ReviewAsync(s.Admin, s.Institution.Value, conversation.Id, "bak", default));

        var reviewed = await s.Messages.ReviewAsync(s.Admin, s.Institution.Value, conversation.Id, "Veli şikâyeti üzerine inceleme", default);
        reviewed.Messages.Single().Body.Should().Be("Ödevimi yapamadım.");
        var access = await s.F.Db.Set<CoachingConversationAccess>().SingleAsync();
        (access.ConversationId, access.ViewerUserId, access.Reason).Should().Be((conversation.Id, s.Admin, "Veli şikâyeti üzerine inceleme"));
        (await s.Messages.ListAsync(s.Teacher, 1, 20, default)).Items.Single().UnreadCount.Should().Be(1, "a review is not reading as the teacher");
        (await s.Messages.GetAsync(s.Student, conversation.Id, null, default)).LastInstitutionReviewAt.Should().Be(s.Clock.GetUtcNow().UtcDateTime);

        var otherAdmin = s.Person("InstitutionAdmin", "Diğer", "Yönetici"); var otherInstitution = Guid.NewGuid();
        s.F.Accounts.Institutions.Add(otherInstitution); s.F.Accounts.Admins[otherAdmin] = new(false, otherInstitution);
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Messages.ReviewAsync(otherAdmin, s.Institution.Value, conversation.Id, "Merak ettim, bakıyorum", default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Messages.ReviewAsync(otherAdmin, otherInstitution, conversation.Id, "Merak ettim, bakıyorum", default));
    }

    // K2 (31.10.2026): when the pair moves to another institution, that institution reviews only messages from its own period.
    [Fact]
    public async Task ANewInstitutionReviewsOnlyTheMessagesSentSinceTheLinkWasMadeThere()
    {
        await using var s = await Setup.CreateAsync(institution: true);
        var conversation = await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default);
        await s.Messages.SendAsync(s.Student, conversation.Id, new("Eski kurumdaki mesaj", null), default);

        var oldLink = await s.F.Db.CoachingTeacherStudentAssignments.SingleAsync();
        oldLink.SetActive(false, s.Admin);
        var next = Guid.NewGuid(); s.F.Accounts.Institutions.Add(next);
        var nextAdmin = s.Person("InstitutionAdmin", "Yeni", "Yönetici"); s.F.Accounts.Admins[nextAdmin] = new(false, next);
        var newLink = CoachingTeacherStudentAssignment.Create(s.Teacher, s.Student, next, null, nextAdmin);
        s.F.Db.Add(newLink);
        s.F.Db.Entry(newLink).Property(x => x.StartDate).CurrentValue = s.Clock.GetUtcNow().UtcDateTime.AddHours(1);
        await s.F.Db.SaveChangesAsync();
        s.Clock.Advance(TimeSpan.FromHours(2));
        await s.Messages.SendAsync(s.Student, conversation.Id, new("Yeni kurumdaki mesaj", null), default);

        var reviewed = await s.Messages.ReviewAsync(nextAdmin, next, conversation.Id, "Veli şikâyeti üzerine inceleme", default);

        reviewed.Messages.Select(x => x.Body).Should().Equal("Yeni kurumdaki mesaj");
    }

    [Fact]
    public async Task IndependentCoachingConversationsAreNotVisibleToAnyInstitution()
    {
        await using var s = await Setup.CreateAsync();
        await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default);
        var admin = s.Person("InstitutionAdmin", "Kurum", "Yönetici"); var institution = Guid.NewGuid();
        s.F.Accounts.Institutions.Add(institution); s.F.Accounts.Admins[admin] = new(false, institution);

        (await s.Messages.ListInstitutionAsync(admin, institution, 1, 20, default)).Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("Parent")]
    public async Task ErasingAnyParticipantRemovesTheirConversationsAndFiles(string erased)
    {
        await using var s = await Setup.CreateAsync();
        var parent = s.Person("Parent", "Ayşe", "Yılmaz"); s.LinkParent(parent, s.Student);
        var withStudent = await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default);
        var withParent = await s.Messages.StartAsync(parent, new(s.Teacher, s.Student), default);
        var file = await s.Messages.UploadAsync(s.Student, withStudent.Id, "soru.pdf", "application/pdf", new MemoryStream(Pdf), default);
        await s.Messages.SendAsync(s.Student, withStudent.Id, new("Dosya ekte", [file.Id]), default);
        await s.Messages.SendAsync(parent, withParent.Id, new("Merhaba", null), default);
        var subject = erased switch { "Student" => s.Student, "Teacher" => s.Teacher, _ => parent };

        var requestId = Guid.NewGuid();
        var request = new PersonalDataErasureExecutionRequestedV1(requestId, requestId, subject, DateTime.UtcNow, PersonalDataScope.Coaching);
        var assessment = await new CoachingErasureAssessmentService(s.F.Db, TimeProvider.System)
            .AssessAsync(new(request.EventId, request.RequestId, subject, request.AuthorizedAt, DryRun: true, request.Scope), default);
        assessment.Should().NotBeNull();
        var storage = new LocalAssignmentAttachmentStorage(Options.Create(new AssignmentAttachmentOptions { RootPath = s.Root }));
        await new CoachingErasureExecutionService(s.F.Db, storage, TimeProvider.System).ExecuteAsync(request, default);

        var remaining = await s.F.Db.Set<CoachingConversation>().Select(x => x.Id).ToListAsync();
        if (erased == "Parent") remaining.Should().Equal(withStudent.Id);
        else if (erased == "Student") remaining.Should().Equal(new[] { withParent.Id }, "the parent's own conversation about the child is the parent's data");
        else remaining.Should().BeEmpty();
        (await s.F.Db.Set<CoachingMessage>().CountAsync()).Should().Be(remaining.Count);
        (await s.F.Db.Set<CoachingMessageAttachment>().AnyAsync()).Should().Be(erased == "Parent");
        Directory.EnumerateFiles(s.Root, "*", SearchOption.AllDirectories).Any().Should().Be(erased == "Parent");
    }

    [Fact]
    public async Task TheStudentsExportContainsTheirOwnConversationsOnly()
    {
        await using var s = await Setup.CreateAsync();
        var parent = s.Person("Parent", "Ayşe", "Yılmaz"); s.LinkParent(parent, s.Student);
        var own = await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default);
        await s.Messages.SendAsync(s.Student, own.Id, new("Benim mesajım", null), default);
        var parents = await s.Messages.StartAsync(parent, new(s.Teacher, s.Student), default);
        await s.Messages.SendAsync(parent, parents.Id, new("Velinin mesajı", null), default);

        var export = await new CoachingDataExportRepository(s.F.Db).ExportStudentDataAsync(s.Student, DateTimeOffset.UtcNow, default);

        export.SchemaVersion.Should().Be("1.10");
        var conversation = export.Conversations.Should().ContainSingle().Subject;
        (conversation.Id, conversation.TeacherUserId).Should().Be((own.Id, s.Teacher));
        conversation.Messages.Should().ContainSingle(x => x.Body == "Benim mesajım" && x.SenderUserId == s.Student);
    }

    [Fact]
    public async Task OldConversationsAndUnsentFilesAreRemovedByTheRetentionRule()
    {
        await using var s = await Setup.CreateAsync();
        var old = await s.Messages.StartAsync(s.Student, new(s.Teacher, s.Student), default);
        var oldFile = await s.Messages.UploadAsync(s.Student, old.Id, "eski.pdf", "application/pdf", new MemoryStream(Pdf), default);
        await s.Messages.SendAsync(s.Student, old.Id, new("Eski", [oldFile.Id]), default);
        await s.Messages.UploadAsync(s.Student, old.Id, "gonderilmedi.pdf", "application/pdf", new MemoryStream(Pdf), default);

        s.Clock.Advance(TimeSpan.FromDays(2));
        (await s.Messages.CleanupAsync(default)).Should().Be(1, "only the file that was never sent is removed after a day");
        (await s.F.Db.Set<CoachingMessageAttachment>().SingleAsync()).Id.Should().Be(oldFile.Id);

        s.Clock.Advance(TimeSpan.FromDays(CoachingMessagingService.RetentionDays));
        await s.Messages.CleanupAsync(default);
        (await s.F.Db.Set<CoachingConversation>().AnyAsync()).Should().BeFalse();
        (await s.F.Db.Set<CoachingMessage>().AnyAsync()).Should().BeFalse();
        Directory.EnumerateFiles(s.Root, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public void WritesAreLimitedAndTheReviewIsForInstitutionAdminsOnly()
    {
        var send = typeof(CoachingMessagesController).GetMethod(nameof(CoachingMessagesController.Send))!;
        send.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Should().ContainSingle();
        ((RequestSizeLimitAttribute)send.GetCustomAttributes(typeof(RequestSizeLimitAttribute), true).Single()).Should().NotBeNull();
        var upload = typeof(CoachingMessagesController).GetMethod(nameof(CoachingMessagesController.Upload))!;
        upload.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Should().ContainSingle();
        typeof(CoachingMessagesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().Contain(x => x.Roles == "Student,Teacher,Parent");
        typeof(CoachingInstitutionMessagesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().Contain(x => x.Roles == "SystemAdmin,InstitutionAdmin,InstitutionOwner");
    }

    internal sealed class Setup : IAsyncDisposable
    {
        public CoachingOwnedProfilePersistenceTests.Fixture F { get; } = new();
        public Clock Clock { get; } = new();
        public Scanner Scanner { get; } = new();
        public Publisher Events { get; } = new();
        public Parents ParentLinks { get; } = new();
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "coaching-messages-" + Guid.NewGuid().ToString("N"));
        public Guid Teacher, Student, Admin;
        public Guid? Institution;

        public CoachingMessagingService Messages => new(F.Db, F.Accounts, ParentLinks, F.Store,
            new LocalAssignmentAttachmentStorage(Options.Create(new AssignmentAttachmentOptions { RootPath = Root })), Scanner, Clock, Events);

        public static async Task<Setup> CreateAsync(bool institution = false)
        {
            var s = new Setup();
            s.Teacher = s.Person("Teacher", "Can", "Öğretmen");
            s.Student = s.Person("Student", "Ada", "Yılmaz");
            if (institution)
            {
                s.Institution = Guid.NewGuid(); s.F.Accounts.Institutions.Add(s.Institution.Value);
                s.Admin = s.Person("InstitutionAdmin", "Kurum", "Yönetici"); s.F.Accounts.Admins[s.Admin] = new(false, s.Institution.Value);
            }
            var link = CoachingTeacherStudentAssignment.Create(s.Teacher, s.Student, s.Institution, null, s.Teacher);
            s.F.Db.CoachingTeacherStudentAssignments.Add(link);
            // The link exists before the conversation (an institution reviews messages from its link onward).
            s.F.Db.Entry(link).Property(x => x.StartDate).CurrentValue = s.Clock.GetUtcNow().UtcDateTime.AddDays(-1);
            await s.F.Db.SaveChangesAsync();
            return s;
        }

        public Guid Person(string role, string first, string last)
        {
            var id = F.AddAccount(role);
            F.Accounts.Users[id] = F.Accounts.Users[id] with { FirstName = first, LastName = last };
            return id;
        }

        public void LinkParent(Guid parent, Guid student)
        {
            F.Accounts.Representatives[(parent, student)] = new(Guid.NewGuid(), "Mother");
            ParentLinks.Links.Add(new(parent, Name(parent), student, Name(student), "Mother"));
        }

        private string Name(Guid id) => $"{F.Accounts.Users[id].FirstName} {F.Accounts.Users[id].LastName}";

        public async ValueTask DisposeAsync()
        {
            await F.DisposeAsync();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    internal sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    internal sealed class Scanner : IAssignmentAttachmentScanner
    {
        public bool Infected { get; set; }
        public Task<AttachmentScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AttachmentScanResult(!Infected, Infected ? "Eicar-Test-Signature" : null));
    }

    internal sealed class Publisher : ICoachingEventPublisher
    {
        public List<object> Messages { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        { Messages.Add(message); return Task.CompletedTask; }
    }

    internal sealed class Parents : ICoachingParentDirectory
    {
        public List<CoachingParentLink> Links { get; } = [];
        public Task<CoachingParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct) =>
            Task.FromResult(new CoachingParentSummaryRecipientPage([], 0));
        public Task<IReadOnlyList<CoachingParentLink>> GetParentLinksAsync(IReadOnlyCollection<Guid> studentUserIds, Guid? parentUserId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CoachingParentLink>>(Links.Where(x => studentUserIds.Contains(x.StudentUserId) || x.ParentUserId == parentUserId).ToArray());
    }
}
