using System.Data.Common;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Coaching.Curriculum.IntegrationTests;

// Teacher list pages show counts only. They must not read every student row, every submission
// attachment or every exam result (with its JSON analysis columns) to produce two numbers.
public sealed class TeacherListSummaryPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AssignmentAndExamListsCountInSqlWithoutLoadingChildRows()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var teacher = Guid.NewGuid(); var students = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var assignment = Assignment.Create(teacher, "Paragraf ödevi", new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc));
        foreach (var student in students) assignment.AssignToStudent(student);
        assignment.SubmitAssignment(students[0], "Bitti");
        var other = Assignment.Create(Guid.NewGuid(), "Başka öğretmen", new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc));
        var exam = Exam.Create(teacher, "Deneme 1", ExamType.Mock, new DateTime(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc), 100m);
        exam.AddResult(ExamResult.Create(exam.Id, students[0], 80m));
        exam.AddResult(ExamResult.Create(exam.Id, students[1], 70m));
        pg.Db.AddRange(assignment, other, exam); await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();

        var sql = new SqlLog();
        await using var reader = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(pg.Db.Database.GetConnectionString()).AddInterceptors(sql).Options);
        var assignments = await ((IAssignmentRepository)new AssignmentRepository(reader)).GetTeacherSummariesAsync(teacher, 1, 20);
        var exams = await ((IExamRepository)new ExamRepository(reader)).GetTeacherSummariesAsync(teacher, 1, 20);

        var row = Assert.Single(assignments.Items);
        Assert.Equal((assignment.Id, 3, 1), (row.Assignment.Id, row.TotalStudents, row.SubmittedCount));
        Assert.Equal(1, assignments.TotalCount);
        var examRow = Assert.Single(exams.Items);
        Assert.Equal((exam.Id, 2), (examRow.Exam.Id, examRow.ResultCount));
        Assert.Empty(reader.ChangeTracker.Entries());
        Assert.DoesNotContain(sql.Commands, text => text.Contains("submission_attachments"));
        Assert.DoesNotContain(sql.Commands, text => text.Contains("topic_analysis") || text.Contains("subject_scores"));
    }

    private sealed class SqlLog : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default) { Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken ct = default) { Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
