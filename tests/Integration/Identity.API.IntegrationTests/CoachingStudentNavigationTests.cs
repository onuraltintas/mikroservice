using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudentNavigationTests
{
    [Fact]
    public async Task IndependentStudentDoesNotSeeOtherStudentsAreas()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        f.Actor.UserId = f.AddAccount("Student");
        f.Db.AssignmentStudents.Add(AssignmentStudent.Create(Guid.NewGuid(), Guid.NewGuid()));
        f.Db.SessionAttendances.Add(SessionAttendance.Create(Guid.NewGuid(), Guid.NewGuid()));
        await f.Db.SaveChangesAsync();
        var data = await Read(f);
        Assert.False(data.GetProperty("showAssignments").GetBoolean());
        Assert.False(data.GetProperty("showSessions").GetBoolean());
        Assert.False(data.GetProperty("showFees").GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoricalRecordsRemainAccessibleWithoutTeacher(bool session)
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var student = f.AddAccount("Student"); f.Actor.UserId = student;
        if (session) f.Db.SessionAttendances.Add(SessionAttendance.Create(Guid.NewGuid(), student));
        else f.Db.AssignmentStudents.Add(AssignmentStudent.Create(Guid.NewGuid(), student));
        await f.Db.SaveChangesAsync();
        var data = await Read(f);
        Assert.Equal(!session, data.GetProperty("showAssignments").GetBoolean());
        Assert.Equal(session, data.GetProperty("showSessions").GetBoolean());
    }

    [Fact]
    public async Task AssignedStudentSeesBothAreasWithoutRecords()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var student = f.AddAccount("Student"); f.Actor.UserId = student;
        var teacher = f.AddAccount("Teacher");
        f.Db.CoachingTeacherStudentAssignments.Add(CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
        await f.Db.SaveChangesAsync();
        var data = await Read(f);
        Assert.True(data.GetProperty("showAssignments").GetBoolean());
        Assert.True(data.GetProperty("showSessions").GetBoolean());
    }

    [Fact]
    public async Task HistoricalFeeRemainsAccessibleWithoutTeacher()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var student = f.AddAccount("Student"); f.Actor.UserId = student;
        var teacher = Guid.NewGuid();
        f.Db.CoachingFeeAgreements.Add(CoachingFeeAgreement.Create(Guid.NewGuid(), student, null, teacher, teacher, "Geçmiş ücret", 10m,
            [new(new DateOnly(2026, 10, 23), 10m)], DateTime.UtcNow));
        await f.Db.SaveChangesAsync();
        Assert.True((await Read(f)).GetProperty("showFees").GetBoolean());
    }

    private static async Task<JsonElement> Read(CoachingOwnedProfilePersistenceTests.Fixture f)
    {
        var controller = new CoachingProfilesController(f.Db, f.Store, f.Accounts, f.Actor);
        var result = Assert.IsType<OkObjectResult>(await controller.Navigation(default));
        return JsonSerializer.SerializeToElement(result.Value).GetProperty("data");
    }
}
