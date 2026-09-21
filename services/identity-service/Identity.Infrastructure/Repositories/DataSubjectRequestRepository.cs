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
        CancellationToken cancellationToken) =>
        context.DataSubjectRequests.AnyAsync(
            request => request.RequesterUserId == userId
                && request.RequestType == requestType
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
            exception.InnerException is PostgresException
            {
                ConstraintName: "IX_DataSubjectRequests_RequesterUserId_RequestType"
            })
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
}
