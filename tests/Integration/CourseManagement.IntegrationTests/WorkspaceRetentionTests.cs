using CourseManagement.API;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace CourseManagement.IntegrationTests;

/// <summary>
/// maxkampüs data retention (user decisions 20.11.2026): warn 30 days before, delete the panel data a year after the plan
/// ended, delete a never-paid workspace 90 days after it was opened; the workspace and its owner stay.
/// </summary>
public sealed class WorkspaceRetentionTests
{
    private static readonly DateTimeOffset Opened = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ended = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static WorkspaceRetentionStep Decide(DateTimeOffset now, DateTimeOffset? lastEnd = null, bool require = true, bool running = false,
        DateTimeOffset? warnedFor = null, DateTimeOffset? warnedAt = null, DateTimeOffset? purgedAt = null) =>
        WorkspaceRetention.Decide(require, running, lastEnd, Opened, warnedFor, warnedAt, purgedAt, now);

    [Fact]
    public void Nothing_happens_while_the_product_asks_for_no_subscription_or_a_plan_is_running()
    {
        Assert.Equal(WorkspaceRetentionStep.Nothing, Decide(Ended.AddYears(3), Ended, require: false));
        Assert.Equal(WorkspaceRetentionStep.Nothing, Decide(Opened.AddYears(3), null, require: false));
        Assert.Equal(WorkspaceRetentionStep.Nothing, Decide(Ended.AddYears(3), Ended, running: true));
    }

    [Fact]
    public void The_owner_is_warned_30_days_before_and_the_data_goes_a_year_after_the_end()
    {
        Assert.Equal(WorkspaceRetentionStep.Nothing, Decide(Ended.AddDays(334), Ended));
        Assert.Equal(WorkspaceRetentionStep.Warn, Decide(Ended.AddDays(335), Ended));
        var warnedAt = Ended.AddDays(335);
        Assert.Equal(WorkspaceRetentionStep.Nothing, Decide(Ended.AddDays(364), Ended, warnedFor: Ended, warnedAt: warnedAt));
        Assert.Equal(WorkspaceRetentionStep.Purge, Decide(Ended.AddDays(365), Ended, warnedFor: Ended, warnedAt: warnedAt));
        Assert.Equal(Ended.AddDays(365), WorkspaceRetention.DeletionDate(Ended, warnedAt));
    }

    [Fact]
    public void A_late_warning_moves_the_deletion_and_is_never_skipped()
    {
        // The job was down: on the deletion day the owner has not been warned yet, so the warning comes first.
        Assert.Equal(WorkspaceRetentionStep.Warn, Decide(Ended.AddDays(400), Ended));
        var lateWarning = Ended.AddDays(400);
        Assert.Equal(WorkspaceRetentionStep.Nothing, Decide(Ended.AddDays(429), Ended, warnedFor: Ended, warnedAt: lateWarning));
        Assert.Equal(WorkspaceRetentionStep.Purge, Decide(Ended.AddDays(430), Ended, warnedFor: Ended, warnedAt: lateWarning));
        Assert.Equal(Ended.AddDays(430), WorkspaceRetention.DeletionDate(Ended, lateWarning));
    }

    [Fact]
    public void A_warning_about_an_earlier_end_does_not_count_after_a_renewal()
    {
        var earlier = Ended.AddYears(-1);
        Assert.Equal(WorkspaceRetentionStep.Warn,
            Decide(Ended.AddDays(365), Ended, warnedFor: earlier, warnedAt: earlier.AddDays(335)));
    }

    [Fact]
    public void Data_deleted_for_an_end_is_not_deleted_again_until_a_later_plan_ends()
    {
        var purged = Ended.AddDays(365);
        Assert.Equal(WorkspaceRetentionStep.Nothing, Decide(Ended.AddDays(800), Ended, purgedAt: purged));
        var laterEnd = purged.AddDays(60);
        Assert.Equal(WorkspaceRetentionStep.Warn, Decide(laterEnd.AddDays(335), laterEnd, purgedAt: purged));
    }

