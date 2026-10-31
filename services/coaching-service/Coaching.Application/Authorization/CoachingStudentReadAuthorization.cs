using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;

namespace Coaching.Application.Authorization;

public static class CoachingStudentReadAuthorization
{
    /// <summary>K2: teaching work (assignments, submissions, attachments, sessions) — not hidden by progress sharing.</summary>
    public static Task<HashSet<Guid>> RequireWorkAsync(ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient, IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken) =>
        RequireAsync(accessPolicy, identityAuthorizationClient, studentIds, cancellationToken, work: true);

    /// <summary>K2: teaching work (assignments, submissions, attachments, sessions) — not hidden by progress sharing.</summary>
    public static Task<HashSet<Guid>> GetAuthorizedWorkStudentIdsAsync(ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient, IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken) =>
        GetAuthorizedStudentIdsAsync(accessPolicy, identityAuthorizationClient, studentIds, cancellationToken, work: true);

    public static async Task<HashSet<Guid>> RequireAsync(
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient,
        IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken,
        bool work = false)
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
            cancellationToken,
            work);

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
        CancellationToken cancellationToken,
        bool work = false)
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
            var allowedBatch = work
                ? await identityAuthorizationClient.AuthorizeStudentWorkReadAsync(currentUserId, batch, cancellationToken)
                : await identityAuthorizationClient.AuthorizeStudentReadAsync(currentUserId, batch, cancellationToken);
            allowedStudentIds.UnionWith(allowedBatch.Where(requestedBatch.Contains));
        }

        return allowedStudentIds;
    }
}
