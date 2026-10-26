using CourseManagement.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Infrastructure;

public sealed class CourseManagementDbContext(DbContextOptions<CourseManagementDbContext> options) : DbContext(options)
{
    public DbSet<CourseUserProfile> UserProfiles => Set<CourseUserProfile>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMembership> Memberships => Set<WorkspaceMembership>();
    public DbSet<WorkspaceSubscription> WorkspaceSubscriptions => Set<WorkspaceSubscription>();
    public DbSet<WorkspaceInvitation> Invitations => Set<WorkspaceInvitation>();
    public DbSet<WorkspaceAuditEntry> AuditEntries => Set<WorkspaceAuditEntry>();
    public DbSet<StudentRecord> Students => Set<StudentRecord>();
    public DbSet<LegacyStudentImportMapping> LegacyStudentImports => Set<LegacyStudentImportMapping>();
    public DbSet<GuardianContact> Guardians => Set<GuardianContact>();
    public DbSet<StudentGuardianLink> StudentGuardianLinks => Set<StudentGuardianLink>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseBranch> Branches => Set<CourseBranch>();
    public DbSet<CourseRoom> Rooms => Set<CourseRoom>();
    public DbSet<CourseGroup> Groups => Set<CourseGroup>();
    public DbSet<StudentGroupEnrollment> Enrollments => Set<StudentGroupEnrollment>();
    public DbSet<LessonSession> LessonSessions => Set<LessonSession>();
    public DbSet<LessonSeries> LessonSeries => Set<LessonSeries>();
    public DbSet<TeacherTimeOff> TeacherTimeOffs => Set<TeacherTimeOff>();
    public DbSet<TeacherAvailabilityWindow> TeacherAvailabilityWindows => Set<TeacherAvailabilityWindow>();
    public DbSet<LessonParticipant> LessonParticipants => Set<LessonParticipant>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<MakeupCredit> MakeupCredits => Set<MakeupCredit>();
    public DbSet<CourseAssignment> Assignments => Set<CourseAssignment>();
    public DbSet<CourseAssignmentRecipient> AssignmentRecipients => Set<CourseAssignmentRecipient>();
    public DbSet<CourseAssignmentSubmission> AssignmentSubmissions => Set<CourseAssignmentSubmission>();
    public DbSet<CourseAssignmentAttachment> AssignmentAttachments => Set<CourseAssignmentAttachment>();
    public DbSet<CourseAssignmentAttachmentCleanupRequest> AttachmentCleanupRequests => Set<CourseAssignmentAttachmentCleanupRequest>();
    public DbSet<FeeAgreement> FeeAgreements => Set<FeeAgreement>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<PackageMovement> PackageMovements => Set<PackageMovement>();
    public DbSet<CourseManagementReminder> Reminders => Set<CourseManagementReminder>();
    public DbSet<CourseManagementErasureAssessment> ErasureAssessments => Set<CourseManagementErasureAssessment>();
    public DbSet<CourseManagementErasureExecution> ErasureExecutions => Set<CourseManagementErasureExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudentRecord>(entity =>
        {
            entity.ToTable("Students");
            entity.HasKey(student => student.Id);
            entity.Property(student => student.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(student => student.Email).HasMaxLength(320);
            entity.Property(student => student.PhoneNumber).HasMaxLength(40);
            entity.Property(student => student.IsActive).HasDefaultValue(true);
            entity.HasIndex(student => new { student.WorkspaceId, student.Email }).IsUnique();
            entity.HasIndex(student => new { student.WorkspaceId, student.UserId }).IsUnique();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(student => student.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LegacyStudentImportMapping>(entity =>
        {
            entity.ToTable("LegacyStudentImportMappings");
            entity.HasKey(mapping => mapping.Id);
            entity.Property(mapping => mapping.SourceSystem).HasMaxLength(32).IsRequired();
            entity.Property(mapping => mapping.SelectedFields).HasMaxLength(80).IsRequired();
            entity.HasIndex(mapping => new { mapping.WorkspaceId, mapping.SourceSystem, mapping.SourceRecordId }).IsUnique();
            entity.HasIndex(mapping => mapping.StudentRecordId).IsUnique();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(mapping => mapping.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(mapping => mapping.StudentRecordId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("Courses");
            entity.HasKey(course => course.Id);
            entity.Property(course => course.Name).HasMaxLength(160).IsRequired();
            entity.Property(course => course.Subject).HasMaxLength(100).IsRequired();
            entity.HasIndex(course => new { course.WorkspaceId, course.Name }).IsUnique();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(course => course.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseBranch>(entity =>
        {
            entity.ToTable("CourseBranches");
            entity.HasKey(branch => branch.Id);
            entity.Property(branch => branch.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(branch => new { branch.WorkspaceId, branch.Name }).IsUnique();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(branch => branch.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseRoom>(entity =>
        {
            entity.ToTable("CourseRooms");
            entity.HasKey(room => room.Id);
            entity.Property(room => room.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(room => new { room.BranchId, room.Name }).IsUnique();
            entity.HasIndex(room => new { room.WorkspaceId, room.BranchId });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(room => room.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseBranch>().WithMany().HasForeignKey(room => room.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<GuardianContact>(entity =>
        {
            entity.ToTable("GuardianContacts");
            entity.HasKey(guardian => guardian.Id);
            entity.Property(guardian => guardian.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(guardian => guardian.Email).HasMaxLength(320);
            entity.Property(guardian => guardian.PhoneNumber).HasMaxLength(40);
            entity.HasIndex(guardian => new { guardian.WorkspaceId, guardian.DisplayName });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(guardian => guardian.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StudentGuardianLink>(entity =>
        {
            entity.ToTable("StudentGuardianLinks");
            entity.HasKey(link => link.Id);
            entity.Property(link => link.IsActive).HasDefaultValue(true);
            entity.HasIndex(link => new { link.StudentId, link.GuardianContactId }).IsUnique();
            entity.HasIndex(link => new { link.WorkspaceId, link.StudentId, link.IsActive });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(link => link.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(link => link.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GuardianContact>().WithMany().HasForeignKey(link => link.GuardianContactId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseGroup>(entity =>
        {
            entity.ToTable("CourseGroups");
            entity.HasKey(group => group.Id);
            entity.Property(group => group.Name).HasMaxLength(160).IsRequired();
            entity.HasIndex(group => new { group.CourseId, group.Name }).IsUnique();
            entity.HasIndex(group => new { group.WorkspaceId, group.TeacherUserId });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(group => group.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Course>().WithMany().HasForeignKey(group => group.CourseId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StudentGroupEnrollment>(entity =>
        {
            entity.ToTable("StudentGroupEnrollments");
            entity.HasKey(enrollment => enrollment.Id);
            entity.HasIndex(enrollment => new { enrollment.GroupId, enrollment.StudentId }).IsUnique();
            entity.HasIndex(enrollment => new { enrollment.WorkspaceId, enrollment.Status });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(enrollment => enrollment.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseGroup>().WithMany().HasForeignKey(enrollment => enrollment.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(enrollment => enrollment.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LessonSession>(entity =>
        {
            entity.ToTable("LessonSessions");
            entity.HasKey(lesson => lesson.Id);
            entity.HasIndex(lesson => new { lesson.WorkspaceId, lesson.StartsAt });
            entity.HasIndex(lesson => new { lesson.WorkspaceId, lesson.TeacherUserId, lesson.Status, lesson.StartsAt });
            entity.HasIndex(lesson => new { lesson.WorkspaceId, lesson.RoomId, lesson.Status, lesson.StartsAt });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(lesson => lesson.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Course>().WithMany().HasForeignKey(lesson => lesson.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LessonSeries>().WithMany().HasForeignKey(lesson => lesson.SeriesId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseGroup>().WithMany().HasForeignKey(lesson => lesson.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseRoom>().WithMany().HasForeignKey(lesson => lesson.RoomId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LessonSeries>(entity =>
        {
            entity.ToTable("LessonSeries");
            entity.HasKey(series => series.Id);
            entity.Property(series => series.TimeZoneId).HasMaxLength(80).IsRequired();
            entity.HasIndex(series => new { series.WorkspaceId, series.StartsOn });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(series => series.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Course>().WithMany().HasForeignKey(series => series.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseGroup>().WithMany().HasForeignKey(series => series.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(series => series.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseRoom>().WithMany().HasForeignKey(series => series.RoomId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TeacherTimeOff>(entity =>
        {
            entity.ToTable("TeacherTimeOff");
            entity.HasKey(timeOff => timeOff.Id);
            entity.Property(timeOff => timeOff.Reason).HasMaxLength(300);
            entity.HasIndex(timeOff => new { timeOff.WorkspaceId, timeOff.TeacherUserId, timeOff.StartsAt });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(timeOff => timeOff.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TeacherAvailabilityWindow>(entity =>
        {
            entity.ToTable("TeacherAvailabilityWindows");
            entity.HasKey(window => window.Id);
            entity.Property(window => window.TimeZoneId).HasMaxLength(80).IsRequired();
            entity.HasIndex(window => new { window.WorkspaceId, window.TeacherUserId, window.Weekday,
                window.StartTime, window.EndTime, window.TimeZoneId }).IsUnique();
            entity.HasIndex(window => new { window.WorkspaceId, window.TeacherUserId, window.Weekday });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(window => window.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LessonParticipant>(entity =>
        {
            entity.ToTable("LessonParticipants");
            entity.HasKey(participant => participant.Id);
            entity.HasIndex(participant => new { participant.LessonSessionId, participant.StudentId }).IsUnique();
            entity.HasIndex(participant => new { participant.WorkspaceId, participant.StudentId });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(participant => participant.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LessonSession>().WithMany().HasForeignKey(participant => participant.LessonSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(participant => participant.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("AttendanceRecords");
            entity.HasKey(attendance => attendance.Id);
            entity.HasIndex(attendance => new { attendance.LessonSessionId, attendance.StudentId }).IsUnique();
            entity.HasIndex(attendance => new { attendance.WorkspaceId, attendance.LessonSessionId });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(attendance => attendance.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LessonSession>().WithMany().HasForeignKey(attendance => attendance.LessonSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(attendance => attendance.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<MakeupCredit>(entity =>
        {
            entity.ToTable("MakeupCredits");
            entity.HasKey(credit => credit.Id);
            entity.HasIndex(credit => new { credit.WorkspaceId, credit.SourceLessonId, credit.StudentId }).IsUnique();
            entity.HasIndex(credit => new { credit.WorkspaceId, credit.StudentId, credit.Status, credit.ExpiresAt });
            entity.HasIndex(credit => credit.MakeupLessonId).IsUnique();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(credit => credit.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(credit => credit.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LessonSession>().WithMany().HasForeignKey(credit => credit.SourceLessonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LessonSession>().WithMany().HasForeignKey(credit => credit.MakeupLessonId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseAssignment>(entity =>
        {
            entity.ToTable("CourseAssignments");
            entity.HasKey(assignment => assignment.Id);
            entity.Property(assignment => assignment.Title).HasMaxLength(160).IsRequired();
            entity.Property(assignment => assignment.Instructions).HasMaxLength(4000);
            entity.HasIndex(assignment => new { assignment.WorkspaceId, assignment.DueAt });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(assignment => assignment.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Course>().WithMany().HasForeignKey(assignment => assignment.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseGroup>().WithMany().HasForeignKey(assignment => assignment.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(assignment => assignment.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseAssignmentRecipient>(entity =>
        {
            entity.ToTable("CourseAssignmentRecipients");
            entity.HasKey(recipient => recipient.Id);
            entity.HasIndex(recipient => new { recipient.AssignmentId, recipient.StudentId }).IsUnique();
            entity.HasIndex(recipient => new { recipient.WorkspaceId, recipient.StudentId });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(recipient => recipient.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseAssignment>().WithMany().HasForeignKey(recipient => recipient.AssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(recipient => recipient.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseAssignmentSubmission>(entity =>
        {
            entity.ToTable("CourseAssignmentSubmissions");
            entity.HasKey(submission => submission.Id);
            entity.Property(submission => submission.Text).HasMaxLength(8000);
            entity.Property(submission => submission.Score).HasPrecision(5, 2);
            entity.Property(submission => submission.Feedback).HasMaxLength(3000);
            entity.HasIndex(submission => new { submission.WorkspaceId, submission.AssignmentId, submission.StudentId }).IsUnique();
            entity.HasIndex(submission => new { submission.WorkspaceId, submission.StudentId, submission.SubmittedAt });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(submission => submission.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseAssignment>().WithMany().HasForeignKey(submission => submission.AssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(submission => submission.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseAssignmentAttachment>(entity =>
        {
            entity.ToTable("CourseAssignmentAttachments");
            entity.HasKey(attachment => attachment.Id);
            entity.Property(attachment => attachment.FileName).HasMaxLength(180).IsRequired();
            entity.Property(attachment => attachment.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(attachment => attachment.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(attachment => attachment.StorageKey).HasMaxLength(300).IsRequired();
            entity.HasIndex(attachment => attachment.StorageKey).IsUnique();
            entity.HasIndex(attachment => new { attachment.WorkspaceId, attachment.AssignmentId, attachment.Purpose, attachment.ExpiresAt });
            entity.HasIndex(attachment => attachment.SubmissionId);
            entity.HasOne<Workspace>().WithMany().HasForeignKey(attachment => attachment.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseAssignment>().WithMany().HasForeignKey(attachment => attachment.AssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(attachment => attachment.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourseAssignmentSubmission>().WithMany().HasForeignKey(attachment => attachment.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseAssignmentAttachmentCleanupRequest>(entity =>
        {
            entity.ToTable("CourseAssignmentAttachmentCleanupRequests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.StorageKey).HasMaxLength(300).IsRequired();
            entity.HasIndex(request => request.StorageKey).IsUnique();
            entity.HasIndex(request => request.QueuedAt);
        });
        modelBuilder.Entity<FeeAgreement>(entity =>
        {
            entity.ToTable("FeeAgreements");
            entity.HasKey(agreement => agreement.Id);
            entity.Property(agreement => agreement.Amount).HasPrecision(12, 2);
            entity.HasIndex(agreement => new { agreement.WorkspaceId, agreement.StudentId, agreement.CourseId })
                .IsUnique().HasFilter("\"IsActive\" = TRUE");
            entity.HasIndex(agreement => new { agreement.WorkspaceId, agreement.StudentId, agreement.Model, agreement.IsActive });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(agreement => agreement.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(agreement => agreement.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Course>().WithMany().HasForeignKey(agreement => agreement.CourseId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Installment>(entity =>
        {
            entity.ToTable("Installments");
            entity.HasKey(installment => installment.Id);
            entity.Property(installment => installment.Amount).HasPrecision(12, 2);
            entity.Property(installment => installment.Label).HasMaxLength(120).IsRequired();
            entity.HasIndex(installment => new { installment.WorkspaceId, installment.StudentId, installment.DueOn });
            entity.HasIndex(installment => new { installment.FeeAgreementId, installment.SourceLessonId })
                .IsUnique().HasFilter("\"SourceLessonId\" IS NOT NULL");
            entity.HasIndex(installment => installment.ReversalOfInstallmentId).IsUnique()
                .HasFilter("\"ReversalOfInstallmentId\" IS NOT NULL");
            entity.HasOne<Workspace>().WithMany().HasForeignKey(installment => installment.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FeeAgreement>().WithMany().HasForeignKey(installment => installment.FeeAgreementId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(installment => installment.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LessonSession>().WithMany().HasForeignKey(installment => installment.SourceLessonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Installment>().WithMany().HasForeignKey(installment => installment.ReversalOfInstallmentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Amount).HasPrecision(12, 2);
            entity.Property(payment => payment.Note).HasMaxLength(300);
            entity.HasIndex(payment => new { payment.WorkspaceId, payment.StudentId, payment.ReceivedAt });
            entity.HasIndex(payment => payment.ReversalOfPaymentId).IsUnique().HasFilter("\"ReversalOfPaymentId\" IS NOT NULL");
            entity.HasOne<Workspace>().WithMany().HasForeignKey(payment => payment.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(payment => payment.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Payment>().WithMany().HasForeignKey(payment => payment.ReversalOfPaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.ToTable("PaymentAllocations");
            entity.HasKey(allocation => allocation.Id);
            entity.Property(allocation => allocation.Amount).HasPrecision(12, 2);
            entity.HasIndex(allocation => new { allocation.PaymentId, allocation.InstallmentId }).IsUnique();
            entity.HasIndex(allocation => new { allocation.WorkspaceId, allocation.InstallmentId });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(allocation => allocation.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Payment>().WithMany().HasForeignKey(allocation => allocation.PaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Installment>().WithMany().HasForeignKey(allocation => allocation.InstallmentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PackageMovement>(entity =>
        {
            entity.ToTable("PackageMovements");
            entity.HasKey(movement => movement.Id);
            entity.HasIndex(movement => new { movement.FeeAgreementId, movement.LessonSessionId }).IsUnique()
                .HasFilter("\"LessonSessionId\" IS NOT NULL");
            entity.HasIndex(movement => movement.ReversalOfMovementId).IsUnique()
                .HasFilter("\"ReversalOfMovementId\" IS NOT NULL");
            entity.HasIndex(movement => new { movement.WorkspaceId, movement.StudentId, movement.OccurredAt });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(movement => movement.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FeeAgreement>().WithMany().HasForeignKey(movement => movement.FeeAgreementId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(movement => movement.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LessonSession>().WithMany().HasForeignKey(movement => movement.LessonSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PackageMovement>().WithMany().HasForeignKey(movement => movement.ReversalOfMovementId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseManagementReminder>(entity =>
        {
            entity.ToTable("CourseManagementReminders");
            entity.HasKey(reminder => reminder.Id);
            entity.HasIndex(reminder => new { reminder.Status, reminder.NotifyAt });
            entity.HasIndex(reminder => new { reminder.WorkspaceId, reminder.RelatedEntityId, reminder.RecipientUserId, reminder.Type, reminder.TargetAt });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(reminder => reminder.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkspaceMembership>(entity =>
        {
            entity.ToTable("WorkspaceMemberships");
            entity.HasKey(member => new { member.WorkspaceId, member.UserId });
            entity.HasIndex(member => new { member.UserId, member.IsActive });
            entity.Property(member => member.IsActive).IsConcurrencyToken();
            entity.Property(member => member.Role).IsConcurrencyToken();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(member => member.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkspaceInvitation>(entity =>
        {
            entity.ToTable("WorkspaceInvitations");
            entity.HasKey(invitation => invitation.Id);
            entity.Property(invitation => invitation.Status).IsConcurrencyToken();
            entity.HasIndex(invitation => new { invitation.WorkspaceId, invitation.TargetUserId }).IsUnique().HasFilter("\"Status\" = 1");
            entity.HasIndex(invitation => new { invitation.TargetUserId, invitation.Status });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(invitation => invitation.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentRecord>().WithMany().HasForeignKey(invitation => invitation.StudentRecordId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkspaceAuditEntry>(entity =>
        {
            entity.ToTable("WorkspaceAuditEntries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Action).HasMaxLength(80).IsRequired();
            entity.HasIndex(entry => new { entry.WorkspaceId, entry.OccurredAt });
            entity.HasIndex(entry => new { entry.OccurredAt, entry.Id })
                .HasDatabaseName("IX_WorkspaceAuditEntries_SubscriptionAudit")
                .HasFilter("\"Action\" IN ('Subscription.Assigned', 'Subscription.Updated')");
            entity.HasOne<Workspace>().WithMany().HasForeignKey(entry => entry.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseUserProfile>(entity =>
        {
            entity.ToTable("UserProfiles");
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.DisplayName).HasMaxLength(200).IsRequired();
        });
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("Workspaces");
            entity.HasKey(workspace => workspace.Id);
            entity.Property(workspace => workspace.Name).HasMaxLength(200).IsRequired();
            entity.Property(workspace => workspace.InstitutionCategory);
            entity.Property(workspace => workspace.IsErasureReadOnly).HasDefaultValue(false);
            entity.HasIndex(workspace => workspace.OwnerUserId);
        });
        modelBuilder.Entity<WorkspaceSubscription>(entity =>
        {
            entity.ToTable("WorkspaceSubscriptions");
            entity.HasKey(subscription => subscription.WorkspaceId);
            entity.Property(subscription => subscription.PlanName).HasMaxLength(80).IsRequired();
            entity.Property(subscription => subscription.AccessStatus).IsRequired();
            entity.HasIndex(subscription => new { subscription.AccessStatus, subscription.EndsAt });
            entity.HasOne<Workspace>().WithOne().HasForeignKey<WorkspaceSubscription>(subscription => subscription.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseManagementErasureAssessment>(entity =>
        {
            entity.ToTable("AccountErasureAssessments");
            entity.HasKey(assessment => assessment.RequestId);
            entity.Property(assessment => assessment.RecordCountsJson).HasColumnType("jsonb").IsRequired();
        });
        modelBuilder.Entity<CourseManagementErasureExecution>(entity =>
        {
            entity.ToTable("AccountErasureExecutions");
            entity.HasKey(execution => execution.RequestId);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
