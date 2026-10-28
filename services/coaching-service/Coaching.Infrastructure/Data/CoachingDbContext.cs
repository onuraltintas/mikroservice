using Microsoft.EntityFrameworkCore;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using MassTransit;
using System.Reflection;
using EduPlatform.Shared.Infrastructure.Middleware;
using Coaching.Application.Exceptions;
using Npgsql;
using EduPlatform.Shared.Kernel.Primitives;
using SharedConcurrencyException = EduPlatform.Shared.Kernel.Exceptions.ConcurrencyException;

namespace Coaching.Infrastructure.Data;

/// <summary>
/// Coaching Service Database Context
/// </summary>
public class CoachingDbContext : DbContext
{
    public DbSet<CoachingInvitation> CoachingInvitations => Set<CoachingInvitation>();
    public CoachingDbContext(DbContextOptions<CoachingDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<CurriculumVersion> CurriculumVersions => Set<CurriculumVersion>();
    public DbSet<StudentLearningContext> StudentLearningContexts => Set<StudentLearningContext>();
    public DbSet<StudentCoursePreference> StudentCoursePreferences => Set<StudentCoursePreference>();
    public DbSet<StudentTopicState> StudentTopicStates => Set<StudentTopicState>();
    public DbSet<CoachingStudentProfile> CoachingStudentProfiles => Set<CoachingStudentProfile>();
    public DbSet<CoachingTeacherProfile> CoachingTeacherProfiles => Set<CoachingTeacherProfile>();
    public DbSet<CoachingInstitutionMembership> CoachingInstitutionMemberships => Set<CoachingInstitutionMembership>();
    public DbSet<CoachingTeacherStudentAssignment> CoachingTeacherStudentAssignments => Set<CoachingTeacherStudentAssignment>();
    public DbSet<StudyCatalogLesson> StudyCatalogLessons => Set<StudyCatalogLesson>();
    public DbSet<StudyCatalogUnit> StudyCatalogUnits => Set<StudyCatalogUnit>();
    public DbSet<StudyCatalogTopic> StudyCatalogTopics => Set<StudyCatalogTopic>();
    public DbSet<TargetCatalogEntry> TargetCatalog => Set<TargetCatalogEntry>();
    public DbSet<TargetUniversityProgram> TargetUniversityPrograms => Set<TargetUniversityProgram>();
    public DbSet<TargetSchool> TargetSchools => Set<TargetSchool>();
    public DbSet<StudyPlanRevision> StudyPlanRevisions => Set<StudyPlanRevision>();
    public DbSet<StudyPlanTask> StudyPlanTasks => Set<StudyPlanTask>();
    public DbSet<StudyAvailability> StudyAvailability => Set<StudyAvailability>();
    public DbSet<CoachingReminderReceipt> CoachingReminderReceipts => Set<CoachingReminderReceipt>();
    public DbSet<CoachingFeeAgreement> CoachingFeeAgreements => Set<CoachingFeeAgreement>();
    public DbSet<CoachingFeeInstallment> CoachingFeeInstallments => Set<CoachingFeeInstallment>();
    public DbSet<CoachingFeePayment> CoachingFeePayments => Set<CoachingFeePayment>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentStudent> AssignmentStudents => Set<AssignmentStudent>();
    public DbSet<AssignmentSubmissionAttachment> AssignmentSubmissionAttachments => Set<AssignmentSubmissionAttachment>();
    public DbSet<AssignmentInstructionAttachment> AssignmentInstructionAttachments => Set<AssignmentInstructionAttachment>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<CoachingSession> CoachingSessions => Set<CoachingSession>();
    public DbSet<SessionAttendance> SessionAttendances => Set<SessionAttendance>();
    public DbSet<AcademicGoal> AcademicGoals => Set<AcademicGoal>();
    public DbSet<CoachingAgreementDocument> CoachingAgreementDocuments => Set<CoachingAgreementDocument>();
    public DbSet<CoachingAgreementAcknowledgement> CoachingAgreementAcknowledgements => Set<CoachingAgreementAcknowledgement>();
    public DbSet<AdminAuditRecord> AdminAuditRecords => Set<AdminAuditRecord>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<CoachingLegalHold> CoachingLegalHolds => Set<CoachingLegalHold>();
    public DbSet<CoachingErasureAssessment> CoachingErasureAssessments => Set<CoachingErasureAssessment>();
    public DbSet<CoachingErasureExecution> CoachingErasureExecutions => Set<CoachingErasureExecution>();
    public DbSet<CoachingCmsEntry> CoachingCmsEntries => Set<CoachingCmsEntry>();
    public DbSet<CoachingCmsRevision> CoachingCmsRevisions => Set<CoachingCmsRevision>();
    public DbSet<CoachingCmsNavigationItem> CoachingCmsNavigationItems => Set<CoachingCmsNavigationItem>();
    public DbSet<CoachingCmsMediaAsset> CoachingCmsMediaAssets => Set<CoachingCmsMediaAsset>();
    public DbSet<CoachingNewsletterSubscriber> CoachingNewsletterSubscribers => Set<CoachingNewsletterSubscriber>();
    public DbSet<CoachingNewsletterUnsubscribeToken> CoachingNewsletterUnsubscribeTokens => Set<CoachingNewsletterUnsubscribeToken>();
    public DbSet<CoachingSubscriptionPlan> CoachingSubscriptionPlans => Set<CoachingSubscriptionPlan>();
    public DbSet<CoachingSubscription> CoachingSubscriptions => Set<CoachingSubscription>();
    public DbSet<CoachingSubscriptionSeat> CoachingSubscriptionSeats => Set<CoachingSubscriptionSeat>();
    public DbSet<CoachingSubscriptionSettings> CoachingSubscriptionSettings => Set<CoachingSubscriptionSettings>();
    public DbSet<CoachingBankTransferRequest> CoachingBankTransferRequests => Set<CoachingBankTransferRequest>();
    public DbSet<CoachingPaymentRecord> CoachingPaymentRecords => Set<CoachingPaymentRecord>();
    public DbSet<CoachingSubscriptionPeriod> CoachingSubscriptionPeriods => Set<CoachingSubscriptionPeriod>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        ConfigureAdminAudit(modelBuilder);
        ConfigureIdempotency(modelBuilder);
        ConfigureCmsAndSubscriptions(modelBuilder);

        // Schema
        modelBuilder.HasDefaultSchema("coaching");

        // MassTransit inbox/outbox tables keep database changes and published
        // events in the same transaction and make consumer delivery idempotent.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    private static void ConfigureAdminAudit(ModelBuilder modelBuilder)
    {
        var audit = modelBuilder.Entity<AdminAuditRecord>();
        audit.ToTable("AdminAuditRecords");
        audit.HasKey(record => record.Id);
        audit.Property(record => record.ServiceName).HasMaxLength(150);
        audit.Property(record => record.ActorUserId).HasMaxLength(100);
        audit.Property(record => record.ActorRoles).HasMaxLength(500);
        audit.Property(record => record.TenantId).HasMaxLength(100);
        audit.Property(record => record.HttpMethod).HasMaxLength(10);
        audit.Property(record => record.Path).HasMaxLength(500);
        audit.Property(record => record.CorrelationId).HasMaxLength(100);
        audit.Property(record => record.ClientIp).HasMaxLength(64);
        audit.Property(record => record.UserAgent).HasMaxLength(256);
        audit.Property(record => record.Action).HasMaxLength(32);
        audit.Property(record => record.ResourceType).HasMaxLength(100);
        audit.Property(record => record.ResourceId).HasMaxLength(100);
        audit.Property(record => record.ChangedFieldsJson).HasMaxLength(2_000);
        audit.HasIndex(record => new { record.OccurredAt, record.Id });
        audit.HasIndex(record => new { record.ActorUserId, record.OccurredAt });
        audit.HasIndex(record => new { record.ResourceType, record.ResourceId, record.OccurredAt });
    }

    private static void ConfigureIdempotency(ModelBuilder modelBuilder)
    {
        var idempotency = modelBuilder.Entity<IdempotencyRecord>();
        idempotency.ToTable("IdempotencyRecords");
        idempotency.HasKey(record => record.Id);
        idempotency.Property(record => record.Scope).HasMaxLength(150).IsRequired();
        idempotency.Property(record => record.Key).HasMaxLength(128).IsRequired();
        idempotency.Property(record => record.RequestHash).HasMaxLength(64).IsRequired();
        idempotency.HasIndex(record => new { record.Scope, record.Key }).IsUnique();
    }

    private static void ConfigureCmsAndSubscriptions(ModelBuilder modelBuilder)
    {
        var newsletterSubscribers = modelBuilder.Entity<CoachingNewsletterSubscriber>();
        newsletterSubscribers.ToTable("cms_newsletter_subscribers", table =>
        {
            table.HasCheckConstraint("ck_coaching_newsletter_subscriber_status", "\"Status\" IN ('PendingConfirmation', 'Active', 'Unsubscribed')");
        });
        newsletterSubscribers.HasKey(subscriber => subscriber.Id);
        newsletterSubscribers.Property(subscriber => subscriber.Email).HasMaxLength(320).IsRequired();
        newsletterSubscribers.Property(subscriber => subscriber.Status).HasMaxLength(30).IsRequired();
        newsletterSubscribers.Property(subscriber => subscriber.Source).HasMaxLength(80).IsRequired();
        newsletterSubscribers.Property(subscriber => subscriber.ConsentTextVersion).HasMaxLength(80).IsRequired();
        newsletterSubscribers.Property(subscriber => subscriber.ConfirmationTokenHash).HasMaxLength(64);
        newsletterSubscribers.Property(subscriber => subscriber.UnsubscribeTokenHash).HasMaxLength(64);
        newsletterSubscribers.HasIndex(subscriber => subscriber.Email).IsUnique();
        newsletterSubscribers.HasIndex(subscriber => subscriber.Status);
        newsletterSubscribers.HasIndex(subscriber => subscriber.ConfirmationTokenHash).IsUnique();
        newsletterSubscribers.HasIndex(subscriber => subscriber.UnsubscribeTokenHash).IsUnique();

        var unsubscribeTokens = modelBuilder.Entity<CoachingNewsletterUnsubscribeToken>();
        unsubscribeTokens.ToTable("cms_newsletter_unsubscribe_tokens");
        unsubscribeTokens.HasKey(token => token.Id);
        unsubscribeTokens.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        unsubscribeTokens.HasIndex(token => token.TokenHash).IsUnique();
        unsubscribeTokens.HasOne<CoachingNewsletterSubscriber>().WithMany()
            .HasForeignKey(token => token.SubscriberId).OnDelete(DeleteBehavior.Cascade);

        var entries = modelBuilder.Entity<CoachingCmsEntry>();
        entries.ToTable("cms_entries");
        entries.HasKey(entry => entry.Id);
        entries.Property(entry => entry.Kind).HasMaxLength(20).IsRequired();
        entries.Property(entry => entry.Group).HasMaxLength(80);
        entries.Property(entry => entry.Title).HasMaxLength(200).IsRequired();
        entries.Property(entry => entry.Slug).HasMaxLength(160).IsRequired();
        entries.Property(entry => entry.Summary).HasMaxLength(500);
        entries.Property(entry => entry.Content).HasColumnType("text").IsRequired();
        entries.Property(entry => entry.SeoTitle).HasMaxLength(200);
        entries.Property(entry => entry.SeoDescription).HasMaxLength(500);
        entries.Property(entry => entry.Eyebrow).HasMaxLength(120);
        entries.Property(entry => entry.LinkLabel).HasMaxLength(150);
        entries.Property(entry => entry.LinkUrl).HasMaxLength(500);
        entries.Property(entry => entry.SecondaryLinkLabel).HasMaxLength(150);
        entries.Property(entry => entry.SecondaryLinkUrl).HasMaxLength(500);
        entries.Property(entry => entry.ImageUrl).HasMaxLength(500);
        entries.Property(entry => entry.Author).HasMaxLength(150);
        entries.Property(entry => entry.CoverImageUrl).HasMaxLength(500);
        entries.Property(entry => entry.TestimonialConsentConfirmed).HasDefaultValue(false);
        entries.Property(entry => entry.TagsJson).HasColumnType("jsonb").IsRequired();
        entries.HasIndex(entry => new { entry.Kind, entry.Slug }).IsUnique();
        entries.HasIndex(entry => new { entry.Kind, entry.Group, entry.SortOrder });
        entries.HasIndex(entry => new { entry.Kind, entry.IsPublished, entry.ScheduledPublishAt });

        var revisions = modelBuilder.Entity<CoachingCmsRevision>();
        revisions.ToTable("cms_revisions");
        revisions.HasKey(revision => revision.Id);
        revisions.Property(revision => revision.Kind).HasMaxLength(20).IsRequired();
        revisions.Property(revision => revision.PayloadJson).HasColumnType("jsonb").IsRequired();
        revisions.HasIndex(revision => new { revision.EntryId, revision.Version }).IsUnique();

        var navigation = modelBuilder.Entity<CoachingCmsNavigationItem>();
        navigation.ToTable("cms_navigation_items");
        navigation.HasKey(item => item.Id);
        navigation.Property(item => item.Menu).HasMaxLength(80).IsRequired();
        navigation.Property(item => item.Label).HasMaxLength(150).IsRequired();
        navigation.Property(item => item.Url).HasMaxLength(500).IsRequired();
        navigation.Property(item => item.Icon).HasMaxLength(80);
        navigation.HasIndex(item => new { item.Menu, item.SortOrder });

        var media = modelBuilder.Entity<CoachingCmsMediaAsset>();
        media.ToTable("cms_media_assets");
        media.HasKey(asset => asset.Id);
        media.Property(asset => asset.FileName).HasMaxLength(255).IsRequired();
        media.Property(asset => asset.ContentType).HasMaxLength(100).IsRequired();
        media.Property(asset => asset.SizeBytes).IsRequired();
        media.Property(asset => asset.Sha256).HasMaxLength(64).IsRequired();
        media.Property(asset => asset.StorageKey).HasMaxLength(300).IsRequired();
        media.Property(asset => asset.AltText).HasMaxLength(300);
        media.HasIndex(asset => new { asset.IsDeleted, asset.CreatedAt });
        media.HasIndex(asset => asset.StorageKey).IsUnique();

        var plans = modelBuilder.Entity<CoachingSubscriptionPlan>();
        plans.ToTable("subscription_plans", table =>
        {
            table.HasCheckConstraint("ck_coaching_subscription_plans_price", "\"Price\" >= 0");
            table.HasCheckConstraint("ck_coaching_subscription_plans_trial", "\"TrialDays\" BETWEEN 0 AND 90 AND (\"TrialDays\" = 0 OR \"Audience\" <> 'Institution')");
            table.HasCheckConstraint("ck_coaching_subscription_plans_extra_seat", "\"ExtraSeatPrice\" IS NULL OR (\"ExtraSeatPrice\" > 0 AND \"Audience\" = 'Institution')");
        });
        plans.HasKey(plan => plan.Id);
        plans.Property(plan => plan.Slug).HasMaxLength(80).IsRequired();
        plans.Property(plan => plan.Name).HasMaxLength(150).IsRequired();
        plans.Property(plan => plan.Description).HasMaxLength(1000).IsRequired();
        plans.Property(plan => plan.Audience).HasMaxLength(20).IsRequired();
        plans.Property(plan => plan.Price).HasPrecision(12, 2);
        plans.Property(plan => plan.ExtraSeatPrice).HasPrecision(12, 2);
        plans.Property(plan => plan.BillingPeriod).HasMaxLength(20).IsRequired();
        plans.Property(plan => plan.FeaturesJson).HasColumnType("jsonb").IsRequired();
        plans.HasIndex(plan => plan.Slug).IsUnique();
        plans.HasIndex(plan => new { plan.IsPublic, plan.IsActive, plan.SortOrder });

        var subscriptions = modelBuilder.Entity<CoachingSubscription>();
        subscriptions.ToTable("subscriptions", table =>
        {
            table.HasCheckConstraint("ck_coaching_subscription_owner", "((\"UserId\" IS NOT NULL)::int + (\"InstitutionId\" IS NOT NULL)::int) = 1");
            table.HasCheckConstraint("ck_coaching_subscription_dates", "\"EndDate\" >= \"StartDate\"");
            table.HasCheckConstraint("ck_coaching_subscription_extra_seats", "\"ExtraSeats\" >= 0 AND (\"ExtraSeats\" = 0 OR \"InstitutionId\" IS NOT NULL)");
        });
        subscriptions.HasKey(subscription => subscription.Id);
        subscriptions.Property(subscription => subscription.Status).HasMaxLength(20).IsRequired();
        subscriptions.Property(subscription => subscription.UserName).HasMaxLength(200);
        subscriptions.Property(subscription => subscription.UserEmail).HasMaxLength(320);
        subscriptions.Property(subscription => subscription.Notes).HasMaxLength(2000);
        subscriptions.Property(subscription => subscription.PaymentReference).HasMaxLength(100);
        subscriptions.HasIndex(subscription => new { subscription.UserId, subscription.Status, subscription.EndDate });
        subscriptions.HasIndex(subscription => new { subscription.InstitutionId, subscription.Status, subscription.EndDate });
        subscriptions.HasIndex(subscription => new { subscription.InstitutionId, subscription.PaymentReference })
            .IsUnique()
            .HasFilter("\"InstitutionId\" IS NOT NULL AND \"PaymentReference\" IS NOT NULL");
        subscriptions.HasIndex(subscription => subscription.BankTransferRequestId).IsUnique();

        var seats = modelBuilder.Entity<CoachingSubscriptionSeat>();
        seats.ToTable("subscription_seats");
        seats.HasKey(seat => seat.Id);
        seats.Property(seat => seat.SuspensionReason).HasMaxLength(500);
        seats.HasIndex(seat => new { seat.SubscriptionId, seat.StudentId }).IsUnique();
        seats.HasIndex(seat => new { seat.StudentId, seat.IsSuspended });

        var periods = modelBuilder.Entity<CoachingSubscriptionPeriod>();
        periods.ToTable("subscription_periods", table =>
        {
            table.HasCheckConstraint("ck_coaching_subscription_period_dates", "\"EndDate\" >= \"StartDate\"");
            table.HasCheckConstraint("ck_coaching_subscription_period_status", "\"Status\" IN ('Active', 'Cancelled')");
            table.HasCheckConstraint("ck_coaching_subscription_period_source", "\"Source\" IN ('BankTransfer', 'Manual', 'Trial')");
        });
        periods.HasKey(period => period.Id);
        periods.Property(period => period.Source).HasMaxLength(20).IsRequired();
        periods.Property(period => period.Status).HasMaxLength(20).IsRequired();
        periods.HasIndex(period => new { period.SubscriptionId, period.StartDate });
        periods.HasIndex(period => period.BankTransferRequestId).IsUnique()
            .HasFilter("\"BankTransferRequestId\" IS NOT NULL");

        var settings = modelBuilder.Entity<CoachingSubscriptionSettings>();
        settings.ToTable("subscription_settings");
        settings.HasKey(value => value.Id);
        settings.Property(value => value.Currency).HasMaxLength(3).IsRequired();
        settings.Property(value => value.AccountHolder).HasMaxLength(200);
        settings.Property(value => value.BankName).HasMaxLength(200);
        settings.Property(value => value.Iban).HasMaxLength(34);
        settings.Property(value => value.PaymentInstructions).HasMaxLength(2000);
        settings.Property(value => value.SellerTitle).HasMaxLength(200);
        settings.Property(value => value.SellerAddress).HasMaxLength(500);
        settings.Property(value => value.SellerTaxOffice).HasMaxLength(100);
        settings.Property(value => value.SellerTaxNumber).HasMaxLength(20);
        settings.Property(value => value.SellerMersisNumber).HasMaxLength(20);
        settings.Property(value => value.SellerEmail).HasMaxLength(320);
        settings.Property(value => value.SellerPhone).HasMaxLength(40);

        var requests = modelBuilder.Entity<CoachingBankTransferRequest>();
        requests.ToTable("bank_transfer_requests", table =>
        {
            table.HasCheckConstraint("ck_coaching_bank_transfer_discount", "\"DiscountAmount\" >= 0 AND (\"CouponCode\" IS NOT NULL OR \"DiscountAmount\" = 0)");
            table.HasCheckConstraint("ck_coaching_bank_transfer_withdrawal", "\"WithdrawalStatus\" IS NULL OR \"WithdrawalStatus\" IN ('Requested', 'Approved', 'Rejected')");
        });
        requests.HasKey(request => request.Id);
        requests.Property(request => request.UserName).HasMaxLength(200).IsRequired();
        requests.Property(request => request.UserEmail).HasMaxLength(320).IsRequired();
        requests.Property(request => request.Currency).HasMaxLength(3).IsRequired();
        requests.Property(request => request.PaymentReference).HasMaxLength(120).IsRequired();
        requests.Property(request => request.PayerName).HasMaxLength(200);
        requests.Property(request => request.Note).HasMaxLength(1000);
        requests.Property(request => request.Status).HasMaxLength(20).IsRequired();
        requests.Property(request => request.ReviewNote).HasMaxLength(1000);
        requests.Property(request => request.Version).IsConcurrencyToken();
        requests.Property(request => request.ListPrice).HasPrecision(12, 2);
        requests.Property(request => request.CouponCode).HasMaxLength(30);
        requests.Property(request => request.DiscountAmount).HasPrecision(12, 2);
        requests.Property(request => request.SalesContractVersion).HasMaxLength(20);
        requests.Property(request => request.SalesDocumentsJson).HasColumnType("jsonb");
        requests.Property(request => request.WithdrawalStatus).HasMaxLength(20);
        requests.Property(request => request.WithdrawalReason).HasMaxLength(500);
        requests.Property(request => request.WithdrawalReviewNote).HasMaxLength(500);
        requests.HasIndex(request => new { request.WithdrawalStatus, request.WithdrawalRequestedAt }).HasFilter("\"WithdrawalStatus\" IS NOT NULL");
        requests.HasIndex(request => new { request.UserId, request.CouponCode }).HasFilter("\"CouponCode\" IS NOT NULL");
        requests.HasIndex(request => new { request.UserId, request.PaymentReference }).IsUnique();
        requests.HasIndex(request => new { request.Status, request.CreatedAt });
        requests.HasIndex(request => new { request.UserId, request.Status });
        requests.HasIndex(request => request.SubmittedByUserId);

        var payments = modelBuilder.Entity<CoachingPaymentRecord>();
        payments.ToTable("payment_records", table => table.HasCheckConstraint("ck_coaching_payment_owner",
            "\"UserId\" IS NOT NULL OR \"InstitutionId\" IS NOT NULL"));
        payments.HasKey(payment => payment.Id);
        payments.Property(payment => payment.UserName).HasMaxLength(200).IsRequired();
        payments.Property(payment => payment.UserEmail).HasMaxLength(320).IsRequired();
        payments.Property(payment => payment.PlanName).HasMaxLength(150).IsRequired();
        payments.Property(payment => payment.PlanName).HasMaxLength(150).IsRequired();
        payments.Property(payment => payment.Amount).HasPrecision(12, 2);
        payments.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
        payments.Property(payment => payment.Status).HasMaxLength(20).IsRequired();
        payments.Property(payment => payment.Provider).HasMaxLength(30).IsRequired();
        payments.Property(payment => payment.Reference).HasMaxLength(120);
        payments.HasIndex(payment => payment.BankTransferRequestId).IsUnique();
        payments.HasIndex(payment => new { payment.CreatedAt, payment.Status });
        payments.HasIndex(payment => payment.InstitutionId);
        payments.HasIndex(payment => payment.UserId);

        // Roadmap O10: trials, coupons and recorded invoices.
        var trials = modelBuilder.Entity<CoachingSubscriptionTrial>();
        trials.ToTable("subscription_trials", table => table.HasCheckConstraint("ck_coaching_subscription_trial_audience", "\"Audience\" IN ('Individual', 'Teacher')"));
        trials.HasKey(trial => trial.Id);
        trials.Property(trial => trial.Audience).HasMaxLength(20).IsRequired();
        trials.HasIndex(trial => new { trial.UserId, trial.Audience }).IsUnique();

        var coupons = modelBuilder.Entity<CoachingCoupon>();
        coupons.ToTable("coupons", table =>
        {
            table.HasCheckConstraint("ck_coaching_coupon_kind", "(\"Kind\" = 'Percent' AND \"Value\" > 0 AND \"Value\" <= 100) OR (\"Kind\" = 'Amount' AND \"Value\" > 0)");
            table.HasCheckConstraint("ck_coaching_coupon_uses", "\"UsedCount\" >= 0 AND (\"MaxUses\" IS NULL OR \"MaxUses\" > 0)");
        });
        coupons.HasKey(coupon => coupon.Id);
        coupons.Property(coupon => coupon.Code).HasMaxLength(30).IsRequired();
        coupons.Property(coupon => coupon.Kind).HasMaxLength(10).IsRequired();
        coupons.Property(coupon => coupon.Value).HasPrecision(12, 2);
        coupons.Property(coupon => coupon.PlanIdsJson).HasColumnType("jsonb").IsRequired();
        coupons.HasIndex(coupon => coupon.Code).IsUnique();

        var invoices = modelBuilder.Entity<CoachingInvoice>();
        invoices.ToTable("invoices");
        invoices.HasKey(invoice => invoice.Id);
        invoices.Property(invoice => invoice.InvoiceNumber).HasMaxLength(40).IsRequired();
        invoices.Property(invoice => invoice.FileName).HasMaxLength(255).IsRequired();
        invoices.Property(invoice => invoice.StorageKey).HasMaxLength(200).IsRequired();
        invoices.Property(invoice => invoice.Sha256).HasMaxLength(64).IsRequired();
        invoices.HasOne<CoachingPaymentRecord>().WithMany().HasForeignKey(invoice => invoice.PaymentRecordId).OnDelete(DeleteBehavior.Restrict);
        invoices.HasIndex(invoice => invoice.PaymentRecordId).IsUnique();
        invoices.HasIndex(invoice => invoice.InvoiceNumber).IsUnique();
        invoices.HasIndex(invoice => invoice.StorageKey).IsUnique();

        // Roadmap O10b: an institution's request for extra seats on a running license.
        var seatRequests = modelBuilder.Entity<CoachingSeatRequest>();
        seatRequests.ToTable("seat_requests", table =>
        {
            table.HasCheckConstraint("ck_coaching_seat_request_status", "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Withdrawn')");
            table.HasCheckConstraint("ck_coaching_seat_request_amounts", "\"Seats\" BETWEEN 1 AND 1000 AND \"UnitPrice\" > 0 AND \"Amount\" >= 0 AND \"RemainingDays\" > 0 AND \"DurationDays\" > 0");
        });
        seatRequests.HasKey(request => request.Id);
        seatRequests.Property(request => request.UnitPrice).HasPrecision(12, 2);
        seatRequests.Property(request => request.Amount).HasPrecision(12, 2);
        seatRequests.Property(request => request.Currency).HasMaxLength(3).IsRequired();
        seatRequests.Property(request => request.PaymentReference).HasMaxLength(120).IsRequired();
        seatRequests.Property(request => request.Note).HasMaxLength(1000);
        seatRequests.Property(request => request.Status).HasMaxLength(20).IsRequired();
        seatRequests.Property(request => request.ReviewNote).HasMaxLength(1000);
        seatRequests.HasOne<CoachingSubscription>().WithMany().HasForeignKey(request => request.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        seatRequests.HasOne<CoachingPaymentRecord>().WithMany().HasForeignKey(request => request.PaymentRecordId).OnDelete(DeleteBehavior.Restrict);
        seatRequests.HasIndex(request => request.SubscriptionId).IsUnique().HasFilter("\"Status\" = 'Pending'").HasDatabaseName("ux_coaching_seat_requests_one_pending");
        seatRequests.HasIndex(request => new { request.Status, request.CreatedAt });
        seatRequests.HasIndex(request => request.InstitutionId);
        seatRequests.HasIndex(request => request.PaymentRecordId).IsUnique();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries<AdminAuditRecord>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Admin audit records are append-only.");
        }

        AdvanceConcurrencyTokens();

        // Timestamps are managed by entities themselves (CreatedAt defaults to UtcNow, UpdatedAt set manually)
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConstraintViolation(exception))
        {
            throw new IdempotencyConflictException(exception);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is PostgresException { SqlState: "23P01", ConstraintName: "coaching_session_overlap" })
        {
            throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException("Session.Conflict",
                "Öğretmen veya öğrencinin bu saat aralığında başka bir seansı var. Başka bir saat seçin.");
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw ToConcurrencyException(exception);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (ChangeTracker.Entries<AdminAuditRecord>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Admin audit records are append-only.");
        }

        AdvanceConcurrencyTokens();

        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is PostgresException { SqlState: "23P01", ConstraintName: "coaching_session_overlap" })
        {
            throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException("Session.Conflict",
                "Öğretmen veya öğrencinin bu saat aralığında başka bir seansı var. Başka bir saat seçin.");
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw ToConcurrencyException(exception);
        }
    }

