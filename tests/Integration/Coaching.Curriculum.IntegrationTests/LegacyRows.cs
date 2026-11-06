using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>
/// Paket C: rows written with the columns every migration has, so a seed also works on a schema migrated only to an older
/// migration (student_profiles gains columns in 20261113090000_StudentOnboarding).
/// </summary>
internal static class LegacyRows
{
    public static Task StudentProfileAsync(DbContext db, Guid user) => db.Database.ExecuteSqlInterpolatedAsync(
        $"""INSERT INTO coaching.student_profiles ("Id","UserId","EducationStatus","ShareProgressWithTeachers","IsActive","CreatedAt","CreatedBy","Version") VALUES ({Guid.NewGuid()},{user},0,true,true,{DateTime.UtcNow},{user.ToString()},1)""");
}
