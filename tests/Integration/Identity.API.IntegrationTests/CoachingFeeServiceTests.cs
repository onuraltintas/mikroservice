using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Finance;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Finance;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingFeeServiceTests(PostgresFixture postgres)
{
    private static readonly DateOnly Today = new(2026, 10, 13);
    private static readonly Clock Time = new();
    private static CoachingFeeCreate Input() => new(Guid.NewGuid(), "Koçluk dönemi", 1000m, [new(Today.AddDays(-1), 400m), new(Today.AddMonths(1), 600m)]);
    private DbContextOptions<CoachingDbContext> Options => new DbContextOptionsBuilder<CoachingDbContext>()
        .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options;

    [Fact]
    public async Task InstitutionAndIndependentCoachOwnSeparateFeesAndStudentIsReadOnly()
    {
        await using var db = new CoachingDbContext(Options); await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try {
            var student = Guid.NewGuid(); var institution = Guid.NewGuid(); var manager = Guid.NewGuid(); var coach = Guid.NewGuid();
            var actor = new Actor(manager, "InstitutionAdmin"); var identity = new Identity(student);
            var scope = new Scope(new(false, institution, [student])); var service = new CoachingFeeService(db, actor, identity, scope, Time);
            var fee = await service.CreateAsync(student, Input()); Assert.Equal(institution, fee.InstitutionId); Assert.Null(fee.CoachId);
            var input = Input(); var other = await service.CreateAsync(student, input);
            Assert.Equal(other.Id, (await service.CreateAsync(student, input)).Id);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(student, input with { Title = "Different retry" }));
            actor.UserId = coach; actor.Roles = ["Teacher"]; identity.Institution = institution;
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(student, Input()));
            Assert.Empty((await service.GetStudentAsync(student)).Agreements);
            identity.Institution = null; var independent = await service.CreateAsync(student, Input());
            Assert.Equal(coach, independent.CoachId); Assert.Null(independent.InstitutionId);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PayAsync(student, fee.Id, new(Guid.NewGuid(), fee.Version, 100m, CoachingFeePaymentMethod.Cash, Today, null)));
            actor.UserId = student; actor.Roles = ["Student"];
            var own = await service.GetStudentAsync(student); Assert.Equal(3, own.TotalCount); Assert.False(own.CanManage); Assert.Equal(3000m, own.Balance);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(student, Input()));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetStudentAsync(Guid.NewGuid()));
            actor.UserId = manager; actor.Roles = ["InstitutionOwner"]; scope.Value = new(false, Guid.NewGuid(), [student]);
            Assert.Empty((await service.GetStudentAsync(student)).Agreements);
            actor.Roles = ["SystemAdmin"]; Assert.Equal(3, (await service.GetStudentAsync(student)).TotalCount);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(student, Input()));
        } finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task PartialPaymentsAllocateEarliestInstallmentsAndCashStatementPreservesReversals()
    {
        await using var db = new CoachingDbContext(Options); await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try {
            var student = Guid.NewGuid(); var actor = new Actor(Guid.NewGuid(), "InstitutionAdmin");
            var scope = new Scope(new(false, Guid.NewGuid(), [student])); var service = new CoachingFeeService(db, actor, new Identity(student), scope, Time);
            var fee = await service.CreateAsync(student, Input()); Assert.Equal(400m, fee.OverdueAmount);
            var paymentId = Guid.NewGuid(); var payment = new CoachingFeePaymentInput(paymentId, fee.Version, 250m, CoachingFeePaymentMethod.Cash, Today, "Makbuz");
            var paid = await service.PayAsync(student, fee.Id, payment); Assert.Equal(750m, paid.Balance); Assert.Equal(150m, paid.OverdueAmount);
            Assert.Equal(paid.Version, (await service.PayAsync(student, fee.Id, payment)).Version);
            Assert.Equal(150m, paid.Installments[0].Balance); Assert.Equal(600m, paid.Installments[1].Balance);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PayAsync(student, fee.Id, payment with { Amount = 200m }));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PayAsync(student, fee.Id, payment with { RequestId = Guid.NewGuid() }));
            var second = await service.PayAsync(student, fee.Id, new(Guid.NewGuid(), paid.Version, 100m, CoachingFeePaymentMethod.BankTransfer, Today, null));
            Assert.Equal(650m, second.Balance); Assert.Equal(50m, second.OverdueAmount);
            var history = await service.GetPaymentsAsync(student, fee.Id, 1, 1); Assert.Single(history.Items); Assert.Equal(2, history.TotalCount);
            var reversed = await service.ReverseAsync(student, fee.Id, paymentId, new(Guid.NewGuid(), second.Version, Today, "Hatalı tahsilat"));
            Assert.Equal(900m, reversed.Balance); Assert.Equal(300m, reversed.OverdueAmount);
            var cash = await service.GetCashAsync(Today, Today); Assert.Equal(0m, cash.CashTotal); Assert.Equal(100m, cash.BankTransferTotal); Assert.Equal(3, cash.TotalCount);
            Assert.Contains(cash.Items, x => x.ReversalOfPaymentId == paymentId && x.Amount == -250m);
            scope.Value = new(false, scope.Value.InstitutionId, []);
            Assert.Equal(900m, (await service.GetStudentAsync(student)).Balance);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(student, Input()));
            await service.PayAsync(student, fee.Id, new(Guid.NewGuid(), reversed.Version, 50m, CoachingFeePaymentMethod.Cash, Today, null));
            Assert.Equal(2, await db.CoachingFeeAgreements.Select(x => x.Payments.Count(p => p.Amount > 0 && !x.Payments.Any(r => r.ReversalOfPaymentId == p.Id))).SingleAsync());
        } finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task ConcurrentReceiptsCannotOverpayAndIdenticalReceiptRetriesDoNotDuplicate()
    {
        await using var db = new CoachingDbContext(Options); await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try {
            var student = Guid.NewGuid(); var actor = new Actor(Guid.NewGuid(), "InstitutionAdmin");
            var scope = new Scope(new(false, Guid.NewGuid(), [student])); var identity = new Identity(student);
            var service = new CoachingFeeService(db, actor, identity, scope, Time); var fee = await service.CreateAsync(student, Input());
            async Task<bool> Pay(Guid id) {
                await using var concurrent = new CoachingDbContext(Options);
                try { await new CoachingFeeService(concurrent, actor, identity, scope, Time).PayAsync(student, fee.Id,
                    new(id, fee.Version, 800m, CoachingFeePaymentMethod.Cash, Today, null)); return true; }
                catch (BusinessRuleException error) when (error.Code == "Finance.Conflict") { return false; }
            }
            var attempts = await Task.WhenAll(Pay(Guid.NewGuid()), Pay(Guid.NewGuid())); Assert.Single(attempts.Where(x => x));
            Assert.Equal(200m, (await service.GetStudentAsync(student)).Balance); Assert.Equal(1, await db.CoachingFeePayments.CountAsync());
        } finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task FutureReceiptsInvalidPagesAndImmutablePaymentUpdatesAreRejected()
    {
        await using var db = new CoachingDbContext(Options); await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try {
            var student = Guid.NewGuid(); var actor = new Actor(Guid.NewGuid(), "Teacher");
            var service = new CoachingFeeService(db, actor, new Identity(student), new Scope(CoachingAdminScope.Global), Time);
            var fee = await service.CreateAsync(student, Input());
            await Assert.ThrowsAsync<ArgumentException>(() => service.PayAsync(student, fee.Id, new(Guid.NewGuid(), fee.Version, 50m, CoachingFeePaymentMethod.Cash, Today.AddDays(1), null)));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetStudentAsync(student, 1, 101));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetCashAsync(Today.AddDays(1), Today));
            await service.PayAsync(student, fee.Id, new(Guid.NewGuid(), fee.Version, 50m, CoachingFeePaymentMethod.Cash, Today, null));
            var payment = await db.CoachingFeePayments.SingleAsync(); db.Entry(payment).Property(x => x.Amount).CurrentValue = 60m;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
            Assert.Equal(50m, (await db.CoachingFeePayments.AsNoTracking().SingleAsync()).Amount);
        } finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero); }
    private sealed class Actor(Guid id, params string[] roles) : ICurrentUserService {
        public Guid? UserId { get; set; } = id; public IEnumerable<string> Roles { get; set; } = roles;
        public string? Email => null; public string? FullName => null; public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
    }
    private sealed class Scope(CoachingAdminScope value) : ICoachingAdminScopeAuthorization {
        public CoachingAdminScope Value { get; set; } = value;
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(Value);
    }
    private sealed class Identity(Guid student) : ICoachingIdentityAuthorizationClient {
        public Guid? Institution { get; set; }
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> students, Guid? institution, bool admin, CancellationToken ct) {
            if (!students.SequenceEqual([student])) throw new BusinessRuleException("Authorization.Forbidden", "Foreign student"); return Task.FromResult(Institution);
        }
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> students, CancellationToken ct) => throw new NotImplementedException();
    }
}