    [Fact]
    public void A_workspace_that_never_had_a_plan_goes_90_days_after_it_was_opened_without_a_warning()
    {
        Assert.Equal(WorkspaceRetentionStep.Nothing, Decide(Opened.AddDays(89)));
        Assert.Equal(WorkspaceRetentionStep.Purge, Decide(Opened.AddDays(90)));
        Assert.Equal(WorkspaceRetentionStep.Nothing, Decide(Opened.AddDays(200), purgedAt: Opened.AddDays(90)));
    }

    [Fact]
    public void The_warning_names_the_dates_in_turkish_and_links_to_the_plan_page()
    {
        var mail = WorkspaceRetentionWorker.WarningEmail(new CourseEmailOptions { PublicBaseUrl = "https://maxkampus.com" }, Guid.NewGuid(),
            "TEST Kurs", Ended, Ended.AddDays(365));
        Assert.Equal(string.Empty, mail.RecipientEmail);
        Assert.NotNull(mail.RecipientUserId);
        Assert.Contains("1 Haziran 2027", mail.Subject);
        Assert.Contains("1 Haziran 2026 tarihinde sona erdi", mail.Paragraphs[0]);
        Assert.Equal("https://maxkampus.com/plan", mail.LinkUrl);
    }

    [Fact]
    public void Every_table_with_a_workspace_is_purged_and_children_go_before_their_parents()
    {
        var options = new DbContextOptionsBuilder<CourseManagementDbContext>().UseNpgsql("Host=127.0.0.1;Database=model_only").Options;
        using var db = new CourseManagementDbContext(options);
        var tables = db.Model.GetEntityTypes()
            .Where(type => type.FindProperty("WorkspaceId") is not null && type.GetTableName() != "Workspaces")
            .Select(type => type.GetTableName()!).ToHashSet();
        Assert.Empty(tables.Except(WorkspaceContentPurge.TablesInDeleteOrder));
        var order = WorkspaceContentPurge.TablesInDeleteOrder.Select((table, index) => (table, index)).ToDictionary(item => item.table, item => item.index);
        foreach (var type in db.Model.GetEntityTypes())
        {
            var table = type.GetTableName();
            if (table is null || !order.TryGetValue(table, out var child)) continue;
            foreach (var key in type.GetForeignKeys())
            {
                var parent = key.PrincipalEntityType.GetTableName();
                if (parent is null || parent == table || !order.TryGetValue(parent, out var parentIndex)) continue;
                Assert.True(child < parentIndex, $"{table} must be emptied before {parent}.");
            }
        }
    }

