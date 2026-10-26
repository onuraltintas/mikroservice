using System.Linq.Expressions;
using CourseManagement.Application.CourseAssignments;
using CourseManagement.Domain;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Infrastructure.Privacy;

public sealed class CourseManagementAccountErasureService(
    CourseManagementDbContext context,
    ICourseAssignmentAttachmentStorage attachmentStorage,
    TimeProvider timeProvider,
    ICourseManagementLegalHoldStatusProvider legalHoldStatusProvider) : ICourseManagementAccountErasureService
{
    public async Task<CourseManagementErasureAssessment> AssessAsync(
        PersonalDataErasureAssessmentRequestedV1 message,
        CancellationToken cancellationToken)
    {
        RequireAccountScope(message.Scope);
        if (!message.DryRun)
            throw new InvalidOperationException("Course Management erasure assessment must be a dry run.");
        if (message.RequestId == Guid.Empty || message.SubjectUserId == Guid.Empty)
            throw new ArgumentException("Erasure request and subject identifiers are required.", nameof(message));

        var existing = await context.ErasureAssessments.SingleOrDefaultAsync(
            assessment => assessment.RequestId == message.RequestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.SubjectUserId != message.SubjectUserId)
                throw new InvalidOperationException("The erasure request is already bound to another subject.");
            return existing;
        }

        var studentIds = await context.Students.AsNoTracking()
            .Where(student => student.UserId == message.SubjectUserId)
            .Select(student => student.Id)
            .ToListAsync(cancellationToken);
        var counts = await CountRecordsAsync(message.SubjectUserId, studentIds, cancellationToken);
        // Identity's complete DSR assessment is the authoritative hold gate and is checked before execution.
        var assessment = CourseManagementErasureAssessment.Create(
            message.RequestId, message.SubjectUserId, counts,
            CourseManagementLegalHoldStatus.NoActiveHold, timeProvider.GetUtcNow());
        context.ErasureAssessments.Add(assessment);
        await context.SaveChangesAsync(cancellationToken);
        return assessment;
    }

    public async Task<CourseManagementErasureExecution> ExecuteAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken)
    {
        RequireAccountScope(message.Scope);
        if (message.RequestId == Guid.Empty || message.SubjectUserId == Guid.Empty)
            throw new ArgumentException("Erasure request and subject identifiers are required.", nameof(message));

        if (!context.Database.IsRelational())
            return await ExecuteLockedAsync(message, cancellationToken);

        var lockKey = $"course-management-erasure:{message.SubjectUserId}";
        if (context.Database.CurrentTransaction is not null)
        {
            await AcquireSubjectLockAsync(lockKey, cancellationToken);
            return await ExecuteLockedAsync(message, cancellationToken);
        }

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await AcquireSubjectLockAsync(lockKey, cancellationToken);
                var execution = await ExecuteLockedAsync(message, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return execution;
            }
            finally
            {
                context.ChangeTracker.Clear();
            }
        });
    }

    private async Task<CourseManagementErasureExecution> ExecuteLockedAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken)
    {
        var existing = await context.ErasureExecutions.SingleOrDefaultAsync(
            execution => execution.RequestId == message.RequestId, cancellationToken);
        if (existing is not null) return existing;

        var assessment = await context.ErasureAssessments.SingleOrDefaultAsync(
            item => item.RequestId == message.RequestId, cancellationToken)
            ?? throw new InvalidOperationException("A completed Course Management erasure assessment is required.");
        if (assessment.SubjectUserId != message.SubjectUserId || !assessment.LegalHoldStatusVerified
            || assessment.HasActiveLegalHold || !assessment.CanProceed)
            throw new InvalidOperationException("The Course Management erasure assessment does not authorize execution.");

        var currentLegalHoldStatus = await legalHoldStatusProvider.GetStatusAsync(
            message.RequestId, message.SubjectUserId, cancellationToken);
        if (currentLegalHoldStatus != CourseManagementLegalHoldStatus.NoActiveHold)
            throw new InvalidOperationException("The current Course Management legal hold status does not authorize execution.");

        var anonymizedUserId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var students = await context.Students
            .Where(student => student.UserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var studentIds = students.Select(student => student.Id).ToList();
        var deletedRecordCount = 0;

        var ownedWorkspaces = await context.Workspaces
            .Where(workspace => workspace.OwnerUserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var workspacesWithErasedSoleOwner = new HashSet<Guid>();
        foreach (var workspace in ownedWorkspaces)
        {
            var anotherActiveOwner = await context.Memberships
                .Where(member =>
                member.WorkspaceId == workspace.Id
                && member.UserId != message.SubjectUserId
                && member.Role == WorkspaceRole.Owner
                && member.IsActive)
                .Select(member => (Guid?)member.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (anotherActiveOwner is { } replacementOwnerId)
                workspace.TransferOwnership(replacementOwnerId);
            else
            {
                workspace.SetErasureReadOnly();
                workspace.TransferOwnership(anonymizedUserId);
                workspacesWithErasedSoleOwner.Add(workspace.Id);
            }
        }
        var profile = await context.UserProfiles.SingleOrDefaultAsync(
            item => item.UserId == message.SubjectUserId, cancellationToken);
        if (profile is not null)
        {
            context.UserProfiles.Remove(profile);
            deletedRecordCount++;
        }

        var memberships = await context.Memberships
            .Where(member => member.UserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        foreach (var membership in memberships)
        {
            context.Memberships.Remove(membership);
            if (membership.Role == WorkspaceRole.Owner
                && workspacesWithErasedSoleOwner.Contains(membership.WorkspaceId))
                context.Memberships.Add(WorkspaceMembership.CreateAnonymized(
                    membership.WorkspaceId, anonymizedUserId, WorkspaceRole.Owner, isActive: false));
        }
        deletedRecordCount += memberships.Count;

        var invitations = await context.Invitations
            .Where(invitation => invitation.TargetUserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        context.Invitations.RemoveRange(invitations);
        deletedRecordCount += invitations.Count;

        foreach (var student in students)
            student.AnonymizeIdentity();

        var legacyMappings = await context.LegacyStudentImports
            .Where(mapping => studentIds.Contains(mapping.StudentRecordId))
            .ToListAsync(cancellationToken);
        context.LegacyStudentImports.RemoveRange(legacyMappings);
        deletedRecordCount += legacyMappings.Count;

        var guardianLinks = await context.StudentGuardianLinks
            .Where(link => studentIds.Contains(link.StudentId))
            .ToListAsync(cancellationToken);
        var guardianIds = guardianLinks.Select(link => link.GuardianContactId).Distinct().ToList();
        context.StudentGuardianLinks.RemoveRange(guardianLinks);
        if (guardianIds.Count > 0)
        {
            var guardiansStillLinked = await context.StudentGuardianLinks.AsNoTracking()
                .Where(link => guardianIds.Contains(link.GuardianContactId) && !studentIds.Contains(link.StudentId))
                .Select(link => link.GuardianContactId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var orphanGuardianIds = guardianIds.Except(guardiansStillLinked).ToList();
            var orphanGuardians = await context.Guardians
                .Where(guardian => orphanGuardianIds.Contains(guardian.Id))
                .ToListAsync(cancellationToken);
            context.Guardians.RemoveRange(orphanGuardians);
            deletedRecordCount += orphanGuardians.Count;
        }
        deletedRecordCount += guardianLinks.Count;

        var attachments = await context.AssignmentAttachments
            .Where(attachment => attachment.Purpose == CourseAssignmentAttachmentPurpose.StudentSubmission
                && (attachment.UploadedByUserId == message.SubjectUserId
                    || (attachment.StudentId.HasValue && studentIds.Contains(attachment.StudentId.Value))))
            .ToListAsync(cancellationToken);
        foreach (var attachment in attachments)
            await attachmentStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
        context.AssignmentAttachments.RemoveRange(attachments);
        deletedRecordCount += attachments.Count;
        var teacherMaterials = await context.AssignmentAttachments
            .Where(attachment => attachment.Purpose == CourseAssignmentAttachmentPurpose.TeacherMaterial
                && attachment.UploadedByUserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        foreach (var teacherMaterial in teacherMaterials)
            teacherMaterial.AnonymizeUploader(anonymizedUserId);

        var submissions = await context.AssignmentSubmissions
            .Where(submission => studentIds.Contains(submission.StudentId)
                || submission.ReviewedByUserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        foreach (var submission in submissions)
        {
            var entry = context.Entry(submission);
            if (studentIds.Contains(submission.StudentId))
                entry.Property(nameof(CourseAssignmentSubmission.Text)).CurrentValue = null;
            entry.Property(nameof(CourseAssignmentSubmission.Feedback)).CurrentValue = null;
        }
        var studentPayments = await context.Payments
            .Where(payment => studentIds.Contains(payment.StudentId)
                || payment.RecordedByUserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        foreach (var payment in studentPayments)
            context.Entry(payment).Property(nameof(Payment.Note)).CurrentValue = null;

        var studentInstallments = await context.Installments
            .Where(installment => studentIds.Contains(installment.StudentId))
            .ToListAsync(cancellationToken);
        foreach (var installment in studentInstallments)
            context.Entry(installment).Property(nameof(Installment.Label)).CurrentValue = "Taksit";

        var reminders = await context.Reminders
            .Where(reminder => reminder.RecipientUserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        context.Reminders.RemoveRange(reminders);
        deletedRecordCount += reminders.Count;

        var referenceCount = 0;
        referenceCount += await ReplaceReferencesAsync<WorkspaceAuditEntry>(nameof(WorkspaceAuditEntry.ActorUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<WorkspaceAuditEntry>(nameof(WorkspaceAuditEntry.TargetId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<CourseGroup>(nameof(CourseGroup.TeacherUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<LessonSeries>(nameof(LessonSeries.TeacherUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<LessonSession>(nameof(LessonSession.TeacherUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<TeacherTimeOff>(nameof(TeacherTimeOff.TeacherUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<TeacherTimeOff>(nameof(TeacherTimeOff.CreatedByUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<TeacherAvailabilityWindow>(nameof(TeacherAvailabilityWindow.TeacherUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<TeacherAvailabilityWindow>(nameof(TeacherAvailabilityWindow.CreatedByUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<AttendanceRecord>(nameof(AttendanceRecord.MarkedByUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<MakeupCredit>(nameof(MakeupCredit.GrantedByUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<CourseAssignment>(nameof(CourseAssignment.TeacherUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<CourseAssignmentSubmission>(nameof(CourseAssignmentSubmission.ReviewedByUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<FeeAgreement>(nameof(FeeAgreement.CreatedByUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        referenceCount += await ReplaceReferencesAsync<Payment>(nameof(Payment.RecordedByUserId),
            message.SubjectUserId, anonymizedUserId, cancellationToken);
        if (referenceCount > 0 || teacherMaterials.Count > 0 || profile is not null || workspacesWithErasedSoleOwner.Count > 0)
            context.UserProfiles.Add(CourseUserProfile.Create(anonymizedUserId, "Silinmiş kullanıcı"));

        context.ErasureAssessments.Remove(assessment);
        var execution = CourseManagementErasureExecution.Complete(message.RequestId, deletedRecordCount, now);
        context.ErasureExecutions.Add(execution);
        await context.SaveChangesAsync(cancellationToken);
        return execution;
    }

    private async Task<IReadOnlyDictionary<string, int>> CountRecordsAsync(
        Guid subjectUserId, List<Guid> studentIds, CancellationToken cancellationToken)
    {
        var paymentIds = await context.Payments.AsNoTracking()
            .Where(payment => payment.RecordedByUserId == subjectUserId || studentIds.Contains(payment.StudentId))
            .Select(payment => payment.Id).ToListAsync(cancellationToken);
        var installmentIds = await context.Installments.AsNoTracking()
            .Where(installment => studentIds.Contains(installment.StudentId))
            .Select(installment => installment.Id).ToListAsync(cancellationToken);

        return new Dictionary<string, int>
        {
            ["profiles"] = await context.UserProfiles.CountAsync(profile => profile.UserId == subjectUserId, cancellationToken),
            ["workspacesOwned"] = await context.Workspaces.CountAsync(workspace => workspace.OwnerUserId == subjectUserId, cancellationToken),
            ["memberships"] = await context.Memberships.CountAsync(member => member.UserId == subjectUserId, cancellationToken),
            ["invitations"] = await context.Invitations.CountAsync(invitation => invitation.TargetUserId == subjectUserId, cancellationToken),
            ["auditEntries"] = await context.AuditEntries.CountAsync(entry => entry.ActorUserId == subjectUserId || entry.TargetId == subjectUserId, cancellationToken),
            ["students"] = studentIds.Count,
            ["legacyImportMappings"] = await context.LegacyStudentImports.CountAsync(mapping => studentIds.Contains(mapping.StudentRecordId), cancellationToken),
            ["guardianLinks"] = await context.StudentGuardianLinks.CountAsync(link => studentIds.Contains(link.StudentId), cancellationToken),
            ["groups"] = await context.Groups.CountAsync(group => group.TeacherUserId == subjectUserId, cancellationToken),
            ["lessonSeries"] = await context.LessonSeries.CountAsync(series => series.TeacherUserId == subjectUserId, cancellationToken),
            ["lessonSessions"] = await context.LessonSessions.CountAsync(lesson => lesson.TeacherUserId == subjectUserId, cancellationToken),
            ["teacherTimeOff"] = await context.TeacherTimeOffs.CountAsync(timeOff =>
                timeOff.TeacherUserId == subjectUserId || timeOff.CreatedByUserId == subjectUserId, cancellationToken),
            ["teacherAvailability"] = await context.TeacherAvailabilityWindows.CountAsync(window =>
                window.TeacherUserId == subjectUserId || window.CreatedByUserId == subjectUserId, cancellationToken),
            ["attendanceRecords"] = await context.AttendanceRecords.CountAsync(attendance => attendance.MarkedByUserId == subjectUserId, cancellationToken),
            ["makeupCredits"] = await context.MakeupCredits.CountAsync(credit => credit.GrantedByUserId == subjectUserId, cancellationToken),
            ["assignments"] = await context.Assignments.CountAsync(assignment => assignment.TeacherUserId == subjectUserId
                || (assignment.StudentId.HasValue && studentIds.Contains(assignment.StudentId.Value)), cancellationToken),
            ["assignmentRecipients"] = await context.AssignmentRecipients.CountAsync(recipient => studentIds.Contains(recipient.StudentId), cancellationToken),
            ["assignmentSubmissions"] = await context.AssignmentSubmissions.CountAsync(submission =>
                studentIds.Contains(submission.StudentId) || submission.ReviewedByUserId == subjectUserId, cancellationToken),
            ["attachments"] = await context.AssignmentAttachments.CountAsync(attachment =>
                attachment.Purpose == CourseAssignmentAttachmentPurpose.StudentSubmission
                    && (attachment.UploadedByUserId == subjectUserId
                        || (attachment.StudentId.HasValue && studentIds.Contains(attachment.StudentId.Value))), cancellationToken),
            ["teacherMaterialsAnonymized"] = await context.AssignmentAttachments.CountAsync(attachment =>
                attachment.Purpose == CourseAssignmentAttachmentPurpose.TeacherMaterial
                    && attachment.UploadedByUserId == subjectUserId, cancellationToken),
            ["feeAgreements"] = await context.FeeAgreements.CountAsync(agreement => agreement.CreatedByUserId == subjectUserId
                || studentIds.Contains(agreement.StudentId), cancellationToken),
            ["installments"] = installmentIds.Count,
            ["payments"] = paymentIds.Count,
            ["paymentAllocations"] = await context.PaymentAllocations.CountAsync(allocation =>
                paymentIds.Contains(allocation.PaymentId) || installmentIds.Contains(allocation.InstallmentId), cancellationToken),
            ["packageMovements"] = await context.PackageMovements.CountAsync(movement => studentIds.Contains(movement.StudentId), cancellationToken),
            ["reminders"] = await context.Reminders.CountAsync(reminder => reminder.RecipientUserId == subjectUserId, cancellationToken)
        };
    }

    private async Task<int> ReplaceReferencesAsync<TEntity>(string propertyName, Guid oldUserId,
        Guid anonymizedUserId, CancellationToken cancellationToken) where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity {typeof(TEntity).Name} is not mapped.");
        var propertyType = entityType.FindProperty(propertyName)?.ClrType
            ?? throw new InvalidOperationException($"Property {typeof(TEntity).Name}.{propertyName} is not mapped.");
        var query = context.Set<TEntity>();

        if (propertyType == typeof(Guid?))
        {
            Expression<Func<TEntity, bool>> predicate = entity => EF.Property<Guid?>(entity, propertyName) == oldUserId;
            var entities = await query.Where(predicate).ToListAsync(cancellationToken);
            foreach (var entity in entities)
                context.Entry(entity).Property<Guid?>(propertyName).CurrentValue = anonymizedUserId;
            return entities.Count;
        }

        Expression<Func<TEntity, bool>> requiredPredicate = entity => EF.Property<Guid>(entity, propertyName) == oldUserId;
        var requiredEntities = await query.Where(requiredPredicate).ToListAsync(cancellationToken);
        foreach (var entity in requiredEntities)
            context.Entry(entity).Property<Guid>(propertyName).CurrentValue = anonymizedUserId;
        return requiredEntities.Count;
    }

    private Task<int> AcquireSubjectLockAsync(string key, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

    private static void RequireAccountScope(PersonalDataScope scope)
    {
        if (scope != PersonalDataScope.Account)
            throw new InvalidOperationException("The erasure scope does not include Course Management.");
    }
}
