using Coaching.Application.Authorization;
using Coaching.Application.Commands.DeleteAssignment;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Coaching.Application.Attachments;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAssignmentAttachmentDeletionTests
{
    [Fact]
    public async Task DeletingAssignment_RemovesItsPhysicalAttachment()
    {
        var root = Path.Combine(Path.GetTempPath(), "coaching-delete-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var context = new CoachingDbContext(
                new DbContextOptionsBuilder<CoachingDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                    .Options);
            var teacherId = Guid.NewGuid();
            var assignment = Assignment.Create(teacherId, "Test assignment", DateTime.UtcNow.AddDays(1));
            assignment.AssignToStudent(Guid.NewGuid());
            const string key = "assignments/test/photo.jpg";
            assignment.AssignedStudents.Single().AddSubmissionAttachment(
                key, "photo.jpg", "image/jpeg", 3, new string('A', 64));
            context.Assignments.Add(assignment);
            await context.SaveChangesAsync();

            var path = Path.Combine(root, "assignments", "test", "photo.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, [1, 2, 3]);

            var handler = new DeleteAssignmentCommandHandler(
                new AssignmentRepository(context),
                new UnitOfWork(context),
                new TeacherAccessPolicy(teacherId),
                new LocalAssignmentAttachmentStorage(Options.Create(new AssignmentAttachmentOptions { RootPath = root })));

            await handler.Handle(new DeleteAssignmentCommand(assignment.Id), CancellationToken.None);

            File.Exists(path).Should().BeFalse();
            (await context.Assignments.CountAsync()).Should().Be(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TeacherAccessPolicy(Guid teacherId) : ICoachingAccessPolicy
    {
        public Guid? CurrentUserId => teacherId;
        public bool IsSystemAdministrator => false;
        public bool IsInstitutionAdministrator => false;
        public bool IsCurrentTeacher(Guid id) => id == teacherId;
        public bool IsCurrentStudent(Guid id) => false;
        public Guid RequireCurrentTeacher() => teacherId;
        public void RequireTeacher(Guid id)
        {
            if (id != teacherId) throw new UnauthorizedAccessException();
        }
        public void RequireStudent(Guid id) => throw new NotSupportedException();
        public void RequireTeacherOrStudent(Guid teacher, Guid student) => throw new NotSupportedException();
        public void RequireTeacherOrAssignedStudent(Guid teacher, IEnumerable<Guid> students) => throw new NotSupportedException();
    }
}
