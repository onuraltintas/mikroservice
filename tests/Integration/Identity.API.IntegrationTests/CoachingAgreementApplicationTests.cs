using Coaching.Application.Authorization;
using Coaching.Application.CoachingAgreements;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Coaching.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAgreementApplicationTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Publish_ShouldDerivePublisherFromAuthenticatedSystemAdministrator()
    {
        var administratorId = Guid.NewGuid();
        var repository = new FakeAgreementRepository();
        var handler = new PublishCoachingAgreementHandler(
            repository,
            new FakeUnitOfWork(),
            new FakeAccessPolicy(administratorId, isSystemAdministrator: true));

        var result = await handler.Handle(new PublishCoachingAgreementCommand(
            "2026.1",
            "tr-TR",
            "Öğrenci Koçluk Anlaşması",
            "https://legal.example.test/coaching/2026.1",
            ValidHash,
            Now.AddDays(1)), default);

        result.DocumentId.Should().NotBeEmpty();
        repository.Documents.Single().PublishedByUserId.Should().Be(administratorId);
    }

    [Fact]
    public async Task Acknowledge_ShouldRejectDocumentThatIsNotCurrent()
    {
        var studentId = Guid.NewGuid();
        var repository = new FakeAgreementRepository
        {
            Current = PublishDocument("2026.2")
        };
        var handler = CreateAcknowledgeHandler(repository, studentId);

        var act = () => handler.Handle(
            new AcknowledgeCurrentCoachingAgreementCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "CoachingAgreement.NotCurrent");
        repository.Acknowledgements.Should().BeEmpty();
    }

    [Fact]
    public async Task Acknowledge_ShouldBeIdempotentForActiveSelfEvidence()
    {
        var studentId = Guid.NewGuid();
        var current = PublishDocument("2026.1");
        var existing = CoachingAgreementAcknowledgement.Create(
            current.Id,
            studentId,
            studentId,
            CoachingAgreementPartyRole.Self,
            Now.AddMinutes(-1));
        var repository = new FakeAgreementRepository
        {
            Current = current,
            Active = existing
        };
        var handler = CreateAcknowledgeHandler(repository, studentId);

        var result = await handler.Handle(
            new AcknowledgeCurrentCoachingAgreementCommand(current.Id), default);

        result.AcknowledgementId.Should().Be(existing.Id);
        repository.Acknowledgements.Should().BeEmpty();
    }

    [Fact]
    public async Task Withdraw_ShouldRejectAnotherStudentsEvidence()
    {
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var document = PublishDocument("2026.1");
        var evidence = CoachingAgreementAcknowledgement.Create(
            document.Id,
            otherStudentId,
            otherStudentId,
            CoachingAgreementPartyRole.Self,
            Now.AddMinutes(-1));
        var repository = new FakeAgreementRepository { ById = evidence };
        var handler = new WithdrawCoachingAgreementAcknowledgementHandler(
            repository,
            new FakeUnitOfWork(),
            new FakeAccessPolicy(studentId),
            new FixedTimeProvider(Now));

        var act = () => handler.Handle(
            new WithdrawCoachingAgreementAcknowledgementCommand(evidence.Id), default);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
        evidence.WithdrawnAt.Should().BeNull();
    }

    [Fact]
    public async Task RepresentativeAcknowledge_ShouldUseIdentityVerifiedPartyRole()
    {
        var parentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var current = PublishDocument("2026.1");
        var repository = new FakeAgreementRepository { Current = current };
        var handler = new AcknowledgeCurrentCoachingAgreementAsRepresentativeHandler(
            repository,
            new FakeUnitOfWork(),
            new FakeAccessPolicy(parentId),
            new FakeRepresentativeAuthorizationClient("Parent"),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new(current.Id, studentId), CancellationToken.None);

        result.AcknowledgementId.Should().NotBeEmpty();
        repository.Acknowledgements.Should().ContainSingle();
        var evidence = repository.Acknowledgements.Single();
        evidence.SubjectStudentId.Should().Be(studentId);
        evidence.AcknowledgedByUserId.Should().Be(parentId);
        evidence.PartyRole.Should().Be(CoachingAgreementPartyRole.Parent);
        evidence.AuthorityReferenceId.Should().Be(FakeRepresentativeAuthorizationClient.RelationshipId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Self")]
    [InlineData("Other")]
    public async Task RepresentativeAcknowledge_ShouldRejectUnverifiedOrUnsupportedAuthority(string? partyRole)
    {
        var current = PublishDocument("2026.1");
        var repository = new FakeAgreementRepository { Current = current };
        var handler = new AcknowledgeCurrentCoachingAgreementAsRepresentativeHandler(
            repository,
            new FakeUnitOfWork(),
            new FakeAccessPolicy(Guid.NewGuid()),
            new FakeRepresentativeAuthorizationClient(partyRole),
            new FixedTimeProvider(Now));

        var action = () => handler.Handle(new(current.Id, Guid.NewGuid()), CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
        repository.Acknowledgements.Should().BeEmpty();
    }

    [Fact]
    public void RepresentativeAcknowledgeEndpoint_ShouldRequireParentRole()
    {
        typeof(CoachingAgreementsController)
            .GetMethod(nameof(CoachingAgreementsController.AcknowledgeCurrentAsRepresentative))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Should().Contain(attribute => attribute.Roles == "Parent");
    }

    [Fact]
    public async Task RepresentativeWithdraw_ShouldAllowOnlyEvidenceOwnerAndPreserveEvidence()
    {
        var parentId = Guid.NewGuid();
        var document = PublishDocument("2026.1");
        var evidence = CoachingAgreementAcknowledgement.Create(
            document.Id,
            Guid.NewGuid(),
            parentId,
            CoachingAgreementPartyRole.Parent,
            Now.AddMinutes(-1),
            Guid.NewGuid());
        var repository = new FakeAgreementRepository { Current = document, ById = evidence };
        var handler = new WithdrawRepresentativeCoachingAgreementAcknowledgementHandler(
            repository,
            new FakeUnitOfWork(),
            new FakeAccessPolicy(parentId),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(new(evidence.Id), CancellationToken.None);

        result.WithdrawnAt.Should().Be(Now);
        evidence.AcknowledgedAt.Should().Be(Now.AddMinutes(-1));
        evidence.WithdrawnByUserId.Should().Be(parentId);
    }

    [Fact]
    public async Task RepresentativeWithdraw_ShouldRejectAnotherRepresentativesEvidence()
    {
        var document = PublishDocument("2026.1");
        var evidence = CoachingAgreementAcknowledgement.Create(
            document.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            CoachingAgreementPartyRole.LegalGuardian,
            Now.AddMinutes(-1),
            Guid.NewGuid());
        var repository = new FakeAgreementRepository { Current = document, ById = evidence };
        var handler = new WithdrawRepresentativeCoachingAgreementAcknowledgementHandler(
            repository,
            new FakeUnitOfWork(),
            new FakeAccessPolicy(Guid.NewGuid()),
            new FixedTimeProvider(Now));

        var action = () => handler.Handle(new(evidence.Id), CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
        evidence.WithdrawnAt.Should().BeNull();
    }

    private static AcknowledgeCurrentCoachingAgreementHandler CreateAcknowledgeHandler(
        FakeAgreementRepository repository,
        Guid studentId) => new(
            repository,
            new FakeUnitOfWork(),
            new FakeAccessPolicy(studentId),
            new FixedTimeProvider(Now));

    private static CoachingAgreementDocument PublishDocument(string version) =>
        CoachingAgreementDocument.Publish(
            version,
            "tr-TR",
            "Öğrenci Koçluk Anlaşması",
            $"https://legal.example.test/coaching/{version}",
            ValidHash,
            Now.AddDays(-1),
            Guid.NewGuid());

    private const string ValidHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeAgreementRepository : ICoachingAgreementRepository
    {
        public CoachingAgreementDocument? Current { get; init; }
        public CoachingAgreementAcknowledgement? Active { get; init; }
        public CoachingAgreementAcknowledgement? RepresentativeActive { get; init; }
        public CoachingAgreementAcknowledgement? ById { get; init; }
        public List<CoachingAgreementDocument> Documents { get; } = [];
        public List<CoachingAgreementAcknowledgement> Acknowledgements { get; } = [];

        public Task<CoachingAgreementDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current?.Id == documentId ? Current : null);

        public Task<CoachingAgreementDocument?> GetCurrentAsync(string locale, DateTime asOfUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);

        public Task<CoachingAgreementAcknowledgement?> GetActiveSelfAcknowledgementAsync(Guid documentId, Guid studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Active);

        public Task<CoachingAgreementAcknowledgement?> GetActiveAcknowledgementForStudentAsync(
            Guid documentId,
            Guid studentId,
            CancellationToken cancellationToken = default) => Task.FromResult(Active ?? RepresentativeActive);

        public Task<CoachingAgreementAcknowledgement?> GetActiveAcknowledgementAsync(
            Guid documentId,
            Guid studentId,
            Guid acknowledgedByUserId,
            CoachingAgreementPartyRole partyRole,
            CancellationToken cancellationToken = default) => Task.FromResult(RepresentativeActive);

        public Task<CoachingAgreementAcknowledgement?> GetAcknowledgementAsync(Guid acknowledgementId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ById);

        public Task AddDocumentAsync(CoachingAgreementDocument document, CancellationToken cancellationToken = default)
        {
            Documents.Add(document);
            return Task.CompletedTask;
        }

        public Task AddAcknowledgementAsync(CoachingAgreementAcknowledgement acknowledgement, CancellationToken cancellationToken = default)
        {
            Acknowledgements.Add(acknowledgement);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepresentativeAuthorizationClient(string? partyRole)
        : ICoachingAgreementRepresentativeAuthorizationClient
    {
        public static readonly Guid RelationshipId = Guid.Parse("21ef28db-d7cb-4e5f-93ea-318d9604ff10");

        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeAsync(
            Guid representativeUserId,
            Guid studentUserId,
            CancellationToken cancellationToken) =>
            Task.FromResult(partyRole is null
                ? null
                : new CoachingAgreementRepresentativeAuthorization(RelationshipId, partyRole));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAccessPolicy(Guid userId, bool isSystemAdministrator = false) : ICoachingAccessPolicy
    {
        public Guid? CurrentUserId => userId;
        public bool IsSystemAdministrator => isSystemAdministrator;
        public bool IsInstitutionAdministrator => false;
        public bool IsCurrentTeacher(Guid teacherId) => false;
        public bool IsCurrentStudent(Guid studentId) => !isSystemAdministrator && studentId == userId;
        public Guid RequireCurrentTeacher() => throw new NotSupportedException();
        public void RequireTeacher(Guid teacherId) => throw new NotSupportedException();
        public void RequireStudent(Guid studentId)
        {
            if (!IsCurrentStudent(studentId))
                throw new BusinessRuleException("Authorization.Forbidden", "Forbidden");
        }
        public void RequireTeacherOrStudent(Guid teacherId, Guid studentId) => throw new NotSupportedException();
        public void RequireTeacherOrAssignedStudent(Guid teacherId, IEnumerable<Guid> studentIds) => throw new NotSupportedException();
    }
}
