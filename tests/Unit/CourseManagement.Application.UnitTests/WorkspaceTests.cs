using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

public sealed class WorkspaceTests
{
    [Theory]
    [InlineData(WorkspaceType.Institution)]
    [InlineData(WorkspaceType.IndependentTeacher)]
    public void WorkspaceKeepsLocalIdentityAndOwner(WorkspaceType type)
    {
        var owner = Guid.NewGuid();
        var workspace = Workspace.Create(Guid.NewGuid(), owner, "  Matematik  ", type);
        Assert.Equal("Matematik", workspace.Name);
        Assert.Equal(owner, workspace.OwnerUserId);
        Assert.Equal(type, workspace.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyWorkspaceNameIsRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => Workspace.Create(Guid.NewGuid(), Guid.NewGuid(), name, WorkspaceType.Institution));
    }

    [Fact]
    public void InvalidWorkspaceIdentifiersAndTypeAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Workspace.Create(Guid.Empty, Guid.NewGuid(), "Kurs", WorkspaceType.Institution));
        Assert.Throws<ArgumentException>(() => Workspace.Create(Guid.NewGuid(), Guid.Empty, "Kurs", WorkspaceType.Institution));
        Assert.Throws<ArgumentException>(() => Workspace.Create(Guid.NewGuid(), Guid.NewGuid(), "Kurs", (WorkspaceType)99));
        Assert.Throws<ArgumentException>(() => Workspace.Create(Guid.NewGuid(), Guid.NewGuid(), new string('a', 201), WorkspaceType.Institution));
    }

    [Fact]
    public void ProfileChangesRemainLocalToCourseProduct()
    {
        var userId = Guid.NewGuid();
        var profile = CourseUserProfile.Create(userId, "  Öğretmen  ");
        profile.UpdateDisplayName(" Yeni ad ");
        Assert.Equal(userId, profile.UserId);
        Assert.Equal("Yeni ad", profile.DisplayName);
        Assert.Throws<ArgumentException>(() => profile.UpdateDisplayName(" "));
        Assert.Throws<ArgumentException>(() => CourseUserProfile.Create(Guid.Empty, "Öğrenci"));
    }
}
