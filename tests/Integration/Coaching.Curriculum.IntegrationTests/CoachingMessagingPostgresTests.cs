using System.Text;
using Coaching.Application.Attachments;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap O3: the messaging queries (last message per conversation, unread counts, institution list, retention and erasure)
// run on PostgreSQL with the migration's constraints.
public sealed class CoachingMessagingPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MessagingRunsOnPostgresWithItsConstraints()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var root = Path.Combine(Path.GetTempPath(), "coaching-messages-pg-" + Guid.NewGuid().ToString("N"));
        try
        {
            var institution = Guid.NewGuid();
            var accounts = new Accounts(institution);
            var teacher = accounts.Add("Teacher", "Can"); var student = accounts.Add("Student", "Ada"); var parent = accounts.Add("Parent", "Ayşe");
            var admin = accounts.Add("InstitutionAdmin", "Kurum");
            accounts.Representatives.Add((parent, student));
            var parents = new Parents([new(parent, "Ayşe Test", student, "Ada Test", "Mother")]);
            var store = new CoachingProfileStore(f.Db, accounts);
            await store.ProvisionAsync(teacher, "Teacher", default); await store.ProvisionAsync(student, "Student", default, new DateOnly(2010, 5, 1));
            f.Db.CoachingTeacherStudentAssignments.Add(CoachingTeacherStudentAssignment.Create(teacher, student, institution, null, teacher));
            await f.Db.SaveChangesAsync();
            var clock = new Clock();
            CoachingMessagingService Service() => new(f.Db, accounts, parents, new CoachingProfileStore(f.Db, accounts),
                new LocalAssignmentAttachmentStorage(Options.Create(new AssignmentAttachmentOptions { RootPath = root })), new DevelopmentAttachmentScanner(), clock);

            var withStudent = await Service().StartAsync(student, new(teacher, student), default);
            await Service().SendAsync(student, withStudent.Id, new("Birinci", null), default);
            clock.Advance(TimeSpan.FromMinutes(1));
            await Service().SendAsync(student, withStudent.Id, new("İkinci", null), default);
            var withParent = await Service().StartAsync(parent, new(teacher, student), default);
            await Service().SendAsync(parent, withParent.Id, new("Veliden", null), default);
            f.Db.ChangeTracker.Clear();

            var list = await Service().ListAsync(teacher, 1, 20, default);
            Assert.Equal(2, list.TotalCount);
            var studentItem = list.Items.Single(x => x.Id == withStudent.Id);
            Assert.Equal((2, "İkinci", true), (studentItem.UnreadCount, studentItem.LastMessagePreview, studentItem.CanSend));
            Assert.True(list.Items.Single(x => x.Id == withParent.Id).CanSend);
            Assert.Equal(2, await Service().UnreadCountAsync(teacher, default));
            Assert.Equal(2, (await Service().ContactsAsync(teacher, default)).Count); // the student and the parent

            Assert.Equal(["Birinci", "İkinci"], (await Service().GetAsync(teacher, withStudent.Id, null, default)).Messages.Select(x => x.Body));
            Assert.Equal(1, await Service().UnreadCountAsync(teacher, default));
            var file = await Service().UploadAsync(teacher, withStudent.Id, "cevap.pdf", "application/pdf", new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4 cevap")), default);
            await Service().SendAsync(teacher, withStudent.Id, new(null, [file.Id]), default);
            f.Db.ChangeTracker.Clear();
            var studentView = await Service().GetAsync(student, withStudent.Id, null, default);
            Assert.Equal(["Birinci", "İkinci", ""], studentView.Messages.Select(x => x.Body));
            Assert.All(studentView.Messages.Take(2), x => Assert.True(x.ReadByOther));
            Assert.Equal("cevap.pdf", studentView.Messages[2].Attachments.Single().FileName);

            var institutionList = await Service().ListInstitutionAsync(admin, institution, 1, 20, default);
            Assert.Equal(2, institutionList.TotalCount);
            await Service().ReviewAsync(admin, institution, withStudent.Id, "Velinin talebiyle inceleme", default);
            f.Db.ChangeTracker.Clear();
            Assert.NotNull((await Service().ListInstitutionAsync(admin, institution, 1, 20, default)).Items.Single(x => x.Id == withStudent.Id).LastReviewedAt);
            await using (var opened = await Service().OpenAttachmentAsync(admin, withStudent.Id, file.Id, default)) Assert.Equal("cevap.pdf", opened.FileName);

            var export = await new CoachingDataExportRepository(f.Db).ExportStudentDataAsync(student, DateTimeOffset.UtcNow, default);
            Assert.Equal(3, Assert.Single(export.Conversations!).Messages.Count);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                f.Db.Add(CoachingConversation.Create(teacher, student, student, CoachingConversationKind.TeacherStudent, institution, DateTime.UtcNow));
                await f.Db.SaveChangesAsync();
            });
            f.Db.ChangeTracker.Clear();

            var requestId = Guid.NewGuid();
            var request = new PersonalDataErasureExecutionRequestedV1(requestId, requestId, student, DateTime.UtcNow, PersonalDataScope.Coaching);
            await new CoachingErasureAssessmentService(f.Db, TimeProvider.System).AssessAsync(new(request.EventId, request.RequestId, student, request.AuthorizedAt, DryRun: true, request.Scope), default);
            await new CoachingErasureExecutionService(f.Db, new LocalAssignmentAttachmentStorage(Options.Create(new AssignmentAttachmentOptions { RootPath = root })), TimeProvider.System)
                .ExecuteAsync(request, default);
            f.Db.ChangeTracker.Clear();
            // Coaching audit K1: the teacher–parent conversation about the erased student goes with the student.
            Assert.Empty(await f.Db.Set<CoachingConversation>().Where(x => x.Id == withParent.Id).ToListAsync());
            Assert.Empty(await f.Db.Set<CoachingConversation>().ToListAsync());
            Assert.False(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any());

            clock.Advance(TimeSpan.FromDays(CoachingMessagingService.RetentionDays + 1));
            Assert.Equal(0, await Service().CleanupAsync(default));
            Assert.False(await f.Db.Set<CoachingMessage>().AnyAsync());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class Accounts(Guid institution) : ICoachingAccountDirectory
    {
        private readonly Dictionary<Guid, CoachingAccount> users = [];
        public HashSet<(Guid Parent, Guid Student)> Representatives { get; } = [];
        public Guid Add(string role, string name)
        {
            var id = Guid.NewGuid(); users[id] = new(id, name, "Test", $"{id:N}@example.test", true, true, [role], null); return id;
        }
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Where(users.ContainsKey).Select(x => users[x]).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken ct) => throw new NotSupportedException();
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<CoachingAdminAccessScope?>(users.TryGetValue(id, out var user) && user.HasRole("InstitutionAdmin") ? new(false, institution) : null);
        public Task<bool> IsActiveInstitutionAsync(Guid id, CancellationToken ct) => Task.FromResult(id == institution);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer, Guid target, CancellationToken ct) =>
            Task.FromResult(Representatives.Contains((viewer, target)) ? new CoachingAgreementRepresentativeAuthorization(Guid.NewGuid(), "Mother") : null);
    }

    private sealed class Parents(IReadOnlyList<CoachingParentLink> links) : ICoachingParentDirectory
    {
        public Task<CoachingParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<CoachingParentLink>> GetParentLinksAsync(IReadOnlyCollection<Guid> studentUserIds, Guid? parentUserId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CoachingParentLink>>(links.Where(x => studentUserIds.Contains(x.StudentUserId) || x.ParentUserId == parentUserId).ToArray());
    }
}
