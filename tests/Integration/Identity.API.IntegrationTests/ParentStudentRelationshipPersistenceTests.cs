using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class ParentStudentRelationshipPersistenceTests
{
    [Fact]
    public void Model_ShouldAllowOnlyOneNonRevokedRelationshipPerParentAndStudent()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(ParentStudentRelationship));

        var activeLinkIndex = entityType!.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
            [
                nameof(ParentStudentRelationship.ParentUserId),
                nameof(ParentStudentRelationship.StudentUserId)
            ]));

        activeLinkIndex.IsUnique.Should().BeTrue();
        activeLinkIndex.GetFilter().Should().Be("status <> 'Revoked'");
    }

    [Fact]
    public void Model_ShouldUseRestrictDeleteForBothIdentitySubjects()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(ParentStudentRelationship));

        entityType!.GetForeignKeys().Should().HaveCount(2);
        entityType.GetForeignKeys().Should().OnlyContain(
            foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
