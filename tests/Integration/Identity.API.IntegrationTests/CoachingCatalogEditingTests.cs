using Coaching.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCatalogEditingTests
{
    [Fact]
    public void LessonEditValidatesBeforeMutatingAndPreservesSourceIdentity()
    {
        var item = StudyCatalogLesson.Create("approved", "1", "Math", 8, "LGS");
        Assert.Throws<ArgumentOutOfRangeException>(() => item.Edit("New", 13, "LGS"));
        Assert.Equal("Math", item.Name);
        item.Edit(" Mathematics ", 9, "tyt");
        Assert.Equal("Mathematics", item.Name);
        Assert.Equal("TYT", item.ExamCode);
        Assert.Equal("approved", item.Source);
        Assert.Equal("1", item.SourceId);
        item.SetActive(true);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void UnitAndTopicEditsDoNotMoveHistoricalOwnership()
    {
        var lesson = Guid.NewGuid();
        var unit = StudyCatalogUnit.Create("approved", "1", lesson, "Unit", 1);
        var topic = StudyCatalogTopic.Create("approved", "2", lesson, unit.Id, "Topic", null, 1, 30);
        Assert.Throws<ArgumentOutOfRangeException>(() => topic.Edit("New", 2, 0));
        Assert.Equal("Topic", topic.Name);
        unit.Edit("New unit", 3);
        topic.Edit("New topic", 4, 45);
        unit.SetActive(true);
        topic.SetActive(true);
        Assert.Equal(lesson, unit.LessonId);
        Assert.Equal(unit.Id, topic.UnitId);
        Assert.Equal(45, topic.EstimatedMinutes);
        Assert.True(unit.IsActive && topic.IsActive);
    }

    [Fact]
    public void TargetEditsAreAtomicAndKeepVerifiedLocation()
    {
        var school = TargetSchool.Create("approved", "s", "School", "Ankara", "Çankaya", 400);
        school.SetVerifiedLocation("06", "123");
        Assert.Throws<ArgumentOutOfRangeException>(() => school.Edit("New", 501, 2025));
        Assert.Equal("School", school.Name);
        school.Edit("New school", 450, 2025);
        school.SetActive(true);
        Assert.Equal("123", school.DistrictId);
        Assert.True(school.IsActive);
        var program = TargetUniversityProgram.Create("approved", "u", "University", "Program", null, "SAY", 400);
        Assert.Throws<ArgumentException>(() => program.Edit("", "New", null, "SAY", 400, 2025));
        Assert.Equal("Program", program.Name);
        program.Edit("New university", "New program", "123", "EA", 420, 2025);
        program.SetActive(true);
        Assert.Equal("New program", program.Name);
        Assert.True(program.IsActive);
    }
}
