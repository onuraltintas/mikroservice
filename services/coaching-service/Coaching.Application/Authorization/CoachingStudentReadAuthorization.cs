using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;

namespace Coaching.Application.Authorization;

public static class CoachingStudentReadAuthorization
{
    public static async Task<HashSet<Guid>> RequireAsync(
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient,
        IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken)
    {
        var distinctStudentIds = studentIds.Distinct().ToArray();
        if (distinctStudentIds.Length == 0)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Öğrenci erişim kapsamı boş olamaz.");
        }

        var currentUserId = accessPolicy.CurrentUserId
            ?? throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Oturum açılmış kullanıcı bulunamadı.");

        var allowedStudentIds = (await identityAuthorizationClient
                .AuthorizeStudentReadAsync(currentUserId, distinctStudentIds, cancellationToken))
            .Where(distinctStudentIds.Contains)
            .ToHashSet();

        if (allowedStudentIds.Count == 0)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Öğrenci verisine erişim yetkiniz yok.");
        }

        return allowedStudentIds;
    }
}
