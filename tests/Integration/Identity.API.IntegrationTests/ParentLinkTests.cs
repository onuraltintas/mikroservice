using FluentAssertions;
using Identity.API.Controllers;
using Identity.Application.ParentLinks;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using IdentityUserRole = Identity.Domain.Enums.UserRole;

namespace Identity.API.IntegrationTests;

// Roadmap Y7: a parent links to their child with a code the child creates; the child approves the link.
public sealed class ParentLinkTests
{
    [Fact]
    public async Task ParentLinksWithTheChildsCodeAndTheChildApproves()
    {
        await using var context = CreateContext(); var clock = new Clock();
        var (parent, student, _) = AddUsers(context); await context.SaveChangesAsync();
        var links = new ParentLinkService(context, clock);

        var issued = (await links.CreateCodeAsync(student.Id, default)).Value;
        issued.Code.Should().MatchRegex("^[A-Z2-9]{4}-[A-Z2-9]{4}$");
        issued.ExpiresAt.Should().Be(clock.GetUtcNow().UtcDateTime.AddDays(7));

        var requested = await links.RequestAsync(parent.Id, issued.Code.ToLowerInvariant().Replace("-", " "), ParentRelationship.Mother, default);
        requested.IsSuccess.Should().BeTrue();
        requested.Value.Status.Should().Be("Pending");
        requested.Value.StudentName.Should().Be("Ada Öğrenci");

        var studentView = (await links.ListAsync(student.Id, default)).Value.Should().ContainSingle().Subject;
        (studentView.ParentName, studentView.Relationship, studentView.Status).Should().Be(("Ayşe Veli", "Mother", "Pending"));
        (await links.ApproveAsync(student.Id, studentView.Id, default)).IsSuccess.Should().BeTrue();

        var row = await context.ParentStudentRelationships.SingleAsync();
        (row.Status, row.VerificationMethod, row.VerifiedByUserId).Should()
            .Be((ParentStudentRelationshipStatus.Verified, ParentStudentVerificationMethod.InvitationAcceptance, student.Id));
        (await new ParentStudentRelationshipRepository(context).AuthorizeCoachingAgreementRepresentativeAsync(parent.Id, student.Id, default))
            .Should().NotBeNull("a verified mother may read her child's Coaching data");
        (await links.ListAsync(parent.Id, default)).Value.Should().ContainSingle(x => x.StudentUserId == student.Id && x.Status == "Verified");
    }

    [Fact]
    public async Task CodesAreSingleUseExpireAndANewCodeReplacesTheOldOne()
    {
        await using var context = CreateContext(); var clock = new Clock();
        var (parent, student, otherParent) = AddUsers(context); await context.SaveChangesAsync();
        var links = new ParentLinkService(context, clock);

        var first = (await links.CreateCodeAsync(student.Id, default)).Value;
        var second = (await links.CreateCodeAsync(student.Id, default)).Value;
        (await links.RequestAsync(parent.Id, first.Code, ParentRelationship.Father, default)).Error.Code.Should().Be("ParentLink.InvalidCode");
        (await links.RequestAsync(parent.Id, second.Code, ParentRelationship.Father, default)).IsSuccess.Should().BeTrue();
        (await links.RequestAsync(otherParent.Id, second.Code, ParentRelationship.Guardian, default)).Error.Code.Should().Be("ParentLink.InvalidCode");

        var third = (await links.CreateCodeAsync(student.Id, default)).Value;
        (await links.RequestAsync(parent.Id, third.Code, ParentRelationship.Father, default)).Error.Code.Should().Be("ParentLink.AlreadyLinked");
        clock.Now = clock.Now.AddDays(8);
        (await links.RequestAsync(otherParent.Id, third.Code, ParentRelationship.Guardian, default)).Error.Code.Should().Be("ParentLink.InvalidCode");
    }

