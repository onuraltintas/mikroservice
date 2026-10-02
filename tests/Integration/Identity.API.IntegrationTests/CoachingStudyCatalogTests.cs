using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudyCatalogTests
{
    [Fact]
    public void ImportedCatalog_IsInactiveAndUnknownOrderRemainsUnknown()
    {
        var lesson = StudyCatalogLesson.Create("provided-catalog", "lesson-1", "Matematik", null, "TYT");
        var unit = StudyCatalogUnit.Create("provided-catalog", "unit-1", lesson.Id, "Sayılar", null);
        var topic = StudyCatalogTopic.Create("provided-catalog", "topic-1", lesson.Id, unit.Id, "Doğal sayılar", null, 1);
        Assert.False(lesson.IsActive);
        Assert.False(unit.IsActive);
        Assert.False(topic.IsActive);
        Assert.Null(unit.DisplayOrder);
        Assert.Null(topic.EstimatedMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Lesson_RejectsInvalidSchoolGrade(int grade)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StudyCatalogLesson.Create("source", "id", "Lesson", grade, null));
    }

    [Fact]
    public void Catalog_RejectsEmptyNameAndMissingOwnership()
    {
        Assert.Throws<ArgumentException>(() => StudyCatalogLesson.Create("source", "id", " ", null, null));
        Assert.Throws<ArgumentException>(() => StudyCatalogUnit.Create("source", "id", Guid.Empty, "Unit", null));
    }

    [Fact]
    public void Model_UsesCoachingSchemaAndRestrictsCrossLessonTopicReferences()
    {
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var topic = db.Model.FindEntityType(typeof(StudyCatalogTopic))!;
        Assert.Equal("coaching", topic.GetSchema());
        Assert.Contains(topic.GetForeignKeys(), key => key.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "UnitId", "LessonId" }));
        Assert.Contains(topic.GetForeignKeys(), key => key.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "ParentId", "UnitId", "LessonId" }));
        var script = db.GetService<IMigrator>().GenerateScript("20260930193749_AddCoachingBankTransferRequestReferenceIndex");
        Assert.Contains("CREATE TABLE coaching.study_catalog_lessons", script);
        Assert.Contains("CREATE TABLE coaching.study_catalog_units", script);
        Assert.Contains("CREATE TABLE coaching.study_catalog_topics", script);
        Assert.DoesNotContain("INSERT INTO", script);
    }
}
