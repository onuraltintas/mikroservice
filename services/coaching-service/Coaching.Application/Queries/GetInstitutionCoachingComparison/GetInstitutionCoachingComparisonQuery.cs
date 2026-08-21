using Coaching.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetInstitutionCoachingComparison;

public sealed record GetInstitutionCoachingComparisonQuery(
    Guid InstitutionId,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int? GradeLevel = null)
    : IRequest<InstitutionCoachingComparisonDto>;

public sealed class GetInstitutionCoachingComparisonQueryValidator
    : AbstractValidator<GetInstitutionCoachingComparisonQuery>
{
    public GetInstitutionCoachingComparisonQueryValidator()
    {
        RuleFor(query => query.InstitutionId).NotEmpty();
        RuleFor(query => query.GradeLevel)
            .InclusiveBetween(1, 12)
            .When(query => query.GradeLevel.HasValue);
        RuleFor(query => query.FromDate)
            .LessThan(query => query.ToDate)
            .When(query => query.FromDate.HasValue && query.ToDate.HasValue);
        RuleFor(query => query)
            .Must(query => !query.FromDate.HasValue
                || !query.ToDate.HasValue
                || query.ToDate.Value - query.FromDate.Value <= TimeSpan.FromDays(366))
            .WithName("DateRange")
            .WithMessage("Rapor aralığı en fazla 366 gün olabilir.");
    }
}

public sealed record InstitutionCoachingComparisonDto(
    Guid InstitutionId,
    int? GradeLevel,
    DateTime FromDate,
    DateTime ToDate,
    int StudentCount,
    int AssignmentCount,
    int AssignedAssignmentCount,
    int SubmittedAssignmentCount,
    int GradedAssignmentCount,
    decimal? AverageAssignmentPercentage,
    int ExamCount,
    int ExamResultCount,
    decimal? AverageExamPercentage,
    int SessionCount,
    int AttendanceRecordedCount,
    int AttendedSessionCount,
    decimal? AttendancePercentage,
    int GoalCount,
    int CompletedGoalCount,
    int AverageGoalProgress);

public sealed class GetInstitutionCoachingComparisonQueryHandler(
    ICoachingComparativeReportRepository repository,
    ICoachingIdentityReportClient identityReportClient,
    Coaching.Application.Authorization.ICoachingAccessPolicy accessPolicy)
    : IRequestHandler<GetInstitutionCoachingComparisonQuery, InstitutionCoachingComparisonDto>
{
    public async Task<InstitutionCoachingComparisonDto> Handle(
        GetInstitutionCoachingComparisonQuery request,
        CancellationToken cancellationToken)
    {
        if (!accessPolicy.IsSystemAdministrator)
        {
            throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException(
                "Authorization.Forbidden",
                "Karşılaştırmalı coaching raporunu yalnızca sistem yöneticisi görüntüleyebilir.");
        }

        var viewerUserId = accessPolicy.CurrentUserId
            ?? throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException(
                "Authorization.Forbidden",
                "Oturum açılmış kullanıcı bulunamadı.");
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        var studentIds = await identityReportClient.GetActiveStudentIdsAsync(
            viewerUserId,
            request.InstitutionId,
            request.GradeLevel,
            cancellationToken);

        return await repository.GetInstitutionComparisonAsync(
            request.InstitutionId,
            studentIds,
            request.GradeLevel,
            fromDate,
            toDate,
            cancellationToken);
    }
}
