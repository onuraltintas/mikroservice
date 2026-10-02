using Coaching.API.Controllers;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudyReportApiTests
{
    [Fact]
    public async Task Report_IsStudentOnlyAndReturnsPeriod()
    {
        Assert.Equal("Student", Assert.Single(typeof(StudentStudyReportsController).GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()).Roles);
        Assert.IsType<OkObjectResult>(await new StudentStudyReportsController(new Stub()).Get(new(2026, 10, 1), new(2026, 10, 2)));
    }

    [Theory]
    [InlineData(false, 400)]
    [InlineData(true, 403)]
    public async Task Report_RejectsInvalidPeriodAndWrongRole(bool forbidden, int status)
    {
        var api = new StudentStudyReportsController(new Stub { Error = forbidden
            ? new BusinessRuleException("Authorization.Forbidden", "Denied") : new ArgumentException("Internal detail") });
        var response = Assert.IsAssignableFrom<ObjectResult>(await api.Get(new(2026, 10, 1), new(2026, 10, 2)));
        Assert.Equal(status, response.StatusCode);
        Assert.DoesNotContain("Internal detail", System.Text.Json.JsonSerializer.Serialize(response.Value));
    }

    private sealed class Stub : IStudentStudyReportService
    {
        public Exception? Error;
        public Task<StudentStudyReport> GetAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
            => Error is null ? Task.FromResult(StudyReportCalculator.Calculate(fromDate, toDate, [])) : Task.FromException<StudentStudyReport>(Error);
    }
}
