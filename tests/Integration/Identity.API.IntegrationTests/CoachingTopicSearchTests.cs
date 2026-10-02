using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingTopicSearchTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Search_FiltersActiveHierarchyAndLiteralTextWithStablePaging()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var lesson = StudyCatalogLesson.Create("test", "l", "Mathematics", 8, "LGS");
            var unit = StudyCatalogUnit.Create("test", "u", lesson.Id, "Numbers", 0);
            var parent = StudyCatalogTopic.Create("test", "p", lesson.Id, unit.Id, "Parent", null, 0);
            var child = StudyCatalogTopic.Create("test", "c", lesson.Id, unit.Id, "100% Numbers", parent.Id, 1);
            var other = StudyCatalogTopic.Create("test", "o", lesson.Id, unit.Id, "Other", null, 2, 30);
            db.AddRange(lesson, unit, parent, child, other);
            foreach (var entry in db.ChangeTracker.Entries()) entry.Property("IsActive").CurrentValue = true;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var service = new CoachingTopicSearchService(db);
            var found = await service.SearchAsync("%", 8, "LGS", 1, 20);
            var item = Assert.Single(found.Items);
            Assert.Equal(child.Id, item.Id);
            Assert.Equal("Mathematics", item.LessonName);
            Assert.Equal("Numbers", item.UnitName);
            Assert.Null(item.EstimatedMinutes);
            Assert.Empty((await service.SearchAsync(null, 9, "LGS", 1, 20)).Items);
            Assert.Empty((await service.SearchAsync(null, 8, "AYT", 1, 20)).Items);
            Assert.Equal(3, (await service.SearchAsync(null, null, null, 1, 1)).TotalCount);
            Assert.Single((await service.SearchAsync(null, null, null, 2, 1)).Items);
            var storedParent = await db.StudyCatalogTopics.SingleAsync(x => x.Id == parent.Id);
            db.Entry(storedParent).Property(x => x.IsActive).CurrentValue = false;
            await db.SaveChangesAsync();
            Assert.Empty((await service.SearchAsync("%", null, null, 1, 20)).Items);
            var storedUnit = await db.StudyCatalogUnits.SingleAsync(x => x.Id == unit.Id);
            db.Entry(storedUnit).Property(x => x.IsActive).CurrentValue = false;
            await db.SaveChangesAsync();
            Assert.Empty((await service.SearchAsync(null, null, null, 1, 20)).Items);
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(null, 13, null, 1, 20));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(null, null, "unknown", 1, 20));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new string('x', 201), null, null, 1, 20));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(null, null, null, 0, 20));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(null, null, null, 1, 51));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
