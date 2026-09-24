namespace Coaching.Application.Authorization;

/// <summary>One-request authority granted only after institution and teacher targets are verified.</summary>
public sealed class CoachingInstitutionDelegation
{
    private Guid? _teacherId;

    public void Grant(Guid teacherId)
    {
        if (teacherId == Guid.Empty)
        {
            throw new ArgumentException("Teacher id is required.", nameof(teacherId));
        }

        if (_teacherId is { } existingTeacherId && existingTeacherId != teacherId)
        {
            throw new InvalidOperationException("An institution request cannot delegate multiple teachers.");
        }

        _teacherId = teacherId;
    }

    public bool CanManage(Guid teacherId) => teacherId != Guid.Empty && _teacherId == teacherId;
}

public static class CoachingInstitutionWriteRules
{
    public static bool OwnsResource(CoachingAdminScope scope, Guid? institutionId) =>
        !scope.IsGlobal && scope.InstitutionId is { } ownId &&
        institutionId is { } resourceId && resourceId != Guid.Empty && ownId == resourceId;
}
