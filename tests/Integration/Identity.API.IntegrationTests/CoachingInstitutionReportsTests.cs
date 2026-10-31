using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

// Roadmap O13: per-teacher workload, branches of one admin, and CSV bulk invitations / teacher links.
public sealed class CoachingInstitutionReportsTests
{
    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero); }
    private static readonly DateTime Now = new(2030, 3, 4, 9, 0, 0, DateTimeKind.Utc);

    private static (Guid Admin, Guid Institution) Institution(CoachingOwnedProfilePersistenceTests.Fixture f, params Guid[] more)
    {
        var institution = Guid.NewGuid(); f.Accounts.Institutions.Add(institution);
        foreach (var other in more) f.Accounts.Institutions.Add(other);
        var admin = f.AddAccount("InstitutionAdmin");
        f.Accounts.Admins[admin] = new(false, institution)
        {
            Institutions = [new(institution, "Merkez şube"), ..more.Select((x, i) => new CoachingAdminInstitutionRef(x, $"Şube {i + 2}"))]
        };
        return (admin, institution);
    }

    private static CoachingInstitutionWorkloadService Workload(CoachingOwnedProfilePersistenceTests.Fixture f) => new(f.Db, f.Accounts, new Clock(Now));

    private static CoachingInstitutionBulkService Bulk(CoachingOwnedProfilePersistenceTests.Fixture f)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Coaching:PublicBaseUrl"] = "https://onuraltintas.net" }).Build();
        var invitations = new CoachingInvitationStore(f.Db, f.Accounts, f.Store, new Clock(Now), new CoachingInstitutionMembershipTests.Events(), configuration);
        return new CoachingInstitutionBulkService(f.Db, f.Accounts, f.Store, invitations);
    }

    [Fact]
    public async Task WorkloadShowsEachTeachersStudentsGradingOverdueWorkMessagesAndSessions()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var (admin, inst) = Institution(f);
        var busy = f.AddAccount("Teacher"); var calm = f.AddAccount("Teacher");
        var a = f.AddAccount("Student"); var b = f.AddAccount("Student"); var lonely = f.AddAccount("Student");
        foreach (var teacher in new[] { busy, calm }) await f.Store.SetMembershipAsync(admin, inst, teacher, CoachingMemberRole.Teacher, true, default);
        foreach (var student in new[] { a, b, lonely }) await f.Store.SetMembershipAsync(admin, inst, student, CoachingMemberRole.Student, true, default);
        await f.Store.UpdateInstitutionStudentAsync(admin, inst, a, 8, busy, default);
        await f.Store.UpdateInstitutionStudentAsync(admin, inst, b, 8, busy, default);
        await f.Store.UpdateInstitutionStudentAsync(admin, inst, b, 8, calm, default);

        var graded = Assignment.Create(busy, "Teslim edilen", Now.AddDays(3), institutionId: inst); graded.AssignToStudents([a, b]); graded.SubmitAssignment(a);
        var late = Assignment.Create(busy, "Geciken", Now.AddDays(1), institutionId: inst); late.AssignToStudents([a]);
        var otherInstitution = Assignment.Create(calm, "Başka kurum", Now.AddDays(1), institutionId: Guid.NewGuid()); otherInstitution.AssignToStudents([b]); otherInstitution.SubmitAssignment(b);
        var conversation = CoachingConversation.Create(calm, b, b, CoachingConversationKind.TeacherStudent, inst, Now.AddHours(-2));
        var message = CoachingMessage.Create(conversation.Id, b, "Merhaba", false, Now.AddHours(-1)); conversation.Record(message, inst);
        var session = CoachingSession.Create(busy, "Seans", Now.AddDays(2), SessionType.OneOnOne); session.AddStudent(a);
        f.Db.AddRange(graded, late, otherInstitution, conversation, message, session); await f.Db.SaveChangesAsync();
        f.Db.Entry(late).Property(x => x.DueDate).CurrentValue = Now.AddDays(-1); await f.Db.SaveChangesAsync();

        var report = await Workload(f).GetAsync(admin, inst, default);
        Assert.Equal((3, 2, 1), (report.Students, report.Teachers, report.StudentsWithoutTeacher));
        var busyRow = Assert.Single(report.TeacherRows, x => x.TeacherUserId == busy);
        Assert.Equal((2, 1, 1, 0, 1), (busyRow.Students, busyRow.AwaitingGrading, busyRow.Overdue, busyRow.UnreadMessages, busyRow.UpcomingSessions));
        var calmRow = Assert.Single(report.TeacherRows, x => x.TeacherUserId == calm);
        Assert.Equal((1, 0, 0, 1), (calmRow.Students, calmRow.AwaitingGrading, calmRow.Overdue, calmRow.UnreadMessages));
        Assert.Equal((1, 1), (report.AwaitingGrading, report.Overdue));
        Assert.Equal(busy, report.TeacherRows[0].TeacherUserId); // most pending work first

        var stranger = f.AddAccount("InstitutionAdmin"); f.Accounts.Admins[stranger] = new(false, Guid.NewGuid());
        await Assert.ThrowsAsync<BusinessRuleException>(() => Workload(f).GetAsync(stranger, inst, default));
    }

    [Fact]
    public async Task AnAdminOfSeveralBranchesSeesEachBranchSummary()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var second = Guid.NewGuid();
        var (admin, inst) = Institution(f, second);
        var teacher = f.AddAccount("Teacher"); var student = f.AddAccount("Student"); var other = f.AddAccount("Student");
        await f.Store.SetMembershipAsync(admin, inst, teacher, CoachingMemberRole.Teacher, true, default);
        await f.Store.SetMembershipAsync(admin, inst, student, CoachingMemberRole.Student, true, default);
        f.Db.Add(CoachingInstitutionMembership.Create(second, other, CoachingMemberRole.Student, admin)); await f.Db.SaveChangesAsync();

        var branches = await Workload(f).BranchesAsync(admin, default);
        Assert.Equal(new[] { ("Merkez şube", true, 1, 1), ("Şube 2", false, 1, 0) },
            branches.Select(x => (x.Name, x.IsCurrent, x.Students, x.Teachers)).ToArray());
    }

    [Fact]
    public async Task BulkInvitationsArePreviewedLineByLineAndAppliedOnlyWhenClean()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var (admin, inst) = Institution(f);
        var teacher = f.AddAccount("Teacher"); var member = f.AddAccount("Student");
        await f.Store.SetMembershipAsync(admin, inst, teacher, CoachingMemberRole.Teacher, true, default);
        await f.Store.SetMembershipAsync(admin, inst, member, CoachingMemberRole.Student, true, default);
        var teacherEmail = f.Accounts.Users[teacher].Email; var memberEmail = f.Accounts.Users[member].Email;
        var bulk = Bulk(f);

        var messy = $"eposta;ogretmen_eposta\nyeni@example.test;{teacherEmail}\nYENI@example.test;\nhatali-adres;\n{memberEmail};\nikinci@example.test;yok@example.test\n";
        var preview = await bulk.PreviewInvitationsAsync(admin, inst, messy, default);
        Assert.True(preview.HasErrors);
        Assert.Equal(new[] { "Invite", "Error", "Error", "AlreadyMember", "Error" }, preview.Rows.Select(x => x.Action));
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, preview.Rows.Select(x => x.Line));
        await Assert.ThrowsAsync<ArgumentException>(() => bulk.ApplyInvitationsAsync(admin, inst, messy, preview.Fingerprint, default));

        var clean = $"e-posta,ogretmen_eposta\nyeni@example.test,{teacherEmail}\nikinci@example.test,\n{memberEmail},\n";
        var ok = await bulk.PreviewInvitationsAsync(admin, inst, clean, default);
        Assert.False(ok.HasErrors); Assert.Equal(2, ok.ToApply);
        await Assert.ThrowsAsync<BusinessRuleException>(() => bulk.ApplyInvitationsAsync(admin, inst, clean + "ucuncu@example.test,\n", ok.Fingerprint, default));
        var result = await bulk.ApplyInvitationsAsync(admin, inst, clean, ok.Fingerprint, default);
        Assert.Equal(2, result.Applied);
        var invitations = await f.Db.CoachingInvitations.OrderBy(x => x.RecipientEmail).ToListAsync();
        Assert.Equal(new[] { "ikinci@example.test", "yeni@example.test" }, invitations.Select(x => x.RecipientEmail));
        Assert.Equal(teacher, invitations[1].TeacherUserId);
        Assert.Equal("PendingInvitation", (await bulk.PreviewInvitationsAsync(admin, inst, clean, default)).Rows[0].Action);
    }

    [Fact]
    public async Task BulkTeacherLinksAddOnlyMissingLinksBetweenMembers()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var (admin, inst) = Institution(f);
        var teacher = f.AddAccount("Teacher"); var first = f.AddAccount("Student"); var second = f.AddAccount("Student"); var outsider = f.AddAccount("Student");
        await f.Store.SetMembershipAsync(admin, inst, teacher, CoachingMemberRole.Teacher, true, default);
        foreach (var s in new[] { first, second }) await f.Store.SetMembershipAsync(admin, inst, s, CoachingMemberRole.Student, true, default);
        await f.Store.UpdateInstitutionStudentAsync(admin, inst, first, 7, teacher, default);
        string Email(Guid id) => f.Accounts.Users[id].Email;
        var bulk = Bulk(f);

        var csv = $"ogrenci_eposta;ogretmen_eposta\n{Email(first)};{Email(teacher)}\n{Email(second)};{Email(teacher)}\n{Email(outsider)};{Email(teacher)}\n";
        var preview = await bulk.PreviewTeacherLinksAsync(admin, inst, csv, default);
        Assert.Equal(new[] { "AlreadyLinked", "Link", "Error" }, preview.Rows.Select(x => x.Action));
        var fixedCsv = string.Join('\n', csv.Split('\n').Where(x => !x.Contains(Email(outsider))));
        var clean = await bulk.PreviewTeacherLinksAsync(admin, inst, fixedCsv, default);
        var result = await bulk.ApplyTeacherLinksAsync(admin, inst, fixedCsv, clean.Fingerprint, default);
        Assert.Equal(1, result.Applied);
        Assert.Equal(2, await f.Db.CoachingTeacherStudentAssignments.CountAsync(x => x.TeacherUserId == teacher && x.IsActive));
        Assert.Equal(7, (await f.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == first)).GradeLevel);
    }
}
