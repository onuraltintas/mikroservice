using Microsoft.EntityFrameworkCore;
using Coaching.Domain.Entities;
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
    public CoachingDbContext(DbContextOptions<CoachingDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<StudyCatalogLesson> StudyCatalogLessons => Set<StudyCatalogLesson>();
    public DbSet<StudyCatalogUnit> StudyCatalogUnits => Set<StudyCatalogUnit>();
    public DbSet<StudyCatalogTopic> StudyCatalogTopics => Set<StudyCatalogTopic>();
    public DbSet<TargetUniversityProgram> TargetUniversityPrograms => Set<TargetUniversityProgram>();
    public DbSet<TargetSchool> TargetSchools => Set<TargetSchool>();
    public DbSet<StudyPlanRevision> StudyPlanRevisions => Set<StudyPlanRevision>();
    public DbSet<StudyPlanTask> StudyPlanTasks => Set<StudyPlanTask>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentStudent> AssignmentStudents => Set<AssignmentStudent>();
    public DbSet<AssignmentSubmissionAttachment> AssignmentSubmissionAttachments => Set<AssignmentSubmissionAttachment>();
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
    public DbSet<CoachingSubscriptionPlan> CoachingSubscriptionPlans => Set<CoachingSubscriptionPlan>();
    public DbSet<CoachingSubscription> CoachingSubscriptions => Set<CoachingSubscription>();
    public DbSet<CoachingSubscriptionSeat> CoachingSubscriptionSeats => Set<CoachingSubscriptionSeat>();
    public DbSet<CoachingSubscriptionSettings> CoachingSubscriptionSettings => Set<CoachingSubscriptionSettings>();
    public DbSet<CoachingBankTransferRequest> CoachingBankTransferRequests => Set<CoachingBankTransferRequest>();
    public DbSet<CoachingPaymentRecord> CoachingPaymentRecords => Set<CoachingPaymentRecord>();

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
        plans.ToTable("subscription_plans", table => table.HasCheckConstraint("ck_coaching_subscription_plans_price", "\"Price\" >= 0"));
        plans.HasKey(plan => plan.Id);
        plans.Property(plan => plan.Slug).HasMaxLength(80).IsRequired();
        plans.Property(plan => plan.Name).HasMaxLength(150).IsRequired();
        plans.Property(plan => plan.Description).HasMaxLength(1000).IsRequired();
        plans.Property(plan => plan.Audience).HasMaxLength(20).IsRequired();
        plans.Property(plan => plan.Price).HasPrecision(12, 2);
        plans.Property(plan => plan.BillingPeriod).HasMaxLength(20).IsRequired();
        plans.Property(plan => plan.FeaturesJson).HasColumnType("jsonb").IsRequired();
        plans.HasIndex(plan => plan.Slug).IsUnique();
        plans.HasIndex(plan => new { plan.IsPublic, plan.IsActive, plan.SortOrder });

        var subscriptions = modelBuilder.Entity<CoachingSubscription>();
        subscriptions.ToTable("subscriptions", table =>
        {
            table.HasCheckConstraint("ck_coaching_subscription_owner", "((\"UserId\" IS NOT NULL)::int + (\"InstitutionId\" IS NOT NULL)::int) = 1");
            table.HasCheckConstraint("ck_coaching_subscription_dates", "\"EndDate\" >= \"StartDate\"");
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

        var settings = modelBuilder.Entity<CoachingSubscriptionSettings>();
        settings.ToTable("subscription_settings");
        settings.HasKey(value => value.Id);
        settings.Property(value => value.Currency).HasMaxLength(3).IsRequired();
        settings.Property(value => value.AccountHolder).HasMaxLength(200);
        settings.Property(value => value.BankName).HasMaxLength(200);
        settings.Property(value => value.Iban).HasMaxLength(34);
        settings.Property(value => value.PaymentInstructions).HasMaxLength(2000);

        var requests = modelBuilder.Entity<CoachingBankTransferRequest>();
        requests.ToTable("bank_transfer_requests");
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
        requests.HasIndex(request => new { request.UserId, request.PaymentReference }).IsUnique();
        requests.HasIndex(request => new { request.Status, request.CreatedAt });
        requests.HasIndex(request => new { request.UserId, request.Status });

        var payments = modelBuilder.Entity<CoachingPaymentRecord>();
        payments.ToTable("payment_records");
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
        catch (DbUpdateConcurrencyException exception)
        {
            throw ToConcurrencyException(exception);
        }
    }

    private void AdvanceConcurrencyTokens()
    {
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
