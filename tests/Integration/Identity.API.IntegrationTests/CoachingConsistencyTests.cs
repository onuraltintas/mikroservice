using Coaching.Application.Commands.CreateSession;
using Coaching.Domain;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;

namespace Identity.API.IntegrationTests;

// Roadmap D2: consistency fixes.
public sealed class CoachingConsistencyTests
{
    [Fact]
    public void GradedWorkCannotBeSubmittedAgain()
    {
        var student = Guid.NewGuid();
        var assignment = Assignment.Create(Guid.NewGuid(), "Paragraf", DateTime.UtcNow.AddDays(2)); assignment.AssignToStudents([student]);
        assignment.SubmitAssignment(student, "ilk"); assignment.SubmitAssignment(student, "düzeltme");
        assignment.GradeAssignment(student, 90);
        var error = Assert.Throws<BusinessRuleException>(() => assignment.SubmitAssignment(student, "tekrar"));
        Assert.Equal("Assignment.AlreadyGraded", error.Code);
        var row = assignment.AssignedStudents.Single();
        Assert.Equal((StudentAssignmentStatus.Graded, "düzeltme"), (row.Status, row.StudentNote));
    }

    [Fact]
    public void ExamDatesWithoutOffsetAreTurkeyTimeAndStoredAsUtc()
    {
        var local = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Unspecified);
        var exam = Exam.Create(Guid.NewGuid(), "LGS deneme", ExamType.LGS, local, 500);
        Assert.Equal((DateTimeKind.Utc, new DateTime(2026, 11, 7, 7, 0, 0)), (exam.ExamDate.Kind, exam.ExamDate));
        exam.UpdateDetails(examDate: new DateTime(2026, 11, 8, 9, 30, 0, DateTimeKind.Unspecified));
        Assert.Equal(new DateTime(2026, 11, 8, 6, 30, 0, DateTimeKind.Utc), exam.ExamDate);
        Assert.Equal(DateTimeKind.Utc, exam.ExamDate.Kind);
    }

    [Fact]
    public void SessionStartInTurkeyTimeIsComparedWithTheCurrentInstant()
    {
        var validator = new CreateSessionCommandValidator();
        var turkeyNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CoachingTime.Turkey);
        CreateSessionCommand At(DateTime start) => new(Guid.NewGuid(), Guid.NewGuid(), DateTime.SpecifyKind(start, DateTimeKind.Unspecified), 45,
            "Görüşme", null, SessionType.OneOnOne, "session-consistency-key-1");
        Assert.False(validator.Validate(At(turkeyNow.AddHours(-1))).IsValid);
        Assert.True(validator.Validate(At(turkeyNow.AddHours(1))).IsValid);
    }
}
