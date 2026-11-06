using System.Text.Json;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure;
using Coaching.Infrastructure.ExternalServices;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Coaching;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

// E3 review (06.11.2026): older clients, older snapshots and the staff lists after the education statuses changed.
public sealed class CoachingEducationReviewTests
{
    private sealed record Body(CoachingEducationStatus EducationStatus, CoachingEducationStatus? Other);

    [Fact]
    public void AnOlderPageSendingGraduateStillMeansALiseGraduate()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Coaching.API.CoachingApiJson.Configure(options);
        foreach (var name in new[] { "Graduate", "graduate", "HighSchoolGraduate", "highschoolgraduate" })
            JsonSerializer.Deserialize<Body>($$"""{"educationStatus":"{{name}}","other":"{{name}}"}""", options)!
                .Should().Be(new Body(CoachingEducationStatus.HighSchoolGraduate, CoachingEducationStatus.HighSchoolGraduate));
        JsonSerializer.Deserialize<Body>("""{"educationStatus":"UniversityStudent","other":null}""", options)!.Other.Should().BeNull();
        JsonSerializer.Serialize(new Body(CoachingEducationStatus.HighSchoolGraduate, CoachingEducationStatus.UniversityGraduate), options)
            .Should().Be("""{"educationStatus":"HighSchoolGraduate","other":"UniversityGraduate"}""");
        // Numbers and unknown names are still refused, as before.
        FluentActions.Invoking(() => JsonSerializer.Deserialize<Body>("""{"educationStatus":2}""", options)).Should().Throw<JsonException>();
        FluentActions.Invoking(() => JsonSerializer.Deserialize<Body>("""{"educationStatus":"Mezun"}""", options)).Should().Throw<JsonException>();
        // Other enums keep the plain string converter.
        JsonSerializer.Serialize(CoachingMemberRole.Teacher, options).Should().Be("\"Teacher\"");
    }

    [Fact]
    public async Task AnOlderSnapshotWithAGradeBelowFiveIsImportedAsItWasAndCounted()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var young = f.AddAccount("Student"); var older = f.AddAccount("Student");
        var snapshot = new CoachingLegacySnapshot(
            [new(young, 3, "Atatürk İlkokulu", "Ankara", null, null, true, true, null), new(older, 10, null, null, null, null, true, true, null)], [], []);
        var report = await new CoachingProfileImporter(f.Db, f.Accounts).ImportAsync(snapshot, apply: true, default);
        (report.Students, report.OlderGradesKept).Should().Be((2, 1));
        var profile = await f.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == young);
        (profile.EducationStatus, profile.GradeLevel, profile.SchoolName).Should().Be((CoachingEducationStatus.Student, (int?)3, "Atatürk İlkokulu"));
    }

    [Fact]
    public void StaffListsReadTheOwnedDirectoryNotTheOlderIdentityReport()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["Coaching:CmsMedia:RootPath"] = Path.Combine(Path.GetTempPath(), "coaching-cms-media"),
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=coaching;Username=test;Password=test"
        }).Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        var reports = services.Where(x => x.ServiceType == typeof(ICoachingIdentityReportClient)).ToArray();
        reports.Should().NotBeEmpty();
        reports.Should().OnlyContain(x => x.ImplementationType != typeof(IdentityAuthorizationClient));
        // The Identity report has no education status; Coaching registers its own directory for the student lists.
        reports.Should().OnlyContain(x => x.ImplementationFactory != null);
    }
}
