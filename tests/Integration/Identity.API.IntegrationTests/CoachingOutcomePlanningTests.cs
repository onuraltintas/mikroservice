using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingOutcomePlanningTests
{
    [Fact]
    public async Task OutcomePreviewUsesReviewedVersion_OverrideIsLocal_AndLegacyMinutesStillWork()
    {
        await using var f = await CoachingOutcomePilotTests.Fixture.Create(); await f.Import();
        var outcome = await f.Db.Set<StudyCatalogOutcome>().FirstAsync();
        await f.Service.ReviewMappingAsync(f.Topic.Id, outcome.Id, new(true, "Kapsam incelendi ve doğrulandı"), default);
        var estimate = await f.Service.EstimateAsync(outcome.Id, new(20, 30, 10, "İlk öğrenme için öğretmen süre incelemesi", "Medium", "Pilot incelemesi"), default);
        await f.Service.PublishAsync(estimate.Id, "İncelenen süreyi yayınla", default);
        var student = Guid.NewGuid(); f.Inner.Actor.UserId = student; f.Inner.Actor.Roles = ["Student"];
        var hours = StudyAvailability.Create(student, "Europe/Istanbul"); hours.ReplaceWindows([new(DayOfWeek.Monday, 1020, 1140)]);
        f.Db.Add(hours); await f.Db.SaveChangesAsync();
        var preview = new CoachingAutomaticStudyPlanPreviewService(f.Db, new CoachingAccessPolicy(f.Inner.Actor));
        var r = new AutomaticStudyPreviewRequest(new(2026, 10, 19), 7, hours.Version,
            [new(f.Topic.Id, null, [new(outcome.Id, estimate.Id)])]);
        var result = await preview.PreviewAsync(r);
        Assert.Equal(60, result.Schedule.ScheduledMinutes); Assert.Equal(estimate.Id, Assert.Single(result.Outcomes!).EstimateId);
        var overridden = await preview.PreviewAsync(r with { Topics = [new(f.Topic.Id, null, [new(outcome.Id, estimate.Id, 35)])] });
        Assert.Equal(35, overridden.Schedule.ScheduledMinutes); Assert.Equal("StudentOverride", overridden.Outcomes![0].Origin);
        Assert.Equal(60, (await f.Db.Set<OutcomeDurationEstimate>().SingleAsync(x => x.Id == estimate.Id)).TotalMinutes);
        Assert.Equal(25, (await preview.PreviewAsync(r with { Topics = [new(f.Topic.Id, 25)] })).Schedule.ScheduledMinutes);
        await Assert.ThrowsAsync<ArgumentException>(() => preview.PreviewAsync(r with { Topics = [new(f.Topic.Id, null, [new(outcome.Id, Guid.NewGuid())])] }));
        await Assert.ThrowsAsync<ArgumentException>(() => preview.PreviewAsync(r with { Topics = [new(f.Topic.Id, 30, [new(outcome.Id, estimate.Id)])] }));
    }

    [Fact]
    public void AdministrativeWritesHaveAuthorizationAndMfa_StudentReadIsStudentOnly()
    {
        Assert.NotEmpty(typeof(CoachingOutcomeAdminController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Contains(typeof(CoachingOutcomeAdminController).GetCustomAttributes(true), x => x.GetType().Name == "HasPermissionAttribute");
        Assert.Contains(typeof(CoachingOutcomeAdminController).GetCustomAttributes(true), x => x.GetType().Name == "MfaCategoryAttribute");
        Assert.Equal("Student", ((AuthorizeAttribute)typeof(StudyOutcomeCatalogController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Single()).Roles);
    }

    [Fact]
    public void PilotPackageHas52Outcomes_12OfficialBudgets_And13CandidateMappings()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "data", "coaching-outcomes"))) root = root.Parent;
        Assert.NotNull(root);
        var package = System.Text.Json.JsonSerializer.Deserialize<Coaching.Infrastructure.Catalogs.OutcomeImportPackage>(
            File.ReadAllText(Path.Combine(root.FullName, "data", "coaching-outcomes", "lgs-matematik-2026-pilot.json")), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Coaching.Infrastructure.Catalogs.CoachingOutcomeCatalogService.Validate(package);
        Assert.Equal(52, package.Outcomes.Count); Assert.Equal(12, package.OfficialTopics.Count); Assert.Equal(180, package.OfficialTopics.Sum(x => x.LessonPeriods));
        Assert.Equal(13, package.Mappings.Count); Assert.Equal(2, package.Outcomes.Sum(x => x.Sources.Count));
        Assert.Contains(package.Outcomes, x => x.Code == "M.8.1.3.3" && x.Name.Contains("√"));
    }
}
