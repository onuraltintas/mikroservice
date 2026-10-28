using System.Security.Cryptography;
using Coaching.Application.Attachments;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.Management;

public sealed record CoachingMessageContact(Guid UserId, string Name, string Role, Guid StudentId, string StudentName, Guid? ConversationId);
public sealed record CoachingConversationStart(Guid OtherUserId, Guid StudentId);
public sealed record CoachingConversationItem(Guid Id, string Kind, Guid StudentId, string StudentName, Guid OtherUserId, string OtherName,
    string OtherRole, DateTime LastMessageAt, string? LastMessagePreview, int UnreadCount, bool CanSend);
public sealed record CoachingConversationPage(IReadOnlyList<CoachingConversationItem> Items, int TotalCount, int PageNumber, int PageSize);
public sealed record CoachingMessageAttachmentView(Guid Id, string FileName, string ContentType, long SizeBytes);
public sealed record CoachingMessageView(Guid Id, Guid SenderUserId, string SenderName, bool Mine, string Body, DateTime SentAt, bool ReadByOther,
    IReadOnlyList<CoachingMessageAttachmentView> Attachments);
public sealed record CoachingConversationThread(CoachingConversationItem Conversation, IReadOnlyList<CoachingMessageView> Messages, bool HasOlder,
    DateTime? LastInstitutionReviewAt);
public sealed record CoachingMessageSend(string? Body, IReadOnlyList<Guid>? AttachmentIds);
public sealed record CoachingInstitutionConversationItem(Guid Id, string Kind, Guid TeacherId, string TeacherName, Guid StudentId, string StudentName,
    Guid CounterpartId, string CounterpartName, DateTime LastMessageAt, int MessageCount, DateTime? LastReviewedAt);
public sealed record CoachingInstitutionConversationPage(IReadOnlyList<CoachingInstitutionConversationItem> Items, int TotalCount, int PageNumber, int PageSize);

