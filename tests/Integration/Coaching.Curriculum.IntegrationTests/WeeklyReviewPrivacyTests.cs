using System.Text.Json;
using Coaching.Application.Attachments;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyReviewPrivacyTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task ErasureIncludesOwnedWeeklyRecordsAndKeepsOtherStudentsAndExistingPlans(bool eraseTeacher)
    {
        await using var f = new CurriculumManagementTests.Fixture(); await f.Seed();
        var student = Guid.NewGuid(); var teacher = Guid.NewGuid(); var other = Guid.NewGuid(); var week = new DateOnly(2026, 10, 12);
        var reflection = WeeklyReflection.Create(student, week); reflection.Edit("Başardım", "", "Tekrar", 4);
        var otherReflection = WeeklyReflection.Create(other, week); otherReflection.Edit("Diğer öğrenci", "", "Tekrar", null);
        var decision = WeeklyDecision.Create(Guid.NewGuid(), student, teacher, week); decision.Edit("Karar", "", week.AddDays(7), "Planned", DateTime.UtcNow);
        var otherDecision = WeeklyDecision.Create(Guid.NewGuid(), other, Guid.NewGuid(), week); otherDecision.Edit("Diğer karar", "", week.AddDays(7), "Planned", DateTime.UtcNow);
        f.Db.AddRange(reflection, otherReflection, decision, otherDecision); await f.Db.SaveChangesAsync();
        var request=IdempotencyRecord.Create("coaching.weekly-decision.study",decision.Id.ToString("D")+":"+Guid.NewGuid().ToString("N"),"hash",Guid.NewGuid());
        var otherRequest=IdempotencyRecord.Create("coaching.weekly-decision.assignment",otherDecision.Id.ToString("D")+":"+Guid.NewGuid().ToString("N"),"hash",Guid.NewGuid());
        f.Db.AddRange(request,otherRequest);await f.Db.SaveChangesAsync();
        var originalPlans = JsonSerializer.Serialize(await f.Db.StudyPlanRevisions.AsNoTracking().ToArrayAsync());
        var id = Guid.NewGuid(); var eventId = Guid.NewGuid(); var subject = eraseTeacher ? teacher : student; var now = DateTime.UtcNow;
        var assessment = await new CoachingErasureAssessmentService(f.Db, TimeProvider.System).AssessAsync(new(eventId, id, subject, now, true, PersonalDataScope.Coaching), default);
        Assert.Equal(eraseTeacher ? 2 : 3, assessment.StudyPlanningRecordCount);
        await new CoachingErasureExecutionService(f.Db, new NoAttachments(), TimeProvider.System).ExecuteAsync(new(eventId, id, subject, now, PersonalDataScope.Coaching), default);
        Assert.False(await f.Db.Set<WeeklyDecision>().AnyAsync(x => x.Id == decision.Id));
        Assert.False(await f.Db.Set<IdempotencyRecord>().AnyAsync(x=>x.Id==request.Id));Assert.True(await f.Db.Set<IdempotencyRecord>().AnyAsync(x=>x.Id==otherRequest.Id));
        Assert.Equal(eraseTeacher, await f.Db.Set<WeeklyReflection>().AnyAsync(x => x.StudentId == student));
        Assert.True(await f.Db.Set<WeeklyReflection>().AnyAsync(x => x.StudentId == other));
        Assert.True(await f.Db.Set<WeeklyDecision>().AnyAsync(x => x.Id == otherDecision.Id));
        Assert.Equal(originalPlans, JsonSerializer.Serialize(await f.Db.StudyPlanRevisions.AsNoTracking().ToArrayAsync()));
    }
    private sealed class NoAttachments : IAssignmentAttachmentStorage
    {
        public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(Guid a, Guid s, Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredAssignmentAttachment> StoreAsync(string key, Stream content, string type, long size, string hash, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
