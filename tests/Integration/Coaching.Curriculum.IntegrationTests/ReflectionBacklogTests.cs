using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;
public sealed class ReflectionBacklogTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task OldMissingWeeksStayPagedUntilSubmittedThenMoveToReviewQueueAndSurviveTeacherChange()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        var reviews=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;
        var profile=await pg.Db.CoachingStudentProfiles.SingleAsync();var assignment=await pg.Db.CoachingTeacherStudentAssignments.SingleAsync();
        pg.Db.Entry(profile).Property(x=>x.CreatedAt).CurrentValue=new DateTime(2026,9,29,0,0,0,DateTimeKind.Utc);
        pg.Db.Entry(assignment).Property(x=>x.StartDate).CurrentValue=new DateTime(2026,9,29,0,0,0,DateTimeKind.Utc);await pg.Db.SaveChangesAsync();
        var accounts=new WeeklyFollowUpPostgresTests.Accounts(teacher);var policy=new CoachingAccessPolicy(user);
        var api=new CoachingTeacherTrackingController(pg.Db,policy,new OwnedCoachingDirectory(pg.Db,accounts,user),accounts,new WeeklyFollowUpPostgresTests.Clock());
        user.UserId=teacher;user.Roles=["Teacher"];
        async Task<TeacherTrackingPage> Page(string kind="reflection",int number=1,int size=1)=>Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(await api.Get(new(2026,10,19),new(2026,10,20),kind,pageNumber:number,pageSize:size)).Value);
        var first=await Page();Assert.Equal(3,first.AwaitingReflectionCount);Assert.Equal(new DateOnly(2026,9,28),Assert.Single(first.Items).WeekStart);
        var second=await Page(number:2);Assert.Equal(new DateOnly(2026,10,5),Assert.Single(second.Items).WeekStart);
        Assert.Equal(first.Items[0].RecordId,second.Items[0].RecordId);Assert.NotEqual(first.Items[0].WeekStart,second.Items[0].WeekStart);
        user.UserId=student;user.Roles=["Student"];var home=await reviews.HomeAsync(default);Assert.Equal(3,home.MissingReflectionCount);Assert.Equal(new DateOnly(2026,9,28),home.WeekStart);
        var saved=await reviews.SaveReflectionAsync(home.WeekStart,new(null,"Eski haftayı tamamladım","","Devam",4),default);
        user.UserId=teacher;user.Roles=["Teacher"];Assert.Equal(2,(await Page()).AwaitingReflectionCount);
        var pending=await Page("weekly-review");Assert.Equal(new DateOnly(2026,9,28),Assert.Single(pending.Items).WeekStart);Assert.Equal(1,pending.AwaitingWeeklyReviewCount);
        await reviews.ReviewReflectionAsync(student,home.WeekStart,new(saved.Version,"İnceledim, takip edeceğim"),default);Assert.Empty((await Page("weekly-review")).Items);
        var next=Guid.NewGuid();assignment.SetActive(false,teacher);pg.Db.AddRange(CoachingTeacherProfile.Create(next),CoachingTeacherStudentAssignment.Create(next,student,null,null,next));await pg.Db.SaveChangesAsync();
        user.UserId=next;var nextAccounts=new WeeklyFollowUpPostgresTests.Accounts(next);api=new(pg.Db,policy,new OwnedCoachingDirectory(pg.Db,nextAccounts,user),nextAccounts,new WeeklyFollowUpPostgresTests.Clock());
        var inherited=await Page(size:25);Assert.Equal(2,inherited.AwaitingReflectionCount);Assert.Equal(new DateOnly(2026,10,5),inherited.Items[0].WeekStart);
        profile.UpdatePersonal(null,null,null,false);await pg.Db.SaveChangesAsync();Assert.Empty((await Page(size:25)).Items);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task EligibilityDoesNotInventWeeksBeforeEnrollmentOrIncludeTheUnfinishedCurrentWeek()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();await WeeklyReviewPostgresTests.Setup(pg,student,Guid.NewGuid());
        foreach(var profile in await pg.Db.CoachingStudentProfiles.ToArrayAsync())pg.Db.Entry(profile).Property(x=>x.CreatedAt).CurrentValue=new DateTime(2026,10,19,0,0,0,DateTimeKind.Utc);
        foreach(var assignment in await pg.Db.CoachingTeacherStudentAssignments.ToArrayAsync())pg.Db.Entry(assignment).Property(x=>x.StartDate).CurrentValue=new DateTime(2026,10,19,0,0,0,DateTimeKind.Utc);
        await pg.Db.SaveChangesAsync();
        Assert.Empty(await CoachingReflectionBacklog.MissingAsync(pg.Db,[student],new(2026,10,20),new(2026,12,31),default));
        Assert.Single(await CoachingReflectionBacklog.MissingAsync(pg.Db,[student],new(2026,10,26),new(2026,10,25),default));
        Assert.Empty(await CoachingReflectionBacklog.MissingAsync(pg.Db,[student],new(2026,10,26),new(2026,10,24),default));
    }
}