/// <summary>An attachment opened for download; the caller disposes it.</summary>
public sealed class CoachingMessageFile(Stream content, string fileName, string contentType) : IAsyncDisposable
{
    public Stream Content { get; } = content;
    public string FileName { get; } = fileName;
    public string ContentType { get; } = contentType;
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>
/// Roadmap O3: one-to-one messages between a teacher and their student, and between a teacher and a verified parent of
/// their student. Only an active teacher–student link (and, for a parent, a verified parent link) allows writing; an ended
/// link leaves the conversation readable but closed. Files go through the attachment policy, signature check and malware
/// scan. The link's institution may review a conversation with a recorded reason that both sides can see.
/// Conversations are removed <see cref="RetentionDays"/> days after their last message, and with an erased participant.
/// </summary>
public sealed class CoachingMessagingService(CoachingDbContext db, ICoachingAccountDirectory accounts, ICoachingParentDirectory parents,
    CoachingProfileStore profiles, IAssignmentAttachmentStorage storage, IAssignmentAttachmentScanner scanner, TimeProvider clock,
    ICoachingEventPublisher? events = null, ILogger<CoachingMessagingService>? logger = null)
{
    public const int RetentionDays = 730;
    public const int MaxAttachmentsPerMessage = 5;
    public const int MaxPendingUploads = 10;
    private const int PageLimit = 50;
    private const int ThreadPage = 50;
    private const string Teacher = "Teacher", Student = "Student", Parent = "Parent";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<CoachingMessageContact>> ContactsAsync(Guid actor, CancellationToken ct)
    {
        var account = await RequireAccountAsync(actor, ct);
        var pairs = new List<(Guid UserId, string Role, Guid StudentId)>();
        if (account.IsEligible(Teacher))
        {
            var students = await ActiveLinks().Where(x => x.TeacherUserId == actor).Select(x => x.StudentUserId).Distinct().ToListAsync(ct);
            pairs.AddRange(students.Select(x => (x, Student, x)));
            if (students.Count > 0)
                pairs.AddRange((await parents.GetParentLinksAsync(students, null, ct)).Select(x => (x.ParentUserId, Parent, x.StudentUserId)));
        }
        if (account.IsEligible(Student))
            pairs.AddRange((await ActiveLinks().Where(x => x.StudentUserId == actor).Select(x => x.TeacherUserId).Distinct().ToListAsync(ct))
                .Select(x => (x, Teacher, actor)));
        if (account.IsEligible(Parent))
        {
            var children = (await parents.GetParentLinksAsync([], actor, ct)).Where(x => x.ParentUserId == actor).Select(x => x.StudentUserId).Distinct().ToList();
            pairs.AddRange((await ActiveLinks().Where(x => children.Contains(x.StudentUserId)).Select(x => new { x.TeacherUserId, x.StudentUserId }).ToListAsync(ct))
                .Select(x => (x.TeacherUserId, Teacher, x.StudentUserId)));
        }
        pairs = pairs.Where(x => x.UserId != actor).Distinct().ToList();
        if (pairs.Count == 0) return [];
        var people = await AccountsAsync(pairs.Select(x => x.UserId).Concat(pairs.Select(x => x.StudentId)), ct);
        var conversations = await db.Set<CoachingConversation>().AsNoTracking()
            .Where(x => x.TeacherUserId == actor || x.CounterpartUserId == actor)
            .Select(x => new { x.Id, x.TeacherUserId, x.CounterpartUserId, x.StudentUserId }).ToListAsync(ct);
        return pairs
            .Where(x => people.TryGetValue(x.UserId, out var person) && person.IsEligible(x.Role) && people.ContainsKey(x.StudentId))
            .Select(x => new CoachingMessageContact(x.UserId, Name(people[x.UserId]), x.Role, x.StudentId, Name(people[x.StudentId]),
                conversations.FirstOrDefault(c => c.StudentUserId == x.StudentId
                    && (c.TeacherUserId == actor && c.CounterpartUserId == x.UserId || c.CounterpartUserId == actor && c.TeacherUserId == x.UserId))?.Id))
            .OrderBy(x => x.StudentName, StringComparer.Create(new("tr-TR"), true)).ThenBy(x => x.Role == Student ? 0 : 1)
            .ThenBy(x => x.Name, StringComparer.Create(new("tr-TR"), true))
            .ToArray();
    }

    public async Task<CoachingConversationItem> StartAsync(Guid actor, CoachingConversationStart request, CancellationToken ct)
    {
        var account = await RequireAccountAsync(actor, ct);
        var people = await AccountsAsync([request.OtherUserId, request.StudentId], ct);
        if (!people.TryGetValue(request.OtherUserId, out var other) || !other.IsActive || request.OtherUserId == actor) throw Forbidden();
        (Guid TeacherId, Guid CounterpartId, CoachingConversationKind Kind)? sides =
            account.IsEligible(Teacher) && request.OtherUserId == request.StudentId && other.IsEligible(Student) ? (actor, request.StudentId, CoachingConversationKind.TeacherStudent)
            : account.IsEligible(Teacher) && request.OtherUserId != request.StudentId && other.IsEligible(Parent) ? (actor, request.OtherUserId, CoachingConversationKind.TeacherParent)
            : account.IsEligible(Student) && actor == request.StudentId && other.IsEligible(Teacher) ? (request.OtherUserId, actor, CoachingConversationKind.TeacherStudent)
            : account.IsEligible(Parent) && actor != request.StudentId && other.IsEligible(Teacher) ? (request.OtherUserId, actor, CoachingConversationKind.TeacherParent)
            : null;
        if (sides is not { } side) throw Forbidden();
        var link = await LinkAsync(side.TeacherId, request.StudentId, ct) ?? throw Forbidden();
        if (side.Kind == CoachingConversationKind.TeacherParent && await accounts.AuthorizeRepresentativeAsync(side.CounterpartId, request.StudentId, ct) is null)
            throw Forbidden();

        var conversation = await FindAsync(side.TeacherId, side.CounterpartId, request.StudentId, ct);
        if (conversation is null)
        {
            conversation = CoachingConversation.Create(side.TeacherId, request.StudentId, side.CounterpartId, side.Kind, link.InstitutionId, Now);
            db.Add(conversation);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // Both sides started it at the same moment: the unique index keeps one.
                db.Entry(conversation).State = EntityState.Detached;
                var existing = await FindAsync(side.TeacherId, side.CounterpartId, request.StudentId, ct);
                if (existing is null) throw;
                conversation = existing;
            }
        }
        return (await ItemsAsync(actor, [conversation], ct)).Single();
    }

