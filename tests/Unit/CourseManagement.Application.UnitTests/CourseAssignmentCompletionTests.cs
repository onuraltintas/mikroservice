using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

public sealed class CourseAssignmentCompletionTests
{
    private static readonly Guid TeacherId = Guid.Parse("f9dcfd6b-7932-47e8-9f51-7ca0738fa983");
    private static readonly DateTimeOffset MarkedAt = new(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(3));

    private static CourseAssignmentRecipient NewRecipient() =>
        CourseAssignmentRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void A_new_recipient_is_not_marked()
    {
        var recipient = NewRecipient();

        Assert.Equal(CourseAssignmentCompletion.NotMarked, recipient.Completion);
        Assert.Null(recipient.CompletionMarkedAt);
    }

    [Fact]
    public void Marking_records_the_teacher_time_and_trimmed_note()
    {
        var recipient = NewRecipient();

        Assert.True(recipient.MarkCompletion(CourseAssignmentCompletion.Partial, "  ilk iki sayfa  ", TeacherId, MarkedAt));

        Assert.Equal(CourseAssignmentCompletion.Partial, recipient.Completion);
        Assert.Equal("ilk iki sayfa", recipient.CompletionNote);
        Assert.Equal(TeacherId, recipient.CompletionMarkedByUserId);
        Assert.Equal(MarkedAt.ToUniversalTime(), recipient.CompletionMarkedAt);
    }

    [Fact]
    public void The_same_mark_again_is_not_a_change()
    {
        var recipient = NewRecipient();
        recipient.MarkCompletion(CourseAssignmentCompletion.NotDone, null, TeacherId, MarkedAt);

        Assert.False(recipient.MarkCompletion(CourseAssignmentCompletion.NotDone, " ", TeacherId, MarkedAt.AddHours(1)));
        Assert.Equal(MarkedAt.ToUniversalTime(), recipient.CompletionMarkedAt);
    }

    [Fact]
    public void Clearing_a_mark_removes_note_teacher_and_time()
    {
        var recipient = NewRecipient();
        recipient.MarkCompletion(CourseAssignmentCompletion.Done, "tamam", TeacherId, MarkedAt);

        Assert.True(recipient.MarkCompletion(CourseAssignmentCompletion.NotMarked, "yok sayılır", TeacherId, MarkedAt));

        Assert.Equal(CourseAssignmentCompletion.NotMarked, recipient.Completion);
        Assert.Null(recipient.CompletionNote);
        Assert.Null(recipient.CompletionMarkedByUserId);
        Assert.Null(recipient.CompletionMarkedAt);
    }

    [Fact]
    public void Invalid_marks_are_rejected()
    {
        var recipient = NewRecipient();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            recipient.MarkCompletion((CourseAssignmentCompletion)9, null, TeacherId, MarkedAt));
        Assert.Throws<ArgumentException>(() =>
            recipient.MarkCompletion(CourseAssignmentCompletion.Done, new string('a', 501), TeacherId, MarkedAt));
        Assert.Throws<ArgumentException>(() =>
            recipient.MarkCompletion(CourseAssignmentCompletion.Done, null, Guid.Empty, MarkedAt));
    }
}
