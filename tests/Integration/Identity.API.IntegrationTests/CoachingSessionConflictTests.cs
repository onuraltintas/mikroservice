using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingSessionConflictTests(PostgresFixture postgres)
{
    private DbContextOptions<CoachingDbContext> Options => new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options;

    [Fact]
    public async Task MigrationPreservesLegacyOverlapsAndMetadataAndRollsBackWithoutDeletingSessions()
    {
        await using var db = new CoachingDbContext(Options);
        await db.Database.EnsureDeletedAsync();
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261013121249_AddStudyTaskQuestionTracking";
        const string current = "20261013123111_GuardCoachingSessionOverlap";
        await migrator.MigrateAsync(previous);
        try
        {
            var teacher = Guid.NewGuid(); var student = Guid.NewGuid(); var start = DateTime.UtcNow.AddDays(2);
            await Save(teacher, student, start); var second = await Save(teacher, student, start);
            await migrator.MigrateAsync(current);
            var legacy = await db.CoachingSessions.SingleAsync(x => x.Id == second);
            legacy.UpdateDetails(title: "Legacy metadata correction"); await db.SaveChangesAsync();
            Assert.Equal(2, await db.CoachingSessions.CountAsync());
            await Assert.ThrowsAsync<BusinessRuleException>(() => Save(teacher, student, start.AddMinutes(30)));
            await migrator.MigrateAsync(previous); Assert.Equal(2, await db.CoachingSessions.CountAsync());
            await migrator.MigrateAsync(current);
            await Assert.ThrowsAsync<BusinessRuleException>(() => Save(teacher, student, start));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task SessionUpdateAndAttendanceErasureAcquireSchedulingLockBeforeRows()
    {
        await using var db = new CoachingDbContext(Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var row = CoachingSession.Create(Guid.NewGuid(), "Session", DateTime.UtcNow.AddDays(2), SessionType.OneOnOne); row.AddStudent(Guid.NewGuid());
            db.Add(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); var attendanceId = row.Attendances.Single().Id;
            await using var updating = new CoachingDbContext(Options); await using var deleting = new CoachingDbContext(Options);
            await using var updateTransaction = await updating.Database.BeginTransactionAsync();
            await using var deleteTransaction = await deleting.Database.BeginTransactionAsync();
            var deletePid = ((Npgsql.NpgsqlConnection)deleting.Database.GetDbConnection()).ProcessID;
            var changedTitle = "Updated"; var changedNote = "Note";
            await updating.Database.ExecuteSqlInterpolatedAsync($"UPDATE coaching.coaching_sessions SET title = {changedTitle} WHERE id = {row.Id}");
            var deleteTask = deleting.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM coaching.session_attendances WHERE id = {attendanceId}");
            try
            {
                var waiting = false;
                for (var i = 0; i < 100 && !waiting; i++)
                {
                    waiting = await db.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE pid = {deletePid} AND wait_event = 'advisory') AS \"Value\"").SingleAsync();
                    if (!waiting) await Task.Delay(20);
                }
                Assert.True(waiting, "Attendance deletion must wait for the scheduling transaction.");
                await updating.Database.ExecuteSqlInterpolatedAsync($"UPDATE coaching.session_attendances SET teacher_note = {changedNote} WHERE id = {attendanceId}");
                await updateTransaction.CommitAsync(); await deleteTask; await deleteTransaction.CommitAsync();
                Assert.False(await db.SessionAttendances.AnyAsync(x => x.Id == attendanceId));
            }
            finally
            {
                await updateTransaction.DisposeAsync();
                await deleteTask; // Observe a failed/deadlock operation after the other transaction releases its locks.
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task DatabaseRejectsTeacherAndStudentOverlapButAllowsAdjacentAndCancelledSlots()
    {
        await using var db = new CoachingDbContext(Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var teacher = Guid.NewGuid(); var student = Guid.NewGuid(); var second = Guid.NewGuid();
            var start = DateTime.UtcNow.Date.AddDays(2).AddHours(9);
            var original = CoachingSession.Create(teacher, "Group", start, SessionType.Group); original.AddStudents([student, second]);
            db.Add(original); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var teacherError = await Assert.ThrowsAsync<BusinessRuleException>(() => Save(teacher, Guid.NewGuid(), start.AddMinutes(30)));
            Assert.Equal("Session.Conflict", teacherError.Code);
            var studentError = await Assert.ThrowsAsync<BusinessRuleException>(() => Save(Guid.NewGuid(), second, start.AddMinutes(30)));
            Assert.Equal("Session.Conflict", studentError.Code);
            var adjacent = await Save(teacher, student, start.AddHours(1));
            await using (var edit = new CoachingDbContext(Options))
            {
                var row = await edit.CoachingSessions.Include(x => x.Attendances).SingleAsync(x => x.Id == adjacent);
                row.UpdateEditableDetails("Moved", null, start.AddMinutes(40), 60, null, null);
                Assert.Equal("Session.Conflict", (await Assert.ThrowsAsync<BusinessRuleException>(() => edit.SaveChangesAsync())).Code);
            }
            Assert.Equal(start.AddHours(1), (await db.CoachingSessions.AsNoTracking().SingleAsync(x => x.Id == adjacent)).ScheduledDate);
            var cancel = await db.CoachingSessions.SingleAsync(x => x.Id == original.Id); cancel.Cancel(); await db.SaveChangesAsync();
            await Save(teacher, student, start);
            Assert.Equal(3, await db.CoachingSessions.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentOverlappingSchedulesAcceptOnlyOne(bool sameTeacher)
    {
        await using var db = new CoachingDbContext(Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var teacher = Guid.NewGuid(); var student = Guid.NewGuid(); var start = DateTime.UtcNow.AddDays(2);
            async Task<bool> Attempt(Guid teacherId, Guid studentId)
            {
                try { await Save(teacherId, studentId, start); return true; }
                catch (BusinessRuleException error) when (error.Code == "Session.Conflict") { return false; }
            }
            var results = await Task.WhenAll(Attempt(teacher, student), Attempt(sameTeacher ? teacher : Guid.NewGuid(), sameTeacher ? Guid.NewGuid() : student));
            Assert.Single(results.Where(x => x)); Assert.Equal(1, await db.CoachingSessions.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private async Task<Guid> Save(Guid teacher, Guid student, DateTime start)
    {
        await using var db = new CoachingDbContext(Options);
        var session = CoachingSession.Create(teacher, "Session", start, SessionType.OneOnOne); session.AddStudent(student);
        db.Add(session); await db.SaveChangesAsync(); return session.Id;
    }
}
