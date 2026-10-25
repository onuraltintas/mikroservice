using EduPlatform.Shared.Contracts.Events.Privacy;
using Identity.Domain.Entities;
using MassTransit;

namespace Identity.Application.DataSubjectRequests;

public interface IDataSubjectRequestAssessmentRepository
{
    Task RecordAsync(DataSubjectRequestAssessmentResult result, CancellationToken cancellationToken);
    Task<IReadOnlyList<DataSubjectRequestAssessmentResult>> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken);
}

public sealed class PersonalDataErasureAssessmentCompletedConsumer(IDataSubjectRequestAssessmentRepository repository)
    : IConsumer<PersonalDataErasureAssessmentCompletedV1>
{
    public Task Consume(ConsumeContext<PersonalDataErasureAssessmentCompletedV1> context) =>
        repository.RecordAsync(DataSubjectRequestAssessmentResult.Record(context.Message), context.CancellationToken);
}