    [Fact]
    public async Task OnlyAParentCanUseACodeOnlyTheChildCanApproveAndEitherSideCanEndTheLink()
    {
        await using var context = CreateContext(); var clock = new Clock();
        var (parent, student, otherParent) = AddUsers(context); await context.SaveChangesAsync();
        var links = new ParentLinkService(context, clock);
        var code = (await links.CreateCodeAsync(student.Id, default)).Value.Code;

        (await links.CreateCodeAsync(parent.Id, default)).Error.Code.Should().Be("ParentLink.Forbidden");
        (await links.RequestAsync(student.Id, code, ParentRelationship.Mother, default)).Error.Code.Should().Be("ParentLink.Forbidden");
        (await links.RequestAsync(parent.Id, code, ParentRelationship.Other, default)).Error.Code.Should().Be("ParentLink.InvalidRelationship");
        var link = (await links.RequestAsync(parent.Id, code, ParentRelationship.Guardian, default)).Value;

        (await links.ApproveAsync(parent.Id, link.Id, default)).Error.Code.Should().Be("ParentLink.NotFound");
        (await links.RevokeAsync(otherParent.Id, link.Id, default)).Error.Code.Should().Be("ParentLink.NotFound");
        (await links.ApproveAsync(student.Id, link.Id, default)).IsSuccess.Should().BeTrue();
        (await links.ApproveAsync(student.Id, link.Id, default)).Error.Code.Should().Be("ParentLink.NotPending");
        (await links.RevokeAsync(parent.Id, link.Id, default)).IsSuccess.Should().BeTrue();

        (await context.ParentStudentRelationships.SingleAsync()).Status.Should().Be(ParentStudentRelationshipStatus.Revoked);
        (await links.ListAsync(student.Id, default)).Value.Should().BeEmpty("ended links leave the list");
    }

    // The weekly-summary recipients (muted parents, product flags, SQL paging) are covered on PostgreSQL in
    // ParentLinkProductsPostgresTests: the preference filter is a jsonb query.

    // V1 (parent panel): each step of a link is told to the other side, with a link to where they act on it.
    [Fact]
    public async Task EachStepOfALinkIsToldToTheOtherSide()
    {
        await using var context = CreateContext(); var clock = new Clock(); var events = new RecordingEvents();
        var (parent, student, otherParent) = AddUsers(context);
        student.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, clock.GetUtcNow().AddDays(-1));
        await context.SaveChangesAsync();
        var links = new ParentLinkService(context, clock, events);

        var link = (await links.RequestAsync(parent.Id, (await links.CreateCodeAsync(student.Id, default)).Value.Code, ParentRelationship.Mother, default)).Value;
        var requested = events.Messages.Should().ContainSingle().Subject;
        (requested.Change, requested.LinkId, requested.RecipientUserId, requested.RecipientRole, requested.RecipientEmail, requested.RecipientFirstName,
            requested.OtherPartyName, requested.LinkProduct, requested.LinkPath)
            .Should().Be(("Requested", link.Id, student.Id, "Student", "student@example.test", "Ada", "Ayşe Veli", "coaching",
                "/coaching-portal/account/settings/profile"));
        requested.OccurredAt.Should().Be(clock.GetUtcNow().UtcDateTime);

        await links.ApproveAsync(student.Id, link.Id, default);
        var approved = events.Messages.Last();
        (approved.Change, approved.RecipientUserId, approved.RecipientRole, approved.RecipientEmail, approved.RecipientFirstName,
            approved.OtherPartyName, approved.LinkProduct, approved.LinkPath)
            .Should().Be(("Approved", parent.Id, "Parent", "parent@example.test", "Ayşe", "Ada Öğrenci", "coaching", "/coaching-portal/parent"));

        await links.RevokeAsync(student.Id, link.Id, default);
        var byStudent = events.Messages.Last();
        (byStudent.Change, byStudent.RecipientUserId, byStudent.LinkPath).Should().Be(("Revoked", parent.Id, "/coaching-portal/parent"));

        var second = (await links.RequestAsync(otherParent.Id, (await links.CreateCodeAsync(student.Id, default)).Value.Code, ParentRelationship.Father, default)).Value;
        await links.RevokeAsync(otherParent.Id, second.Id, default);
        var byParent = events.Messages.Last();
        (byParent.Change, byParent.RecipientUserId, byParent.OtherPartyName, byParent.LinkProduct)
            .Should().Be(("Revoked", student.Id, "Can Veli", "coaching"));

