namespace Coaching.Application.StudyPlanning;

public interface IStudentStudyReportService
{
    Task<StudentStudyReport> GetAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}
