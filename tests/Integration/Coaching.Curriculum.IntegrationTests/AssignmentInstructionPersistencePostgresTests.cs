using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class AssignmentInstructionPersistencePostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigratedSchemaLoadsGradedAssignmentAndRoundTripsTeacherAttachmentsWithoutChangingSubmission()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        Assert.False(f.Db.Database.HasPendingModelChanges());
        var student = Guid.NewGuid();
        var assignment = Assignment.Create(Guid.NewGuid(), "Bildirim ödevi", DateTime.UtcNow.AddDays(1));
        assignment.AssignToStudent(student);
        assignment.SubmitAssignment(student, "Teslim notu korunacak");
        assignment.GradeAssignment(student, 95, "Değerlendirme korunacak");
        f.Db.Add(assignment); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var repository = new AssignmentRepository(f.Db);
        var loaded = Assert.IsType<Assignment>(await repository.GetByIdAsync(assignment.Id));
        var submission = Assert.Single(loaded.AssignedStudents);
        var submittedAt = submission.SubmittedAt;
        Assert.Empty(loaded.InstructionAttachments);
        loaded.AddInstructionAttachment("test/material.pdf", "çalışma.pdf", "application/pdf", 100, new string('a',64), DateTime.UtcNow);
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        loaded = Assert.IsType<Assignment>(await repository.GetByIdAsync(assignment.Id));
        Assert.Equal("çalışma.pdf", Assert.Single(loaded.InstructionAttachments).OriginalFileName);
        submission = Assert.Single(loaded.AssignedStudents);
        Assert.Equal(student, submission.StudentId); Assert.Equal(95m, submission.Score);
        Assert.Equal(StudentAssignmentStatus.Graded, submission.Status);
        Assert.Equal("Teslim notu korunacak", submission.StudentNote);
        Assert.Equal("Değerlendirme korunacak", submission.TeacherFeedback);
        Assert.Equal(submittedAt, submission.SubmittedAt);
    }
}
