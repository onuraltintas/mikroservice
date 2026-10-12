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

    [Fact]
    public void Revised_submission_clears_the_previous_review()
    {
        var submittedAt = DateTimeOffset.UtcNow;
        var submission = CourseAssignmentSubmission.Create(Guid.NewGuid(), WorkspaceId, Guid.NewGuid(),
            Guid.NewGuid(), "İlk yanıt", submittedAt);
        submission.Review(95, "Doğru çözüm.", TeacherId, submittedAt.AddHours(1));

        submission.UpdateSubmission("Düzeltilmiş yanıt", submittedAt.AddHours(2));

        Assert.Equal("Düzeltilmiş yanıt", submission.Text);
        Assert.Null(submission.Score);
        Assert.Null(submission.Feedback);
        Assert.Null(submission.ReviewedByUserId);
        Assert.Null(submission.ReviewedAt);
    }

    [Fact]
    public void Review_rejects_scores_outside_the_scale_and_empty_reviews()
    {
        var now = DateTimeOffset.UtcNow;
        var submission = CourseAssignmentSubmission.Create(Guid.NewGuid(), WorkspaceId, Guid.NewGuid(),
            Guid.NewGuid(), "Yanıt", now);

        Assert.Throws<ArgumentOutOfRangeException>(() => submission.Review(101, "Geri bildirim", TeacherId, now));
        Assert.Throws<ArgumentException>(() => submission.Review(null, " ", TeacherId, now));
    }

    [Fact]
    public void Assignment_attachment_expires_180_days_after_the_assignment_due_date()
    {
        var dueAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var attachment = CourseAssignmentAttachment.Create(Guid.NewGuid(), WorkspaceId, Guid.NewGuid(),
            null, null, CourseAssignmentAttachmentPurpose.TeacherMaterial, "worksheet.pdf", "application/pdf",
            128, new string('A', 64), "workspace/assignment/file.pdf", TeacherId, dueAt.AddDays(-1), dueAt);

        Assert.Equal("worksheet.pdf", attachment.FileName);
        Assert.Equal(dueAt.AddDays(180), attachment.ExpiresAt);
    }

    [Fact]
    public void Assignment_attachment_rejects_oversized_files_and_invalid_purpose_links()
    {
        var dueAt = DateTimeOffset.UtcNow.AddDays(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => CourseAssignmentAttachment.Create(Guid.NewGuid(),
            WorkspaceId, Guid.NewGuid(), null, null, CourseAssignmentAttachmentPurpose.TeacherMaterial,
            "worksheet.pdf", "application/pdf", 10 * 1024 * 1024 + 1, new string('A', 64),
            "workspace/assignment/file.pdf", TeacherId, DateTimeOffset.UtcNow, dueAt));
        Assert.Throws<ArgumentException>(() => CourseAssignmentAttachment.Create(Guid.NewGuid(),
            WorkspaceId, Guid.NewGuid(), null, null, CourseAssignmentAttachmentPurpose.StudentSubmission,
            "answer.pdf", "application/pdf", 128, new string('A', 64), "workspace/assignment/file.pdf",
            TeacherId, DateTimeOffset.UtcNow, dueAt));
    }
}
