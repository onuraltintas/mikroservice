using Coaching.API.Controllers;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Coaching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingProfileCutoverTests
{
    [Fact]
    public async Task ImportTranslatesRelationshipsToUserIdsAndIsIdempotentWithoutTouchingAccounts()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var teacher = f.AddAccount("Teacher"); var student = f.AddAccount("Student");
        var institution = Guid.NewGuid(); f.Accounts.Institutions.Add(institution);
        var snapshot = new CoachingLegacySnapshot(
            [new(student, 11, "School", "Ankara", null, null, true, true, institution)],
            [new(teacher, "Coach", ["Matematik"], 5, null, null, false, true, institution)],
            [new(teacher, student, institution, "Matematik", true)]);
        var importer = new CoachingProfileImporter(f.Db, f.Accounts);
        var preview = await importer.ImportAsync(snapshot, apply: false, default);
        Assert.Equal(1, preview.Students); Assert.Empty(f.Db.CoachingStudentProfiles);
        await importer.ImportAsync(snapshot, apply: true, default);
        await importer.ImportAsync(snapshot, apply: true, default);
        var profile = await f.Db.CoachingStudentProfiles.SingleAsync();
        Assert.Equal(student, profile.UserId); Assert.Equal(11, profile.GradeLevel);
        Assert.Equal("School", profile.SchoolName);
        var assignment = await f.Db.CoachingTeacherStudentAssignments.SingleAsync();
        Assert.Equal(student, assignment.StudentUserId); Assert.Equal(teacher, assignment.TeacherUserId);
        Assert.Equal(2, await f.Db.CoachingInstitutionMemberships.CountAsync());
        Assert.Equal(2, f.Accounts.Users.Count);
    }

    [Fact]
    public async Task OwnProfileReadDoesNotProvisionAndWritesRequireFreshProductRole()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var student = f.AddAccount("Student"); f.Actor.UserId = student;
        var controller = new CoachingProfilesController(f.Db, f.Store, f.Accounts, f.Actor);
        Assert.IsType<OkObjectResult>(await controller.Get(default));
        Assert.Empty(f.Db.CoachingStudentProfiles);
        Assert.IsType<OkObjectResult>(await controller.Provision(new("Student"), default));
        Assert.Single(f.Db.CoachingStudentProfiles);
        f.Accounts.Users[student] = f.Accounts.Users[student] with { HasCoachingAccess = false };
        var denied = Assert.IsType<ObjectResult>(await controller.Get(default));
        Assert.Equal(403, denied.StatusCode);
    }
}
