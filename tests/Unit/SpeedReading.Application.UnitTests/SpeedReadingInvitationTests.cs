using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingInvitationTests
{
    [Fact]
    public async Task Standalone_teacher_invitation_is_idempotent_and_acceptance_creates_only_local_relationship()
    {
        await using var context = CreateContext();
        var eligibility = new AllowAllSpeedReadingMembers();
        var invitations = new OwnedSpeedReadingInvitations(context, eligibility);
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var created = await invitations.CreateAsync(
            " Student@Example.com ", SpeedReadingInstitutionMemberRole.Student,
            null, teacherId, teacherId, now);
        var duplicate = await invitations.CreateAsync(
            "student@example.com", SpeedReadingInstitutionMemberRole.Student,
            null, teacherId, teacherId, now.AddMinutes(1));

        created.Result.Should().Be(SpeedReadingInvitationCreateResult.Created);
        duplicate.Result.Should().Be(SpeedReadingInvitationCreateResult.AlreadyPending);
        duplicate.Invitation!.InvitationId.Should().Be(created.Invitation!.InvitationId);
        context.Invitations.Should().ContainSingle();

        var accepted = await invitations.AcceptAsync(
            created.Invitation.InvitationId, studentId, "STUDENT@example.com", now.AddMinutes(2));

        accepted.Should().Be(SpeedReadingInvitationAcceptResult.Accepted);
        context.InstitutionMemberships.Should().BeEmpty();
        context.TeacherStudentAssignments.Should().ContainSingle(item =>
            item.TeacherUserId == teacherId && item.StudentUserId == studentId && item.InstitutionId == null);
        (await invitations.AcceptAsync(
            created.Invitation.InvitationId, studentId, "student@example.com", now.AddMinutes(3)))
            .Should().Be(SpeedReadingInvitationAcceptResult.AlreadyAccepted);
    }

    [Fact]
    public async Task Acceptance_rejects_a_different_account_email_without_consuming_invitation()
    {
        await using var context = CreateContext();
        var teacherId = Guid.NewGuid();
        var invitations = new OwnedSpeedReadingInvitations(context, new AllowAllSpeedReadingMembers());
        var created = await invitations.CreateAsync(
            "student@example.com", SpeedReadingInstitutionMemberRole.Student,
            null, teacherId, teacherId, DateTime.UtcNow);

        (await invitations.AcceptAsync(
            created.Invitation!.InvitationId, Guid.NewGuid(), "other@example.com", DateTime.UtcNow))
            .Should().Be(SpeedReadingInvitationAcceptResult.WrongEmail);
        context.TeacherStudentAssignments.Should().BeEmpty();
        context.Invitations.Single().Status.Should().Be(SpeedReadingInvitationStatus.Pending);
    }

    [Fact]
    public async Task Acceptance_requires_active_speed_reading_product_role()
    {
        await using var context = CreateContext();
        var eligibility = new AllowAllSpeedReadingMembers();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var invitations = new OwnedSpeedReadingInvitations(context, eligibility);
        var created = await invitations.CreateAsync(
            "student@example.com", SpeedReadingInstitutionMemberRole.Student,
            null, teacherId, teacherId, DateTime.UtcNow);
        eligibility.IneligibleUserId = studentId;

        (await invitations.AcceptAsync(
            created.Invitation!.InvitationId, studentId, "student@example.com", DateTime.UtcNow))
            .Should().Be(SpeedReadingInvitationAcceptResult.ProductRoleRequired);
        context.TeacherStudentAssignments.Should().BeEmpty();
        context.Invitations.Single().Status.Should().Be(SpeedReadingInvitationStatus.Pending);
    }

    [Fact]
    public async Task Expired_invitation_cannot_be_accepted()
    {
        await using var context = CreateContext();
        var invitations = new OwnedSpeedReadingInvitations(context, new AllowAllSpeedReadingMembers());
        var now = DateTime.UtcNow;
        var teacherId = Guid.NewGuid();
        var created = await invitations.CreateAsync(
            "student@example.com", SpeedReadingInstitutionMemberRole.Student,
            null, teacherId, teacherId, now);

        (await invitations.AcceptAsync(
            created.Invitation!.InvitationId, Guid.NewGuid(), "student@example.com", now.AddDays(8)))
            .Should().Be(SpeedReadingInvitationAcceptResult.Expired);
        context.TeacherStudentAssignments.Should().BeEmpty();
        context.Invitations.Single().Status.Should().Be(SpeedReadingInvitationStatus.Expired);
    }

    [Fact]
    public async Task Pending_sent_invitations_are_scoped_to_sender_and_exclude_accepted_invitations()
    {
        await using var context = CreateContext();
        var invitations = new OwnedSpeedReadingInvitations(context, new AllowAllSpeedReadingMembers());
        var firstTeacherId = Guid.NewGuid();
        var secondTeacherId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var pending = await invitations.CreateAsync(
            "pending@example.com", SpeedReadingInstitutionMemberRole.Student,
            null, firstTeacherId, firstTeacherId, now);
        var accepted = await invitations.CreateAsync(
            "accepted@example.com", SpeedReadingInstitutionMemberRole.Student,
            null, firstTeacherId, firstTeacherId, now);
        var otherSender = await invitations.CreateAsync(
            "other@example.com", SpeedReadingInstitutionMemberRole.Student,
            null, secondTeacherId, secondTeacherId, now);
        await invitations.AcceptAsync(accepted.Invitation!.InvitationId, Guid.NewGuid(), "accepted@example.com", now);

        var firstSenderList = await invitations.GetPendingByInviterAsync(firstTeacherId, now);
        var secondSenderList = await invitations.GetPendingByInviterAsync(secondTeacherId, now);

        firstSenderList.Should().ContainSingle().Which.InvitationId.Should().Be(pending.Invitation!.InvitationId);
        firstSenderList.Single().Email.Should().Be("pending@example.com");
        secondSenderList.Should().ContainSingle().Which.InvitationId.Should().Be(otherSender.Invitation!.InvitationId);
        (await invitations.GetPendingByInviterAsync(firstTeacherId, now.AddDays(8))).Should().BeEmpty();
    }

    [Fact]
    public async Task Only_sender_can_cancel_a_pending_invitation_and_cancelled_invitation_cannot_be_accepted()
    {
        await using var context = CreateContext();
        var invitations = new OwnedSpeedReadingInvitations(context, new AllowAllSpeedReadingMembers());
        var teacherId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var created = await invitations.CreateAsync(
            "student@example.com", SpeedReadingInstitutionMemberRole.Student,
            null, teacherId, teacherId, now);
        var invitationId = created.Invitation!.InvitationId;

        (await invitations.CancelAsync(invitationId, Guid.NewGuid(), now.AddMinutes(1)))
            .Should().Be(SpeedReadingInvitationCancelResult.NotFound);
        (await invitations.CancelAsync(invitationId, teacherId, now.AddMinutes(1)))
            .Should().Be(SpeedReadingInvitationCancelResult.Cancelled);
        context.Invitations.Single().Status.Should().Be(SpeedReadingInvitationStatus.Cancelled);
        (await invitations.AcceptAsync(invitationId, Guid.NewGuid(), "student@example.com", now.AddMinutes(2)))
            .Should().Be(SpeedReadingInvitationAcceptResult.NotPending);
    }

    [Fact]
    public async Task Expired_sent_invitation_is_not_listed_or_cancelled_as_pending()
    {
        await using var context = CreateContext();
        var invitations = new OwnedSpeedReadingInvitations(context, new AllowAllSpeedReadingMembers());
        var teacherId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var created = await invitations.CreateAsync(
            "student@example.com", SpeedReadingInstitutionMemberRole.Student,
            null, teacherId, teacherId, now);

        (await invitations.GetPendingByInviterAsync(teacherId, now.AddDays(8))).Should().BeEmpty();
        (await invitations.CancelAsync(created.Invitation!.InvitationId, teacherId, now.AddDays(8)))
            .Should().Be(SpeedReadingInvitationCancelResult.NotPending);
        context.Invitations.Single().Status.Should().Be(SpeedReadingInvitationStatus.Expired);
    }

    [Fact]
    public async Task Institution_student_invitation_acceptance_creates_membership_and_relationship()
    {
        await using var context = CreateContext();
        var eligibility = new AllowAllSpeedReadingMembers();
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        context.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(
            institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher, teacherId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var invitations = new OwnedSpeedReadingInvitations(context, eligibility);
        var created = await invitations.CreateAsync(
            "student@example.com", SpeedReadingInstitutionMemberRole.Student,
            institutionId, teacherId, Guid.NewGuid(), DateTime.UtcNow);

        created.Result.Should().Be(SpeedReadingInvitationCreateResult.Created);
        (await invitations.AcceptAsync(
            created.Invitation!.InvitationId, studentId, "student@example.com", DateTime.UtcNow))
            .Should().Be(SpeedReadingInvitationAcceptResult.Accepted);

        context.InstitutionMemberships.Should().ContainSingle(item =>
            item.InstitutionId == institutionId
            && item.UserId == studentId
            && item.Role == SpeedReadingInstitutionMemberRole.Student
            && item.IsActive);
        context.TeacherStudentAssignments.Should().ContainSingle(item =>
            item.InstitutionId == institutionId
            && item.TeacherUserId == teacherId
            && item.StudentUserId == studentId
            && item.IsActive);
    }

    [Fact]
    public async Task Institution_invitation_adds_a_second_role_without_replacing_existing_membership()
    {
        await using var context = CreateContext();
        var eligibility = new AllowAllSpeedReadingMembers();
        var institutionId = Guid.NewGuid();
        var invitingTeacherId = Guid.NewGuid();
        var dualRoleUserId = Guid.NewGuid();
        context.InstitutionMemberships.AddRange(
            SpeedReadingInstitutionMembership.Create(
                institutionId, invitingTeacherId, SpeedReadingInstitutionMemberRole.Teacher, invitingTeacherId, DateTime.UtcNow),
            SpeedReadingInstitutionMembership.Create(
                institutionId, dualRoleUserId, SpeedReadingInstitutionMemberRole.Teacher, invitingTeacherId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var invitations = new OwnedSpeedReadingInvitations(context, eligibility);
        var created = await invitations.CreateAsync(
            "dual@example.com", SpeedReadingInstitutionMemberRole.Student,
            institutionId, invitingTeacherId, invitingTeacherId, DateTime.UtcNow);

        (await invitations.AcceptAsync(
            created.Invitation!.InvitationId, dualRoleUserId, "dual@example.com", DateTime.UtcNow))
            .Should().Be(SpeedReadingInvitationAcceptResult.Accepted);

        context.InstitutionMemberships.Where(item => item.UserId == dualRoleUserId)
            .Select(item => item.Role)
            .Should().BeEquivalentTo(new[]
            {
                SpeedReadingInstitutionMemberRole.Teacher,
                SpeedReadingInstitutionMemberRole.Student
            });
    }

    [Fact]
    public async Task Institution_student_invitation_reassigns_only_the_institution_teacher_relationship()
    {
        await using var context = CreateContext();
        var eligibility = new AllowAllSpeedReadingMembers();
        var institutionId = Guid.NewGuid();
        var previousTeacherId = Guid.NewGuid();
        var invitingTeacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        context.InstitutionMemberships.AddRange(
            SpeedReadingInstitutionMembership.Create(institutionId, previousTeacherId, SpeedReadingInstitutionMemberRole.Teacher, previousTeacherId, DateTime.UtcNow),
            SpeedReadingInstitutionMembership.Create(institutionId, invitingTeacherId, SpeedReadingInstitutionMemberRole.Teacher, invitingTeacherId, DateTime.UtcNow),
            SpeedReadingInstitutionMembership.Create(institutionId, studentId, SpeedReadingInstitutionMemberRole.Student, studentId, DateTime.UtcNow));
        context.TeacherStudentAssignments.AddRange(
            SpeedReadingTeacherStudentAssignment.Create(institutionId, previousTeacherId, studentId, previousTeacherId, DateTime.UtcNow),
            SpeedReadingTeacherStudentAssignment.Create(null, previousTeacherId, studentId, previousTeacherId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var invitations = new OwnedSpeedReadingInvitations(context, eligibility);
        var created = await invitations.CreateAsync(
            "student@example.com", SpeedReadingInstitutionMemberRole.Student,
            institutionId, invitingTeacherId, invitingTeacherId, DateTime.UtcNow);

        (await invitations.AcceptAsync(
            created.Invitation!.InvitationId, studentId, "student@example.com", DateTime.UtcNow.AddMinutes(1)))
            .Should().Be(SpeedReadingInvitationAcceptResult.Accepted);

        (await context.TeacherStudentAssignments.SingleAsync(item => item.InstitutionId == institutionId
            && item.TeacherUserId == previousTeacherId)).IsActive.Should().BeFalse();
        (await context.TeacherStudentAssignments.SingleAsync(item => item.InstitutionId == institutionId
            && item.TeacherUserId == invitingTeacherId)).IsActive.Should().BeTrue();
        (await context.TeacherStudentAssignments.SingleAsync(item => item.InstitutionId == null)).IsActive.Should().BeTrue();
    }

    [Fact]
    public void Ef_model_persists_invitations_in_product_owned_schema_with_pending_deduplication()
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql("Host=localhost;Database=speed_reading_model_only;Username=unused;Password=unused")
            .Options;
        using var context = new OwnedSpeedReadingDbContext(options);
        var entity = context.Model.FindEntityType(typeof(SpeedReadingInvitation))!;

        entity.GetTableName().Should().Be("invitations");
        entity.GetSchema().Should().Be("speed_reading");
        entity.GetForeignKeys().Should().BeEmpty();
        entity.GetIndexes().Should().Contain(index =>
            index.IsUnique
            && index.GetFilter() == "status = 'Pending'"
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(SpeedReadingInvitation.DeduplicationKey) }));
        context.Database.GetMigrations()
            .Should().Contain(migration => migration.EndsWith("_AddSpeedReadingInvitations", StringComparison.Ordinal));
    }

    private static OwnedSpeedReadingDbContext CreateContext() => new(
        new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class AllowAllSpeedReadingMembers : ISpeedReadingInstitutionMemberEligibility
    {
        public Guid? IneligibleUserId { get; set; }

        public Task<bool> IsEligibleAsync(
            Guid userId,
            SpeedReadingInstitutionMemberRole role,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(userId != IneligibleUserId);
    }
}
