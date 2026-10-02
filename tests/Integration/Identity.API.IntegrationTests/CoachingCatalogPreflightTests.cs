using Coaching.Infrastructure.Catalogs;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCatalogPreflightTests
{
    private static Dictionary<string, string> EmptyCatalogs() => new()
    {
        ["lessons.json"] = "[]", ["units-derived.json"] = "[]",
        ["upper-subjects.json"] = "[]", ["subjects.json"] = "[]",
        ["university-programs.json"] = "[]", ["lgs-programs.json"] = "[]"
    };

    [Fact]
    public void Preflight_RejectsPrivateFilesWithoutParsingTheirContents()
    {
        var files = EmptyCatalogs();
        files["student-goals.json"] = "not even JSON";
        Assert.Throws<ArgumentException>(() => CatalogPreflight.Validate(files));
    }

    [Fact]
    public void Preflight_CountsValidRelationshipsWithoutWritingData()
    {
        var files = EmptyCatalogs();
        files["lessons.json"] = """[{"id":"l","name":"Lesson"}]""";
        files["units-derived.json"] = """[{"id":"u","name":"Unit","lessonId":"l"}]""";
        files["upper-subjects.json"] = """[{"id":"p","name":"Parent","unitId":"u","lessonId":"l"}]""";
        files["subjects.json"] = """[{"id":"t","name":"Topic","unitId":"u","upperSubjectId":"p"}]""";
        var report = CatalogPreflight.Validate(files);
        Assert.Empty(report.Errors);
        Assert.Equal(1, report.Counts["subjects.json"]);
    }

    [Fact]
    public void Preflight_ReportsDuplicateAndOrphanIdentities()
    {
        var files = EmptyCatalogs();
        files["lessons.json"] = """[{"id":"l","name":"Lesson"},{"id":"l","name":"Other"}]""";
        files["units-derived.json"] = """[{"id":"u","name":"Unit","lessonId":"missing"}]""";
        var report = CatalogPreflight.Validate(files);
        Assert.Contains(report.Errors, x => x.Contains("duplicate"));
        Assert.Contains(report.Errors, x => x.Contains("lessonId"));
    }

    [Fact]
    public void Preflight_RejectsTopicInDifferentUnitFromParent()
    {
        var files = EmptyCatalogs();
        files["lessons.json"] = """[{"id":"l","name":"Lesson"}]""";
        files["units-derived.json"] = """[{"id":"u","name":"Unit","lessonId":"l"},{"id":"v","name":"Other","lessonId":"l"}]""";
        files["upper-subjects.json"] = """[{"id":"p","name":"Parent","unitId":"u","lessonId":"l"}]""";
        files["subjects.json"] = """[{"id":"t","name":"Topic","unitId":"v","upperSubjectId":"p"}]""";
        Assert.Contains(CatalogPreflight.Validate(files).Errors, x => x.Contains("parent unit"));
    }
}
