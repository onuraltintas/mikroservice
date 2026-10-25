using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyReviewQueuePagingTests
{
    private static readonly DateOnly Week = new(2026,10,12);
    private static CoachingTeacherTrackingController Controller(CurriculumPostgresTests.Pg pg,Guid teacher)
    {
        pg.Data.User.UserId=teacher;pg.Data.User.Roles=["Teacher"];
        var accounts=new WeeklyFollowUpPostgresTests.Accounts(teacher);
        return new(pg.Db,new CoachingAccessPolicy(pg.Data.User),new OwnedCoachingDirectory(pg.Db,accounts,pg.Data.User),accounts,new WeeklyFollowUpPostgresTests.Clock());
    }
    private static async Task<TeacherTrackingPage> Page(CoachingTeacherTrackingController api,int page=1,int size=25,string mode="weekly-review",string? search=null)
        =>Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(await api.Get(new(2026,10,19),new(2026,10,20),mode,search,page,size)).Value);
    private static Task Seed(CurriculumPostgresTests.Pg pg,Guid student,int count)=>pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO coaching.weekly_reflections
        ("Id","StudentId","WeekStart","Successes","Obstacles","NextFocus","Rating","Version","CreatedAt","ReflectionHistoryJson")
        SELECT gen_random_uuid(),{student},{Week} - 7*i,'Başarı','','Devam',3,3,{DateTime.UtcNow},'[]'::jsonb
        FROM generate_series(0,{count-1}) g(i)
        """);
    private static async Task<string> Checksum(CurriculumPostgresTests.Pg pg)=>
        string.Join("/",await pg.Db.Database.SqlQueryRaw<string>("""
        SELECT md5(COALESCE(string_agg(to_jsonb(r)::text,'' ORDER BY "Id"),'')) AS "Value" FROM coaching.weekly_reflections r
        UNION ALL
        SELECT md5(COALESCE(string_agg(to_jsonb(r)::text,'' ORDER BY "Id"),'')) AS "Value" FROM coaching.weekly_reflection_reviews r
        """).ToArrayAsync());

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MoreThanTenThousandPendingRowsHaveExactCountsAndPagesBeyondOneThousandWithoutWrites()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        await WeeklyReviewPostgresTests.Setup(pg,student,teacher);await Seed(pg,student,10025);
        var api=Controller(pg,teacher);var before=await Checksum(pg);
        var ids=await pg.Db.Set<WeeklyReflection>().OrderBy(x=>x.WeekStart).ThenBy(x=>x.Id).Select(x=>x.Id).ToArrayAsync();
        var first=await Page(api,1,10);Assert.Equal(10025,first.TotalCount);Assert.Equal(10025,first.AwaitingWeeklyReviewCount);Assert.Equal(ids.Take(10),first.Items.Select(x=>x.RecordId));
        var distant=await Page(api,1001,10);Assert.Equal(1001,distant.PageNumber);Assert.Equal(ids.Skip(10000).Take(10),distant.Items.Select(x=>x.RecordId));
        var last=await Page(api,int.MaxValue,10);Assert.Equal(1003,last.PageNumber);Assert.Equal(ids.TakeLast(5),last.Items.Select(x=>x.RecordId));
        Assert.Equal(10025,(await Page(api,mode:"all")).TotalCount);
        Assert.Empty((await Page(api,search:"eşleşmez")).Items);
        Assert.Equal(before,await Checksum(pg));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task LargeReviewedHistoryDoesNotHidePendingVersionsLegacyReviewsOrChangedResults()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);await Seed(pg,student,10030);var api=Controller(pg,teacher);
        var view=await service.GetAsync(student,Week,1,default);
        await service.ReviewReflectionAsync(student,Week,new(view.Reflection!.Version,"İlk sonuçları inceledim",view.Results!.Fingerprint),default);
        var hash=(await pg.Db.Set<WeeklyReflectionReview>().SingleAsync()).EvidenceFingerprint!;
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.weekly_reflection_reviews
            ("Id","ReflectionId","StudentId","TeacherId","ReflectionVersion","Note","ReflectionSnapshotJson","EvidenceFingerprint","EvidenceSnapshotJson","Version","CreatedAt")
            SELECT gen_random_uuid(),"Id","StudentId",{teacher},"Version",'Geçmiş inceleme',jsonb_build_object(),{hash},jsonb_build_object(),0,{DateTime.UtcNow}
            FROM coaching.weekly_reflections WHERE "WeekStart" < {Week} AND "WeekStart" > {Week.AddDays(-7*10029)}
            """);
        // One unreviewed, one changed reflection, one legacy review and one changed evidence fingerprint.
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE coaching.weekly_reflections SET "Version"="Version"+1 WHERE "WeekStart"={Week.AddDays(-7)}
            """);
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE coaching.weekly_reflection_reviews SET "EvidenceFingerprint"=NULL WHERE "ReflectionId" IN
            (SELECT "Id" FROM coaching.weekly_reflections WHERE "WeekStart"={Week.AddDays(-14)})
            """);
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE coaching.weekly_reflection_reviews SET "EvidenceFingerprint"={new string('a',64)} WHERE "ReflectionId" IN
            (SELECT "Id" FROM coaching.weekly_reflections WHERE "WeekStart"={Week.AddDays(-21)})
            """);
        pg.Db.ChangeTracker.Clear();var before=await Checksum(pg);
        var first=await Page(api,1,2);var second=await Page(api,2,2);
        Assert.Equal(4,first.TotalCount);Assert.Equal(4,second.AwaitingWeeklyReviewCount);
        Assert.Equal(new[]{"ReflectionSubmitted","ResultsChanged","LegacyEvidenceMissing","ReflectionUpdated"},first.Items.Concat(second.Items).Select(x=>x.ReviewReason));
        Assert.Equal(1,(await Page(api,search:"sonrasında")).TotalCount);
        Assert.Equal(before,await Checksum(pg));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task EqualWeekCursorAndEveryPageRetainAllAuthorizedStudentsInStableOrder()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var teacher=Guid.NewGuid();var student=Guid.NewGuid();
        await WeeklyReviewPostgresTests.Setup(pg,student,teacher);
        for(var i=0;i<270;i++) {
            var id=i==0?student:Guid.NewGuid();
            if(i>0)pg.Db.AddRange(CoachingStudentProfile.Create(id),CoachingTeacherStudentAssignment.Create(teacher,id,null,null,teacher));
            var reflection=WeeklyReflection.Create(id,Week);reflection.Edit("Başarı","","Devam",3);pg.Db.Add(reflection);
        }
        var foreign=WeeklyReflection.Create(Guid.NewGuid(),Week);foreign.Edit("Gizli","","Devam",3);pg.Db.Add(foreign);
        await pg.Db.SaveChangesAsync();var api=Controller(pg,teacher);var all=new List<Guid>();
        for(var page=1;page<=11;page++) {var result=await Page(api,page);Assert.Equal(270,result.TotalCount);all.AddRange(result.Items.Select(x=>x.RecordId));}
        Assert.Equal(270,all.Distinct().Count());Assert.DoesNotContain(foreign.Id,all);
        Assert.Equal(await pg.Db.Set<WeeklyReflection>().Where(x=>x.Id!=foreign.Id).OrderBy(x=>x.Id).Select(x=>x.Id).ToArrayAsync(),all);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ReviewingFinalItemClampsTheRequestedPageAndKeepsReviewHistory()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);await Seed(pg,student,2);var api=Controller(pg,teacher);
        Assert.Equal(Week,(await Page(api,2,1)).Items.Single().WeekStart);
        var view=await service.GetAsync(student,Week,1,default);
        await service.ReviewReflectionAsync(student,Week,new(view.Reflection!.Version,"Son haftayı inceledim",view.Results!.Fingerprint),default);
        var before=await Checksum(pg);var remaining=await Page(api,2,1);
        Assert.Equal(1,remaining.PageNumber);Assert.Equal(1,remaining.TotalCount);Assert.Equal(Week.AddDays(-7),remaining.Items.Single().WeekStart);
        Assert.Equal(before,await Checksum(pg));Assert.Equal(2,await pg.Db.Set<WeeklyReflection>().CountAsync());Assert.Single(await pg.Db.Set<WeeklyReflectionReview>().ToArrayAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ManyValidWeeksSplitReportRangesAndMergeOtherPendingKindsBeforePaging()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        await WeeklyReviewPostgresTests.Setup(pg,student,teacher);await Seed(pg,student,26);
        var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Geçmiş plan");plan.Activate();pg.Db.Add(plan);
        for(var week=0;week<26;week++)for(var task=0;task<400;task++)
            pg.Db.Add(StudyPlanTask.Create(plan,Week.AddDays(-7*week),"Geçmiş çalışma",30));
        foreach(var reflection in await pg.Db.Set<WeeklyReflection>().ToArrayAsync())
            pg.Db.Add(WeeklyReflectionReview.Create(reflection,teacher,"Eski sonuç incelemesi",new string('a',64),"{}"));
        var late=StudyPlanTask.Create(plan,new(2026,10,19),"Geciken çalışma",30);pg.Db.Add(late);
        var assignment=Assignment.Create(teacher,"Geciken ödev",new DateTime(2026,10,19,9,0,0,DateTimeKind.Utc));assignment.AssignToStudent(student);pg.Db.Add(assignment);
        await pg.Db.SaveChangesAsync();pg.Db.ChangeTracker.Clear();var api=Controller(pg,teacher);var before=await Checksum(pg);
        var ids=new List<Guid>();
        for(var page=1;page<=3;page++) {
            var result=await Page(api,page,10,"all");Assert.Equal(28,result.TotalCount);Assert.Equal(26,result.AwaitingWeeklyReviewCount);
            Assert.Equal(1,result.OverdueStudyCount);Assert.Equal(1,result.OverdueAssignmentCount);ids.AddRange(result.Items.Select(x=>x.RecordId));
        }
        Assert.Equal(28,ids.Distinct().Count());Assert.Equal(new[]{late.Id,assignment.Id},ids.TakeLast(2));
        Assert.Equal(before,await Checksum(pg));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RetryEnabledSnapshotKeepsCountsAndRowsConsistentDuringConcurrentReview()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);await Seed(pg,student,300);Controller(pg,teacher);
        var prototype=await service.GetAsync(student,Week,1,default);
        await service.ReviewReflectionAsync(student,Week,new(prototype.Reflection!.Version,"Örnek sonuç incelemesi",prototype.Results!.Fingerprint),default);
        var hash=(await pg.Db.Set<WeeklyReflectionReview>().SingleAsync()).EvidenceFingerprint!;
        var target=await pg.Db.Set<WeeklyReflection>().SingleAsync(x=>x.WeekStart==Week.AddDays(-7));
        var hook=new DuringScan(async()=>{
            await using var writer=new Coaching.Infrastructure.Data.CoachingDbContext(new DbContextOptionsBuilder<Coaching.Infrastructure.Data.CoachingDbContext>().UseNpgsql(pg.Db.Database.GetConnectionString()).Options);
            writer.Add(WeeklyReflectionReview.Create(target,teacher,"Eşzamanlı inceleme",hash,"{}"));await writer.SaveChangesAsync();
        });
        await using var reader=new Coaching.Infrastructure.Data.CoachingDbContext(new DbContextOptionsBuilder<Coaching.Infrastructure.Data.CoachingDbContext>()
            .UseNpgsql(pg.Db.Database.GetConnectionString(),options=>options.EnableRetryOnFailure()).AddInterceptors(hook).Options);
        var accounts=new WeeklyFollowUpPostgresTests.Accounts(teacher);
        var api=new CoachingTeacherTrackingController(reader,new CoachingAccessPolicy(pg.Data.User),new OwnedCoachingDirectory(reader,accounts,pg.Data.User),accounts,new WeeklyFollowUpPostgresTests.Clock());
        Assert.Equal(299,(await Page(api)).TotalCount);Assert.True(hook.Fired);
        Assert.Equal(298,(await Page(api)).TotalCount);
    }
    private sealed class DuringScan(Func<Task> action):Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public bool Fired{get;private set;}
        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,CancellationToken ct=default)
        {
            if(!Fired&&command.CommandText.Contains("weekly_reflections")&&command.CommandText.Contains("LIMIT")) {
                Fired=true;await action();
                Assert.DoesNotContain("Successes",command.CommandText);Assert.DoesNotContain("ResultsJson",command.CommandText);
                Assert.Equal(System.Data.IsolationLevel.RepeatableRead,command.Transaction!.IsolationLevel);
            }
            return result;
        }
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task IndexMigrationUpgradeRepeatAndDowngradePreserveHistoricalRows()
    {
        const string previous="20261024234143_AutomaticCompletedOutcomeEvidence";
        await using var pg=await CurriculumPostgresTests.Pg.Create(previous);var reflection=WeeklyReflection.Create(Guid.NewGuid(),Week);reflection.Edit("Başarı","Engel","Devam",4);
        pg.Db.AddRange(reflection,WeeklyReflectionReview.Create(reflection,Guid.NewGuid(),"Eski inceleme notu"));await pg.Db.SaveChangesAsync();
        var before=await Checksum(pg);await pg.Db.Database.MigrateAsync();await pg.Db.Database.MigrateAsync();Assert.Equal(before,await Checksum(pg));Assert.False(pg.Db.Database.HasPendingModelChanges());
        await pg.Db.GetService<IMigrator>().MigrateAsync(previous);Assert.Equal(before,await Checksum(pg));
        await pg.Db.Database.MigrateAsync();Assert.Equal(before,await Checksum(pg));
    }
}
