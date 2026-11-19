using System.Reflection;
using CourseManagement.API;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.IntegrationTests.Fixtures;

namespace CourseManagement.IntegrationTests;

/// <summary>maxkampus.com registration: Identity asks, Course Management opens the named workspace once.</summary>
public sealed class CourseManagementWorkspaceRequestedConsumerTests
{
    [Fact]
    public async Task A_registration_opens_one_workspace_with_its_owner_and_a_repeat_opens_nothing()
    {
        var postgres = new PostgresFixture();
        await postgres.InitializeAsync();
        try
        {
            var options = new DbContextOptionsBuilder<CourseManagementDbContext>().UseNpgsql(postgres.ConnectionString).Options;
            await using (var setup = new CourseManagementDbContext(options)) await setup.Database.MigrateAsync();
            var userId = Guid.NewGuid();
            var request = new CourseManagementWorkspaceRequestedV1(userId, Guid.NewGuid(), " TEST Dershanesi ", "institution", "TEST Kişi", DateTimeOffset.UtcNow);

            await Consume(options, request);
            await Consume(options, request);
            // A second registration of the same owner (another id) opens nothing either.
            await Consume(options, request with { WorkspaceId = Guid.NewGuid(), WorkspaceName = "Başka" });
            await Consume(options, request with { UserId = Guid.NewGuid(), WorkspaceId = Guid.NewGuid(), Kind = "parent" });

            await using var db = new CourseManagementDbContext(options);
            var workspace = Assert.Single(await db.Workspaces.ToListAsync());
            Assert.Equal((request.WorkspaceId, "TEST Dershanesi", WorkspaceType.Institution, userId),
                (workspace.Id, workspace.Name, workspace.Type, workspace.OwnerUserId));
            var member = Assert.Single(await db.Memberships.ToListAsync());
            Assert.Equal((userId, WorkspaceRole.Owner), (member.UserId, member.Role));
            Assert.Equal("TEST Kişi", (await db.UserProfiles.SingleAsync()).DisplayName);
        }
        finally
        {
            await postgres.DisposeAsync();
        }
    }

    private static async Task Consume(DbContextOptions<CourseManagementDbContext> options, CourseManagementWorkspaceRequestedV1 message)
    {
        await using var db = new CourseManagementDbContext(options);
        var context = DispatchProxy.Create<ConsumeContext<CourseManagementWorkspaceRequestedV1>, MessageContext>();
        ((MessageContext)(object)context).Message = message;
        await new CourseManagementWorkspaceRequestedConsumer(db, NullLogger<CourseManagementWorkspaceRequestedConsumer>.Instance).Consume(context);
    }

    public class MessageContext : DispatchProxy
    {
        public object Message { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "get_Message" => Message,
            "get_CancellationToken" => CancellationToken.None,
            _ => throw new NotSupportedException(method?.Name)
        };
    }
}