    [Fact]
    public async Task Purging_empties_one_panel_and_keeps_the_workspace_its_owner_and_other_workspaces()
    {
        var postgres = new PostgresFixture();
        await postgres.InitializeAsync();
        try
        {
            var options = new DbContextOptionsBuilder<CourseManagementDbContext>().UseNpgsql(postgres.ConnectionString).Options;
            await using (var migration = new CourseManagementDbContext(options))
                await migration.Database.MigrateAsync();

            var owner = Guid.NewGuid();
            var teacher = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var expired = Workspace.Create(Guid.NewGuid(), owner, "TEST Süresi biten kurs", WorkspaceType.Institution);
            var other = Workspace.Create(Guid.NewGuid(), Guid.NewGuid(), "TEST Diğer kurs", WorkspaceType.Institution);
            await using (var seed = new CourseManagementDbContext(options))
            {
                seed.Workspaces.AddRange(expired, other);
                foreach (var workspace in new[] { expired, other })
                {
                    var student = StudentRecord.Create(Guid.NewGuid(), workspace.Id, "TEST Öğrenci", "ogrenci@example.test", null, now);
                    var course = Course.Create(Guid.NewGuid(), workspace.Id, "Matematik", "Matematik", now);
                    var assignment = CourseAssignment.Create(Guid.NewGuid(), workspace.Id, course.Id, null, student.Id, workspace.OwnerUserId,
                        "Kesirler", null, now.AddDays(1), now);
                    var submission = CourseAssignmentSubmission.Create(Guid.NewGuid(), workspace.Id, assignment.Id, student.Id, "Yanıt", now);
                    var attachment = CourseAssignmentAttachment.Create(Guid.NewGuid(), workspace.Id, assignment.Id, student.Id, submission.Id,
                        CourseAssignmentAttachmentPurpose.StudentSubmission, "yanit.pdf", "application/pdf", 16, new string('A', 64),
                        $"retention/{workspace.Id:N}.pdf", workspace.OwnerUserId, now, assignment.DueAt);
                    var agreement = FeeAgreement.Create(Guid.NewGuid(), workspace.Id, student.Id, course.Id, workspace.OwnerUserId,
                        FeeModel.MonthlyFixed, 500, null, null, null, now);
                    var installment = Installment.Create(Guid.NewGuid(), workspace.Id, agreement.Id, student.Id, "Taksit", 500,
                        DateOnly.FromDateTime(DateTime.UtcNow), now);
                    var payment = Payment.Create(Guid.NewGuid(), workspace.Id, student.Id, workspace.OwnerUserId, PaymentMethod.Cash, 200, now, null);
                    seed.Students.Add(student);
                    seed.Courses.Add(course);
                    seed.Assignments.Add(assignment);
                    seed.AssignmentSubmissions.Add(submission);
                    seed.AssignmentAttachments.Add(attachment);
                    seed.FeeAgreements.Add(agreement);
                    seed.Installments.Add(installment);
                    seed.Payments.Add(payment);
                    seed.Memberships.Add(WorkspaceMembership.Create(workspace.Id, workspace.OwnerUserId, WorkspaceRole.Owner));
                    seed.AuditEntries.Add(WorkspaceAuditEntry.Create(workspace.Id, workspace.OwnerUserId, student.Id, "Student.Created"));
                }
                seed.Memberships.Add(WorkspaceMembership.Create(expired.Id, teacher, WorkspaceRole.Teacher));
                await seed.SaveChangesAsync();
            }

            int deleted;
            await using (var db = new CourseManagementDbContext(options))
                deleted = await WorkspaceContentPurge.PurgeAsync(db, expired.Id, now, CancellationToken.None);
            Assert.True(deleted >= 10, $"Deleted {deleted} rows.");

            await using var check = new CourseManagementDbContext(options);
            var kept = await check.Workspaces.SingleAsync(item => item.Id == expired.Id);
            Assert.NotNull(kept.ContentPurgedAt);
            Assert.Equal(owner, Assert.Single(await check.Memberships.Where(item => item.WorkspaceId == expired.Id).ToListAsync()).UserId);
            Assert.Empty(await check.Students.Where(item => item.WorkspaceId == expired.Id).ToListAsync());
            Assert.Empty(await check.Payments.Where(item => item.WorkspaceId == expired.Id).ToListAsync());
            Assert.Empty(await check.AssignmentAttachments.Where(item => item.WorkspaceId == expired.Id).ToListAsync());
            Assert.Equal(WorkspaceContentPurge.PurgedAuditAction,
                Assert.Single(await check.AuditEntries.Where(item => item.WorkspaceId == expired.Id).ToListAsync()).Action);
            Assert.Contains(await check.AttachmentCleanupRequests.ToListAsync(), item => item.StorageKey == $"retention/{expired.Id:N}.pdf");
            Assert.DoesNotContain(await check.AttachmentCleanupRequests.ToListAsync(), item => item.StorageKey == $"retention/{other.Id:N}.pdf");

            Assert.Single(await check.Students.Where(item => item.WorkspaceId == other.Id).ToListAsync());
            Assert.Single(await check.Payments.Where(item => item.WorkspaceId == other.Id).ToListAsync());
            Assert.Single(await check.AssignmentAttachments.Where(item => item.WorkspaceId == other.Id).ToListAsync());
            Assert.Null((await check.Workspaces.SingleAsync(item => item.Id == other.Id)).ContentPurgedAt);
        }
        finally
        {
            await postgres.DisposeAsync();
        }
    }
}
