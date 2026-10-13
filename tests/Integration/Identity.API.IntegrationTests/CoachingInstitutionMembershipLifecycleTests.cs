using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Commands.UpdateInstitutionStudent;
using Identity.Application.Commands.RemoveStudentFromInstitution;
using Identity.Application.Commands.RemoveTeacherFromInstitution;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingInstitutionMembershipLifecycleTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("InstitutionAdmin")]
    [InlineData("InstitutionOwner")]
    public async Task CurrentInstitutionReassignsAndDetachesMembersWithoutDeletingHistory(string role)
    {
        var sourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(postgres.ConnectionString);
        sourceBuilder.EnableDynamicJson();
        await using var source = sourceBuilder.Build();
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(source).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var institution = Institution.Create("Kurum", InstitutionType.School);
            var outside = Institution.Create("Diğer kurum", InstitutionType.School);
            var admin = User.Create(Guid.NewGuid(), "admin@example.test");
            var studentUser = User.Create(Guid.NewGuid(), "student@example.test");
            var oldTeacherUser = User.Create(Guid.NewGuid(), "old@example.test");
            var newTeacherUser = User.Create(Guid.NewGuid(), "new@example.test");
            var foreignTeacherUser = User.Create(Guid.NewGuid(), "foreign@example.test");
            var student = StudentProfile.Create(studentUser.Id, "Ada", "Test", institution.Id);
            var oldTeacher = TeacherProfile.Create(oldTeacherUser.Id, "Eski", "Koç", institution.Id);
            var newTeacher = TeacherProfile.Create(newTeacherUser.Id, "Yeni", "Koç", institution.Id);
            var foreignTeacher = TeacherProfile.Create(foreignTeacherUser.Id, "Dış", "Koç", outside.Id);
            var oldAssignment = TeacherStudentAssignment.Create(oldTeacher.Id, student.Id, institution.Id);
            db.AddRange(institution, outside, admin, studentUser, oldTeacherUser, newTeacherUser, foreignTeacherUser,
                student, oldTeacher, newTeacher, foreignTeacher, oldAssignment,
                InstitutionAdmin.Create(admin.Id, institution.Id, InstitutionAdminRole.Admin, PlatformProduct.Coaching));
            await db.SaveChangesAsync();
            var institutions = new InstitutionRepository(db); var students = new StudentRepository(db);
            var teachers = new TeacherRepository(db); var unit = new UnitOfWork(db); var actor = new Actor(admin.Id, role);
            var update = new UpdateInstitutionStudentCommandHandler(institutions, students, teachers, unit, actor);
            Assert.True((await update.Handle(new(studentUser.Id, 9, foreignTeacherUser.Id), default)).IsFailure);
            Assert.Null(student.GradeLevel); Assert.True(oldAssignment.IsActive);
            Assert.True((await update.Handle(new(studentUser.Id, 9, newTeacherUser.Id), default)).IsSuccess);
            Assert.False(oldAssignment.IsActive); Assert.Equal(9, student.GradeLevel);
            Assert.True((await update.Handle(new(studentUser.Id, 9, newTeacherUser.Id), default)).IsSuccess);
            Assert.Single(await teachers.GetActiveAssignmentsForStudentAsync(student.Id, institution.Id, default));
            Assert.True((await new RemoveTeacherFromInstitutionCommandHandler(institutions, teachers, unit, actor)
                .Handle(new(newTeacherUser.Id), default)).IsSuccess);
            Assert.Null(newTeacher.InstitutionId);
            Assert.Empty(await teachers.GetActiveAssignmentsForStudentAsync(student.Id, institution.Id, default));
            Assert.True((await new RemoveStudentFromInstitutionCommandHandler(institutions, students, teachers, unit, actor)
                .Handle(new(studentUser.Id), default)).IsSuccess);
            Assert.Null(student.InstitutionId);
            Assert.Equal(2, await db.TeacherStudentAssignments.CountAsync());
            Assert.Equal(1, await db.StudentProfiles.CountAsync()); Assert.Equal(3, await db.TeacherProfiles.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Actor(Guid id, string role) : ICurrentUserService
    {
        public Guid? UserId => id;
        public IEnumerable<string> Roles => [role];
        public string? Email => null;
        public string? FullName => null;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
