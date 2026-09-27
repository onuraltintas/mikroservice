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

        var allowedStudentIds = await GetAuthorizedStudentIdsAsync(
            accessPolicy,
            identityAuthorizationClient,
            distinctStudentIds,
            cancellationToken);

        if (allowedStudentIds.Count == 0)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Öğrenci verisine erişim yetkiniz yok.");
        }

        return allowedStudentIds;
    }

    public static async Task<HashSet<Guid>> GetAuthorizedStudentIdsAsync(
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient,
        IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken)
    {
        var distinctStudentIds = studentIds.Distinct().ToArray();
        if (distinctStudentIds.Length == 0)
            return [];

        var currentUserId = accessPolicy.CurrentUserId
            ?? throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Oturum açılmış kullanıcı bulunamadı.");
        var allowedStudentIds = new HashSet<Guid>();
        foreach (var batch in distinctStudentIds.Chunk(100))
        {
            var requestedBatch = batch.ToHashSet();
            var allowedBatch = await identityAuthorizationClient.AuthorizeStudentReadAsync(
                currentUserId,
                batch,
                cancellationToken);
            allowedStudentIds.UnionWith(allowedBatch.Where(requestedBatch.Contains));
        }

        return allowedStudentIds;
    }
}
