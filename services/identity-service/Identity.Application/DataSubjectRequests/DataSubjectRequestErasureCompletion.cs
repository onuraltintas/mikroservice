using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Kernel.Exceptions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;

namespace Identity.Application.DataSubjectRequests;

public interface IDataSubjectRequestExecutionRepository
{
    Task<bool> ExistsAsync(Guid requestId, string serviceName, CancellationToken cancellationToken);
    Task AddAsync(DataSubjectRequestExecutionResult result, CancellationToken cancellationToken);
    Task<IReadOnlyList<DataSubjectRequestExecutionResult>> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken);
}

public interface IIdentityAccountErasureService
{
    Task ExecuteAsync(Guid subjectUserId, CancellationToken cancellationToken);
}

public sealed class DataSubjectRequestErasureCompletionHandler(
    IDataSubjectRequestRepository requestRepository,
    IDataSubjectRequestExecutionRepository executionRepository,
    IIdentityAccountErasureService identityAccountErasureService)
{
    public async Task HandleAsync(
        PersonalDataErasureExecutionCompletedV1 message,
        CancellationToken cancellationToken)
    {
        if (await executionRepository.ExistsAsync(
                message.RequestId, message.ServiceName, cancellationToken))
            return;

        var request = await requestRepository.GetByIdAsync(message.RequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(DataSubjectRequest), message.RequestId);
        if (request.Status != DataSubjectRequestStatus.Processing)
            throw new InvalidOperationException("The erasure request is not processing.");

        var requiredServices = DataErasureAssessmentScopePolicy.RequiredServices(request.Scope);
        if (!requiredServices.Contains(message.ServiceName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("The completion service is outside the request scope.");

        var existing = await executionRepository.GetByRequestIdAsync(
            message.RequestId, cancellationToken);
        var result = DataSubjectRequestExecutionResult.Record(message);
        await executionRepository.AddAsync(result, cancellationToken);

        var completedServices = existing
            .Select(item => item.ServiceName)
            .Append(result.ServiceName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requiredServices.All(completedServices.Contains))
        {
            if (request.Scope == PersonalDataScope.Account)
                await identityAccountErasureService.ExecuteAsync(
                    request.RequesterUserId, cancellationToken);
            request.Complete(message.CompletedAt);
        }

        await requestRepository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DataSubjectRequestErasureBlockedHandler(IDataSubjectRequestRepository requestRepository)
{
    public async Task HandleAsync(PersonalDataErasureExecutionBlockedV1 message, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(message.RequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(DataSubjectRequest), message.RequestId);
        // A redelivery, or a request an operator has already moved on: nothing to do.
        if (request.Status != DataSubjectRequestStatus.Processing)
            return;

        var requiredServices = DataErasureAssessmentScopePolicy.RequiredServices(request.Scope);
        if (!requiredServices.Contains(message.ServiceName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("The blocking service is outside the request scope.");

        request.Fail($"{message.ServiceName} silmeyi durdurdu: {ReasonText(message.ReasonCode)}", message.BlockedAt);
        await requestRepository.SaveChangesAsync(cancellationToken);
    }

    private static string ReasonText(string reasonCode) => reasonCode switch
    {
        "LegalHold" => "kişi için etkin bir hukuki saklama kaydı var.",
        "AssessmentMissing" => "bu talep için tamamlanmış bir değerlendirme yok.",
        "AssessmentDenied" => "değerlendirme silmeye izin vermiyor.",
        _ => reasonCode.Length > 200 ? reasonCode[..200] : reasonCode
    };
}

public sealed class PersonalDataErasureExecutionBlockedConsumer(DataSubjectRequestErasureBlockedHandler handler)
    : IConsumer<PersonalDataErasureExecutionBlockedV1>
{
    public Task Consume(ConsumeContext<PersonalDataErasureExecutionBlockedV1> context) =>
        handler.HandleAsync(context.Message, context.CancellationToken);
}

public sealed class PersonalDataErasureExecutionCompletedConsumer(
    DataSubjectRequestErasureCompletionHandler handler)
    : IConsumer<PersonalDataErasureExecutionCompletedV1>
{
    public Task Consume(ConsumeContext<PersonalDataErasureExecutionCompletedV1> context) =>
        handler.HandleAsync(context.Message, context.CancellationToken);
}
