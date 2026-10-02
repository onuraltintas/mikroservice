using Coaching.API.Controllers;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudentExamApiTests
{
    [Fact]
    public async Task StudentApi_RequiresStudentAndReturnsCreatedLocationAndMissingResult()
    {
        Assert.Equal("Student", Assert.Single(typeof(StudentExamsController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
        var api = new StudentExamsController(new Stub());
        Assert.IsType<CreatedAtActionResult>(await api.Create(Input()));
        Assert.IsType<NotFoundObjectResult>(await api.Get(Guid.NewGuid()));
        Assert.IsType<NoContentResult>(await api.Delete(Guid.NewGuid(), 1));
    }

    [Theory]
    [InlineData("invalid", 400)]
    [InlineData("missing", 404)]
    [InlineData("conflict", 409)]
    [InlineData("forbidden", 403)]
    public async Task StudentApi_MapsExpectedFailures(string kind, int status)
    {
        Exception error = kind switch { "missing" => new KeyNotFoundException(), "conflict" => new BusinessRuleException("StudyPlanning.Conflict", "Changed"),
            "forbidden" => new BusinessRuleException("Authorization.Forbidden", "Denied"), _ => new ArgumentException() };
        var api = new StudentExamsController(new Stub { Failure = error });
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await api.Create(Input())).StatusCode);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await api.Replace(Guid.NewGuid(), new ReplaceStudentExamRequest(Input(), 1))).StatusCode);
    }

    private static StudentExamInput Input() => new("Title", ExamType.Mock, new(2026, 10, 1), 0, 100, 0, 0, 0, []);
    private sealed class Stub : IStudentExamService
    {
        public Exception? Failure;
        private Task<StudentExamView> Result() => Failure is null
            ? Task.FromResult(new StudentExamView(Guid.NewGuid(), 1, "Title", ExamType.Mock, new(2026, 10, 1), 0, 100, 0, 0, 0, "StudentReported", []))
            : Task.FromException<StudentExamView>(Failure);
        public Task<StudentExamView> CreateAsync(StudentExamInput request, CancellationToken cancellationToken = default) => Result();
        public Task<StudentExamView> ReplaceAsync(Guid id, ReplaceStudentExamRequest request, CancellationToken cancellationToken = default) => Result();
        public Task<StudentExamView?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<StudentExamView?>(null);
        public Task<TargetSearchPage<StudentExamView>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(new TargetSearchPage<StudentExamView>([], 0, pageNumber, pageSize));
        public Task DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
