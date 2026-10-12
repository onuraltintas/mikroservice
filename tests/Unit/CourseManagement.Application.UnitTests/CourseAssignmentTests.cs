using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

public sealed class CourseAssignmentTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("8ae5aaed-08f6-4d4a-a9a3-5cbeb6c9b125");
    private static readonly Guid CourseId = Guid.Parse("6daf47a6-78d8-40a2-bdb4-6de916478a8b");
    private static readonly Guid GroupId = Guid.Parse("0bdde331-54cc-450f-9ef7-ceb177102b62");
    private static readonly Guid TeacherId = Guid.Parse("f9dcfd6b-7932-47e8-9f51-7ca0738fa983");

    [Fact]
    public void Create_preserves_group_target_and_required_due_date()
    {
        var dueAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var assignment = CourseAssignment.Create(Guid.NewGuid(), WorkspaceId, CourseId, GroupId, null,
            TeacherId, "Kesirler", "Çözümleri gösterin", dueAt, dueAt.AddDays(-1));

        Assert.Equal(GroupId, assignment.GroupId);
        Assert.Null(assignment.StudentId);
        Assert.Equal(dueAt, assignment.DueAt);
    }

    [Fact]
    public void Create_requires_exactly_one_group_or_student_target()
    {
        var dueAt = DateTimeOffset.UtcNow.AddDays(1);

        Assert.Throws<ArgumentException>(() => CourseAssignment.Create(Guid.NewGuid(), WorkspaceId, CourseId,
            null, null, TeacherId, "Ödev", null, dueAt, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => CourseAssignment.Create(Guid.NewGuid(), WorkspaceId, CourseId,
            GroupId, Guid.NewGuid(), TeacherId, "Ödev", null, dueAt, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_requires_a_due_date()
    {
        Assert.Throws<ArgumentException>(() => CourseAssignment.Create(Guid.NewGuid(), WorkspaceId, CourseId,
            GroupId, null, TeacherId, "Ödev", null, default, DateTimeOffset.UtcNow));
    }
}
