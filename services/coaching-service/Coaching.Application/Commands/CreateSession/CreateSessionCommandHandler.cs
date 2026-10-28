using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Application.Authorization;
using Coaching.Application.Exceptions;
using Coaching.Application.Idempotency;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MediatR;

namespace Coaching.Application.Commands.CreateSession;

public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, CreateSessionResponse>
{
    private const string IdempotencyScope = "coaching.sessions.create";

    private readonly ICoachingSessionRepository _repository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly ICoachingEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public CreateSessionCommandHandler(
        ICoachingSessionRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient,
        IIdempotencyRepository idempotencyRepository,
        ICoachingEventPublisher eventPublisher)
    {
        _repository = repository;
        _idempotencyRepository = idempotencyRepository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
        _eventPublisher = eventPublisher;
    }

    public async Task<CreateSessionResponse> Handle(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(command.TeacherId, command.IsInstitutionAdminOperation);

        var studentIds = command.Type == Coaching.Domain.Enums.SessionType.Group
            ? command.StudentIds?.Where(studentId => studentId != Guid.Empty).Distinct().ToArray()
            : new[] { command.StudentId };

        if (studentIds is null || studentIds.Length == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(command.StudentIds)] = ["At least one student is required."]
            });
        }

        var starts = SeriesStarts(command.StartTime, command.Recurrence);

        var key = command.IdempotencyKey?.Trim();
        EnsureKey(key);
        var requestHash = IdempotencyRequestHasher.Create(
            IdempotencyRequestHasher.Format(command.TeacherId),
            IdempotencyRequestHasher.Format(command.InstitutionId),
            string.Join(',', studentIds.Select(IdempotencyRequestHasher.Format)),
            IdempotencyRequestHasher.Format(command.StartTime),
            command.DurationMinutes.ToString(),
            command.Subject,
            command.Notes,
            command.Type.ToString(),
            command.MeetingLink,
            command.TeacherNotesVisibility.ToString(),
            command.Recurrence is { } recurrence ? $"{recurrence.IntervalWeeks}x{recurrence.Count}" : null);
        var existing = await _idempotencyRepository.GetAsync(IdempotencyScope, key!, cancellationToken);
        if (existing is not null)
        {
            if (!existing.Matches(requestHash))
                throw IdempotencyConflict();

            return await ReplayAsync(existing.ResourceId, cancellationToken);
        }

        var institutionId = await _identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
            command.TeacherId,
            studentIds,
            command.InstitutionId,
            _accessPolicy.IsSystemAdministrator,
            cancellationToken);

        // Roadmap O6: a recurring series is created as separate sessions sharing a series id, all or none.
        var seriesId = command.Recurrence is null ? (Guid?)null : Guid.NewGuid();
        CoachingSession? session = null;
        foreach (var start in starts)
        {
            var occurrence = CoachingSession.Create(
                command.TeacherId,
                command.Subject ?? "Coaching Session", // Use subject as Title
                start,
                command.Type,
                command.DurationMinutes,
                institutionId
            );
            occurrence.AddStudents(studentIds);
            if (!string.IsNullOrEmpty(command.Notes))
                occurrence.AddTeacherNotes(command.Notes, command.TeacherNotesVisibility);
            if (!string.IsNullOrWhiteSpace(command.MeetingLink))
                occurrence.SetMeetingLink(command.MeetingLink.Trim());
            if (seriesId is { } id) occurrence.AssignSeries(id);
            await _repository.AddAsync(occurrence, cancellationToken);
            session ??= occurrence;
        }
        if (session is null) throw new InvalidOperationException("No session was created.");
        await _idempotencyRepository.AddAsync(
            IdempotencyRecord.Create(IdempotencyScope, key!, requestHash, session.Id),
            cancellationToken);
        await _eventPublisher.PublishAsync(
            new SessionScheduledEvent(
                session.Id,
                session.TeacherId,
                session.InstitutionId,
                session.Attendances.Select(attendance => attendance.StudentId).ToArray(),
                session.ScheduledDate),
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (IdempotencyConflictException)
        {
            var concurrent = await _idempotencyRepository.GetAsync(IdempotencyScope, key!, cancellationToken);
            if (concurrent is not null && concurrent.Matches(requestHash))
                return await ReplayAsync(concurrent.ResourceId, cancellationToken);

            throw IdempotencyConflict();
        }

        return new CreateSessionResponse(session.Id, seriesId, starts.Count);
    }

    /// <summary>
    /// The start of every session: the same local (Türkiye) weekday and time every one or two weeks, so a series keeps
    /// "Tuesday 18:00" even if the offset of the time zone changes.
    /// </summary>
    internal static IReadOnlyList<DateTime> SeriesStarts(DateTime start, SessionRecurrence? recurrence)
    {
        var first = Coaching.Domain.CoachingTime.ToUtc(start);
        if (recurrence is null) return [first];
        var errors = new Dictionary<string, string[]>();
        if (recurrence.IntervalWeeks is not (1 or 2))
            errors[nameof(SessionRecurrence.IntervalWeeks)] = ["Tekrar her hafta ya da iki haftada bir olabilir."];
        if (recurrence.Count is < 2 or > SessionRecurrence.MaxCount)
            errors[nameof(SessionRecurrence.Count)] = [$"Tekrarlayan seans 2–{SessionRecurrence.MaxCount} seans olabilir."];
        if (errors.Count > 0) throw new ValidationException(errors);
        var local = TimeZoneInfo.ConvertTimeFromUtc(first, Coaching.Domain.CoachingTime.Turkey);
        return Enumerable.Range(0, recurrence.Count)
            .Select(index => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.AddDays(7 * recurrence.IntervalWeeks * index), DateTimeKind.Unspecified),
                Coaching.Domain.CoachingTime.Turkey))
            .ToArray();
    }

    private async Task<CreateSessionResponse> ReplayAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait seans bulunamadı; yeni bir anahtar kullanın.");
        }

        return session.SeriesId is { } seriesId
            ? new CreateSessionResponse(sessionId, seriesId, (await _repository.GetSeriesAsync(seriesId, cancellationToken)).Count)
            : new CreateSessionResponse(sessionId);
    }

    private static void EnsureKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BusinessRuleException(
                "Idempotency.Required",
                "Seans oluşturma isteği için Idempotency-Key zorunludur.");
        }
    }

    private static BusinessRuleException IdempotencyConflict() => new(
        "Idempotency.Conflict",
        "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
}
