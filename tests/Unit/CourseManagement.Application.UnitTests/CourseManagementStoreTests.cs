using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Application.UnitTests;

public sealed class CourseManagementStoreTests
{
    private static CourseManagementDbContext CreateDatabase() => new(new DbContextOptionsBuilder<CourseManagementDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task ProfilesCanBeCreatedUpdatedAndReadOnlyByTheirIdentity()
    {
        await using var db = CreateDatabase();
        var store = new CourseManagementStore(db);
        var userId = Guid.NewGuid();
        var profile = CourseUserProfile.Create(userId, "Öğretmen");
        await store.SaveProfileAsync(profile, CancellationToken.None);
        db.ChangeTracker.Clear();
        var loaded = await store.GetProfileAsync(userId, CancellationToken.None);
        Assert.NotNull(loaded);
        loaded.UpdateDisplayName("Yeni ad");
        await store.SaveProfileAsync(loaded, CancellationToken.None);
        db.ChangeTracker.Clear();
        Assert.Equal("Yeni ad", (await store.GetProfileAsync(userId, CancellationToken.None))!.DisplayName);
        Assert.Null(await store.GetProfileAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task OwnedWorkspaceQueryExcludesOtherOwners()
    {
        await using var db = CreateDatabase();
        var owner = Guid.NewGuid();
        var own = Workspace.Create(Guid.NewGuid(), owner, "Kurum", WorkspaceType.Institution);
        db.Workspaces.AddRange(own,
            Workspace.Create(Guid.NewGuid(), Guid.NewGuid(), "Başka kurum", WorkspaceType.Institution));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var result = await new CourseManagementStore(db).GetOwnedWorkspacesAsync(owner, CancellationToken.None);
        Assert.Equal(own.Id, Assert.Single(result).Id);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
