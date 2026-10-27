using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap Y8: the birth date asked at registration is kept on the Coaching student profile.
public sealed class CoachingBirthDateTests
{
    [Fact]
    public async Task ProvisioningKeepsTheBirthDateGivenAtRegistrationAndDoesNotOverwriteIt()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var student = f.AddAccount("Student");

        await f.Store.ProvisionAsync(student, "Student", default, new DateOnly(2011, 5, 3));
        Assert.Equal(new DateOnly(2011, 5, 3), (await f.Db.CoachingStudentProfiles.SingleAsync()).BirthDate);

        await f.Store.ProvisionAsync(student, "Student", default, new DateOnly(2012, 1, 1));
        Assert.Equal(new DateOnly(2011, 5, 3), (await f.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync()).BirthDate);
    }
}
