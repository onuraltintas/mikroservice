using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingTeacherStudentAssignmentTests
{
    [Fact]
    public void Create_rejects_missing_or_self_referential_assignments()
    {
        var actorId = Guid.NewGuid();
        var institutionId = Guid.NewGuid();

        var emptyTeacher = () => SpeedReadingTeacherStudentAssignment.Create(
            institutionId, Guid.Empty, Guid.NewGuid(), actorId, DateTime.UtcNow);
        var emptyStudent = () => SpeedReadingTeacherStudentAssignment.Create(
            institutionId, Guid.NewGuid(), Guid.Empty, actorId, DateTime.UtcNow);
        var selfAssignment = () => SpeedReadingTeacherStudentAssignment.Create(
            institutionId, actorId, actorId, actorId, DateTime.UtcNow);

        emptyTeacher.Should().Throw<ArgumentException>();
        emptyStudent.Should().Throw<ArgumentException>();
        selfAssignment.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_allows_an_independent_teacher_assignment_without_an_institution()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var assignment = SpeedReadingTeacherStudentAssignment.Create(
            null, teacherId, studentId, teacherId, DateTime.UtcNow);

        assignment.InstitutionId.Should().BeNull();
        assignment.TeacherUserId.Should().Be(teacherId);
        assignment.StudentUserId.Should().Be(studentId);
    }

    [Fact]
    public void Ef_model_stores_assignments_without_cross_database_foreign_keys()
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql("Host=localhost;Database=speed_reading_model_only;Username=unused;Password=unused")
            .Options;
        using var context = new OwnedSpeedReadingDbContext(options);
        var entity = context.Model.FindEntityType(typeof(SpeedReadingTeacherStudentAssignment))!;

        entity.GetTableName().Should().Be("teacher_student_assignments");
        entity.GetSchema().Should().Be("speed_reading");
        entity.GetForeignKeys().Should().BeEmpty();
        entity.FindProperty(nameof(SpeedReadingTeacherStudentAssignment.InstitutionId))!
            .IsNullable.Should().BeTrue();
        entity.GetIndexes().Should().Contain(index =>
            index.IsUnique
            && index.GetFilter() == "institution_id IS NOT NULL"
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(SpeedReadingTeacherStudentAssignment.InstitutionId),
                    nameof(SpeedReadingTeacherStudentAssignment.TeacherUserId),
                    nameof(SpeedReadingTeacherStudentAssignment.StudentUserId)
                }));
        entity.GetIndexes().Should().Contain(index =>
            index.IsUnique
            && index.GetFilter() == "institution_id IS NOT NULL AND is_active = true"
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(SpeedReadingTeacherStudentAssignment.InstitutionId),
                    nameof(SpeedReadingTeacherStudentAssignment.StudentUserId)
                }));
        entity.GetIndexes().Should().Contain(index =>
            index.IsUnique
            && index.GetFilter() == "institution_id IS NULL"
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(SpeedReadingTeacherStudentAssignment.TeacherUserId),
                    nameof(SpeedReadingTeacherStudentAssignment.StudentUserId)
                }));
        context.Database.GetMigrations()
            .Should().Contain("20260925120316_AddSpeedReadingTeacherStudentAssignments")
            .And.Contain("20260925131708_EnforceSingleActiveInstitutionTeacher");
    }

    [Fact]
    public async Task Store_requires_same_product_membership_and_scopes_links_by_institution()
    {
        await using var context = CreateContext();
        var eligibility = new AllowAllSpeedReadingMembers();
        var store = new OwnedSpeedReadingTeacherStudentAssignments(context, eligibility);
        var institutionId = Guid.NewGuid();
        var otherInstitutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        AddMembership(context, institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institutionId, studentId, SpeedReadingInstitutionMemberRole.Student);
        AddMembership(context, otherInstitutionId, studentId, SpeedReadingInstitutionMemberRole.Student);
        await context.SaveChangesAsync();

        (await store.AssignAsync(institutionId, teacherId, studentId, actorId, DateTime.UtcNow))
            .Should().Be(SpeedReadingTeacherStudentAssignmentResult.Created);
        (await store.AssignAsync(institutionId, teacherId, studentId, actorId, DateTime.UtcNow))
            .Should().Be(SpeedReadingTeacherStudentAssignmentResult.AlreadyActive);
        (await store.GetStudentsAsync(institutionId, teacherId, 1, 25))
            .Items.Should().ContainSingle(item => item.StudentUserId == studentId);
        (await store.GetStudentsAsync(otherInstitutionId, teacherId, 1, 25))
            .TotalCount.Should().Be(0);

        (await store.RemoveAsync(institutionId, teacherId, studentId, actorId, DateTime.UtcNow))
            .Should().BeTrue();
        (await store.GetStudentUserIdsAsync(teacherId, [studentId]))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Store_reassigns_an_institution_student_without_touching_independent_relationships()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var nextTeacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        AddMembership(context, institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institutionId, nextTeacherId, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institutionId, studentId, SpeedReadingInstitutionMemberRole.Student);
        context.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(
            null, teacherId, studentId, actorId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var store = new OwnedSpeedReadingTeacherStudentAssignments(context, new AllowAllSpeedReadingMembers());

        (await store.AssignAsync(institutionId, teacherId, studentId, actorId, DateTime.UtcNow))
            .Should().Be(SpeedReadingTeacherStudentAssignmentResult.Created);
        (await store.AssignAsync(institutionId, nextTeacherId, studentId, actorId, DateTime.UtcNow.AddMinutes(1)))
            .Should().Be(SpeedReadingTeacherStudentAssignmentResult.Created);

        (await context.TeacherStudentAssignments.SingleAsync(item => item.InstitutionId == institutionId
            && item.TeacherUserId == teacherId)).IsActive.Should().BeFalse();
        (await context.TeacherStudentAssignments.SingleAsync(item => item.InstitutionId == institutionId
            && item.TeacherUserId == nextTeacherId)).IsActive.Should().BeTrue();
        (await context.TeacherStudentAssignments.SingleAsync(item => item.InstitutionId == null)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Store_supports_independent_teacher_student_links_without_institution_memberships()
    {
        await using var context = CreateContext();
        var teacherId = Guid.NewGuid();
        var anotherTeacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var store = new OwnedSpeedReadingTeacherStudentAssignments(context, new AllowAllSpeedReadingMembers());

        (await store.AssignAsync(null, teacherId, studentId, teacherId, DateTime.UtcNow))
            .Should().Be(SpeedReadingTeacherStudentAssignmentResult.Created);
        (await store.AssignAsync(null, anotherTeacherId, studentId, anotherTeacherId, DateTime.UtcNow))
            .Should().Be(SpeedReadingTeacherStudentAssignmentResult.Created);
        (await store.AssignAsync(null, teacherId, studentId, teacherId, DateTime.UtcNow))
            .Should().Be(SpeedReadingTeacherStudentAssignmentResult.AlreadyActive);

        var roster = await store.GetTeacherRosterAsync(teacherId, [], 1, 25);
        roster.Items.Should().ContainSingle(item => item.StudentUserId == studentId && item.InstitutionId == null);
        (await store.GetStudentUserIdsAsync(teacherId, [studentId])).Should().BeEquivalentTo([studentId]);
        (await store.GetStudentUserIdsAsync(anotherTeacherId, [studentId])).Should().BeEquivalentTo([studentId]);

        (await store.RemoveAsync(null, teacherId, studentId, teacherId, DateTime.UtcNow))
            .Should().BeTrue();
        (await store.GetStudentUserIdsAsync(teacherId, [studentId])).Should().BeEmpty();
        (await store.GetStudentUserIdsAsync(anotherTeacherId, [studentId])).Should().BeEquivalentTo([studentId]);
    }

    [Fact]
    public async Task Store_hides_relationships_when_either_institution_membership_is_inactive()
    {
        await using var context = CreateContext();
        var store = new OwnedSpeedReadingTeacherStudentAssignments(context, new AllowAllSpeedReadingMembers());
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var teacherMembership = SpeedReadingInstitutionMembership.Create(
            institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher, actorId, DateTime.UtcNow);
        var studentMembership = SpeedReadingInstitutionMembership.Create(
            institutionId, studentId, SpeedReadingInstitutionMemberRole.Student, actorId, DateTime.UtcNow);
        context.InstitutionMemberships.AddRange(teacherMembership, studentMembership);
        await context.SaveChangesAsync();
        (await store.AssignAsync(institutionId, teacherId, studentId, actorId, DateTime.UtcNow))
            .Should().Be(SpeedReadingTeacherStudentAssignmentResult.Created);

        studentMembership.SetActive(false, actorId, DateTime.UtcNow.AddMinutes(1));
        await context.SaveChangesAsync();

        (await store.GetStudentsAsync(institutionId, teacherId, 1, 25)).TotalCount.Should().Be(0);
        (await store.GetStudentUserIdsAsync(teacherId, [studentId])).Should().BeEmpty();

        studentMembership.SetActive(true, actorId, DateTime.UtcNow.AddMinutes(2));
        teacherMembership.SetActive(false, actorId, DateTime.UtcNow.AddMinutes(3));
        await context.SaveChangesAsync();
        (await store.GetStudentsAsync(institutionId, teacherId, 1, 25)).TotalCount.Should().Be(0);
        (await store.GetStudentUserIdsAsync(teacherId, [studentId])).Should().BeEmpty();
    }

    [Fact]
    public async Task Store_returns_teacher_roster_with_speed_reading_profile_data_only()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        AddMembership(context, institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institutionId, studentId, SpeedReadingInstitutionMemberRole.Student);
        context.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(
            institutionId, teacherId, studentId, teacherId, DateTime.UtcNow));
        context.UserProfiles.Add(SpeedReadingUserProfile.Import(
            Guid.NewGuid(), studentId, 4, 215, 82, 35, null, institutionId,
            true, DateTime.UtcNow, teacherId.ToString(), null, null, learningStyle: "visual", gradeLevel: 7));
        await context.SaveChangesAsync();

        var page = await new OwnedSpeedReadingTeacherStudentAssignments(context, new AllowAllSpeedReadingMembers())
            .GetTeacherRosterAsync(teacherId, [institutionId], 1, 25);

        page.Items.Should().ContainSingle();
        var record = page.Items[0];
        record.StudentUserId.Should().Be(studentId);
        record.CurrentLevel.Should().Be(4);
        record.GradeLevel.Should().Be(7);
        record.TargetWpm.Should().Be(215);
        record.TargetComprehension.Should().Be(82);
        record.DailyGoalMinutes.Should().Be(35);
        record.LearningStyle.Should().Be("visual");
    }

    [Fact]
    public async Task Store_filters_teacher_roster_by_school_grade_before_pagination()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var gradeSevenStudentId = Guid.NewGuid();
        var gradeEightStudentId = Guid.NewGuid();
        AddMembership(context, institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institutionId, gradeSevenStudentId, SpeedReadingInstitutionMemberRole.Student);
        AddMembership(context, institutionId, gradeEightStudentId, SpeedReadingInstitutionMemberRole.Student);
        context.TeacherStudentAssignments.AddRange(
            SpeedReadingTeacherStudentAssignment.Create(institutionId, teacherId, gradeSevenStudentId, teacherId, DateTime.UtcNow),
            SpeedReadingTeacherStudentAssignment.Create(institutionId, teacherId, gradeEightStudentId, teacherId, DateTime.UtcNow.AddMinutes(1)));
        context.UserProfiles.AddRange(
            SpeedReadingUserProfile.Import(
                Guid.NewGuid(), gradeSevenStudentId, 2, 150, 70, 20, null, institutionId,
                true, DateTime.UtcNow, teacherId.ToString(), null, null, gradeLevel: 7),
            SpeedReadingUserProfile.Import(
                Guid.NewGuid(), gradeEightStudentId, 4, 215, 82, 35, null, institutionId,
                true, DateTime.UtcNow, teacherId.ToString(), null, null, gradeLevel: 8));
        await context.SaveChangesAsync();

        var page = await new OwnedSpeedReadingTeacherStudentAssignments(context, new AllowAllSpeedReadingMembers())
            .GetTeacherRosterAsync(teacherId, [institutionId], 1, 1, gradeLevel: 8);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(item => item.StudentUserId == gradeEightStudentId);
    }

    [Fact]
    public async Task Store_can_resolve_one_assigned_student_without_loading_the_full_roster()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var requestedStudentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        AddMembership(context, institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institutionId, requestedStudentId, SpeedReadingInstitutionMemberRole.Student);
        AddMembership(context, institutionId, otherStudentId, SpeedReadingInstitutionMemberRole.Student);
        context.TeacherStudentAssignments.AddRange(
            SpeedReadingTeacherStudentAssignment.Create(institutionId, teacherId, requestedStudentId, teacherId, DateTime.UtcNow),
            SpeedReadingTeacherStudentAssignment.Create(institutionId, teacherId, otherStudentId, teacherId, DateTime.UtcNow.AddMinutes(1)));
        await context.SaveChangesAsync();

        var page = await new OwnedSpeedReadingTeacherStudentAssignments(context, new AllowAllSpeedReadingMembers())
            .GetTeacherRosterAsync(teacherId, [institutionId], 1, 1, studentUserId: requestedStudentId);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(item => item.StudentUserId == requestedStudentId);
    }

    [Fact]
    public async Task Store_rejects_missing_membership_or_product_role()
    {
        await using var context = CreateContext();
        var eligibility = new AllowAllSpeedReadingMembers();
        var store = new OwnedSpeedReadingTeacherStudentAssignments(context, eligibility);
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        AddMembership(context, institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher);
        await context.SaveChangesAsync();

        (await store.AssignAsync(institutionId, teacherId, studentId, actorId, DateTime.UtcNow))
            .Should().Be(SpeedReadingTeacherStudentAssignmentResult.MembershipRequired);

        AddMembership(context, institutionId, studentId, SpeedReadingInstitutionMemberRole.Student);
        await context.SaveChangesAsync();
        eligibility.IneligibleUserId = teacherId;

        (await store.AssignAsync(institutionId, teacherId, studentId, actorId, DateTime.UtcNow))
            .Should().Be(SpeedReadingTeacherStudentAssignmentResult.ProductRoleRequired);
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
        public Guid? IneligibleUserId { get; set; }

        public Task<bool> IsEligibleAsync(
            Guid userId,
            SpeedReadingInstitutionMemberRole role,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(userId != IneligibleUserId);
    }
}