        events.Messages.Select(x => x.EventId).Should().OnlyHaveUniqueItems().And.NotContain(Guid.Empty);
        (await links.ApproveAsync(student.Id, second.Id, default)).IsFailure.Should().BeTrue();
        events.Messages.Should().HaveCount(5, "a refused step tells no one");
    }

    [Fact]
    public async Task ASpeedReadingOnlyStudentIsSentToTheSpeedReadingSite()
    {
        await using var context = CreateContext(); var clock = new Clock(); var events = new RecordingEvents();
        var (parent, student, _) = AddUsers(context);
        student.GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.Admin, null, clock.GetUtcNow().AddDays(-1));
        await context.SaveChangesAsync();
        var links = new ParentLinkService(context, clock, events);

        await links.RequestAsync(parent.Id, (await links.CreateCodeAsync(student.Id, default)).Value.Code, ParentRelationship.Guardian, default);

        var message = events.Messages.Should().ContainSingle().Subject;
        (message.LinkProduct, message.LinkPath).Should().Be(("speed-reading", "/veli-baglantisi"));
    }

    [Fact]
    public async Task EventIdsAreStablePerLinkStepAndRecipient()
    {
        var linkId = Guid.NewGuid(); var recipient = Guid.NewGuid();
        ParentLinkService.EventId(linkId, "Approved", recipient).Should().Be(ParentLinkService.EventId(linkId, "Approved", recipient))
            .And.NotBe(ParentLinkService.EventId(linkId, "Revoked", recipient))
            .And.NotBe(ParentLinkService.EventId(linkId, "Approved", Guid.NewGuid()));
        await Task.CompletedTask;
    }

    // Roadmap O3: Coaching asks for verified links by child (a teacher's message contacts) or by parent (a parent's children).
    [Fact]
    public async Task VerifiedLinksAreListedByChildOrByParent()
    {
        await using var context = CreateContext(); var clock = new Clock();
        var (parent, student, otherParent) = AddUsers(context); await context.SaveChangesAsync();
        var links = new ParentLinkService(context, clock);
        var verified = (await links.RequestAsync(parent.Id, (await links.CreateCodeAsync(student.Id, default)).Value.Code, ParentRelationship.Mother, default)).Value;
        await links.ApproveAsync(student.Id, verified.Id, default);
        await links.RequestAsync(otherParent.Id, (await links.CreateCodeAsync(student.Id, default)).Value.Code, ParentRelationship.Father, default);

        var byChild = (await links.GetVerifiedLinksAsync([student.Id], null, default)).Should().ContainSingle().Subject;
        (byChild.ParentUserId, byChild.ParentName, byChild.StudentUserId, byChild.StudentName, byChild.Relationship)
            .Should().Be((parent.Id, "Ayşe Veli", student.Id, "Ada Öğrenci", "Mother"));
        (await links.GetVerifiedLinksAsync([], parent.Id, default)).Should().ContainSingle(x => x.StudentUserId == student.Id);
        (await links.GetVerifiedLinksAsync([], otherParent.Id, default)).Should().BeEmpty("a pending link is not a link yet");

        await links.RevokeAsync(student.Id, verified.Id, default);
        (await links.GetVerifiedLinksAsync([student.Id], null, default)).Should().BeEmpty();
        typeof(InternalCoachingController).GetMethod(nameof(InternalCoachingController.GetParentLinks))!
            .GetCustomAttributes(typeof(Identity.API.InternalServiceKeyAttribute), true).Should().ContainSingle();
    }

    [Theory]
    [InlineData(nameof(ParentLinksController.CreateCode), "Student")]
    [InlineData(nameof(ParentLinksController.RequestLink), "Parent")]
    [InlineData(nameof(ParentLinksController.Approve), "Student")]
    [InlineData(nameof(ParentLinksController.Revoke), "Student,Parent")]
    [InlineData(nameof(ParentLinksController.List), "Student,Parent")]
    [InlineData(nameof(ParentLinksController.SetProducts), "Student")]
    public void SelfServiceEndpointsAreLimitedToTheTwoSides(string action, string roles) =>
        typeof(ParentLinksController).GetMethod(action)!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Should().Contain(x => x.Roles == roles);

    [Fact]
    public void ParentReadAuthorizationIsServiceToServiceOnlyAndNotRoutedByTheGateway()
    {
        var method = typeof(InternalParentsController).GetMethod(nameof(InternalParentsController.AuthorizeRead))!;
        method.GetCustomAttributes(typeof(Identity.API.InternalServiceKeyAttribute), true).Should().ContainSingle();
        method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().ContainSingle();
        typeof(InternalParentsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), true)
            .Cast<Microsoft.AspNetCore.Mvc.RouteAttribute>().Single().Template.Should().Be("api/internal/parents");

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "EduPlatform.sln"))) root = root.Parent!;
        using var gateway = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "services", "api-gateway", "appsettings.json")),
            new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var paths = gateway.RootElement.GetProperty("ReverseProxy").GetProperty("Routes").EnumerateObject()
            .Select(route => route.Value.GetProperty("Match").TryGetProperty("Path", out var path) ? path.GetString()! : "")
            .Where(path => path.Length > 0).ToArray();
        paths.Should().NotBeEmpty();
        paths.Should().NotContain(path => path.StartsWith("/api/internal", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/{", StringComparison.Ordinal)
            || path.StartsWith("/{", StringComparison.Ordinal), "internal endpoints must not be reachable from the public gateway");
    }

    [Fact]
    public async Task ParentReadAuthorizationAnswersForbiddenOrTheLink()
    {
        var links = new StubLinks();
        var controller = new InternalParentsController(links);
        var parent = Guid.NewGuid(); var student = Guid.NewGuid();

        (await controller.AuthorizeRead(new(parent, student, "Chess"), default)).Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>();
        (await controller.AuthorizeRead(new(Guid.Empty, student, "Coaching"), default)).Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>();
        var refused = await controller.AuthorizeRead(new(parent, student, "SpeedReading"), default);
        refused.Should().BeOfType<Microsoft.AspNetCore.Mvc.StatusCodeResult>().Which.StatusCode.Should().Be(403);
        links.Answer = new ParentReadAuthorization(Guid.NewGuid(), "Mother");
        var ok = (await controller.AuthorizeRead(new(parent, student, "Coaching"), default)).Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Subject;
        ok.Value.Should().Be(new ParentReadAuthorizationResponse(links.Answer.RelationshipId, "Mother"));
        links.Asked.Should().Equal((parent, student, PlatformProduct.SpeedReading), (parent, student, PlatformProduct.Coaching));
    }

    private sealed class StubLinks : IParentLinkService
    {
        public ParentReadAuthorization? Answer { get; set; }
        public List<(Guid, Guid, PlatformProduct)> Asked { get; } = [];
        public Task<ParentReadAuthorization?> AuthorizeReadAsync(Guid parentUserId, Guid studentUserId, PlatformProduct product, CancellationToken ct)
        {
            Asked.Add((parentUserId, studentUserId, product));
            return Task.FromResult(Answer);
        }
        public Task<EduPlatform.Shared.Kernel.Results.Result<ParentLinkCodeIssued>> CreateCodeAsync(Guid studentUserId, CancellationToken ct) => throw new NotSupportedException();
        public Task<EduPlatform.Shared.Kernel.Results.Result<ParentLinkView>> RequestAsync(Guid parentUserId, string code, ParentRelationship relationship, CancellationToken ct) => throw new NotSupportedException();
        public Task<EduPlatform.Shared.Kernel.Results.Result<IReadOnlyList<ParentLinkView>>> ListAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<EduPlatform.Shared.Kernel.Results.Result> ApproveAsync(Guid studentUserId, Guid linkId, CancellationToken ct) => throw new NotSupportedException();
        public Task<EduPlatform.Shared.Kernel.Results.Result> RevokeAsync(Guid userId, Guid linkId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<VerifiedParentLink>> GetVerifiedLinksAsync(IReadOnlyCollection<Guid> studentUserIds, Guid? parentUserId, CancellationToken ct) => throw new NotSupportedException();
        public Task<EduPlatform.Shared.Kernel.Results.Result> SetProductVisibilityAsync(Guid studentUserId, Guid linkId, bool coaching, bool speedReading, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class RecordingEvents : IParentLinkEventPublisher
    {
        public List<EduPlatform.Shared.Contracts.Events.Identity.ParentLinkNotificationV1> Messages { get; } = [];
        public Task PublishAsync(EduPlatform.Shared.Contracts.Events.Identity.ParentLinkNotificationV1 message, CancellationToken ct)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private static (User Parent, User Student, User OtherParent) AddUsers(IdentityDbContext context)
    {
        var parentRole = Role.Create(IdentityUserRole.Parent.ToString(), "Parent", true);
        var studentRole = Role.Create(IdentityUserRole.Student.ToString(), "Student", true);
        User Make(string email, string first, string last, Role role)
        {
            var user = User.Create(Guid.NewGuid(), email, first, last);
            user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id)); return user;
        }
        var parent = Make("parent@example.test", "Ayşe", "Veli", parentRole);
        var student = Make("student@example.test", "Ada", "Öğrenci", studentRole);
        var other = Make("other@example.test", "Can", "Veli", parentRole);
        context.AddRange(parentRole, studentRole, parent, student, other);
        return (parent, student, other);
    }

    private static IdentityDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
