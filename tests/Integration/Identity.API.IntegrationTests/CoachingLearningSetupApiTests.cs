using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingLearningSetupApiTests
{
    [Fact]
    public async Task OwnSetupEndpoints_DoNotAcceptStudentId_AndMapValidationAndConflict()
    {
        await using var f = await CoachingLearningSetupTests.Fixture.Create();
        var controller = new LearningSetupController(f.Service);
        Assert.Equal("Student", typeof(LearningSetupController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.IsType<OkObjectResult>(await controller.Get(default));
        Assert.IsType<BadRequestObjectResult>(await controller.Save(new(null, 2026, LearningPurpose.Yks, null, null, null, [], [], false), default));
        Assert.IsType<OkObjectResult>(await controller.Save(new(null, 2026, LearningPurpose.Lgs, 2027, null, null, [], [], false), default));
        Assert.IsType<ConflictObjectResult>(await controller.Save(new(null, 2026, LearningPurpose.Lgs, 2027, null, null, [], [], false), default));
        f.Actor.Roles = ["Teacher"];
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.Get(default)).StatusCode);
    }

    [Fact]
    public async Task CurriculumManagement_RequiresGlobalScope_AndPreservesLessonVersion()
    {
        await using var f = await CoachingLearningSetupTests.Fixture.Create();
        var scope = new Scope();
        var service = new CoachingCurriculumService(f.Db, scope, f.Actor);
        var lesson = StudyCatalogLesson.Create("test", "lesson", "Matematik", 12, null);
        f.Db.Add(lesson); await f.Db.SaveChangesAsync();
        var request = new CurriculumCreateRequest("test-2026", "Test", "v1", 2026, 2026, "Test kaynağı", null, [lesson.Id], "Kurulum testi");
        scope.Global = false;
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(request, default));
        scope.Global = true;
        var created = await service.CreateAsync(request, default);
        Assert.False(created.IsPublished); Assert.Equal(created.Id, (await f.Db.StudyCatalogLessons.SingleAsync()).CurriculumVersionId);
        lesson.SetActive(true); await f.Db.SaveChangesAsync();
        var initialVersion = created.Version;
        var published = await service.SetPublishedAsync(created.Id, new(initialVersion, true, "Yayına hazırlık"), default);
        Assert.True(published.IsPublished);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetPublishedAsync(created.Id, new(initialVersion, false, "Eski sürüm isteği"), default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request with { Code = "other" }, default));
        Assert.Single(await f.Db.CurriculumVersions.ToListAsync());
    }

    private sealed class Scope : ICoachingAdminScopeAuthorization
    {
        public bool Global { get; set; } = true;
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(new CoachingAdminScope(Global, Global ? null : Guid.NewGuid()));
    }

    [Fact]
    public async Task CurriculumControllerMapsScopeValidationMissingAndStalePublication()
    {
        await using var f = await CoachingLearningSetupTests.Fixture.Create(); var scope = new Scope();
        var service = new CoachingCurriculumService(f.Db, scope, f.Actor);
        var controller = new CoachingCurriculumAdminController(service);
        Assert.IsType<OkObjectResult>(await controller.List(default));
        Assert.IsType<NotFoundObjectResult>(await controller.Publish(Guid.NewGuid(), new(0, true, "Yayın gerekçesi"), default));
        var request = new CurriculumCreateRequest("test", "Test", "v1", 2026, 2026, "Test", null, [], "Gerekçe");
        Assert.IsType<BadRequestObjectResult>(await controller.Create(request, default));
        scope.Global = false; Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.List(default)).StatusCode);
        scope.Global = true;
        var lesson = StudyCatalogLesson.Create("test", "lesson", "Matematik", 12, null); lesson.SetActive(true); f.Db.Add(lesson); await f.Db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await controller.Create(request with { LessonIds = [lesson.Id] }, default));
        var version = await f.Db.CurriculumVersions.SingleAsync();
        Assert.IsType<ConflictObjectResult>(await controller.Publish(version.Id, new(-1, true, "Yayın gerekçesi"), default));
        Assert.IsType<OkObjectResult>(await controller.Publish(version.Id, new(version.Version, true, "Yayın gerekçesi"), default));
    }
}
