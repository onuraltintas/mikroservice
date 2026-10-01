using Coaching.Application.Interfaces;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Npgsql;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingSubscriptionPostgresRetryTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("create-transfer")]
    [InlineData("review-transfer")]
    [InlineData("grant-institution")]
    [InlineData("grant-teacher")]
    [InlineData("update-teacher")]
    [InlineData("assign-seat")]
    [InlineData("create-transfer", true)]
    [InlineData("review-transfer", true)]
    [InlineData("grant-teacher", true)]
    [InlineData("update-teacher", true)]
    [InlineData("assign-seat", true)]
    public async Task SubscriptionWrites_WorkWithProductionPostgresRetryPolicy(string operation, bool loseCommitAcknowledgement = false)
    {
        var failure = new LostCommitAcknowledgement();
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, options => options.EnableRetryOnFailure(2, TimeSpan.Zero, null))
            .AddInterceptors(failure).Options);
        // The fixture owns this disposable database; never use a production connection here.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var actor = Guid.NewGuid();
            var institution = Guid.NewGuid();
            var student = Guid.NewGuid();
            var individualPlan = new CoachingSubscriptionPlan { Slug = "student", Name = "Student", Price = 499, DurationDays = 365 };
            var teacherPlan = new CoachingSubscriptionPlan { Slug = "teacher", Name = "Teacher", Audience = "Teacher", Price = 999, DurationDays = 365, IncludedStudentSeats = 1 };
            var institutionPlan = new CoachingSubscriptionPlan { Slug = "institution", Name = "Institution", Audience = "Institution", Price = 999, DurationDays = 365, IncludedStudentSeats = 1 };
            var transfer = new CoachingBankTransferRequest { UserId = user, PlanId = individualPlan.Id, Amount = 499, PaymentReference = "POSTGRES-REVIEW" };
            var teacherSubscription = new CoachingSubscription { PlanId = teacherPlan.Id, UserId = user, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(364), Status = operation == "grant-teacher" ? "Suspended" : "Active" };
            db.CoachingSubscriptionPlans.AddRange(individualPlan, teacherPlan, institutionPlan);
            if (operation == "review-transfer") db.CoachingBankTransferRequests.Add(transfer);
            db.CoachingSubscriptions.Add(teacherSubscription);
            db.CoachingSubscriptionSettings.Add(new CoachingSubscriptionSettings { BankTransferEnabled = true, AccountHolder = "Test", BankName = "Test", Iban = "TR330006100519786457841326" });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            failure.Armed = loseCommitAcknowledgement;
            var service = new CoachingSubscriptionService(db, new IdentityAuthorization());

            switch (operation)
            {
                case "create-transfer":
                    var request = new CoachingBankTransferRequestCreate(individualPlan.Id, "POSTGRES-CREATE", "Test", null);
                    var created = await service.CreateBankTransferRequestAsync(user, "Test", "test@example.invalid", request, "postgres-create-key-01");
                    created.Should().NotBeNull();
                    var replay = await service.CreateBankTransferRequestAsync(user, "Test", "test@example.invalid", request, "postgres-create-key-01");
                    replay!.Id.Should().Be(created!.Id);
                    (await db.CoachingBankTransferRequests.CountAsync()).Should().Be(1);
                    break;
                case "review-transfer":
                    var reviewed = await service.ReviewBankTransferRequestAsync(transfer.Id, new("Approved", null), actor, "postgres-review-key-01");
                    reviewed!.Status.Should().Be("Approved");
                    var reviewReplay = await service.ReviewBankTransferRequestAsync(transfer.Id, new("Approved", null), actor, "postgres-review-key-01");
                    reviewReplay!.SubscriptionId.Should().Be(reviewed.SubscriptionId);
                    (await db.CoachingPaymentRecords.CountAsync()).Should().Be(1);
                    break;
                case "grant-institution":
                    (await service.CreateSubscriptionAsync(new(institutionPlan.Id, null, null, null, institution, [student], DateTime.UtcNow, null), actor)).Should().NotBeNull();
                    (await db.CoachingSubscriptionSeats.CountAsync()).Should().Be(1);
                    break;
                case "grant-teacher":
                    (await service.CreateSubscriptionAsync(new(teacherPlan.Id, user, null, null, null, [], DateTime.UtcNow, null), actor)).Should().NotBeNull();
                    (await db.CoachingSubscriptions.CountAsync()).Should().Be(2);
                    break;
                case "update-teacher":
                    (await service.UpdateSubscriptionAsync(teacherSubscription.Id, new("Active", DateTime.UtcNow.AddDays(365), null), actor)).Should().NotBeNull();
                    break;
                case "assign-seat":
                    (await service.AssignMyTeacherStudentSeatAsync(user, student)).Should().BeTrue();
                    (await service.AssignMyTeacherStudentSeatAsync(user, student)).Should().BeTrue();
                    (await service.AssignMyTeacherStudentSeatAsync(user, Guid.NewGuid())).Should().BeFalse();
                    (await db.CoachingSubscriptionSeats.CountAsync()).Should().Be(1);
                    break;
            }
            failure.Failures.Should().Be(loseCommitAcknowledgement ? 1 : 0);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class LostCommitAcknowledgement : DbTransactionInterceptor
    {
        public bool Armed { get; set; }
        public int Failures { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                Armed = false;
                Failures++;
                throw new NpgsqlException("Injected connection loss after commit", new TimeoutException());
            }
            return Task.CompletedTask;
        }
    }

    private sealed class IdentityAuthorization : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId, bool isSystemAdministrator, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }
}
