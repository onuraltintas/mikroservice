using CourseManagement.Domain;
using EduPlatform.Shared.Contracts.Events.Privacy;

namespace CourseManagement.Infrastructure.Privacy;

public interface ICourseManagementAccountErasureService
{
    Task<CourseManagementErasureAssessment> AssessAsync(
        PersonalDataErasureAssessmentRequestedV1 message,
        CancellationToken cancellationToken);

    Task<CourseManagementErasureExecution> ExecuteAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken);
}
