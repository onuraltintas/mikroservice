using FluentAssertions;
using SpeedReading.Domain.Assignments;
using SpeedReading.Application.Assignments;
using SpeedReading.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingInstitutionAssignmentTests
{
    [Fact]
    public void Institution_assignment_keeps_a_stable_tenant_scope()
    {
        var institutionId = Guid.NewGuid();
        var assignment = Assignment.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "Reading", null,
            DateTime.UtcNow.AddDays(7), institutionId: institutionId);

        assignment.InstitutionId.Should().Be(institutionId);
    }

    [Fact]
    public async Task Institution_list_excludes_assignments_from_other_tenants_and_unscoped_teachers()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Assignments.AddRange(
            Assignment.Create(teacherId, Guid.NewGuid(), null, "Own", null,
                DateTime.UtcNow.AddDays(7), institutionId: institutionId),
            Assignment.Create(teacherId, Guid.NewGuid(), null, "Other", null,
                DateTime.UtcNow.AddDays(7), institutionId: Guid.NewGuid()),
            Assignment.Create(teacherId, Guid.NewGuid(), null, "Unscoped", null,
                DateTime.UtcNow.AddDays(7)));
        await db.SaveChangesAsync();
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssignments")!;
        var service = (ISpeedReadingAssignments)Activator.CreateInstance(type, db, null)!;

        var page = await service.GetInstitutionAssignmentsAsync(
            institutionId, 1, 25, null, null, null, null);

        page.Items.Select(item => item.Title).Should().Equal("Own");
    }

    [Fact]
    public async Task Institution_create_rejects_teacher_without_active_membership()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssignments")!;
        var service = (ISpeedReadingAssignments)Activator.CreateInstance(type, db, null)!;

        var id = await service.CreateForInstitutionAsync(institutionId, teacherId,
            Guid.NewGuid(), new CreateAssignmentRequest(Guid.NewGuid(), null, [],
                "Reading", null, DateTime.UtcNow.AddDays(7)), CancellationToken.None);

        id.Should().BeNull();
    }
}
