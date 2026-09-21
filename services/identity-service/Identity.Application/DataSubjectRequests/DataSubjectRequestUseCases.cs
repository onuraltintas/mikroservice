using EduPlatform.Shared.Security.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.DataSubjectRequests;

public interface IDataSubjectRequestRepository
{
    Task<bool> HasActiveAsync(
        Guid userId,
        DataSubjectRequestType requestType,
        CancellationToken cancellationToken);
    Task AddAsync(DataSubjectRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DataSubjectRequest>> GetByRequesterAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

public sealed record SubmitDataSubjectRequestCommand(
    DataSubjectRequestType RequestType,
    string Reason) : IRequest<DataSubjectRequestDto>;

public sealed record GetMyDataSubjectRequestsQuery : IRequest<IReadOnlyList<DataSubjectRequestDto>>;

public sealed record DataSubjectRequestDto(
    Guid Id,
    Guid RequesterUserId,
    DataSubjectRequestType RequestType,
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
        request.Status,
        request.Reason,
        request.SubmittedAt,
        request.IdentityVerifiedAt,
        request.DecisionReason,
        request.DecidedAt,
        request.CompletedAt,
        request.FailureReason);
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
        if (await repository.HasActiveAsync(userId, command.RequestType, cancellationToken))
            throw new InvalidOperationException("Aynı türde aktif bir ilgili kişi talebi zaten bulunuyor.");

        var request = DataSubjectRequest.Create(
            userId,
            command.RequestType,
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
