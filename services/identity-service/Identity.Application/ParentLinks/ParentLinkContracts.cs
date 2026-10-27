using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Enums;

namespace Identity.Application.ParentLinks;

/// <summary>
/// Self-service parent links: the student issues a code, the parent requests a link with it and the student
/// approves. A verified link lets the parent read the child's Coaching data (roadmap Y7).
/// </summary>
public interface IParentLinkService
{
    Task<Result<ParentLinkCodeIssued>> CreateCodeAsync(Guid studentUserId, CancellationToken ct);
    Task<Result<ParentLinkView>> RequestAsync(Guid parentUserId, string code, ParentRelationship relationship, CancellationToken ct);
    Task<Result<IReadOnlyList<ParentLinkView>>> ListAsync(Guid userId, CancellationToken ct);
    Task<Result> ApproveAsync(Guid studentUserId, Guid linkId, CancellationToken ct);
    Task<Result> RevokeAsync(Guid userId, Guid linkId, CancellationToken ct);
    Task<ParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct);
}

public sealed record ParentLinkCodeIssued(string Code, DateTime ExpiresAt);

public sealed record ParentLinkView(Guid Id, Guid ParentUserId, string ParentName, Guid StudentUserId, string StudentName,
    string Relationship, string Status, DateTime RequestedAt, DateTime? VerifiedAt);

public sealed record ParentSummaryRecipient(Guid ParentUserId, string ParentEmail, string ParentName, Guid StudentUserId, string StudentName);
public sealed record ParentSummaryRecipientPage(IReadOnlyList<ParentSummaryRecipient> Items, int TotalCount);

public static class ParentLinkErrors
{
    public static readonly Error Forbidden = new("ParentLink.Forbidden", "Bu işlem için öğrenci ya da veli hesabıyla giriş yapmalısın.");
    public static readonly Error InvalidCode = new("ParentLink.InvalidCode", "Kod geçersiz, kullanılmış veya süresi dolmuş. Öğrenciden yeni bir kod iste.");
    public static readonly Error InvalidRelationship = new("ParentLink.InvalidRelationship", "Yakınlık olarak anne, baba ya da vasi seçilmelidir.");
    public static readonly Error AlreadyLinked = new("ParentLink.AlreadyLinked", "Bu öğrenciyle zaten bir bağlantın ya da bekleyen bir isteğin var.");
    public static readonly Error NotFound = new("ParentLink.NotFound", "Veli bağlantısı bulunamadı.");
    public static readonly Error NotPending = new("ParentLink.NotPending", "Bu bağlantı onay beklemiyor.");
}
