using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Identity.API.IntegrationTests;

public sealed class CoachingProfilePostgresFactAttribute : FactAttribute
{
    public CoachingProfilePostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COACHING_PROFILE_TEST_POSTGRES")))
            Skip = "Set COACHING_PROFILE_TEST_POSTGRES to a disposable local PostgreSQL instance.";
    }
}

public sealed class CoachingOwnedProfilePostgresTests
{
    [CoachingProfilePostgresFact]
    public async Task UpgradePreservesCompletedWorkAndEnforcesUserIdRelationships()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COACHING_PROFILE_TEST_POSTGRES"));
        Assert.Contains(connection.Host, new[] { "127.0.0.1", "localhost" });
        connection.Pooling = false;
        var testName = "test_coaching_ownership_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{testName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            connection.Database = testName;
            var options = new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString,
                x => x.MigrationsHistoryTable("__ef_migrations_history", "coaching")).Options;
            await using var db = new CoachingDbContext(options);
            var baseline = db.Database.GetMigrations().Last(x => !x.Contains("OwnedCoaching"));
            await db.GetService<IMigrator>().MigrateAsync(baseline);
            var student = Guid.NewGuid(); var teacher = Guid.NewGuid();
            var revision = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Existing work"); revision.Activate();
            var task = StudyPlanTask.Create(revision, new(2026, 10, 16), "Completed", 30, plannedQuestions: 10);
            task.Complete(25, 8); db.AddRange(revision, task); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await db.Database.MigrateAsync();
            var identitySourceBuilder = new NpgsqlDataSourceBuilder(connection.ConnectionString);
            identitySourceBuilder.EnableDynamicJson();
            await using var identitySource = identitySourceBuilder.Build();
            await using var identity = new Identity.Infrastructure.Persistence.IdentityDbContext(
                new DbContextOptionsBuilder<Identity.Infrastructure.Persistence.IdentityDbContext>().UseNpgsql(identitySource).Options);
            await identity.Database.ExecuteSqlRawAsync(identity.Database.GenerateCreateScript());
            var studentRole = Identity.Domain.Entities.Role.Create("Student", "Student");
            var teacherRole = Identity.Domain.Entities.Role.Create("Teacher", "Teacher");
            var account = Identity.Domain.Entities.User.Create(student, "student@example.test", "Core", "Student");
            var teacherAccount = Identity.Domain.Entities.User.Create(teacher, "teacher@example.test", "Core", "Teacher");
            account.SetPassword([1, 2, 3], [4, 5, 6]); account.ConfirmEmail(); teacherAccount.ConfirmEmail();
            account.GrantProductAccess(Identity.Domain.Enums.PlatformProduct.Coaching, Identity.Domain.Enums.UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
            account.GrantProductAccess(Identity.Domain.Enums.PlatformProduct.SpeedReading, Identity.Domain.Enums.UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
            teacherAccount.GrantProductAccess(Identity.Domain.Enums.PlatformProduct.Coaching, Identity.Domain.Enums.UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
            account.AddRole(new Identity.Domain.Entities.UserRole(student, studentRole.Id, Identity.Domain.Enums.PlatformProduct.Coaching));
            account.AddRole(new Identity.Domain.Entities.UserRole(student, studentRole.Id, Identity.Domain.Enums.PlatformProduct.SpeedReading));
            teacherAccount.AddRole(new Identity.Domain.Entities.UserRole(teacher, teacherRole.Id, Identity.Domain.Enums.PlatformProduct.Coaching));
            var oldStudent = Identity.Domain.Entities.StudentProfile.Create(student, "Old profile", "Name");
            oldStudent.UpdateEducationInfo(12);
            var oldTeacher = Identity.Domain.Entities.TeacherProfile.Create(teacher, "Old coach", "Name");
            oldTeacher.SetSubjects(["Matematik"]);
            identity.AddRange(studentRole, teacherRole, account, teacherAccount, oldStudent, oldTeacher);
            identity.Add(Identity.Domain.Entities.TeacherStudentAssignment.Create(oldTeacher.Id, oldStudent.Id, null, null, teacher));
            await identity.SaveChangesAsync(); identity.ChangeTracker.Clear();
            var snapshot = await new Identity.Infrastructure.Services.CoachingLegacyProfileReader(identity).ReadAsync(default);
            var accounts = new CoachingOwnedProfilePersistenceTests.FakeAccounts();
            var reader = new Identity.Infrastructure.Services.CoachingAccountReader(identity);
            foreach (var row in await reader.GetAsync([student, teacher], default)) accounts.Users[row.UserId] = row;
            var importer = new Coaching.Infrastructure.Profiles.CoachingProfileImporter(db, accounts);
            await importer.ImportAsync(snapshot, false, default);
            Assert.Empty(db.CoachingStudentProfiles);
            await importer.ImportAsync(snapshot, true, default);
            await importer.ImportAsync(snapshot, true, default); db.ChangeTracker.Clear();
            var common = await identity.Users.Include(x => x.ProductAccesses).SingleAsync(x => x.Id == student);
            Assert.Equal(new byte[] { 1, 2, 3 }, common.PasswordHash);
            Assert.True(common.HasProductAccess(Identity.Domain.Enums.PlatformProduct.SpeedReading));
            Assert.Equal("Core", common.FirstName); Assert.True(common.EmailConfirmed);
            Assert.Equal(2, await identity.Users.CountAsync());
            Assert.Single(await identity.TeacherStudentAssignments.ToListAsync());
            Assert.Equal(oldStudent.Id, (await identity.StudentProfiles.SingleAsync()).Id);
            var preserved = await db.StudyPlanTasks.SingleAsync();
            Assert.Equal(task.Id, preserved.Id); Assert.Equal(student, preserved.StudentId);
            Assert.Equal(25, preserved.ActualMinutes); Assert.Equal(8, preserved.ActualQuestions); Assert.True(preserved.IsCompleted);
            Assert.Equal(student, (await db.CoachingTeacherStudentAssignments.SingleAsync()).StudentUserId);
            Assert.Equal(snapshot.Assignments[0].StartDate, (await db.CoachingTeacherStudentAssignments.SingleAsync()).StartDate);
            Assert.Equal("Matematik", Assert.Single((await db.CoachingTeacherProfiles.SingleAsync()).Subjects));
            db.Add(CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)duplicate.GetBaseException()).SqlState);
            db.ChangeTracker.Clear();
            db.Add(CoachingTeacherStudentAssignment.Create(teacher, Guid.NewGuid(), null, null, teacher));
            var missingUser = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)missingUser.GetBaseException()).SqlState);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{testName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
