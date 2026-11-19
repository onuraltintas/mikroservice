using CourseManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Infrastructure;

/// <summary>
/// Deletes a workspace's panel data for retention (maxkampüs, user decision 20.11.2026): every record of the workspace
/// goes, except the workspace itself and its owner's membership, so the owner can sign in, buy a plan and start again
/// in an empty panel. Uploaded files are handed to the attachment cleanup queue in the same transaction.
/// One transaction: the panel is either untouched or empty.
/// </summary>
public static class WorkspaceContentPurge
{
    /// <summary>
    /// Every table with a "WorkspaceId" column, children before the tables they point to (all foreign keys restrict).
    /// A test checks this list against the model, so a new table cannot be forgotten.
    /// </summary>
    public static readonly IReadOnlyList<string> TablesInDeleteOrder =
    [
        "PaymentAllocations",
        "Payments",
        "PackageMovements",
        "Installments",
        "FeeAgreements",
        "MakeupCredits",
        "AttendanceRecords",
        "LessonParticipants",
        "CourseAssignmentAttachments",
        "CourseAssignmentSubmissions",
        "CourseAssignmentRecipients",
        "CourseAssignments",
        "LessonSessions",
        "LessonSeries",
        "StudentGroupEnrollments",
        "CourseGroups",
        "CourseRooms",
        "CourseBranches",
        "Courses",
        "CourseEmails",
        "CourseEmailOptOuts",
        "StudentGuardianLinks",
        "GuardianContacts",
        "LegacyStudentImportMappings",
        "WorkspaceInvitations",
        "Students",
        "SupportAccessLogs",
        "SupportAccessGrants",
        "TeacherAvailabilityWindows",
        "TeacherTimeOff",
        "TeacherEmailInvitations",
        "CourseManagementReminders",
        "WorkspaceEmailSettings",
        "WorkspaceSubscriptions",
        "WorkspaceAuditEntries",
        "WorkspaceMemberships"
    ];

    /// <summary>Rows that point to rows of the same table (reversals); cut before the table is emptied.</summary>
    private static readonly (string Table, string Column)[] SelfReferences =
    [
        ("Payments", "ReversalOfPaymentId"),
        ("PackageMovements", "ReversalOfMovementId"),
        ("Installments", "ReversalOfInstallmentId")
    ];

    public const string PurgedAuditAction = "Retention.ContentPurged";

    /// <summary>The statements, built once from the fixed names above (letters only), never from input.</summary>
    private static readonly string[] CutSelfReferences = SelfReferences
        .Select(item => $"UPDATE \"{Name(item.Table)}\" SET \"{Name(item.Column)}\" = NULL WHERE \"WorkspaceId\" = {{0}} AND \"{Name(item.Column)}\" IS NOT NULL")
        .ToArray();
    private static readonly string[] DeleteStatements = TablesInDeleteOrder
        .Select(table => table == "WorkspaceMemberships"
            ? "DELETE FROM \"WorkspaceMemberships\" WHERE \"WorkspaceId\" = {0} AND \"UserId\" <> {1}"
            : $"DELETE FROM \"{Name(table)}\" WHERE \"WorkspaceId\" = {{0}}")
        .ToArray();

    private static string Name(string value) =>
        value.Length > 0 && value.All(char.IsAsciiLetter) ? value : throw new InvalidOperationException("Unexpected table or column name.");

    /// <summary>Deletes the panel data; returns the number of rows deleted. The caller saves nothing else in this context.</summary>
    public static async Task<int> PurgeAsync(CourseManagementDbContext db, Guid workspaceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var workspace = await db.Workspaces.SingleAsync(item => item.Id == workspaceId, cancellationToken);
            var owner = workspace.OwnerUserId;

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "CourseAssignmentAttachmentCleanupRequests" ("Id", "StorageKey", "QueuedAt")
                SELECT gen_random_uuid(), a."StorageKey", {now} FROM "CourseAssignmentAttachments" a WHERE a."WorkspaceId" = {workspaceId}
                ON CONFLICT ("StorageKey") DO NOTHING
                """, cancellationToken);
            foreach (var statement in CutSelfReferences)
                await db.Database.ExecuteSqlRawAsync(statement, [workspaceId], cancellationToken);

            var deleted = 0;
            foreach (var statement in DeleteStatements)
                deleted += statement.Contains("{1}", StringComparison.Ordinal)
                    ? await db.Database.ExecuteSqlRawAsync(statement, [workspaceId, owner], cancellationToken)
                    : await db.Database.ExecuteSqlRawAsync(statement, [workspaceId], cancellationToken);

            workspace.MarkContentPurged(now);
            // The one line the owner finds in the empty panel's history.
            db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, Guid.Empty, workspaceId, PurgedAuditAction));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return deleted;
        });
    }
}
