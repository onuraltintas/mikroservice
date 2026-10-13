using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingSessionConflictTests(PostgresFixture postgres)
{
    private DbContextOptions<CoachingDbContext> Options => new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options;

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
