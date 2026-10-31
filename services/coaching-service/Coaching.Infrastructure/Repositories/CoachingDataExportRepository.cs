using Coaching.Application.Interfaces;
using Coaching.Application.Queries.ExportCoachingData;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Coaching.Infrastructure.Repositories;

public sealed class CoachingDataExportRepository(CoachingDbContext context)
    : ICoachingDataExportRepository
{
    public async Task<CoachingDataExportDto> ExportStudentDataAsync(
        Guid studentId,
        DateTimeOffset exportedAt,
        CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational()) return await ExportSnapshotAsync(studentId, exportedAt, cancellationToken);
        if (context.Database.CurrentTransaction is { } current) {
            if (current.GetDbTransaction().IsolationLevel is not (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
                throw new InvalidOperationException("Coaching exports require a consistent read snapshot.");
            return await ExportSnapshotAsync(studentId, exportedAt, cancellationToken);
        }
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            return await ExportSnapshotAsync(studentId, exportedAt, cancellationToken);
        });
    }

    private async Task<CoachingDataExportDto> ExportSnapshotAsync(Guid studentId, DateTimeOffset exportedAt, CancellationToken cancellationToken)
    {
        var assignmentRows = await context.AssignmentStudents
            .AsNoTracking()
            .Where(row => row.StudentId == studentId)
            .Include(row => row.Assignment)
            .OrderBy(row => row.CreatedAt)
            .ToListAsync(cancellationToken);
        var assignmentStudentIds = assignmentRows.Select(row => row.Id).ToList();
        var submissionFiles = await context.AssignmentSubmissionAttachments.AsNoTracking()
            .Where(file => assignmentStudentIds.Contains(file.AssignmentStudentId))
            .OrderBy(file => file.CreatedAt).ThenBy(file => file.Id).ToListAsync(cancellationToken);
        var assignments = assignmentRows.Select(row => new CoachingDataAssignmentDto(
            row.AssignmentId, row.Assignment.Title, row.Assignment.Subject, row.Assignment.DueDate,
            row.Status.ToString(), row.SubmittedAt, row.Score, row.StudentNote, row.TeacherFeedback,
            submissionFiles.Where(file => file.AssignmentStudentId == row.Id).Select(file => new CoachingDataSubmissionFileDto(file.Id,
                file.OriginalFileName, file.ContentType, file.SizeBytes, file.Sha256, file.Status.ToString(), file.UploadedAt, file.RejectionReason)).ToArray())).ToArray();

        var examRows = await context.ExamResults
            .AsNoTracking()
            .Where(row => row.StudentId == studentId)
            .Include(row => row.Exam)
            .OrderBy(row => row.CreatedAt)
            .ToListAsync(cancellationToken);
        var exams = examRows.Select(row => new CoachingDataExamDto(
            row.ExamId, row.Exam.Title, row.Exam.Subject, row.Exam.ExamDate, row.Exam.MaxScore,
            row.Score, row.CorrectAnswers, row.WrongAnswers, row.EmptyAnswers,
            row.SubjectScoresJson, row.Ranking, row.Exam.StudentOwnerId.HasValue ? "StudentReported" : "TeacherRecorded", row.GetLessonAnswers(),
            row.GetPracticeExam(), row.TopicAnalysisJson is null ? null : row.GetTopicAnalysis())).ToArray();

        var goalRows = await context.AcademicGoals.AsNoTracking()
            .Where(goal => goal.StudentId == studentId)
            .OrderBy(goal => goal.CreatedAt)
            .ToListAsync(cancellationToken);
        var goals = goalRows.Select(goal => new CoachingDataGoalDto(
            goal.Id, goal.Title, goal.Description, goal.Category.ToString(), goal.CurrentProgress,
            goal.IsCompleted, goal.TargetDate, goal.TargetScore, goal.TargetSchoolId, goal.TargetUniversityProgramId,
            goal.TargetMaxScore, goal.TargetExamType?.ToString(), goal.TargetSubject, goal.NetTargets)).ToArray();

        var sessionRows = await context.SessionAttendances.AsNoTracking()
            .Where(attendance => attendance.StudentId == studentId)
            .Include(attendance => attendance.Session)
            .OrderBy(attendance => attendance.CreatedAt)
            .ToListAsync(cancellationToken);
        var sessions = sessionRows.Select(attendance => new CoachingDataSessionDto(
            attendance.SessionId, attendance.Session.Title, attendance.Session.ScheduledDate,
            attendance.Session.DurationMinutes, attendance.Session.Status.ToString(),
            attendance.AttendanceStatus.ToString(), attendance.StudentNote,
            attendance.Session.TeacherNotesVisibility == CoachingNoteVisibility.StudentVisible
                ? attendance.Session.TeacherNotes
                : null)).ToArray();

        var agreementRows = await context.CoachingAgreementAcknowledgements.AsNoTracking()
            .Where(acknowledgement => acknowledgement.SubjectStudentId == studentId)
            .Include(acknowledgement => acknowledgement.AgreementDocument)
            .OrderBy(acknowledgement => acknowledgement.AcknowledgedAt)
            .ToListAsync(cancellationToken);
        var agreements = agreementRows.Select(acknowledgement => new CoachingDataAgreementDto(
            acknowledgement.Id, acknowledgement.AgreementDocumentId,
            acknowledgement.AgreementDocument.DocumentVersion,
            acknowledgement.AgreementDocument.Locale,
            acknowledgement.AgreementDocument.Title,
            acknowledgement.AgreementDocument.DocumentReference,
            acknowledgement.AgreementDocument.ContentSha256,
            acknowledgement.PartyRole.ToString(),
            acknowledgement.AcknowledgedAt,
            acknowledgement.WithdrawnAt)).ToArray();

        var planRows = await context.StudyPlanRevisions.AsNoTracking().Where(x => x.StudentId == studentId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var taskRows = await context.StudyPlanTasks.AsNoTracking().Where(x => x.StudentId == studentId)
            .OrderBy(x => x.PlannedDate).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var outcomeRows = await Coaching.Infrastructure.StudyPlanning.StudyPlanOutcomeReader.ReadAsync(context, taskRows.Select(x => x.Id).ToArray(), cancellationToken);
        var plans = planRows.Select(x => new CoachingDataStudyPlanDto(x.Id, x.PlanId, x.RevisionNumber, x.Version,
            x.Title, x.Status.ToString(), x.AutomaticAvailabilityVersion, x.AutomaticSourceRevisionId, x.AutomaticSourceRevisionVersion,
            taskRows.Where(t => t.RevisionId == x.Id).Select(t => new Coaching.Application.StudyPlanning.ManualStudyTaskView(
                t.Id, t.PlannedDate, t.Title, t.PlannedMinutes, t.TopicId, t.IsPinned, t.IsCompleted, t.ActualMinutes, t.CompletedAt, t.SourceName, t.PlannedQuestions, t.ActualQuestions, outcomeRows[t.Id].ToArray(),t.PlannedStartMinute,t.WeeklyLessonId,t.LessonNameSnapshot,t.RealizationHistory,t.RemainingMinutes,t.RemainingQuestions,t.OriginalTaskId,t.PredecessorTaskId,t.WorkDays,t.CompletionSourceTaskId,PlanKind:t.PlanKind,PlanReason:t.PlanReason)).ToArray(),x.WeeklySource,x.AutomaticOutcomeEvidence)).ToArray();
        var hours = await context.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        var availability = hours is null ? null : new Coaching.Application.StudyPlanning.StudyAvailabilityView(hours.Version, hours.TimeZoneId,
            hours.Windows.Select(x => new Coaching.Application.StudyPlanning.StudyWindowInput(x.Day, x.StartMinute, x.EndMinute)).ToArray());
        var weeklyDraft = await context.Set<Coaching.Domain.Entities.WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        var reminders = await context.CoachingReminderReceipts.AsNoTracking().Where(x => x.RecipientUserId == studentId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new CoachingDataReminderDto(x.Id, x.SourceId, x.Kind, x.DueAt, x.CreatedAt)).ToListAsync(cancellationToken);
        var feeRows = await context.CoachingFeeAgreements.AsNoTracking().Where(x => x.StudentId == studentId)
            .Include(x => x.Installments).Include(x => x.Payments).AsSplitQuery().OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var fees = feeRows.Select(x => new CoachingDataFeeDto(x.Id, x.InstitutionId, x.CoachId, x.CreatedByUserId, x.Title,
            "TRY", x.TotalAmount, x.PaidAmount, x.IsCancelled, x.CancelledByUserId, x.CancellationReason, x.CancelledAt, x.CreatedAt,
            x.Installments.OrderBy(i => i.Sequence).Select(i => new CoachingDataFeeInstallmentDto(i.Id, i.Sequence, i.DueOn, i.Amount)).ToArray(),
            x.Payments.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => new Coaching.Application.Finance.CoachingFeePaymentView(p.Id,
                x.Id, x.StudentId, p.RecordedByUserId, p.ReversalOfPaymentId, p.Method, p.Amount, p.ReceivedOn, p.Reference, p.CreatedAt)).ToArray())).ToArray();
        var profile = new Coaching.Application.Profiles.CoachingOwnProfile(
            await context.CoachingStudentProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == studentId, cancellationToken),
            await context.CoachingTeacherProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == studentId, cancellationToken),
            await context.CoachingInstitutionMemberships.AsNoTracking().Where(x => x.UserId == studentId).ToArrayAsync(cancellationToken));
        var profileAssignments = await context.CoachingTeacherStudentAssignments.AsNoTracking()
            .Where(x => x.StudentUserId == studentId || x.TeacherUserId == studentId).ToArrayAsync(cancellationToken);
        var subscriptionRows = await context.CoachingSubscriptions.AsNoTracking().Where(x => x.UserId == studentId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var planIds = subscriptionRows.Select(x => x.PlanId).Distinct().ToArray();
        var planNames = await context.CoachingSubscriptionPlans.AsNoTracking().Where(x => Enumerable.Contains(planIds, x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var subscriptions = subscriptionRows.Select(x => new CoachingDataSubscriptionDto(x.Id, x.PlanId, planNames.GetValueOrDefault(x.PlanId),
            x.InstitutionId, x.Status, x.StartDate, x.EndDate, x.PaymentReference, x.BankTransferRequestId, x.CreatedAt)).ToArray();
        var seats = await context.CoachingSubscriptionSeats.AsNoTracking().Where(x => x.StudentId == studentId)
            .OrderBy(x => x.AssignedAt).ThenBy(x => x.Id)
            .Select(x => new CoachingDataSeatDto(x.Id, x.SubscriptionId, x.IsSuspended, x.SuspensionReason, x.AssignedAt)).ToArrayAsync(cancellationToken);
        // Coaching audit K1: the user's own purchases and those they sent for a child, with everything the purchase recorded.
        var transfers = await context.CoachingBankTransferRequests.AsNoTracking().Where(x => x.UserId == studentId || x.SubmittedByUserId == studentId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new CoachingDataBankTransferDto(x.Id, x.PlanId, x.Amount, x.Currency, x.PaymentReference, x.PayerName, x.Note,
                x.Status, x.SubscriptionId, x.ReviewedAt, x.ReviewNote, x.CreatedAt, x.UserId, x.SubmittedByUserId, x.ListPrice, x.CouponCode,
                x.DiscountAmount, x.DurationDays, x.SalesContractVersion, x.SalesContractAcceptedAt, x.SalesDocumentsJson,
                x.AdultPayerDeclarationVersion, x.AdultPayerDeclaredAt, x.WithdrawalStatus, x.WithdrawalReason, x.WithdrawalRequestedAt,
                x.WithdrawalReviewNote, x.WithdrawalReviewedAt)).ToArrayAsync(cancellationToken);
        var payments = await context.CoachingPaymentRecords.AsNoTracking().Where(x => x.UserId == studentId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new CoachingDataPaymentDto(x.Id, x.PlanId, x.PlanName, x.SubscriptionId, x.BankTransferRequestId,
                x.Amount, x.Currency, x.Status, x.Provider, x.Reference, x.CreatedAt)).ToArrayAsync(cancellationToken);
        // The user's own conversations: as the student or parent writing with a teacher, or as the teacher.
        var conversationRows = await context.Set<Coaching.Domain.Entities.CoachingConversation>().AsNoTracking()
            .Where(x => x.CounterpartUserId == studentId || x.TeacherUserId == studentId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var conversationIds = conversationRows.Select(x => x.Id).ToList();
        var chatMessages = await context.Set<Coaching.Domain.Entities.CoachingMessage>().AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId)).OrderBy(x => x.SentAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var chatFiles = await context.Set<Coaching.Domain.Entities.CoachingMessageAttachment>().AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId) && x.MessageId != null).ToListAsync(cancellationToken);
        var conversations = conversationRows.Select(x => new CoachingDataConversationDto(x.Id, x.TeacherUserId, x.CreatedAt,
            chatMessages.Where(m => m.ConversationId == x.Id).Select(m => new CoachingDataMessageDto(m.Id, m.SenderUserId, m.Body, m.SentAt,
                chatFiles.Where(f => f.MessageId == m.Id).OrderBy(f => f.CreatedAt).Select(f => f.FileName).ToArray())).ToArray())).ToArray();
        var subscriptionIds = subscriptionRows.Select(x => x.Id).ToList();
        var periods = await context.CoachingSubscriptionPeriods.AsNoTracking().Where(x => subscriptionIds.Contains(x.SubscriptionId))
            .OrderBy(x => x.StartDate).ThenBy(x => x.Id)
            .Select(x => new CoachingDataSubscriptionPeriodDto(x.Id, x.SubscriptionId, x.StartDate, x.EndDate, x.DurationDays, x.Source, x.Status,
                x.BankTransferRequestId, x.CreatedAt, x.CancelledAt)).ToArrayAsync(cancellationToken);
        var trials = await context.Set<Coaching.Domain.Entities.CoachingSubscriptionTrial>().AsNoTracking().Where(x => x.UserId == studentId)
            .OrderBy(x => x.StartedAt).Select(x => new CoachingDataTrialDto(x.Id, x.Audience, x.PlanId, x.SubscriptionId, x.StartedAt, x.EndsAt))
            .ToArrayAsync(cancellationToken);
        var paymentIds = payments.Select(x => x.Id).ToList();
        var invoices = await context.Set<Coaching.Domain.Entities.CoachingInvoice>().AsNoTracking().Where(x => paymentIds.Contains(x.PaymentRecordId))
            .OrderBy(x => x.IssuedOn).Select(x => new CoachingDataInvoiceDto(x.Id, x.PaymentRecordId, x.InvoiceNumber, x.IssuedOn, x.FileName, x.SizeBytes))
            .ToArrayAsync(cancellationToken);
        var primaryGoal = await context.Set<Coaching.Domain.Entities.StudentPrimaryGoal>().AsNoTracking()
            .Where(x => x.StudentId == studentId).Select(x => (Guid?)x.GoalId).SingleOrDefaultAsync(cancellationToken);
        return new CoachingDataExportDto(
            "1.11", studentId, exportedAt, assignments, exams, goals, sessions, agreements, plans, availability, reminders, fees, profile, profileAssignments,
            await context.StudentLearningContexts.AsNoTracking().Include(x => x.CoursePreferences).Include(x => x.TopicStates)
                .SingleOrDefaultAsync(x => x.StudentUserId == studentId, cancellationToken),
            weeklyDraft is null ? null : Coaching.Infrastructure.StudyPlanning.CoachingWeeklyStudyDraftService.View(weeklyDraft),
            await context.Set<Coaching.Domain.Entities.WeeklyReflection>().AsNoTracking().Where(x => x.StudentId == studentId).OrderBy(x => x.WeekStart).ToArrayAsync(cancellationToken),
            await context.Set<Coaching.Domain.Entities.WeeklyDecision>().AsNoTracking().Where(x => x.StudentId == studentId || x.TeacherId == studentId || x.ResponsibleTeacherId == studentId).OrderBy(x => x.DueOn).ThenBy(x => x.Id).ToArrayAsync(cancellationToken),
            await context.Set<Coaching.Domain.Entities.WeeklyReflectionReview>().AsNoTracking().Where(x => x.StudentId == studentId || x.TeacherId == studentId).OrderBy(x => x.CreatedAt).ToArrayAsync(cancellationToken),
            subscriptions, seats, transfers, payments, conversations, primaryGoal, trials, periods, invoices);
    }
}
