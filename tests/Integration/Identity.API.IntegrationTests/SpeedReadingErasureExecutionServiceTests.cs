using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Privacy;
using SpeedReading.Domain.Profiles;
using SpeedReading.Domain.Institutions;
using SpeedReading.Application.Assignments;
using EduPlatform.Shared.Contracts.Reporting;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Privacy;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingErasureExecutionServiceTests
{
    [Fact]
    public async Task Continuation_keeps_no_approver_identity_and_subject_erasure_removes_own_decision()
    {
        await using var context = CreateContext();
        var student = Guid.NewGuid();
        var decision = SpeedReading.Domain.Programs.ProgramContinuation.Request(Guid.NewGuid(), student,
            Guid.NewGuid(), Guid.NewGuid(), 3, 3, "test-v1", false, DateTime.UtcNow);
        decision.Confirm(Guid.NewGuid(), DateTime.UtcNow);
        context.ProgramContinuations.Add(decision);
        await context.SaveChangesAsync();
        var inventory = new SpeedReadingPrivacyInventoryRepository(context);
        typeof(SpeedReading.Domain.Programs.ProgramContinuation).GetProperty("ConfirmedBy").Should().BeNull();
        (await inventory.CountByUserAsync(student, CancellationToken.None)).GetValueOrDefault("programContinuations").Should().Be(1);
        var service = new SpeedReadingErasureExecutionService(context, new Directory(null), TimeProvider.System);
        await service.ExecuteAsync(Request(student), CancellationToken.None);
        (await context.ProgramContinuations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Execute_ShouldDeleteOnlySubjectOwnedDataAndRemainIdempotent()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        context.UserProfiles.AddRange(
            SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), subjectId, DateTime.UtcNow),
            SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), otherUserId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var request = Request(subjectId);
        var service = new SpeedReadingErasureExecutionService(
            context, new Directory(null), TimeProvider.System);

        var first = await service.ExecuteAsync(request, CancellationToken.None);
        var replay = await service.ExecuteAsync(request, CancellationToken.None);

        first.DeletedRecordCount.Should().Be(1);
        replay.Id.Should().Be(first.Id);
        (await context.UserProfiles.CountAsync(profile => profile.UserId == subjectId)).Should().Be(0);
        (await context.UserProfiles.CountAsync(profile => profile.UserId == otherUserId)).Should().Be(1);
        (await context.ErasureExecutions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Execute_ErasesEverythingElseAndKeepsPaidRecordsForAYear()
    {
        // Review H2b: payments no longer block erasure. Paid records stay unchanged for a year (then anonymised);
        // failed card attempts, free text, memberships, invitations and e-mail-keyed rows are erased.
        await using var context = CreateContext();
        var subject = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var institution = Guid.NewGuid();
        context.UserProfiles.Add(SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), subject, now));
        context.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(institution, subject, SpeedReadingInstitutionMemberRole.Student, other, now));
        context.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(institution, other, subject, other, now));
        context.Invitations.Add(SpeedReadingInvitation.Create("Ada@Example.test", SpeedReadingInstitutionMemberRole.Student, institution, null, other, now));
        Add(context, "LegacyRsvpSession", new { Id = Guid.NewGuid(), UserId = subject, TextContent = "Kendi yapıştırdığı metin", CreatedAt = now });
        Add(context, "LegacyNewsletterSubscriber", new { Id = Guid.NewGuid(), Email = "ada@example.test", CreatedAt = now });
        Add(context, "LegacyContactMessage", new { Id = Guid.NewGuid(), Name = "Ada", Email = "ADA@example.test", CreatedAt = now });
        Add(context, "LegacyContactMessage", new { Id = Guid.NewGuid(), Name = "Başkası", Email = "other@example.test", CreatedAt = now });
        Add(context, "LegacyPayment", new { Id = Guid.NewGuid(), UserId = subject, UserName = "Ada", UserEmail = "ada@example.test", Amount = 100m, Status = "Success", Provider = "Iyzico", CreatedAt = now, UpdatedAt = now });
        Add(context, "LegacyPayment", new { Id = Guid.NewGuid(), UserId = subject, UserName = "Ada", UserEmail = "ada@example.test", Amount = 100m, Status = "Failed", Provider = "Iyzico", CreatedAt = now, UpdatedAt = now });
        Add(context, "LegacyBankTransferPaymentRequest", new { Id = Guid.NewGuid(), UserId = subject, UserName = "Ada", UserEmail = "ada@example.test", Amount = 100m,
            PaymentReference = "EFT-1", Status = "Approved", PayerName = "Veli Ada", CreatedAt = now, UpdatedAt = now });
        Add(context, "LegacyUserSubscription", new { Id = Guid.NewGuid(), UserId = subject, UserName = "Ada", UserEmail = "ada@example.test", Status = "Active",
            StartDate = now, EndDate = (DateTime?)now.AddDays(30), CreatedAt = now });
        await context.SaveChangesAsync();
        var service = new SpeedReadingErasureExecutionService(context, new Directory("ada@example.test"), TimeProvider.System);

        var result = await service.ExecuteAsync(Request(subject), CancellationToken.None);

        (await context.UserProfiles.CountAsync()).Should().Be(0);
        (await context.InstitutionMemberships.CountAsync()).Should().Be(0);
        (await context.TeacherStudentAssignments.CountAsync()).Should().Be(0);
        (await context.Invitations.CountAsync()).Should().Be(0);
        Rows(context, "LegacyRsvpSession").Should().BeEmpty();
        Rows(context, "LegacyNewsletterSubscriber").Should().BeEmpty();
        Rows(context, "LegacyContactMessage").Select(row => Get(row, "Name")).Should().Equal("Başkası");
        Rows(context, "LegacyPayment").Select(row => Get(row, "Status")).Should().Equal("Success");
        Rows(context, "LegacyBankTransferPaymentRequest").Should().ContainSingle();
        Rows(context, "LegacyUserSubscription").Should().ContainSingle();
        var receipt = await context.ErasureExecutions.SingleAsync();
        receipt.SubjectUserId.Should().Be(subject);
        receipt.FinancialRetentionUntil.Should().BeCloseTo(result.CompletedAt.AddYears(1), TimeSpan.FromSeconds(1));
        receipt.FinancialAnonymizedAt.Should().BeNull();
    }

    [Fact]
    public async Task Retained_financial_records_are_anonymised_after_a_year()
    {
        await using var context = CreateContext();
        var subject = Guid.NewGuid();
        var now = DateTime.UtcNow;
        Add(context, "LegacyPayment", new { Id = Guid.NewGuid(), UserId = subject, UserName = "Ada", UserEmail = "ada@example.test", Amount = 100m, Status = "Success",
            Provider = "Iyzico", ProviderResponse = "{\"cardHolder\":\"Ada\"}", CreatedAt = now, UpdatedAt = now });
        Add(context, "LegacyBankTransferPaymentRequest", new { Id = Guid.NewGuid(), UserId = subject, UserName = "Ada", UserEmail = "ada@example.test", Amount = 100m,
            PaymentReference = "EFT-1", Status = "Approved", PayerName = "Veli Ada", Note = "not", ReviewNote = "ok", CreatedAt = now, UpdatedAt = now });
        Add(context, "LegacyUserSubscription", new { Id = Guid.NewGuid(), UserId = subject, UserName = "Ada", UserEmail = "ada@example.test", Notes = "not", Status = "Active",
            StartDate = now, CreatedAt = now });
        await context.SaveChangesAsync();
        var clock = new MovableClock(now);
        await new SpeedReadingErasureExecutionService(context, new Directory(null), clock).ExecuteAsync(Request(subject), CancellationToken.None);
        var processor = new SpeedReadingFinancialRetentionProcessor(context, clock);

        (await processor.AnonymizeDueAsync(CancellationToken.None)).Should().Be(0, "nothing is due within the year");
        clock.Now = now.AddYears(1).AddDays(1);
        (await processor.AnonymizeDueAsync(CancellationToken.None)).Should().Be(1);

        context.ChangeTracker.Clear();
        var payment = Rows(context, "LegacyPayment").Single();
        Get(payment, "UserId").Should().NotBe(subject);
        Get(payment, "UserEmail").Should().Be(string.Empty);
        Get(payment, "ProviderResponse").Should().BeNull();
        Get(payment, "Amount").Should().Be(100m);
        var transfer = Rows(context, "LegacyBankTransferPaymentRequest").Single();
        Get(transfer, "UserId").Should().Be(Get(payment, "UserId"));
        Get(transfer, "PayerName").Should().BeNull();
        Get(transfer, "Note").Should().BeNull();
        Get(transfer, "PaymentReference").Should().Be("EFT-1");
        var subscription = Rows(context, "LegacyUserSubscription").Single();
        Get(subscription, "UserId").Should().Be(Get(payment, "UserId"));
        Get(subscription, "UserEmail").Should().BeNull();
        (await context.ErasureExecutions.SingleAsync()).FinancialAnonymizedAt.Should().NotBeNull();
        (await processor.AnonymizeDueAsync(CancellationToken.None)).Should().Be(0, "anonymisation runs once");
    }

    private static List<object> Rows(OwnedSpeedReadingDbContext context, string name)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType($"SpeedReading.Infrastructure.Legacy.{name}")!;
        var set = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(type).Invoke(context, null)!;
        return ((System.Collections.IEnumerable)set).Cast<object>().ToList();
    }

    private static object? Get(object row, string property) => row.GetType().GetProperty(property)!.GetValue(row);

    private static void Add(OwnedSpeedReadingDbContext context, string name, object values)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType($"SpeedReading.Infrastructure.Legacy.{name}")!;
        var entity = Activator.CreateInstance(type)!;
        foreach (var property in values.GetType().GetProperties())
            type.GetProperty(property.Name)!.SetValue(entity, property.GetValue(values));
        context.Add(entity);
    }

    private sealed class Directory(string? email) : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse(email is null ? [] : [new(userIds.First(), "Ada", "Yılmaz", true) { Email = email }]));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class MovableClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    private static PersonalDataErasureExecutionRequestedV1 Request(Guid subjectId)
    {
        var requestId = Guid.NewGuid();
        return new PersonalDataErasureExecutionRequestedV1(
            requestId, requestId, subjectId, DateTime.UtcNow, PersonalDataScope.SpeedReading);
    }

    private static OwnedSpeedReadingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OwnedSpeedReadingDbContext(options);
    }

}
