using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class CatalogCleanStartTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationRefusesToDiscardAnyRemainingGroupData()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("20261020065330_MebiLgsProgramCatalog");
        var migrator=pg.Db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261018203437_TopicOutcomeScopeShares");
        var id=Guid.NewGuid();
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO coaching.study_catalog_lesson_groups (\"Id\",\"Name\",\"DisplayOrder\",\"IsActive\") VALUES ({id},'Korunacak grup',0,true)");
        await Assert.ThrowsAsync<PostgresException>(()=>migrator.MigrateAsync());
        Assert.Equal(1,await pg.Db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM coaching.study_catalog_lesson_groups WHERE \"Id\"={id}").SingleAsync());
    }

    [Fact]
    public async Task ModelAndApiNoLongerExposeLegacyLessonGroups()
    {
        await using var f=new CurriculumManagementTests.Fixture();
        Assert.DoesNotContain(f.Db.Model.GetEntityTypes(),x=>x.GetTableName()=="study_catalog_lesson_groups");
        Assert.Null(f.Db.Model.FindEntityType(typeof(StudyCatalogLesson))!.FindProperty("GroupId"));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ChildrenAsync("groups",null,null,1,default));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SaveAsync("groups",null,new("Eski grup","Grup oluşturmayı dene"),default));
    }

    [Fact]
    public async Task LessonsUseExamParentsAndRejectUnknownOrMismatchingParents()
    {
        await using var f=new CurriculumManagementTests.Fixture();
        var exam=await f.Db.Set<StudyCatalogExam>().SingleAsync(x=>x.Code=="LGS");
        var lesson=await f.Service.SaveAsync("lessons",null,new("Matematik","LGS dersi oluştur",ParentId:exam.Id,ExamCode:"LGS"),default);
        Assert.Equal("exams",lesson.ParentKind);Assert.Equal(exam.Id,lesson.ParentId);
        Assert.Equal(lesson.Id,Assert.Single((await f.Service.ChildrenAsync("lessons",exam.Id,null,1,default)).Items).Id);
        Assert.Empty((await f.Service.ChildrenAsync("lessons",Guid.NewGuid(),null,1,default)).Items);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SaveAsync("lessons",null,new("Ders","Bilinmeyen üst kaydı dene",ParentId:Guid.NewGuid(),ExamCode:"LGS"),default));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SaveAsync("lessons",null,new("Ders","Farklı sınavı dene",ParentId:exam.Id,ExamCode:"TYT"),default));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.MoveAsync("lessons",lesson.Id,new(Guid.NewGuid(),lesson.Fingerprint,"Bilinmeyen yere taşı"),default));
    }
}