    public async Task<CoachingConversationPage> ListAsync(Guid actor, int pageNumber, int pageSize, CancellationToken ct)
    {
        await RequireAccountAsync(actor, ct);
        (pageNumber, pageSize) = Paging(pageNumber, pageSize);
        var query = db.Set<CoachingConversation>().AsNoTracking().Where(x => x.TeacherUserId == actor || x.CounterpartUserId == actor);
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(x => x.LastMessageAt).ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(await ItemsAsync(actor, page, ct), total, pageNumber, pageSize);
    }

    /// <summary>The number of conversations with a message the user has not read (the menu badge).</summary>
    public async Task<int> UnreadCountAsync(Guid actor, CancellationToken ct)
    {
        await RequireAccountAsync(actor, ct);
        return await db.Set<CoachingConversation>().CountAsync(x => x.MessageCount > 0
            && (x.TeacherUserId == actor && (x.TeacherReadAt == null || x.LastMessageAt > x.TeacherReadAt)
                || x.CounterpartUserId == actor && (x.CounterpartReadAt == null || x.LastMessageAt > x.CounterpartReadAt)), ct);
    }

    /// <summary>The latest messages (or those before <paramref name="before"/>). Opening the latest page marks it read.</summary>
    public async Task<CoachingConversationThread> GetAsync(Guid actor, Guid conversationId, DateTime? before, CancellationToken ct)
    {
        await RequireAccountAsync(actor, ct);
        var conversation = await ParticipantConversationAsync(actor, conversationId, ct);
        if (before is null)
        {
            conversation.MarkRead(actor, Now);
            await db.SaveChangesAsync(ct);
        }
        return await ThreadAsync(conversation, actor, before, ct);
    }

    public async Task<CoachingMessageView> SendAsync(Guid actor, Guid conversationId, CoachingMessageSend request, CancellationToken ct)
    {
        var account = await RequireAccountAsync(actor, ct);
        var conversation = await ParticipantConversationAsync(actor, conversationId, ct);
        var link = await RequireOpenAsync(conversation, ct);
        var ids = (request.AttachmentIds ?? []).Distinct().ToList();
        if (ids.Count > MaxAttachmentsPerMessage) throw new ArgumentException($"Bir mesaja en fazla {MaxAttachmentsPerMessage} dosya eklenebilir.");
        var files = ids.Count == 0 ? [] : await db.Set<CoachingMessageAttachment>()
            .Where(x => ids.Contains(x.Id) && x.ConversationId == conversation.Id && x.UploaderUserId == actor && x.MessageId == null)
            .ToListAsync(ct);
        if (files.Count != ids.Count) throw new ArgumentException("Dosya bulunamadı ya da zaten gönderildi.");

        var recipient = conversation.OtherParticipant(actor);
        var recipientReadAt = conversation.ReadAtOf(recipient);
        var recipientHadUnread = conversation.MessageCount > 0 && (recipientReadAt is null || conversation.LastMessageAt > recipientReadAt);
        // Messages of a conversation keep their order even when two arrive within the same microsecond.
        // PostgreSQL keeps microseconds, so the comparison is made at that precision.
        var sentAt = Now; sentAt = sentAt.AddTicks(-(sentAt.Ticks % 10));
        if (conversation.MessageCount > 0 && sentAt <= conversation.LastMessageAt) sentAt = conversation.LastMessageAt.AddTicks(10);
        var message = CoachingMessage.Create(conversation.Id, actor, request.Body, files.Count > 0, sentAt);
        db.Add(message);
        conversation.Record(message, link.InstitutionId);
        foreach (var file in files.OrderBy(x => ids.IndexOf(x.Id))) file.AttachTo(message.Id);
        // One notification per unread batch: the recipient hears about the first message they have not read yet.
        if (!recipientHadUnread && events is not null)
            await events.PublishAsync(new CoachingMessageReceivedEvent(conversation.Id, recipient, RoleOf(conversation, recipient), Name(account),
                files.Count > 0), ct);
        await db.SaveChangesAsync(ct);
        return new(message.Id, actor, Name(account), true, message.Body, message.SentAt, false, files.Select(View).ToArray());
    }

