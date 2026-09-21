using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.Infrastructure.Repositories;

public sealed class DataSubjectRequestRepository(IdentityDbContext context)
    : IDataSubjectRequestRepository
{
    private static readonly DataSubjectRequestStatus[] ActiveStatuses =
    [
        DataSubjectRequestStatus.Submitted,
        DataSubjectRequestStatus.IdentityVerified,
        DataSubjectRequestStatus.Approved,
        DataSubjectRequestStatus.Processing
    ];

    public Task<bool> HasActiveAsync(
        Guid userId,
        DataSubjectRequestType requestType,
        EduPlatform.Shared.Contracts.Events.Privacy.PersonalDataScope scope,
        CancellationToken cancellationToken) =>
        context.DataSubjectRequests.AnyAsync(
            request => request.RequesterUserId == userId
                && request.RequestType == requestType
                && request.Scope == scope
                && ActiveStatuses.Contains(request.Status),
            cancellationToken);

    public async Task AddAsync(DataSubjectRequest request, CancellationToken cancellationToken)
    {
        context.DataSubjectRequests.Add(request);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgresException
            && postgresException.ConstraintName is
                "IX_DataSubjectRequests_RequesterUserId_RequestType" or
                "IX_DataSubjectRequests_RequesterUserId_RequestType_Scope")
        {
            throw new InvalidOperationException(
                "Aynı türde aktif bir ilgili kişi talebi zaten bulunuyor.",
                exception);
        }
    }

    public async Task<IReadOnlyList<DataSubjectRequest>> GetByRequesterAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await context.DataSubjectRequests
            .AsNoTracking()
            .Where(request => request.RequesterUserId == userId)
            .OrderByDescending(request => request.SubmittedAt)
            .ThenByDescending(request => request.Id)
            .ToListAsync(cancellationToken);

    public Task<DataSubjectRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.DataSubjectRequests.SingleOrDefaultAsync(request => request.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<DataSubjectRequest> Items, int TotalCount)> GetForReviewAsync(
        DataSubjectRequestStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = context.DataSubjectRequests.AsNoTracking();
        if (status.HasValue)
            query = query.Where(request => request.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(request => request.SubmittedAt)
            .ThenBy(request => request.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
