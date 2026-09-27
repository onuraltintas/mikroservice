using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingInstitutionStudentManagementTests
{
    [Fact]
    public void Ef_model_persists_school_grade_in_speed_reading_profile_and_indexes_it()
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql("Host=localhost;Database=speed_reading_model_only;Username=unused;Password=unused")
            .Options;
        using var context = new OwnedSpeedReadingDbContext(options);
        var entity = context.Model.FindEntityType(typeof(SpeedReadingUserProfile))!;

        entity.GetTableName().Should().Be("user_profiles");
        entity.GetSchema().Should().Be("speed_reading");
        entity.FindProperty(nameof(SpeedReadingUserProfile.GradeLevel))!.IsNullable.Should().BeTrue();
        entity.GetIndexes().Should().Contain(index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { nameof(SpeedReadingUserProfile.GradeLevel) }));
        context.Database.GetMigrations().Should().Contain("20260925131153_AddSpeedReadingSchoolGrade");
    }

    [Fact]
    public async Task Update_changes_local_grade_and_reassigns_only_the_selected_institution_relationship()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var otherInstitutionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var previousTeacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        AddMembership(context, institutionId, studentId, SpeedReadingInstitutionMemberRole.Student);
        AddMembership(context, institutionId, previousTeacherId, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institutionId, newTeacherId, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, otherInstitutionId, studentId, SpeedReadingInstitutionMemberRole.Student);
        context.UserProfiles.Add(SpeedReadingUserProfile.Import(
            Guid.NewGuid(), studentId, 4, 215, 82, 35, null, institutionId,
            true, DateTime.UtcNow, actorId.ToString(), null, null, learningStyle: "visual"));
        context.TeacherStudentAssignments.AddRange(
            SpeedReadingTeacherStudentAssignment.Create(institutionId, previousTeacherId, studentId, actorId, DateTime.UtcNow),
            SpeedReadingTeacherStudentAssignment.Create(otherInstitutionId, previousTeacherId, studentId, actorId, DateTime.UtcNow),
            SpeedReadingTeacherStudentAssignment.Create(null, previousTeacherId, studentId, actorId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var store = new OwnedSpeedReadingInstitutionStudentManagement(context, new AllowAllSpeedReadingMembers());

        var result = await store.UpdateAsync(
            institutionId, studentId, gradeLevel: 9, teacherUserId: newTeacherId, actorId, DateTime.UtcNow.AddMinutes(1));

        result.Should().Be(SpeedReadingInstitutionStudentUpdateResult.Updated);
        var profile = await context.UserProfiles.SingleAsync(item => item.UserId == studentId);
        profile.GradeLevel.Should().Be(9);
        profile.CurrentLevel.Should().Be(4);
        var assignments = await context.TeacherStudentAssignments.Where(item => item.StudentUserId == studentId).ToListAsync();
        assignments.Should().Contain(item => item.InstitutionId == institutionId && item.TeacherUserId == previousTeacherId && !item.IsActive);
        assignments.Should().Contain(item => item.InstitutionId == institutionId && item.TeacherUserId == newTeacherId && item.IsActive);
        assignments.Should().Contain(item => item.InstitutionId == otherInstitutionId && item.TeacherUserId == previousTeacherId && item.IsActive);
        assignments.Should().Contain(item => item.InstitutionId == null && item.TeacherUserId == previousTeacherId && item.IsActive);
    }

    [Fact]
    public async Task Update_rejects_a_teacher_without_active_membership_or_product_role()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        AddMembership(context, institutionId, studentId, SpeedReadingInstitutionMemberRole.Student);
        context.UserProfiles.Add(SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), studentId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var eligibility = new SelectiveSpeedReadingMembers { IneligibleUserId = teacherId };
        var store = new OwnedSpeedReadingInstitutionStudentManagement(context, eligibility);

        var result = await store.UpdateAsync(
            institutionId, studentId, gradeLevel: 7, teacherUserId: teacherId, actorId, DateTime.UtcNow);

        result.Should().Be(SpeedReadingInstitutionStudentUpdateResult.TeacherMembershipRequired);
        (await context.UserProfiles.SingleAsync(item => item.UserId == studentId)).GradeLevel.Should().BeNull();
        (await context.TeacherStudentAssignments.ToListAsync()).Should().BeEmpty();

        AddMembership(context, institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher);
        await context.SaveChangesAsync();
        result = await store.UpdateAsync(
            institutionId, studentId, gradeLevel: 7, teacherUserId: teacherId, actorId, DateTime.UtcNow);

        result.Should().Be(SpeedReadingInstitutionStudentUpdateResult.ProductRoleRequired);
        (await context.UserProfiles.SingleAsync(item => item.UserId == studentId)).GradeLevel.Should().BeNull();
        (await context.TeacherStudentAssignments.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Update_creates_a_product_local_profile_when_student_has_not_started_reading_yet()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        AddMembership(context, institutionId, studentId, SpeedReadingInstitutionMemberRole.Student);
        await context.SaveChangesAsync();
        var store = new OwnedSpeedReadingInstitutionStudentManagement(context, new AllowAllSpeedReadingMembers());

        var result = await store.UpdateAsync(
            institutionId, studentId, gradeLevel: 2, teacherUserId: null, actorId, DateTime.UtcNow);

        result.Should().Be(SpeedReadingInstitutionStudentUpdateResult.Updated);
        var profile = await context.UserProfiles.SingleAsync(item => item.UserId == studentId);
        profile.GradeLevel.Should().Be(2);
        profile.CurrentLevel.Should().Be(1);
        (await context.TeacherStudentAssignments.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Update_can_clear_grade_and_teacher_without_changing_reading_level()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        AddMembership(context, institutionId, studentId, SpeedReadingInstitutionMemberRole.Student);
        AddMembership(context, institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher);
        context.UserProfiles.Add(SpeedReadingUserProfile.Import(
            Guid.NewGuid(), studentId, 6, 180, 70, 30, null, institutionId,
            true, DateTime.UtcNow, actorId.ToString(), null, null, gradeLevel: 11));
        context.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(
            institutionId, teacherId, studentId, actorId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var store = new OwnedSpeedReadingInstitutionStudentManagement(context, new AllowAllSpeedReadingMembers());

        var result = await store.UpdateAsync(
            institutionId, studentId, gradeLevel: null, teacherUserId: null, actorId, DateTime.UtcNow.AddMinutes(1));

        result.Should().Be(SpeedReadingInstitutionStudentUpdateResult.Updated);
        (await context.UserProfiles.SingleAsync(item => item.UserId == studentId)).GradeLevel.Should().BeNull();
        (await context.UserProfiles.SingleAsync(item => item.UserId == studentId)).CurrentLevel.Should().Be(6);
        (await context.TeacherStudentAssignments.SingleAsync()).IsActive.Should().BeFalse();
    }

    private static OwnedSpeedReadingDbContext CreateContext() => new(
        new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddMembership(
        OwnedSpeedReadingDbContext context,
        Guid institutionId,
        Guid userId,
        SpeedReadingInstitutionMemberRole role) =>
        context.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(
            institutionId, userId, role, Guid.NewGuid(), DateTime.UtcNow));

    private sealed class AllowAllSpeedReadingMembers : ISpeedReadingInstitutionMemberEligibility
    {
        public Task<bool> IsEligibleAsync(
            Guid userId,
            SpeedReadingInstitutionMemberRole role,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class SelectiveSpeedReadingMembers : ISpeedReadingInstitutionMemberEligibility
    {
        public Guid? IneligibleUserId { get; init; }

        public Task<bool> IsEligibleAsync(
            Guid userId,
            SpeedReadingInstitutionMemberRole role,
            CancellationToken cancellationToken = default) => Task.FromResult(userId != IneligibleUserId);
    }
}
