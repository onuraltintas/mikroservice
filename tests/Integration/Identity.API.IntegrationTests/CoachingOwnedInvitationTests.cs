using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingOwnedInvitationTests
{
    [Fact]
    public async Task RecipientCanRejectAndOwnerCanCancelWithoutCreatingRelationships()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var teacher = f.AddAccount("Teacher"); var student = f.AddAccount("Student");
        await f.Store.ProvisionAsync(teacher, "Teacher", default);
        var service = new CoachingInvitationStore(f.Db, f.Accounts, f.Store, TimeProvider.System, new Events(), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var invitation = await service.SendTeacherAsync(teacher, f.Accounts.Users[student].Email, null, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SendTeacherAsync(teacher, f.Accounts.Users[student].Email, null, default));
        await service.RejectAsync(student, invitation.Id, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(student, invitation.Id, default));
        var replacement = await service.SendTeacherAsync(teacher, f.Accounts.Users[student].Email, null, default);
        await service.CancelAsync(teacher, replacement.Id, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(student, replacement.Id, default));
        Assert.Empty(f.Db.CoachingTeacherStudentAssignments); Assert.Empty(f.Db.CoachingStudentProfiles);
    }
    [Fact]
    public async Task InstitutionInvitationAcceptsOnlyRecipientAndCreatesUserIdRelationsOnce()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var inst = Guid.NewGuid(); f.Accounts.Institutions.Add(inst);
        var admin = f.AddAccount("InstitutionAdmin"); f.Accounts.Admins[admin] = new(false, inst);
        var teacher = f.AddAccount("Teacher"); var student = f.AddAccount("Student"); var stranger = f.AddAccount("Student");
        await f.Store.SetMembershipAsync(admin, inst, teacher, CoachingMemberRole.Teacher, true, default);
        var service = new CoachingInvitationStore(f.Db, f.Accounts, f.Store, TimeProvider.System, new Events(), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var invitation = await service.SendInstitutionAsync(admin, inst, f.Accounts.Users[student].Email, "Student", teacher, null, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(stranger, invitation.Id, default));
        await service.AcceptAsync(student, invitation.Id, default);
        await service.AcceptAsync(student, invitation.Id, default);
        Assert.Single(await f.Db.CoachingStudentProfiles.ToListAsync());
        var assignment = await f.Db.CoachingTeacherStudentAssignments.SingleAsync();
        Assert.Equal(teacher, assignment.TeacherUserId); Assert.Equal(student, assignment.StudentUserId);
        Assert.Equal(inst, assignment.InstitutionId);
        Assert.Single(f.Accounts.Users[student].Roles); Assert.Equal("Student", f.Accounts.Users[student].Roles[0]);
    }

    [Fact]
    public async Task RevokedInviterAndWrongProductRoleCannotBeAccepted()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var inst = Guid.NewGuid(); f.Accounts.Institutions.Add(inst);
        var admin = f.AddAccount("InstitutionAdmin"); f.Accounts.Admins[admin] = new(false, inst);
        var student = f.AddAccount("Student");
        var service = new CoachingInvitationStore(f.Db, f.Accounts, f.Store, TimeProvider.System, new Events(), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var invitation = await service.SendInstitutionAsync(admin, inst, f.Accounts.Users[student].Email, "Student", null, null, default);
        f.Accounts.Admins.Clear();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(student, invitation.Id, default));
        Assert.Empty(f.Db.CoachingInstitutionMemberships);
        f.Accounts.Admins[admin] = new(false, inst);
        f.Accounts.Users[student] = f.Accounts.Users[student] with { HasCoachingAccess = false };
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(student, invitation.Id, default));
        Assert.Empty(f.Db.CoachingStudentProfiles);
    }

    [Fact]
    public async Task IndependentTeacherInvitationDoesNotGrantInstitutionMembership()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var teacher = f.AddAccount("Teacher"); var student = f.AddAccount("Student");
        await f.Store.ProvisionAsync(teacher, "Teacher", default);
        var service = new CoachingInvitationStore(f.Db, f.Accounts, f.Store, TimeProvider.System, new Events(), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var invitation = await service.SendTeacherAsync(teacher, f.Accounts.Users[student].Email, null, default);
        await service.AcceptAsync(student, invitation.Id, default);
        Assert.Empty(f.Db.CoachingInstitutionMemberships);
        Assert.Null((await f.Db.CoachingTeacherStudentAssignments.SingleAsync()).InstitutionId);
        await f.Store.EndAssignmentAsync(student, teacher, student, studentInitiated: true, default);
        Assert.False((await f.Db.CoachingTeacherStudentAssignments.SingleAsync()).IsActive);
        await service.AcceptAsync(student, invitation.Id, default);
        Assert.False((await f.Db.CoachingTeacherStudentAssignments.SingleAsync()).IsActive);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CancelAsync(student, invitation.Id, default));
    }
    private sealed class Events : Coaching.Application.Interfaces.ICoachingEventPublisher
    {
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    }

}