    public async Task<CoachingMessageAttachmentView> UploadAsync(Guid actor, Guid conversationId, string fileName, string contentType,
        Stream content, CancellationToken ct)
    {
        await RequireAccountAsync(actor, ct);
        var conversation = await ParticipantConversationAsync(actor, conversationId, ct);
        await RequireOpenAsync(conversation, ct);
        var name = (fileName ?? string.Empty).Trim();
        var type = (contentType ?? string.Empty).Split(';')[0].Trim();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920]; int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > AssignmentAttachmentPolicy.MaxFileSizeBytes)
                throw new ArgumentException("Dosya en fazla 10 MB olabilir.");
            buffer.Write(chunk, 0, read);
        }
        try { AssignmentAttachmentPolicy.ValidateMetadata(name, type, buffer.Length); }
        catch (ArgumentException) { throw UnsupportedFile(); }
        if (await db.Set<CoachingMessageAttachment>().CountAsync(x => x.ConversationId == conversation.Id && x.UploaderUserId == actor && x.MessageId == null, ct)
            >= MaxPendingUploads) throw new ArgumentException("Gönderilmemiş çok fazla dosya var; önce mesajını gönder.");

        var id = Guid.NewGuid();
        var key = CoachingMessageAttachment.KeyFor(conversation.Id, id);
        var sha = Convert.ToHexString(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)));
        buffer.Position = 0;
        StoredAssignmentAttachment stored;
        try { stored = await storage.StoreAsync(key, buffer, type, buffer.Length, sha, ct); }
        catch (ArgumentException) { throw UnsupportedFile(); }
        try
        {
            AttachmentScanResult scan;
            await using (var copy = await storage.OpenReadAsync(key, ct)) scan = await scanner.ScanAsync(copy, ct);
            if (!scan.IsClean)
            {
                logger?.LogWarning("Message attachment rejected by the scanner: {Threat}", scan.ThreatName);
                throw new ArgumentException("Dosya güvenlik taramasından geçemedi.");
            }
            var attachment = CoachingMessageAttachment.Create(id, conversation.Id, actor, name, stored.ContentType, stored.SizeBytes, stored.Sha256, Now);
            db.Add(attachment);
            await db.SaveChangesAsync(ct);
            return View(attachment);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }
    }

    public async Task<CoachingMessageFile> OpenAttachmentAsync(Guid actor, Guid conversationId, Guid attachmentId, CancellationToken ct)
    {
        var conversation = await db.Set<CoachingConversation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == conversationId, ct)
            ?? throw new KeyNotFoundException();
        // Participants open their files; an institution admin only right after a recorded review of this conversation.
        if (conversation.IsParticipant(actor)) await RequireAccountAsync(actor, ct);
        else if (!await HasRecentReviewAsync(actor, conversation, ct)) throw new KeyNotFoundException();
        var file = await db.Set<CoachingMessageAttachment>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == attachmentId && x.ConversationId == conversation.Id && (x.MessageId != null || x.UploaderUserId == actor), ct)
            ?? throw new KeyNotFoundException();
        return new(await storage.OpenReadAsync(file.StorageKey, ct), file.FileName, file.ContentType);
    }

    public async Task<CoachingInstitutionConversationPage> ListInstitutionAsync(Guid actor, Guid institutionId, int pageNumber, int pageSize, CancellationToken ct)
    {
        await profiles.RequireInstitutionAdminAsync(actor, institutionId, ct);
        (pageNumber, pageSize) = Paging(pageNumber, pageSize);
        var query = db.Set<CoachingConversation>().AsNoTracking().Where(x => x.InstitutionId == institutionId && x.MessageCount > 0);
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(x => x.LastMessageAt).ThenBy(x => x.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = page.Select(x => x.Id).ToList();
        var reviews = await db.Set<CoachingConversationAccess>().AsNoTracking().Where(x => ids.Contains(x.ConversationId))
            .GroupBy(x => x.ConversationId).Select(x => new { x.Key, Last = x.Max(y => y.AccessedAt) }).ToDictionaryAsync(x => x.Key, x => x.Last, ct);
        var people = await AccountsAsync(page.SelectMany(x => new[] { x.TeacherUserId, x.StudentUserId, x.CounterpartUserId }), ct);
        return new(page.Select(x => new CoachingInstitutionConversationItem(x.Id, x.Kind.ToString(), x.TeacherUserId, NameOf(people, x.TeacherUserId),
            x.StudentUserId, NameOf(people, x.StudentUserId), x.CounterpartUserId, NameOf(people, x.CounterpartUserId), x.LastMessageAt, x.MessageCount,
            reviews.TryGetValue(x.Id, out var last) ? last : null)).ToArray(), total, pageNumber, pageSize);
    }

    /// <summary>The institution reads a conversation; the reason is recorded and both participants see that it happened.</summary>
    public async Task<CoachingConversationThread> ReviewAsync(Guid actor, Guid institutionId, Guid conversationId, string? reason, CancellationToken ct)
    {
        await profiles.RequireInstitutionAdminAsync(actor, institutionId, ct);
        var conversation = await db.Set<CoachingConversation>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == conversationId && x.InstitutionId == institutionId, ct) ?? throw new KeyNotFoundException();
        db.Add(CoachingConversationAccess.Create(conversation.Id, actor, reason, Now));
        await db.SaveChangesAsync(ct);
        return await ThreadAsync(conversation, null, null, ct);
    }

    /// <summary>Removes files that were uploaded but never sent (after a day) and conversations past the retention period.</summary>
    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        const int batch = 200;
        var now = Now;
        var unsentBefore = now.AddDays(-1);
        var unsent = await db.Set<CoachingMessageAttachment>().Where(x => x.MessageId == null && x.CreatedAt < unsentBefore)
            .OrderBy(x => x.CreatedAt).Take(batch).ToListAsync(ct);
        var expiredBefore = now.AddDays(-RetentionDays);
        var expired = await db.Set<CoachingConversation>().Where(x => x.LastMessageAt < expiredBefore)
            .OrderBy(x => x.LastMessageAt).Take(batch).ToListAsync(ct);
        var removed = await RemoveConversationsAsync(db, expired, ct);
        db.RemoveRange(unsent.Where(x => !removed.Files.Contains(x)));
        await db.SaveChangesAsync(ct);
        foreach (var key in unsent.Select(x => x.StorageKey).Concat(removed.Files.Select(x => x.StorageKey)).Distinct())
        {
            try { await storage.DeleteAsync(key, ct); }
            catch (Exception error) when (error is not OperationCanceledException)
            { logger?.LogWarning(error, "Message attachment file {Key} could not be deleted", key); }
        }
        return unsent.Count + expired.Count;
    }

    /// <summary>Marks the conversations, their messages, files and review records for removal. Files are returned for deletion after commit.</summary>
    internal static async Task<(int Records, IReadOnlyList<CoachingMessageAttachment> Files)> RemoveConversationsAsync(CoachingDbContext db,
        IReadOnlyCollection<CoachingConversation> conversations, CancellationToken ct)
    {
        if (conversations.Count == 0) return (0, []);
        var ids = conversations.Select(x => x.Id).ToList();
        var files = await db.Set<CoachingMessageAttachment>().Where(x => ids.Contains(x.ConversationId)).ToListAsync(ct);
        var messages = await db.Set<CoachingMessage>().Where(x => ids.Contains(x.ConversationId)).ToListAsync(ct);
        var reviews = await db.Set<CoachingConversationAccess>().Where(x => ids.Contains(x.ConversationId)).ToListAsync(ct);
        db.RemoveRange(files); db.RemoveRange(messages); db.RemoveRange(reviews); db.RemoveRange(conversations);
        return (conversations.Count + files.Count + messages.Count + reviews.Count, files);
    }

    private IQueryable<Coaching.Domain.Profiles.CoachingTeacherStudentAssignment> ActiveLinks() =>
        db.CoachingTeacherStudentAssignments.AsNoTracking().Where(x => x.IsActive);

    private Task<Coaching.Domain.Profiles.CoachingTeacherStudentAssignment?> LinkAsync(Guid teacher, Guid student, CancellationToken ct) =>
        ActiveLinks().Where(x => x.TeacherUserId == teacher && x.StudentUserId == student)
            .OrderByDescending(x => x.StartDate).FirstOrDefaultAsync(ct);

    private Task<CoachingConversation?> FindAsync(Guid teacher, Guid counterpart, Guid student, CancellationToken ct) =>
        db.Set<CoachingConversation>().SingleOrDefaultAsync(x => x.TeacherUserId == teacher && x.CounterpartUserId == counterpart && x.StudentUserId == student, ct);

    private async Task<CoachingConversation> ParticipantConversationAsync(Guid actor, Guid conversationId, CancellationToken ct) =>
        await db.Set<CoachingConversation>().SingleOrDefaultAsync(x => x.Id == conversationId
            && (x.TeacherUserId == actor || x.CounterpartUserId == actor), ct) ?? throw new KeyNotFoundException();

    private async Task<Coaching.Domain.Profiles.CoachingTeacherStudentAssignment> RequireOpenAsync(CoachingConversation conversation, CancellationToken ct)
    {
        var link = await LinkAsync(conversation.TeacherUserId, conversation.StudentUserId, ct);
        var people = await AccountsAsync([conversation.TeacherUserId, conversation.CounterpartUserId], ct);
        var open = link is not null
            && people.TryGetValue(conversation.TeacherUserId, out var teacher) && teacher.IsEligible(Teacher)
            && people.TryGetValue(conversation.CounterpartUserId, out var counterpart)
            && counterpart.IsEligible(conversation.Kind == CoachingConversationKind.TeacherStudent ? Student : Parent)
            && (conversation.Kind == CoachingConversationKind.TeacherStudent
                || await accounts.AuthorizeRepresentativeAsync(conversation.CounterpartUserId, conversation.StudentUserId, ct) is not null);
        return open ? link! : throw new BusinessRuleException("Messaging.Closed", "Bu yazışma kapandı; yeni mesaj gönderilemez.");
    }

    private async Task<bool> HasRecentReviewAsync(Guid actor, CoachingConversation conversation, CancellationToken ct)
    {
        if (conversation.InstitutionId is not { } institution) return false;
        var since = Now.AddHours(-1);
        if (!await db.Set<CoachingConversationAccess>().AnyAsync(x => x.ConversationId == conversation.Id && x.ViewerUserId == actor && x.AccessedAt >= since, ct))
            return false;
        try { await profiles.RequireInstitutionAdminAsync(actor, institution, ct); return true; }
        catch (BusinessRuleException) { return false; }
    }

    private async Task<CoachingConversationThread> ThreadAsync(CoachingConversation conversation, Guid? viewer, DateTime? before, CancellationToken ct)
    {
        var rows = await db.Set<CoachingMessage>().AsNoTracking()
            .Where(x => x.ConversationId == conversation.Id && (before == null || x.SentAt < before))
            .OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id).Take(ThreadPage + 1).ToListAsync(ct);
        var hasOlder = rows.Count > ThreadPage;
        var messages = rows.Take(ThreadPage).Reverse().ToList();
        var messageIds = messages.Select(x => (Guid?)x.Id).ToList();
        var files = (await db.Set<CoachingMessageAttachment>().AsNoTracking().Where(x => messageIds.Contains(x.MessageId)).ToListAsync(ct))
            .GroupBy(x => x.MessageId!.Value).ToDictionary(x => x.Key, x => x.OrderBy(y => y.CreatedAt).Select(View).ToArray());
        var people = await AccountsAsync([conversation.TeacherUserId, conversation.CounterpartUserId, conversation.StudentUserId], ct);
        var review = await db.Set<CoachingConversationAccess>().AsNoTracking().Where(x => x.ConversationId == conversation.Id)
            .OrderByDescending(x => x.AccessedAt).Select(x => (DateTime?)x.AccessedAt).FirstOrDefaultAsync(ct);
        var item = viewer is { } user ? (await ItemsAsync(user, [conversation], ct)).Single()
            : new CoachingConversationItem(conversation.Id, conversation.Kind.ToString(), conversation.StudentUserId, NameOf(people, conversation.StudentUserId),
                conversation.CounterpartUserId, NameOf(people, conversation.CounterpartUserId), RoleOf(conversation, conversation.CounterpartUserId),
                conversation.LastMessageAt, null, 0, false);
        return new(item, messages.Select(x =>
        {
            var otherReadAt = conversation.ReadAtOf(conversation.OtherParticipant(x.SenderUserId));
            return new CoachingMessageView(x.Id, x.SenderUserId, NameOf(people, x.SenderUserId), viewer == x.SenderUserId, x.Body, x.SentAt,
                viewer == x.SenderUserId && otherReadAt >= x.SentAt, files.GetValueOrDefault(x.Id) ?? []);
        }).ToArray(), hasOlder, review);
    }

    private async Task<IReadOnlyList<CoachingConversationItem>> ItemsAsync(Guid actor, IReadOnlyList<CoachingConversation> conversations, CancellationToken ct)
    {
        if (conversations.Count == 0) return [];
        var ids = conversations.Select(x => x.Id).ToList();
        var last = (await db.Set<CoachingMessage>().AsNoTracking().Where(x => ids.Contains(x.ConversationId))
                .GroupBy(x => x.ConversationId).Select(x => x.OrderByDescending(y => y.SentAt).ThenByDescending(y => y.Id).First()).ToListAsync(ct))
            .ToDictionary(x => x.ConversationId);
        var unread = await (from message in db.Set<CoachingMessage>().AsNoTracking()
                            join conversation in db.Set<CoachingConversation>().AsNoTracking() on message.ConversationId equals conversation.Id
                            where ids.Contains(conversation.Id) && message.SenderUserId != actor
                                && (conversation.TeacherUserId == actor
                                    ? conversation.TeacherReadAt == null || message.SentAt > conversation.TeacherReadAt
                                    : conversation.CounterpartReadAt == null || message.SentAt > conversation.CounterpartReadAt)
                            group message by message.ConversationId into grouped
                            select new { grouped.Key, Count = grouped.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var people = await AccountsAsync(conversations.SelectMany(x => new[] { x.TeacherUserId, x.CounterpartUserId, x.StudentUserId }), ct);
        var teacherIds = conversations.Select(x => x.TeacherUserId).Distinct().ToList();
        var studentIds = conversations.Select(x => x.StudentUserId).Distinct().ToList();
        var links = (await ActiveLinks().Where(x => teacherIds.Contains(x.TeacherUserId) && studentIds.Contains(x.StudentUserId))
            .Select(x => new { x.TeacherUserId, x.StudentUserId }).ToListAsync(ct)).Select(x => (x.TeacherUserId, x.StudentUserId)).ToHashSet();
        var parentStudents = conversations.Where(x => x.Kind == CoachingConversationKind.TeacherParent).Select(x => x.StudentUserId).Distinct().ToList();
        var parentLinks = parentStudents.Count == 0 ? [] : (await parents.GetParentLinksAsync(parentStudents, null, ct))
            .Select(x => (x.ParentUserId, x.StudentUserId)).ToHashSet();
        return conversations.Select(x =>
        {
            var other = x.OtherParticipant(actor);
            var open = links.Contains((x.TeacherUserId, x.StudentUserId))
                && people.TryGetValue(x.TeacherUserId, out var teacher) && teacher.IsEligible(Teacher)
                && people.TryGetValue(x.CounterpartUserId, out var counterpart)
                && (x.Kind == CoachingConversationKind.TeacherStudent ? counterpart.IsEligible(Student)
                    : counterpart.IsEligible(Parent) && parentLinks.Contains((x.CounterpartUserId, x.StudentUserId)));
            var preview = last.TryGetValue(x.Id, out var message) ? Preview(message.Body) : null;
            return new CoachingConversationItem(x.Id, x.Kind.ToString(), x.StudentUserId, NameOf(people, x.StudentUserId), other, NameOf(people, other),
                RoleOf(x, other), x.LastMessageAt, preview, unread.GetValueOrDefault(x.Id), open);
        }).ToArray();
    }

    private static string Preview(string body) => body.Length == 0 ? "Dosya gönderildi" : body.Length <= 120 ? body : body[..117] + "...";

    private static string RoleOf(CoachingConversation conversation, Guid user) => user == conversation.TeacherUserId ? Teacher
        : conversation.Kind == CoachingConversationKind.TeacherStudent ? Student : Parent;

    private async Task<CoachingAccount> RequireAccountAsync(Guid actor, CancellationToken ct)
    {
        if (actor == Guid.Empty) throw Forbidden();
        var account = (await accounts.GetAccountsAsync([actor], ct)).SingleOrDefault(x => x.UserId == actor);
        return account is { IsActive: true } && (account.IsEligible(Teacher) || account.IsEligible(Student) || account.IsEligible(Parent))
            ? account : throw Forbidden();
    }

    private async Task<Dictionary<Guid, CoachingAccount>> AccountsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(x => x != Guid.Empty).Distinct().ToArray();
        return distinct.Length == 0 ? [] : (await accounts.GetAccountsAsync(distinct, ct)).ToDictionary(x => x.UserId);
    }

    private static (int, int) Paging(int pageNumber, int pageSize) => (Math.Max(1, pageNumber), Math.Clamp(pageSize, 1, PageLimit));
    private static string Name(CoachingAccount account) => $"{account.FirstName} {account.LastName}".Trim();
    private static string NameOf(IReadOnlyDictionary<Guid, CoachingAccount> people, Guid id) => people.TryGetValue(id, out var account) ? Name(account) : "Kullanıcı";
    private static CoachingMessageAttachmentView View(CoachingMessageAttachment x) => new(x.Id, x.FileName, x.ContentType, x.SizeBytes);
    private static ArgumentException UnsupportedFile() =>
        new("Dosya türü ya da içeriği desteklenmiyor. 10 MB'a kadar fotoğraf (JPG, PNG, WEBP), PDF, Word ya da Markdown dosyası ekleyebilirsin.");
    private static BusinessRuleException Forbidden() => new("Authorization.Forbidden", "Bu yazışma için aktif bir öğretmen–öğrenci ya da veli bağlantısı gereklidir.");
}

public sealed class CoachingMessagingRetentionWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<CoachingMessagingRetentionWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int removed;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    removed = await scope.ServiceProvider.GetRequiredService<CoachingMessagingService>().CleanupAsync(stoppingToken);
                } while (removed > 0 && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Message retention cleanup failed; the next cycle will retry."); }
            await Task.Delay(TimeSpan.FromHours(6), clock, stoppingToken);
        }
    }
}
