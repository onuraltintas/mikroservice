using EduPlatform.Shared.Security.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Contracts.Events.Privacy;

namespace Identity.Application.DataSubjectRequests;

public interface IDataSubjectRequestRepository
{
    Task<bool> HasActiveAsync(
        Guid userId,
        DataSubjectRequestType requestType,
        PersonalDataScope scope,
        CancellationToken cancellationToken);
    Task AddAsync(DataSubjectRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DataSubjectRequest>> GetByRequesterAsync(
        Guid userId,
        CancellationToken cancellationToken);
    Task<DataSubjectRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<DataSubjectRequest> Items, int TotalCount)> GetForReviewAsync(
        DataSubjectRequestStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IDataSubjectRequestEventPublisher
{
    Task PublishAssessmentRequestedAsync(
        PersonalDataErasureAssessmentRequestedV1 message,
        CancellationToken cancellationToken);
}

public sealed record SubmitDataSubjectRequestCommand(
    DataSubjectRequestType RequestType,
    PersonalDataScope Scope,
    string Reason) : IRequest<DataSubjectRequestDto>;

public sealed record GetMyDataSubjectRequestsQuery : IRequest<IReadOnlyList<DataSubjectRequestDto>>;
public sealed record GetDataSubjectRequestReviewDetailQuery(Guid RequestId)
    : IRequest<DataSubjectRequestReviewDetailDto>;
public sealed record GetDataSubjectRequestsForReviewQuery(
    DataSubjectRequestStatus? Status,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<DataSubjectRequestPageDto>;
public sealed record VerifyDataSubjectRequestIdentityCommand(Guid RequestId) : IRequest<DataSubjectRequestDto>;
public sealed record DecideDataSubjectRequestCommand(
    Guid RequestId,
    bool Approve,
    string Reason) : IRequest<DataSubjectRequestDto>;

public sealed record DataSubjectRequestPageDto(
    IReadOnlyList<DataSubjectRequestDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public static class DataErasureAssessmentScopePolicy
{
    public static IReadOnlyList<string> RequiredServices(PersonalDataScope scope) => scope switch
    {
        PersonalDataScope.Account => ["Coaching", "Notification", "SpeedReading"],
        PersonalDataScope.Coaching => ["Coaching"],
        PersonalDataScope.SpeedReading => ["SpeedReading"],
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };
}

public sealed record DataSubjectRequestReviewDetailDto(
    DataSubjectRequestDto Request,
    DataSubjectRequestAssessmentSummaryDto Assessment);

public sealed record DataSubjectRequestAssessmentItemDto(
    string ServiceName,
    bool CanProceed,
    bool HasActiveLegalHold,
    int AssignmentCount,
    int AttachmentCount,
    int ExamResultCount,
    int GoalCount,
    int SessionCount,
    int AgreementCount,
    int TotalRecordCount,
    DateTime AssessedAt)
{
    public static DataSubjectRequestAssessmentItemDto From(DataSubjectRequestAssessmentResult result) => new(
        result.ServiceName,
        result.CanProceed,
        result.HasActiveLegalHold,
        result.AssignmentCount,
        result.AttachmentCount,
        result.ExamResultCount,
        result.GoalCount,
        result.SessionCount,
        result.AgreementCount,
        result.TotalRecordCount,
        result.AssessedAt);
}

public sealed record DataSubjectRequestAssessmentSummaryDto(
    IReadOnlyList<DataSubjectRequestAssessmentItemDto> Services,
    IReadOnlyList<string> MissingServices,
    bool IsComplete,
    bool HasBlockingLegalHold,
    bool IsReadyForErasure,
    int TotalRecordCount)
{
    public static DataSubjectRequestAssessmentSummaryDto Create(
        IEnumerable<string> requiredServices,
        IEnumerable<DataSubjectRequestAssessmentResult> results)
    {
        var required = requiredServices
            .Where(service => !string.IsNullOrWhiteSpace(service))
            .Select(service => service.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(service => service, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var items = results
            .GroupBy(result => result.ServiceName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(result => result.AssessedAt).First())
            .Where(result => required.Contains(result.ServiceName, StringComparer.OrdinalIgnoreCase))
            .OrderBy(result => result.ServiceName, StringComparer.OrdinalIgnoreCase)
            .Select(DataSubjectRequestAssessmentItemDto.From)
            .ToArray();
        var missing = required
            .Where(service => items.All(item =>
                !string.Equals(item.ServiceName, service, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var isComplete = required.Length > 0 && missing.Length == 0;
        var hasBlockingLegalHold = items.Any(item => item.HasActiveLegalHold);
        var isReadyForErasure = isComplete
            && !hasBlockingLegalHold
            && items.All(item => item.CanProceed);

        return new DataSubjectRequestAssessmentSummaryDto(
            items,
            missing,
            isComplete,
            hasBlockingLegalHold,
            isReadyForErasure,
            items.Sum(item => item.TotalRecordCount));
    }
}

public sealed record DataSubjectRequestDto(
    Guid Id,
    Guid RequesterUserId,
    DataSubjectRequestType RequestType,
    PersonalDataScope Scope,
    DataSubjectRequestStatus Status,
    string Reason,
    DateTime SubmittedAt,
    DateTime? IdentityVerifiedAt,
    string? DecisionReason,
    DateTime? DecidedAt,
    DateTime? CompletedAt,
    string? FailureReason)
{
    public static DataSubjectRequestDto From(DataSubjectRequest request) => new(
        request.Id,
        request.RequesterUserId,
        request.RequestType,
        request.Scope,
        request.Status,
        request.Reason,
        request.SubmittedAt,
        request.IdentityVerifiedAt,
        request.DecisionReason,
        request.DecidedAt,
        request.CompletedAt,
        request.FailureReason);
}

public sealed class GetDataSubjectRequestsForReviewQueryHandler(
    IDataSubjectRequestRepository repository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDataSubjectRequestsForReviewQuery, DataSubjectRequestPageDto>
{
    public async Task<DataSubjectRequestPageDto> Handle(
        GetDataSubjectRequestsForReviewQuery query,
        CancellationToken cancellationToken)
    {
        RequireSystemAdministrator(currentUser);
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var result = await repository.GetForReviewAsync(
            query.Status, pageNumber, pageSize, cancellationToken);
        return new DataSubjectRequestPageDto(
            result.Items.Select(DataSubjectRequestDto.From).ToArray(),
            result.TotalCount,
            pageNumber,
            pageSize);
    }

    internal static Guid RequireSystemAdministrator(ICurrentUserService currentUser)
    {
        if (currentUser is not { IsAuthenticated: true, UserId: { } userId }
            || !currentUser.Roles.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("System administrator access is required.");
        }

        return userId;
    }
}

public sealed class GetDataSubjectRequestReviewDetailQueryHandler(
    IDataSubjectRequestRepository requestRepository,
    IDataSubjectRequestAssessmentRepository assessmentRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDataSubjectRequestReviewDetailQuery, DataSubjectRequestReviewDetailDto>
{
    public async Task<DataSubjectRequestReviewDetailDto> Handle(
        GetDataSubjectRequestReviewDetailQuery query,
        CancellationToken cancellationToken)
    {
        GetDataSubjectRequestsForReviewQueryHandler.RequireSystemAdministrator(currentUser);
        var request = await requestRepository.GetByIdAsync(query.RequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(DataSubjectRequest), query.RequestId);
        var assessments = await assessmentRepository.GetByRequestIdAsync(
            query.RequestId, cancellationToken);

        return new DataSubjectRequestReviewDetailDto(
            DataSubjectRequestDto.From(request),
            DataSubjectRequestAssessmentSummaryDto.Create(
                DataErasureAssessmentScopePolicy.RequiredServices(request.Scope),
                assessments));
    }
}

public sealed class VerifyDataSubjectRequestIdentityCommandHandler(
    IDataSubjectRequestRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<VerifyDataSubjectRequestIdentityCommand, DataSubjectRequestDto>
{
    public async Task<DataSubjectRequestDto> Handle(
        VerifyDataSubjectRequestIdentityCommand command,
        CancellationToken cancellationToken)
    {
        GetDataSubjectRequestsForReviewQueryHandler.RequireSystemAdministrator(currentUser);
        var request = await repository.GetByIdAsync(command.RequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(DataSubjectRequest), command.RequestId);
        request.VerifyIdentity(timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveChangesAsync(cancellationToken);
        return DataSubjectRequestDto.From(request);
    }
}

public sealed class DecideDataSubjectRequestCommandHandler(
    IDataSubjectRequestRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IDataSubjectRequestEventPublisher eventPublisher)
    : IRequestHandler<DecideDataSubjectRequestCommand, DataSubjectRequestDto>
{
    public async Task<DataSubjectRequestDto> Handle(
        DecideDataSubjectRequestCommand command,
        CancellationToken cancellationToken)
    {
        var reviewerId = GetDataSubjectRequestsForReviewQueryHandler
            .RequireSystemAdministrator(currentUser);
        var request = await repository.GetByIdAsync(command.RequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(DataSubjectRequest), command.RequestId);
        var decidedAt = timeProvider.GetUtcNow().UtcDateTime;
        if (command.Approve)
        {
            request.Approve(reviewerId, command.Reason, decidedAt);
            await eventPublisher.PublishAssessmentRequestedAsync(
                new PersonalDataErasureAssessmentRequestedV1(
                    request.Id,
                    request.Id,
                    request.RequesterUserId,
                    decidedAt,
                    DryRun: true,
                    Scope: request.Scope),
                cancellationToken);
        }
        else
            request.Reject(reviewerId, command.Reason, decidedAt);
        await repository.SaveChangesAsync(cancellationToken);
        return DataSubjectRequestDto.From(request);
    }
}

public sealed class SubmitDataSubjectRequestCommandHandler(
    IDataSubjectRequestRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitDataSubjectRequestCommand, DataSubjectRequestDto>
{
    public async Task<DataSubjectRequestDto> Handle(
        SubmitDataSubjectRequestCommand command,
        CancellationToken cancellationToken)
    {
        var userId = RequireAuthenticatedUser(currentUser);
        if (await repository.HasActiveAsync(
                userId, command.RequestType, command.Scope, cancellationToken))
            throw new InvalidOperationException("Aynı türde aktif bir ilgili kişi talebi zaten bulunuyor.");

        var request = DataSubjectRequest.Create(
            userId,
            command.RequestType,
            command.Scope,
            command.Reason,
            timeProvider.GetUtcNow().UtcDateTime);
        await repository.AddAsync(request, cancellationToken);
        return DataSubjectRequestDto.From(request);
    }

    private static Guid RequireAuthenticatedUser(ICurrentUserService currentUser) =>
        currentUser is { IsAuthenticated: true, UserId: { } userId }
            ? userId
            : throw new UnauthorizedAccessException("Authenticated user is required.");
}

public sealed class GetMyDataSubjectRequestsQueryHandler(
    IDataSubjectRequestRepository repository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMyDataSubjectRequestsQuery, IReadOnlyList<DataSubjectRequestDto>>
{
    public async Task<IReadOnlyList<DataSubjectRequestDto>> Handle(
        GetMyDataSubjectRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser is { IsAuthenticated: true, UserId: { } id }
            ? id
            : throw new UnauthorizedAccessException("Authenticated user is required.");
        return (await repository.GetByRequesterAsync(userId, cancellationToken))
            .Select(DataSubjectRequestDto.From)
            .ToArray();
    }
}
