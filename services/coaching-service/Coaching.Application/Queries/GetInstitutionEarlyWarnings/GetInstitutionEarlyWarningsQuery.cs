using Coaching.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetInstitutionEarlyWarnings;

public sealed record GetInstitutionEarlyWarningsQuery(
    Guid InstitutionId,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int? GradeLevel = null,
    int PageNumber = 1,
    int PageSize = 25)
    : IRequest<InstitutionEarlyWarningReportDto>;

public sealed class GetInstitutionEarlyWarningsQueryValidator
    : AbstractValidator<GetInstitutionEarlyWarningsQuery>
{
    public GetInstitutionEarlyWarningsQueryValidator()
    {
        RuleFor(query => query.InstitutionId).NotEmpty();
        RuleFor(query => query.GradeLevel)
            .InclusiveBetween(1, 12)
            .When(query => query.GradeLevel.HasValue);
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
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

public sealed record InstitutionEarlyWarningReportDto(
    Guid InstitutionId,
    int? GradeLevel,
    DateTime FromDate,
    DateTime ToDate,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<StudentEarlyWarningDto> Items);

public sealed record StudentEarlyWarningDto(
    Guid StudentId,
    EarlyWarningRiskLevel RiskLevel,
    int RiskScore,
    IReadOnlyList<string> ReasonCodes,
    int AssignmentCount,
    int SubmittedAssignmentCount,
    decimal? AverageAssignmentPercentage,
    int RecordedAttendanceCount,
    int AttendedSessionCount,
    decimal? AttendancePercentage,
    int GoalCount,
    int AverageGoalProgress,
    DateTime? LastActivityAt);

public enum EarlyWarningRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public static class EarlyWarningReasonCodes
{
    public const string LowAssignmentSubmission = "low_assignment_submission";
    public const string LowAssignmentPerformance = "low_assignment_performance";
    public const string LowAttendance = "low_attendance";
    public const string LowGoalProgress = "low_goal_progress";
    public const string NoRecentActivity = "no_recent_activity";
}

public sealed class GetInstitutionEarlyWarningsQueryHandler(
    ICoachingEarlyWarningRepository repository,
    ICoachingIdentityReportClient identityReportClient,
    Coaching.Application.Authorization.ICoachingAccessPolicy accessPolicy)
    : IRequestHandler<GetInstitutionEarlyWarningsQuery, InstitutionEarlyWarningReportDto>
{
    private const int NoRecentActivityDays = 14;

    public async Task<InstitutionEarlyWarningReportDto> Handle(
        GetInstitutionEarlyWarningsQuery request,
        CancellationToken cancellationToken)
    {
        if (!accessPolicy.IsSystemAdministrator && !accessPolicy.IsInstitutionAdministrator)
        {
            throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException(
                "Authorization.Forbidden",
                "Erken uyarı raporuna erişim yetkiniz yok.");
        }

        var viewerUserId = accessPolicy.CurrentUserId
            ?? throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException(
                "Authorization.Forbidden",
                "Oturum açılmış kullanıcı bulunamadı.");
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        var page = await identityReportClient.GetActiveStudentPageAsync(
            viewerUserId,
            request.InstitutionId,
            request.GradeLevel,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var metrics = page.StudentUserIds.Count == 0
            ? Array.Empty<CoachingStudentEarlyWarningMetrics>()
            : await repository.GetStudentMetricsAsync(
                request.InstitutionId,
                page.StudentUserIds,
                request.GradeLevel,
                fromDate,
                toDate,
                cancellationToken);
        var metricsByStudent = metrics.ToDictionary(item => item.StudentId);

        var items = page.StudentUserIds
            .Select(studentId => metricsByStudent.TryGetValue(studentId, out var studentMetrics)
                ? CreateWarning(studentMetrics, toDate)
                : CreateWarning(
                    new CoachingStudentEarlyWarningMetrics(
                        studentId,
                        0,
                        0,
                        0,
                        null,
                        0,
                        0,
                        0,
                        0,
                        0,
                        null),
                    toDate))
            .OrderByDescending(item => item.RiskScore)
            .ThenBy(item => item.StudentId)
            .ToArray();

        var totalPages = page.TotalCount == 0
            ? 0
            : (page.TotalCount + request.PageSize - 1) / request.PageSize;

        return new InstitutionEarlyWarningReportDto(
            request.InstitutionId,
            request.GradeLevel,
            fromDate,
            toDate,
            request.PageNumber,
            request.PageSize,
            page.TotalCount,
            totalPages,
            items);
    }

    private static StudentEarlyWarningDto CreateWarning(
        CoachingStudentEarlyWarningMetrics metrics,
        DateTime toDate)
    {
        var score = 0;
        var reasonCodes = new List<string>();

        if (metrics.AssignmentCount >= 3
            && (decimal)metrics.SubmittedAssignmentCount / metrics.AssignmentCount < 0.60m)
        {
            score += 30;
            reasonCodes.Add(EarlyWarningReasonCodes.LowAssignmentSubmission);
        }

        if (metrics.GradedAssignmentCount >= 2
            && metrics.AverageAssignmentPercentage.HasValue
            && metrics.AverageAssignmentPercentage.Value < 60m)
        {
            score += 25;
            reasonCodes.Add(EarlyWarningReasonCodes.LowAssignmentPerformance);
        }

        if (metrics.RecordedAttendanceCount >= 3
            && (decimal)metrics.AttendedSessionCount / metrics.RecordedAttendanceCount < 0.75m)
        {
            score += 25;
            reasonCodes.Add(EarlyWarningReasonCodes.LowAttendance);
        }

        if (metrics.GoalCount > 0 && metrics.AverageGoalProgress < 50)
        {
            score += 15;
            reasonCodes.Add(EarlyWarningReasonCodes.LowGoalProgress);
        }

        if (!metrics.LastActivityAt.HasValue
            || metrics.LastActivityAt.Value < toDate.AddDays(-NoRecentActivityDays))
        {
            score += 10;
            reasonCodes.Add(EarlyWarningReasonCodes.NoRecentActivity);
        }

        score = Math.Min(score, 100);
        var level = score >= 50
            ? EarlyWarningRiskLevel.High
            : score >= 25
                ? EarlyWarningRiskLevel.Medium
                : EarlyWarningRiskLevel.Low;
        var attendancePercentage = metrics.RecordedAttendanceCount == 0
            ? (decimal?)null
            : Math.Round(
                (decimal)metrics.AttendedSessionCount / metrics.RecordedAttendanceCount * 100,
                2);

        return new StudentEarlyWarningDto(
            metrics.StudentId,
            level,
            score,
            reasonCodes,
            metrics.AssignmentCount,
            metrics.SubmittedAssignmentCount,
            metrics.AverageAssignmentPercentage,
            metrics.RecordedAttendanceCount,
            metrics.AttendedSessionCount,
            attendancePercentage,
            metrics.GoalCount,
            metrics.AverageGoalProgress,
            metrics.LastActivityAt);
    }
}