    private void AdvanceConcurrencyTokens()
    {
        var deletedFees = ChangeTracker.Entries<CoachingFeeAgreement>().Where(x => x.State == EntityState.Deleted).Select(x => x.Entity.Id).ToHashSet();
        if (ChangeTracker.Entries().Any(x => x.Entity is CoachingFeePayment payment
                && (x.State == EntityState.Modified || x.State == EntityState.Deleted && !deletedFees.Contains(payment.AgreementId))
            || x.Entity is CoachingFeeInstallment installment
                && (x.State == EntityState.Modified || x.State == EntityState.Deleted && !deletedFees.Contains(installment.AgreementId))))
            throw new InvalidOperationException("Fee installments and receipts are immutable; cancel or reverse instead.");
        foreach (var entry in ChangeTracker.Entries<AggregateRoot>())
        {
            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            var originalVersion = entry.Property(goal => goal.Version).OriginalValue;
            entry.Property(goal => goal.Version).CurrentValue = originalVersion + 1;
        }
    }

    private static SharedConcurrencyException ToConcurrencyException(DbUpdateConcurrencyException exception)
    {
        var entry = exception.Entries.FirstOrDefault();
        var entityName = entry?.Metadata.ClrType.Name ?? "Entity";
        var entityId = entry?.Property("Id").CurrentValue ?? "unknown";
        return new SharedConcurrencyException(entityName, entityId);
    }

    private static bool IsIdempotencyConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && string.Equals(
            postgresException.ConstraintName,
            "IX_IdempotencyRecords_Scope_Key",
            StringComparison.Ordinal);
}
