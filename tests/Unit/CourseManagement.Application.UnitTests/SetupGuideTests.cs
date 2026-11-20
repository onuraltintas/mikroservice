using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Application.UnitTests;

/// <summary>The setup guide's domain rules (20.11.2026).</summary>
public sealed class SetupGuideTests
{
    [Fact]
    public void Setup_progress_keeps_known_skipped_steps_and_the_closed_mark()
    {
        var workspace = Workspace.Create(Guid.NewGuid(), Guid.NewGuid(), "TEST Kurs", WorkspaceType.Institution);
        var now = DateTimeOffset.UtcNow;
        workspace.SetSetupProgress(["rooms", "unknown", "fees", "rooms"], false, now);
        Assert.Equal("fees,rooms", workspace.SetupSkippedSteps);
        Assert.Null(workspace.SetupClosedAt);

        workspace.SetSetupProgress([], true, now);
        Assert.Null(workspace.SetupSkippedSteps);
        Assert.Equal(now, workspace.SetupClosedAt);
        workspace.SetSetupProgress([], true, now.AddDays(1));
        Assert.Equal(now, workspace.SetupClosedAt);
        workspace.SetSetupProgress([], false, now);
        Assert.Null(workspace.SetupClosedAt);
    }

    [Fact]
    public void The_owner_names_the_institution_and_chooses_its_kind()
    {
        var workspace = Workspace.Create(Guid.NewGuid(), Guid.NewGuid(), "TEST", WorkspaceType.Institution);
        workspace.UpdateProfile("  TEST Etüt Merkezi ", InstitutionCategory.StudyCenter);
        Assert.Equal("TEST Etüt Merkezi", workspace.Name);
        Assert.Equal(InstitutionCategory.StudyCenter, workspace.InstitutionCategory);
        Assert.Throws<ArgumentException>(() => workspace.UpdateProfile(" ", null));
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.UpdateProfile("TEST", (InstitutionCategory)99));
    }

    [Fact]
    public void A_group_is_renamed_and_handed_to_another_teacher()
    {
        var group = CourseGroup.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "8-A", DateTimeOffset.UtcNow);
        var teacher = Guid.NewGuid();
        group.Rename(" 8-B ");
        group.ChangeTeacher(teacher);
        Assert.Equal("8-B", group.Name);
        Assert.Equal(teacher, group.TeacherUserId);
        Assert.Throws<ArgumentException>(() => group.Rename(""));
        Assert.Throws<ArgumentException>(() => group.ChangeTeacher(Guid.Empty));
    }

    [Fact]
    public async Task An_accepted_invitation_hands_its_groups_lessons_hours_leave_and_one_to_one_students_to_the_teacher()
    {
        var options = new DbContextOptionsBuilder<CourseManagementDbContext>().UseInMemoryDatabase($"setup-handover-{Guid.NewGuid():N}").Options;
        var workspaceId = Guid.NewGuid();
        var otherWorkspace = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var courseId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var student = StudentRecord.Create(Guid.NewGuid(), workspaceId, "TEST Birebir", null, null, now);
        student.SetOneToOneTeacher(invitationId);
        var keptStudent = StudentRecord.Create(Guid.NewGuid(), workspaceId, "TEST Başka", null, null, now);
        keptStudent.SetOneToOneTeacher(owner);
        await using (var db = new CourseManagementDbContext(options))
        {
            db.Groups.Add(CourseGroup.Create(groupId, workspaceId, courseId, invitationId, "8-A", now));
            db.Groups.Add(CourseGroup.Create(Guid.NewGuid(), otherWorkspace, courseId, invitationId, "Başka kurum", now));
            db.LessonSessions.Add(LessonSession.Create(Guid.NewGuid(), workspaceId, courseId, groupId, invitationId, now.AddDays(1), now.AddDays(1).AddHours(1), now));
            db.TeacherAvailabilityWindows.Add(TeacherAvailabilityWindow.Create(Guid.NewGuid(), workspaceId, invitationId, 1,
                new TimeOnly(9, 0), new TimeOnly(12, 0), "Europe/Istanbul", owner, now));
            db.TeacherTimeOffs.Add(TeacherTimeOff.Create(Guid.NewGuid(), workspaceId, invitationId, now.AddDays(3), now.AddDays(4), null, owner, now));
            db.Students.AddRange(student, keptStudent);
            await db.SaveChangesAsync();
        }

        await using (var db = new CourseManagementDbContext(options))
        {
            var moved = await TeacherInvitationHandOver.HandOverAsync(db, workspaceId, invitationId, teacherId, CancellationToken.None);
            await db.SaveChangesAsync();
            Assert.Equal(5, moved);
        }

        await using (var db = new CourseManagementDbContext(options))
        {
            Assert.Equal(teacherId, (await db.Groups.SingleAsync(item => item.Id == groupId)).TeacherUserId);
            Assert.Equal(invitationId, (await db.Groups.SingleAsync(item => item.WorkspaceId == otherWorkspace)).TeacherUserId);
            Assert.All(await db.LessonSessions.ToListAsync(), lesson => Assert.Equal(teacherId, lesson.TeacherUserId));
            Assert.All(await db.TeacherAvailabilityWindows.ToListAsync(), window => Assert.Equal(teacherId, window.TeacherUserId));
            Assert.All(await db.TeacherTimeOffs.ToListAsync(), block => Assert.Equal(teacherId, block.TeacherUserId));
            Assert.Equal(teacherId, (await db.Students.SingleAsync(item => item.Id == student.Id)).OneToOneTeacherUserId);
            Assert.Equal(owner, (await db.Students.SingleAsync(item => item.Id == keptStudent.Id)).OneToOneTeacherUserId);
        }
    }
}
