using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>
/// User decision 15.11.2026: a student with an active teacher changes her own plan only when the teacher allows it. Tests
/// about something else, in which such a student changes her plan, switch it on here.
/// </summary>
internal static class StudentPlanningTestSupport
{
    public static async Task AllowOwnPlanningAsync(CoachingDbContext db, Guid student)
    {
        var profile = await db.CoachingStudentProfiles.SingleOrDefaultAsync(x => x.UserId == student);
        if (profile is null) db.Add(profile = CoachingStudentProfile.Create(student));
        profile.SetCanPlanOwnStudy(true, student);
        await db.SaveChangesAsync();
    }
}
