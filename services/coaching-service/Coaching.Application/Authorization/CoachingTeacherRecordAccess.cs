using Coaching.Application.Interfaces;

namespace Coaching.Application.Authorization;

/// <summary>
/// Writes to records a teacher created earlier (sessions, assignments, exams). Being the creator is not
/// enough: the teacher must still be an active teacher in the record's institution and, for changes that
/// concern specific students, still teach those students. A teacher removed from an institution therefore
/// cannot keep changing links, notes or grades that former students see.
/// System administrators and institution-admin operations are authorized by their own scope checks.
/// </summary>
public static class CoachingTeacherRecordAccess
{
    public static async Task RequireCurrentTeacherAsync(this ICoachingIdentityAuthorizationClient identity,
        ICoachingAccessPolicy access, Guid teacherId, Guid? institutionId, bool institutionAdminOperation,
        CancellationToken cancellationToken, IEnumerable<Guid>? studentIds = null)
    {
        access.RequireTeacher(teacherId, institutionAdminOperation);
        if (access.IsSystemAdministrator || institutionAdminOperation && access.IsInstitutionAdministrator)
            return;
        var students = (studentIds ?? []).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (students.Length == 0)
        {
            await identity.AuthorizeTeacherTargetsAsync(teacherId, [], institutionId, false, cancellationToken);
            return;
        }
        foreach (var batch in students.Chunk(100))
            await identity.AuthorizeTeacherTargetsAsync(teacherId, batch, institutionId, false, cancellationToken);
    }
}
