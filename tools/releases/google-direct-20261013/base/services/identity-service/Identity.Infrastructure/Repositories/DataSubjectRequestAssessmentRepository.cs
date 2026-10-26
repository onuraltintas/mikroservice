using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class DataSubjectRequestAssessmentRepository(IdentityDbContext context) : IDataSubjectRequestAssessmentRepository
{
    public async Task<IReadOnlyList<DataSubjectRequestAssessmentResult>> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken) =>
        await context.DataSubjectRequestAssessmentResults
            .AsNoTracking()
            .Where(result => result.RequestId == requestId)
            .OrderBy(result => result.ServiceName)
            .ToListAsync(cancellationToken);

    public async Task RecordAsync(DataSubjectRequestAssessmentResult result, CancellationToken cancellationToken)
    {
        if (await context.DataSubjectRequestAssessmentResults.AnyAsync(
            item => item.RequestId == result.RequestId && item.ServiceName == result.ServiceName, cancellationToken))
            return;
        if (!await context.DataSubjectRequests.AnyAsync(
            request => request.Id == result.RequestId && request.RequesterUserId == result.SubjectUserId, cancellationToken))
            throw new InvalidOperationException("Assessment result does not match an existing privacy request.");
        context.DataSubjectRequestAssessmentResults.Add(result);
        await context.SaveChangesAsync(cancellationToken);
    }
}
