using System.Text.Json.Serialization;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;

namespace Coaching.Application.Commands.UpdateSessionParticipants;

/// <summary>
/// Roadmap O6: changes who takes part in a planned session, optionally also in the later sessions of its series.
/// Added students are told the session was scheduled for them, removed ones that it was cancelled for them.
/// </summary>
public sealed record UpdateSessionParticipantsCommand(Guid SessionId, IReadOnlyCollection<Guid> StudentIds, bool IncludeFollowing = false) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}

public sealed class UpdateSessionParticipantsCommandHandler(
    ICoachingSessionRepository repository,
    IUnitOfWork unitOfWork,
    ICoachingAccessPolicy accessPolicy,
    ICoachingIdentityAuthorizationClient identity,
    ICoachingEventPublisher events) : IRequestHandler<UpdateSessionParticipantsCommand>
{
    public const int MaxGroupStudents = 100;

    public async Task Handle(UpdateSessionParticipantsCommand command, CancellationToken cancellationToken)
    {
        var session = await repository.GetByIdAsync(command.SessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session not found");
        var studentIds = (command.StudentIds ?? []).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (studentIds.Length == 0 || session.SessionType == SessionType.OneOnOne && studentIds.Length != 1 || studentIds.Length > MaxGroupStudents)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(command.StudentIds)] = [session.SessionType == SessionType.OneOnOne
                    ? "Birebir seansta tam bir öğrenci olur."
                    : $"Grup seansında 1–{MaxGroupStudents} öğrenci olabilir."]
            });

        await identity.RequireCurrentTeacherAsync(accessPolicy, session.TeacherId, session.InstitutionId,
            command.IsInstitutionAdminOperation, cancellationToken);
        await identity.AuthorizeTeacherTargetsAsync(session.TeacherId, studentIds, session.InstitutionId,
            accessPolicy.IsSystemAdministrator, cancellationToken);

        var now = DateTime.UtcNow;
        var viewer = accessPolicy.CurrentUserId ?? session.TeacherId;
        // The sessions are tracked: saving inserts the new attendances and deletes the removed ones. Calling Update would
        // mark the new attendances (with their own ids) as modified instead of added.
        var (added, removed) = session.SetParticipants(await KeepHiddenAsync(session, studentIds, viewer, cancellationToken), now);
        if (command.IncludeFollowing && session.SeriesId is { } seriesId)
        {
            foreach (var later in (await repository.GetSeriesAsync(seriesId, cancellationToken))
                         .Where(item => item.Id != session.Id && item.ScheduledDate > session.ScheduledDate
                             && item.Status == SessionStatus.Scheduled && item.ScheduledDate > now))
            {
                later.SetParticipants(await KeepHiddenAsync(later, studentIds, viewer, cancellationToken), now);
            }
        }

        if (added.Length > 0)
            await events.PublishAsync(new SessionScheduledEvent(session.Id, session.TeacherId, session.InstitutionId, added, session.ScheduledDate), cancellationToken);
        if (removed.Length > 0)
            await events.PublishAsync(new SessionCancelledEvent(session.Id, session.TeacherId, session.InstitutionId, removed, session.ScheduledDate), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// K2 (31.10.2026): the caller only sends the participants they can see. Students already in a group session whom the
    /// caller cannot see stay in it; a one-to-one session is replaced as asked.
    /// </summary>
    private async Task<IReadOnlyCollection<Guid>> KeepHiddenAsync(CoachingSession session, IReadOnlyCollection<Guid> requested,
        Guid viewer, CancellationToken cancellationToken)
    {
        if (session.SessionType == SessionType.OneOnOne) return requested;
        var present = session.Attendances.Select(attendance => attendance.StudentId).Where(id => !requested.Contains(id)).Distinct().ToArray();
        if (present.Length == 0) return requested;
        var visible = (await identity.AuthorizeStudentWorkReadAsync(viewer, present, cancellationToken)).ToHashSet();
        return requested.Concat(present.Where(id => !visible.Contains(id))).ToArray();
    }
}
